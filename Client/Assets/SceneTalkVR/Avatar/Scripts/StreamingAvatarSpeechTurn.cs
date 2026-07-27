using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SceneTalkVR.AvatarSystem
{
    internal enum StreamingSpeechTurnState
    {
        Idle,
        Receiving,
        Draining,
        Completed,
        Failed,
        Aborted
    }

    internal enum StreamingSpeechSegmentState
    {
        Queued,
        Synthesizing,
        Ready,
        Playing,
        Played,
        Failed
    }

    internal enum StreamingTextReconciliation
    {
        Exact,
        MissingSuffix,
        Diverged
    }

    internal sealed class StreamingSpeechSegment
    {
        public StreamingSpeechSegment(int turnId, int sequence, string text)
        {
            TurnId = turnId;
            Sequence = sequence;
            Text = text;
            State = StreamingSpeechSegmentState.Queued;
        }

        public int TurnId { get; }
        public int Sequence { get; }
        public string Text { get; }
        public StreamingSpeechSegmentState State { get; private set; }
        public PreparedAvatarSpeech PreparedSpeech { get; set; }

        public void SetState(StreamingSpeechSegmentState state)
        {
            State = state;
        }
    }

    /// <summary>
    /// Owns the semantic state of one streamed Avatar reply. Coroutines may prepare
    /// and play segments independently, but only this state machine decides when a
    /// reply is complete.
    /// </summary>
    internal sealed class StreamingAvatarSpeechTurn
    {
        private readonly List<StreamingSpeechSegment> segments = new List<StreamingSpeechSegment>();
        private int nextTurnId;

        public int TurnId { get; private set; }
        public StreamingSpeechTurnState State { get; private set; } = StreamingSpeechTurnState.Idle;
        public string ExpectedDialogueText { get; private set; } = string.Empty;
        public string Error { get; private set; } = string.Empty;
        public IReadOnlyList<StreamingSpeechSegment> Segments => segments;
        public bool HasSegments => segments.Count > 0;
        public bool HasPlaybackStarted => segments.Any(segment =>
            segment.State == StreamingSpeechSegmentState.Playing
            || segment.State == StreamingSpeechSegmentState.Played);
        public bool IsTerminal => State == StreamingSpeechTurnState.Completed
            || State == StreamingSpeechTurnState.Failed
            || State == StreamingSpeechTurnState.Aborted;
        public int PlayedCount => segments.Count(segment => segment.State == StreamingSpeechSegmentState.Played);

        public void Begin()
        {
            TurnId = unchecked(++nextTurnId);
            if (TurnId == 0)
            {
                TurnId = unchecked(++nextTurnId);
            }

            segments.Clear();
            ExpectedDialogueText = string.Empty;
            Error = string.Empty;
            State = StreamingSpeechTurnState.Receiving;
        }

        public StreamingSpeechSegment Enqueue(string text)
        {
            if (State != StreamingSpeechTurnState.Receiving)
            {
                return null;
            }

            var normalized = NormalizeWhitespace(text);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var segment = new StreamingSpeechSegment(TurnId, segments.Count, normalized);
            segments.Add(segment);
            return segment;
        }

        public StreamingTextReconciliation Reconcile(
            string expectedDialogueText,
            out string missingSuffix)
        {
            ExpectedDialogueText = NormalizeWhitespace(expectedDialogueText);
            return EvaluateExpectedText(ExpectedDialogueText, out missingSuffix);
        }

        public void ReplaceUnplayed(string expectedDialogueText)
        {
            if (HasPlaybackStarted)
            {
                throw new InvalidOperationException("Cannot replace streamed speech after playback has started.");
            }

            segments.Clear();
            ExpectedDialogueText = NormalizeWhitespace(expectedDialogueText);
        }

        public void BeginDraining(string expectedDialogueText)
        {
            if (State != StreamingSpeechTurnState.Receiving)
            {
                return;
            }

            ExpectedDialogueText = NormalizeWhitespace(expectedDialogueText);
            State = StreamingSpeechTurnState.Draining;
            TryComplete();
        }

        public bool IsCurrent(int turnId)
        {
            return TurnId == turnId
                && (State == StreamingSpeechTurnState.Receiving
                    || State == StreamingSpeechTurnState.Draining);
        }

        public void MarkSynthesizing(StreamingSpeechSegment segment)
        {
            SetSegmentState(segment, StreamingSpeechSegmentState.Synthesizing);
        }

        public void MarkReady(StreamingSpeechSegment segment)
        {
            SetSegmentState(segment, StreamingSpeechSegmentState.Ready);
        }

        public void MarkPlaying(StreamingSpeechSegment segment)
        {
            SetSegmentState(segment, StreamingSpeechSegmentState.Playing);
        }

        public void MarkPlayed(StreamingSpeechSegment segment)
        {
            SetSegmentState(segment, StreamingSpeechSegmentState.Played);
            TryComplete();
        }

        public void Fail(string error, StreamingSpeechSegment segment = null)
        {
            if (IsTerminal)
            {
                return;
            }

            if (segment != null
                && (segment.TurnId != TurnId || !segments.Contains(segment)))
            {
                return;
            }

            if (segment != null)
            {
                segment.SetState(StreamingSpeechSegmentState.Failed);
            }

            Error = string.IsNullOrWhiteSpace(error)
                ? "Streaming Avatar speech failed."
                : error.Trim();
            State = StreamingSpeechTurnState.Failed;
        }

        public void Abort()
        {
            if (State == StreamingSpeechTurnState.Idle)
            {
                return;
            }

            State = StreamingSpeechTurnState.Aborted;
        }

        public void Reset()
        {
            segments.Clear();
            ExpectedDialogueText = string.Empty;
            Error = string.Empty;
            State = StreamingSpeechTurnState.Idle;
        }

        public bool TryComplete()
        {
            if (State != StreamingSpeechTurnState.Draining
                || segments.Count == 0
                || segments.Any(segment => segment.State != StreamingSpeechSegmentState.Played))
            {
                return false;
            }

            if (EvaluateExpectedText(ExpectedDialogueText, out _) != StreamingTextReconciliation.Exact)
            {
                Fail("Played streamed dialogue does not match the final LLM reply.");
                return false;
            }

            State = StreamingSpeechTurnState.Completed;
            return true;
        }

        internal static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var pendingSpace = false;
            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private StreamingTextReconciliation EvaluateExpectedText(
            string normalizedExpectedText,
            out string missingSuffix)
        {
            missingSuffix = string.Empty;
            var cursor = 0;
            foreach (var segment in segments)
            {
                while (cursor < normalizedExpectedText.Length
                    && char.IsWhiteSpace(normalizedExpectedText[cursor]))
                {
                    cursor++;
                }

                var segmentText = NormalizeWhitespace(segment.Text);
                if (string.IsNullOrEmpty(segmentText)
                    || cursor + segmentText.Length > normalizedExpectedText.Length
                    || string.CompareOrdinal(
                        normalizedExpectedText,
                        cursor,
                        segmentText,
                        0,
                        segmentText.Length) != 0)
                {
                    return StreamingTextReconciliation.Diverged;
                }

                cursor += segmentText.Length;
            }

            while (cursor < normalizedExpectedText.Length
                && char.IsWhiteSpace(normalizedExpectedText[cursor]))
            {
                cursor++;
            }

            if (cursor == normalizedExpectedText.Length)
            {
                return StreamingTextReconciliation.Exact;
            }

            missingSuffix = normalizedExpectedText.Substring(cursor).TrimStart();
            return StreamingTextReconciliation.MissingSuffix;
        }

        private void SetSegmentState(
            StreamingSpeechSegment segment,
            StreamingSpeechSegmentState state)
        {
            if (segment == null
                || segment.TurnId != TurnId
                || !segments.Contains(segment)
                || IsTerminal)
            {
                return;
            }

            segment.SetState(state);
        }
    }
}
