using NUnit.Framework;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class StreamingAvatarSpeechTurnTests
    {
        [Test]
        public void Draining_DoesNotCompleteWhileOnlySegmentIsSynthesizing()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var segment = turn.Enqueue("The complete reply.");
            turn.MarkSynthesizing(segment);

            turn.BeginDraining("The complete reply.");

            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Draining));
            Assert.That(turn.TryComplete(), Is.False);

            turn.MarkReady(segment);
            turn.MarkPlaying(segment);
            turn.MarkPlayed(segment);

            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Completed));
            Assert.That(turn.PlayedCount, Is.EqualTo(1));
        }

        [Test]
        public void Reconcile_MissingFinalSentenceReturnsSuffix()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            turn.Enqueue("The first sentence.");

            var result = turn.Reconcile(
                "The first sentence. The final sentence.",
                out var suffix);

            Assert.That(result, Is.EqualTo(StreamingTextReconciliation.MissingSuffix));
            Assert.That(suffix, Is.EqualTo("The final sentence."));
        }

        [Test]
        public void Reconcile_MissingSuffixWithoutWhitespaceBoundaryReturnsSuffix()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            turn.Enqueue("First.");

            var result = turn.Reconcile("First.Second.", out var suffix);

            Assert.That(result, Is.EqualTo(StreamingTextReconciliation.MissingSuffix));
            Assert.That(suffix, Is.EqualTo("Second."));
        }

        [Test]
        public void Completion_AcceptsEquivalentWhitespaceOnlyBetweenSegments()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var first = turn.Enqueue("First.");
            var second = turn.Enqueue("Second.");
            turn.MarkPlayed(first);
            turn.MarkPlayed(second);

            turn.BeginDraining("First.Second.");

            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Completed));
        }

        [Test]
        public void Reconcile_DivergenceAfterPlaybackCannotBeReportedComplete()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var segment = turn.Enqueue("An unsafe streamed reply.");
            turn.MarkSynthesizing(segment);
            turn.MarkReady(segment);
            turn.MarkPlaying(segment);

            var result = turn.Reconcile("The sanitized final reply.", out _);
            turn.Fail("final_text_mismatch", segment);

            Assert.That(result, Is.EqualTo(StreamingTextReconciliation.Diverged));
            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Failed));
            Assert.That(turn.TryComplete(), Is.False);
        }

        [Test]
        public void BeginAfterAbort_InvalidatesCallbacksFromPreviousTurn()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var staleTurnId = turn.TurnId;
            turn.Abort();

            turn.Begin();

            Assert.That(turn.TurnId, Is.Not.EqualTo(staleTurnId));
            Assert.That(turn.IsCurrent(staleTurnId), Is.False);
            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Receiving));
        }

        [Test]
        public void Reset_InvalidatesCallbacksFromPreviousTurn()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var staleTurnId = turn.TurnId;

            turn.Reset();

            Assert.That(turn.IsCurrent(staleTurnId), Is.False);
            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Idle));
        }

        [Test]
        public void ReplaceUnplayed_IgnoresCallbacksForRemovedSegments()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var removed = turn.Enqueue("Streamed draft.");
            turn.MarkSynthesizing(removed);

            turn.ReplaceUnplayed("Final reply.");
            var current = turn.Enqueue("Final reply.");
            turn.MarkReady(removed);
            turn.Fail("stale preparation failure", removed);

            Assert.That(removed.State, Is.EqualTo(StreamingSpeechSegmentState.Synthesizing));
            Assert.That(current.State, Is.EqualTo(StreamingSpeechSegmentState.Queued));
            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Receiving));
        }

        [Test]
        public void Completion_RejectsPlayedTextThatDoesNotMatchFinalReply()
        {
            var turn = new StreamingAvatarSpeechTurn();
            turn.Begin();
            var segment = turn.Enqueue("A different reply.");
            turn.MarkSynthesizing(segment);
            turn.MarkReady(segment);
            turn.MarkPlaying(segment);
            turn.MarkPlayed(segment);

            turn.BeginDraining("The final reply.");

            Assert.That(turn.State, Is.EqualTo(StreamingSpeechTurnState.Failed));
            Assert.That(turn.Error, Does.Contain("does not match"));
        }
    }
}
