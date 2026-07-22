using System;
using NUnit.Framework;
using SceneTalkVR.Core;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class FeedbackFirstTurnTests
    {
        [TestCase(FormalConditionCode.NE)]
        [TestCase(FormalConditionCode.NR)]
        [TestCase(FormalConditionCode.SE)]
        [TestCase(FormalConditionCode.SR)]
        public void EveryFormalCondition_WithFeedback_IsFeedbackThenDialogue(FormalConditionCode condition)
        {
            var fake = new FakeTurn(condition);
            fake.ResolvePlanner(true, "same correction");
            fake.PrepareDialogueTts();
            Assert.That(fake.Gate.IsDialogueGateOpen, Is.False);
            fake.PlayFeedback();
            fake.PlayDialogue();
            CollectionAssert.AreEqual(new[] { "feedback", "dialogue" }, fake.Playback);
            Assert.That(fake.Gate.State, Is.EqualTo(FeedbackFirstTurnState.Completed));
        }

        [Test]
        public void NoFeedback_ImmediatelyOpensDialogueGate()
        {
            var fake = new FakeTurn(FormalConditionCode.NE);
            fake.ResolvePlanner(false, string.Empty);
            Assert.That(fake.Gate.IsDialogueGateOpen, Is.True);
            fake.PlayDialogue();
            CollectionAssert.AreEqual(new[] { "dialogue" }, fake.Playback);
        }

        [Test]
        public void PreparedDialogue_CannotPlayBeforePlanner()
        {
            var fake = new FakeTurn(FormalConditionCode.SE);
            fake.PrepareDialogueTts();
            Assert.Throws<InvalidOperationException>(() => fake.PlayDialogue());
        }

        [Test]
        public void FeedbackEnd_OpensGateExactlyOnce()
        {
            var fake = new FakeTurn(FormalConditionCode.NE);
            fake.ResolvePlanner(true, "x");
            fake.PlayFeedback();
            Assert.That(fake.Gate.GateOpenCount, Is.EqualTo(1));
            Assert.That(fake.Gate.OpenDialogueGate(), Is.False);
            Assert.That(fake.Gate.GateOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void FeedbackAndDialogue_CannotPlayTwice()
        {
            var fake = new FakeTurn(FormalConditionCode.NR);
            fake.ResolvePlanner(true, "x");
            fake.PlayFeedback();
            Assert.Throws<InvalidOperationException>(() => fake.PlayFeedback());
            fake.PlayDialogue();
            Assert.Throws<InvalidOperationException>(() => fake.PlayDialogue());
        }

        [TestCase(FormalConditionCode.NE, FormalConditionCode.SE)]
        [TestCase(FormalConditionCode.NR, FormalConditionCode.SR)]
        public void ProviderPair_UsesIdenticalFeedbackTextAndHash(FormalConditionCode first, FormalConditionCode second)
        {
            var a = new FakeTurn(first); var b = new FakeTurn(second);
            a.ResolvePlanner(true, "identical model output"); b.ResolvePlanner(true, "identical model output");
            Assert.That(a.FeedbackHash, Is.EqualTo(b.FeedbackHash));
        }

        [Test]
        public void ProviderDoesNotEnterFakePlannerInput()
        {
            var ne = new FakeTurn(FormalConditionCode.NE);
            var se = new FakeTurn(FormalConditionCode.SE);
            Assert.That(ne.PlannerKey, Is.EqualTo(se.PlannerKey));
        }

        [Test]
        public void AgentDialogueCorrectionLeakage_IsTechnicalInvalid()
        {
            var fake = new FakeTurn(FormalConditionCode.SE);
            fake.ResolvePlanner(true, "tip");
            fake.RejectDialogueLeakage("Grammar tip: use are.");
            Assert.That(fake.Gate.State, Is.EqualTo(FeedbackFirstTurnState.TechnicalInvalid));
        }

        [TestCase("correction_planner_timeout")]
        [TestCase("dialogue_timeout")]
        [TestCase("correction_tts_failure")]
        [TestCase("dialogue_tts_failure")]
        public void Failures_CloseGateAndPreserveRealReason(string reason)
        {
            var fake = new FakeTurn(FormalConditionCode.SR);
            fake.Fail(reason);
            Assert.That(fake.Gate.IsDialogueGateOpen, Is.False);
            Assert.That(fake.Gate.InvalidReason, Is.EqualTo(reason));
        }

        [Test]
        public void Reset_ClearsGateQueueAudioAndEvents()
        {
            var fake = new FakeTurn(FormalConditionCode.SE);
            fake.ResolvePlanner(false, string.Empty); fake.PrepareDialogueTts(); fake.AudioPlaying = true;
            fake.Reset();
            Assert.That(fake.Gate.State, Is.EqualTo(FeedbackFirstTurnState.Planning));
            Assert.That(fake.PreparedDialogueCount, Is.Zero);
            Assert.That(fake.AudioPlaying, Is.False);
            Assert.That(fake.Timeline.Events, Is.Empty);
        }

        [Test]
        public void EventTimeline_IsStrictlyMonotonic()
        {
            var timeline = new ExperimentEventTimeline();
            timeline.Add(Event(ExperimentTimingEventType.UserSpeechEnded, 5));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionRequestStarted, 5));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionTextReady, 4));
            Assert.That(timeline.Events[0].monotonicElapsedMs, Is.LessThan(timeline.Events[1].monotonicElapsedMs));
            Assert.That(timeline.Events[1].monotonicElapsedMs, Is.LessThan(timeline.Events[2].monotonicElapsedMs));
        }

        [Test]
        public void TurnSummary_IsExactlyRecomputableFromRawEvents()
        {
            var timeline = new ExperimentEventTimeline();
            timeline.Add(Event(ExperimentTimingEventType.UserSpeechEnded, 10));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionRequestStarted, 12));
            timeline.Add(Event(ExperimentTimingEventType.DialogueRequestStarted, 13));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionTtsStarted, 30));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionTextReady, 32));
            timeline.Add(Event(ExperimentTimingEventType.DialogueFirstSentenceReady, 40));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionTtsReady, 50));
            timeline.Add(Event(ExperimentTimingEventType.DialogueTtsStarted, 55));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionPlaybackStarted, 60));
            timeline.Add(Event(ExperimentTimingEventType.DialogueFirstTtsReady, 65));
            timeline.Add(Event(ExperimentTimingEventType.CorrectionPlaybackEnded, 90));
            timeline.Add(Event(ExperimentTimingEventType.DialoguePlaybackStarted, 100));
            var result = timeline.CalculateSummary();
            Assert.That(result.userEndToFeedbackAudioMs, Is.EqualTo(50));
            Assert.That(result.userEndToDialogueAudioMs, Is.EqualTo(90));
            Assert.That(result.feedbackToDialogueGapMs, Is.EqualTo(10));
            Assert.That(result.correctionGenerationMs, Is.EqualTo(20));
            Assert.That(result.dialogueFirstSentenceGenerationMs, Is.EqualTo(27));
            Assert.That(result.correctionTtsMs, Is.EqualTo(20));
            Assert.That(result.dialogueFirstTtsMs, Is.EqualTo(10));
        }

        private static ExperimentTimingEvent Event(ExperimentTimingEventType type, long ms) => new ExperimentTimingEvent
        { eventType = type.ToString(), monotonicElapsedMs = ms };

        private sealed class FakeTurn
        {
            public readonly FeedbackFirstPlaybackGate Gate = new FeedbackFirstPlaybackGate();
            public readonly ExperimentEventTimeline Timeline = new ExperimentEventTimeline();
            public readonly System.Collections.Generic.List<string> Playback = new System.Collections.Generic.List<string>();
            public int PreparedDialogueCount { get; private set; }
            public bool AudioPlaying { get; set; }
            public string FeedbackHash { get; private set; }
            public string PlannerKey { get; }

            public FakeTurn(FormalConditionCode condition)
            {
                FormalConditionResolver.TryResolve(condition, out _, out var style);
                PlannerKey = "input+" + style;
            }
            public void ResolvePlanner(bool hasFeedback, string text) { FeedbackHash = ExperimentEventTimeline.HashText(text); Gate.PlannerResolved(hasFeedback); }
            public void PrepareDialogueTts() => PreparedDialogueCount++;
            public void PlayFeedback() { Gate.FeedbackStarted(); Playback.Add("feedback"); Gate.FeedbackEnded(); }
            public void PlayDialogue() { Gate.DialogueStarted(); Playback.Add("dialogue"); Gate.DialogueEnded(); }
            public void RejectDialogueLeakage(string _) => Gate.MarkTechnicalInvalid("dialogue_correction_leakage");
            public void Fail(string reason) => Gate.MarkTechnicalInvalid(reason);
            public void Reset() { Gate.Reset(); Timeline.Reset(); PreparedDialogueCount = 0; AudioPlaying = false; Playback.Clear(); }
        }
    }
}
