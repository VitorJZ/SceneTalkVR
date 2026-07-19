using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SceneTalkVR.Core
{
    public enum FeedbackFirstTurnState
    {
        Planning,
        FeedbackPending,
        FeedbackSpeaking,
        DialogueReady,
        DialogueSpeaking,
        Completed,
        TechnicalInvalid
    }

    public enum ExperimentTimingEventType
    {
        UserSpeechEnded,
        CorrectionRequestStarted,
        CorrectionFirstToken,
        CorrectionTextReady,
        CorrectionTtsStarted,
        CorrectionTtsReady,
        CorrectionPlaybackStarted,
        CorrectionPlaybackEnded,
        DialogueRequestStarted,
        DialogueFirstToken,
        DialogueFirstSentenceReady,
        DialogueTtsStarted,
        DialogueFirstTtsReady,
        DialoguePlaybackStarted,
        DialoguePlaybackEnded,
        DialogueGateClosed,
        DialogueGateOpened,
        TurnCompleted,
        TurnTechnicalInvalid
    }

    [Serializable]
    public sealed class ExperimentTimingEvent
    {
        public string schemaVersion = ExperimentEventTimeline.SchemaVersion;
        public string timestampUtc;
        public long monotonicElapsedMs;
        public string participantId;
        public string sessionId;
        public string turnId;
        public int turnIndex;
        public string condition;
        public string provider;
        public string style;
        public string taskId;
        public string eventType;
        public string technicalValidity;
        public string failureStage;
        public string reason;
        public string actualPlaybackActor;
        public string voiceProfile;
        public string speakingSpeed;
        public float volume;
        public string subtitlePolicy;
        public string feedbackTextHash;
        public string embodimentCondition;
        public string pilotRunId;
        public string fallback;
    }

    [Serializable]
    public sealed class ExperimentTurnTimingSummary
    {
        public long userEndToFeedbackAudioMs = -1;
        public long userEndToDialogueAudioMs = -1;
        public long feedbackToDialogueGapMs = -1;
        public long correctionGenerationMs = -1;
        public long dialogueFirstSentenceGenerationMs = -1;
        public long correctionTtsMs = -1;
        public long dialogueFirstTtsMs = -1;
    }

    public sealed class ExperimentEventTimeline
    {
        public const string SchemaVersion = "1.0";
        private readonly List<ExperimentTimingEvent> events = new List<ExperimentTimingEvent>();
        private long lastElapsedMs = -1;

        public IReadOnlyList<ExperimentTimingEvent> Events => events;

        public void Reset()
        {
            events.Clear();
            lastElapsedMs = -1;
        }

        public ExperimentTimingEvent Add(ExperimentTimingEvent value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            value.monotonicElapsedMs = Math.Max(value.monotonicElapsedMs, lastElapsedMs + 1);
            lastElapsedMs = value.monotonicElapsedMs;
            events.Add(value);
            return value;
        }

        public ExperimentTurnTimingSummary CalculateSummary()
        {
            return new ExperimentTurnTimingSummary
            {
                userEndToFeedbackAudioMs = Difference(ExperimentTimingEventType.UserSpeechEnded, ExperimentTimingEventType.CorrectionPlaybackStarted),
                userEndToDialogueAudioMs = Difference(ExperimentTimingEventType.UserSpeechEnded, ExperimentTimingEventType.DialoguePlaybackStarted),
                feedbackToDialogueGapMs = Difference(ExperimentTimingEventType.CorrectionPlaybackEnded, ExperimentTimingEventType.DialoguePlaybackStarted),
                correctionGenerationMs = Difference(ExperimentTimingEventType.CorrectionRequestStarted, ExperimentTimingEventType.CorrectionTextReady),
                dialogueFirstSentenceGenerationMs = Difference(ExperimentTimingEventType.DialogueRequestStarted, ExperimentTimingEventType.DialogueFirstSentenceReady),
                correctionTtsMs = Difference(ExperimentTimingEventType.CorrectionTtsStarted, ExperimentTimingEventType.CorrectionTtsReady),
                dialogueFirstTtsMs = Difference(ExperimentTimingEventType.DialogueTtsStarted, ExperimentTimingEventType.DialogueFirstTtsReady)
            };
        }

        private long Difference(ExperimentTimingEventType start, ExperimentTimingEventType end)
        {
            ExperimentTimingEvent a = null;
            ExperimentTimingEvent b = null;
            var startName = start.ToString();
            var endName = end.ToString();
            for (var i = 0; i < events.Count; i++)
            {
                if (a == null && events[i].eventType == startName) a = events[i];
                if (b == null && events[i].eventType == endName) b = events[i];
            }
            return a == null || b == null ? -1 : b.monotonicElapsedMs - a.monotonicElapsedMs;
        }

        public static string HashText(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    public sealed class FeedbackFirstPlaybackGate
    {
        public FeedbackFirstTurnState State { get; private set; } = FeedbackFirstTurnState.Planning;
        public bool IsDialogueGateOpen { get; private set; }
        public bool FeedbackPlayed { get; private set; }
        public bool DialoguePlayed { get; private set; }
        public int GateOpenCount { get; private set; }
        public string InvalidReason { get; private set; }

        public void PlannerResolved(bool hasFeedback)
        {
            Require(State == FeedbackFirstTurnState.Planning, "Planner may resolve only once.");
            State = hasFeedback ? FeedbackFirstTurnState.FeedbackPending : FeedbackFirstTurnState.DialogueReady;
            if (!hasFeedback) OpenDialogueGate();
        }

        public void FeedbackStarted()
        {
            Require(State == FeedbackFirstTurnState.FeedbackPending && !FeedbackPlayed, "Feedback playback is out of order or duplicated.");
            FeedbackPlayed = true;
            State = FeedbackFirstTurnState.FeedbackSpeaking;
        }

        public void FeedbackEnded()
        {
            Require(State == FeedbackFirstTurnState.FeedbackSpeaking, "Feedback end has no matching start.");
            State = FeedbackFirstTurnState.DialogueReady;
            OpenDialogueGate();
        }

        public bool OpenDialogueGate()
        {
            Require(State == FeedbackFirstTurnState.DialogueReady, "Dialogue Gate cannot open before planner/feedback completion.");
            if (IsDialogueGateOpen) return false;
            IsDialogueGateOpen = true;
            GateOpenCount++;
            return true;
        }

        public void DialogueStarted()
        {
            Require(IsDialogueGateOpen && State == FeedbackFirstTurnState.DialogueReady && !DialoguePlayed, "Dialogue playback is gated or duplicated.");
            DialoguePlayed = true;
            State = FeedbackFirstTurnState.DialogueSpeaking;
        }

        public void DialogueEnded()
        {
            Require(State == FeedbackFirstTurnState.DialogueSpeaking, "Dialogue end has no matching start.");
            State = FeedbackFirstTurnState.Completed;
        }

        public void MarkTechnicalInvalid(string reason)
        {
            InvalidReason = string.IsNullOrWhiteSpace(reason) ? "unspecified_failure" : reason;
            IsDialogueGateOpen = false;
            State = FeedbackFirstTurnState.TechnicalInvalid;
        }

        public void Reset()
        {
            State = FeedbackFirstTurnState.Planning;
            IsDialogueGateOpen = false;
            FeedbackPlayed = false;
            DialoguePlayed = false;
            GateOpenCount = 0;
            InvalidReason = string.Empty;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
