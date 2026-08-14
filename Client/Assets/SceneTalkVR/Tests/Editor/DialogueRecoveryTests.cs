using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class DialogueRecoveryTests
    {
        [UnityTest]
        public IEnumerator SpeakButton_DoubleClickDuringStartup_DoesNotStopCapture()
        {
            var host = new GameObject("SpeakButtonDebounceTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var speechInput = host.AddComponent<FakeManualSpeechInput>();
                orchestrator.ConfigureModules(speechInput: speechInput);

                orchestrator.ToggleRequestSpeechCapture();
                orchestrator.ToggleRequestSpeechCapture();

                Assert.That(orchestrator.IsSpeechRecording, Is.True);
                Assert.That(speechInput.StopRequestCount, Is.Zero);

                yield return new WaitForSecondsRealtime(0.4f);
                orchestrator.ToggleRequestSpeechCapture();

                Assert.That(speechInput.StopRequestCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator LlmFailure_AbortsStreamingSpeaksPromptAndEnablesRetry()
        {
            var host = new GameObject("DialogueRecoveryTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var voice = host.AddComponent<FakeRecoveryVoice>();
                var speechInput = host.AddComponent<FakeManualSpeechInput>();
                orchestrator.ConfigureModules(speechInput: speechInput, avatarVoice: voice);
                var method = typeof(SceneTalkOrchestrator).GetMethod(
                    "RecoverFromLlmFailure",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                var routine = (IEnumerator)method!.Invoke(
                    orchestrator,
                    new object[] { "HTTP 429", "Dialogue reply generation failed." });
                LogAssert.Expect(
                    LogType.Warning,
                    "[SceneTalkVR] Dialogue reply generation failed. HTTP 429");
                yield return routine;

                Assert.That(voice.AbortCount, Is.EqualTo(1));
                Assert.That(voice.RecoveryCount, Is.EqualTo(1));
                Assert.That(
                    voice.LastPrompt,
                    Is.EqualTo("Sorry, I didn't catch that. Could you say it again?"));
                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Error));
                Assert.That(orchestrator.LastError, Is.EqualTo("Please try again."));
                Assert.That(orchestrator.IsTurnRunning, Is.False);
                Assert.That(orchestrator.IsSpeechRecording, Is.False);

                orchestrator.RetryAfterError();
                yield return null;

                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Recording));
                Assert.That(orchestrator.IsSpeechRecording, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator SpeechRecognitionFailure_RetryStartsFreshRecording()
        {
            var host = new GameObject("SpeechRecognitionRetryTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var speechInput = host.AddComponent<FakeManualSpeechInput>();
                orchestrator.ConfigureModules(speechInput: speechInput);
                var handler = typeof(SceneTalkOrchestrator).GetMethod(
                    "HandleErrorOrFinish",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Speech recognition failed. Please retry recording.");
                var handled = (bool)handler!.Invoke(
                    orchestrator,
                    new object[] { "stt_timeout", "Speech input failed." });

                Assert.That(handled, Is.True);
                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Error));
                Assert.That(orchestrator.LastError,
                    Is.EqualTo("Speech recognition failed. Please retry recording."));

                orchestrator.RetryAfterError();
                yield return null;

                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Recording));
                Assert.That(orchestrator.IsSpeechRecording, Is.True);
                Assert.That(speechInput.StopRequestCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator AvatarPlaybackFailure_StaysOutOfTurnReviewUntilCachedReplyRetrySucceeds()
        {
            var host = new GameObject("AvatarPlaybackRetryTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var voice = host.AddComponent<FakeRecoveryVoice>();
                orchestrator.ConfigureModules(avatarVoice: voice);
                var payload = new SpringScenePayload
                {
                    dialogueReply = "The complete cached reply.",
                    correctionFeedback = new CorrectionFeedbackData { hasFeedback = true }
                };
                var handler = typeof(SceneTalkOrchestrator).GetMethod(
                    "HandleAvatarVoiceErrorOrFinish",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Avatar voice playback failed: tts_timeout");
                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Avatar voice playback failed. Please retry.");
                var handled = (bool)handler!.Invoke(
                    orchestrator,
                    new object[] { "tts_timeout", payload, false });

                Assert.That(handled, Is.True);
                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.Error));
                Assert.That(voice.PresentReplyCount, Is.Zero);

                orchestrator.ToggleDialogueSpeechCapture();
                yield return null;
                yield return null;

                Assert.That(voice.PresentReplyCount, Is.EqualTo(1));
                Assert.That(voice.LastReplyPayload.dialogueReply, Is.EqualTo(payload.dialogueReply));
                Assert.That(voice.LastReplyPayload.correctionFeedback.hasFeedback, Is.False);
                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.TurnReview));
                Assert.That(orchestrator.IsTurnRunning, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator CorrectionPlaybackFailure_RetriesCorrectionThenCompleteCachedReply()
        {
            var host = new GameObject("CorrectionPlaybackRetryTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var voice = host.AddComponent<FakeRecoveryVoice>();
                voice.FailureStage = AvatarReplyPlaybackFailureStage.CorrectionFeedback;
                orchestrator.ConfigureModules(avatarVoice: voice);
                var payload = new SpringScenePayload
                {
                    dialogueReply = "The complete cached reply.",
                    correctionFeedback = new CorrectionFeedbackData
                    {
                        hasFeedback = true,
                        feedbackText = "Use the corrected sentence."
                    }
                };
                var handler = typeof(SceneTalkOrchestrator).GetMethod(
                    "HandleAvatarVoiceErrorOrFinish",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Avatar voice playback failed: correction_tts_timeout");
                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Correction voice playback failed. Please retry.");
                var handled = (bool)handler!.Invoke(
                    orchestrator,
                    new object[] { "correction_tts_timeout", payload, false });

                Assert.That(handled, Is.True);
                orchestrator.ToggleDialogueSpeechCapture();
                yield return null;
                yield return null;

                Assert.That(voice.PresentReplyCount, Is.EqualTo(1));
                Assert.That(voice.LastReplyPayload.dialogueReply, Is.EqualTo(payload.dialogueReply));
                Assert.That(voice.LastReplyPayload.correctionFeedback.hasFeedback, Is.True);
                Assert.That(voice.LastReplyPayload.correctionFeedback.feedbackText,
                    Is.EqualTo(payload.correctionFeedback.feedbackText));
                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.TurnReview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ControllerSpeechCaptureMechanism_IsRemoved()
        {
            Assert.That(typeof(SceneTalkOrchestrator).GetMethod("TryBeginControllerSpeechCapture"), Is.Null);
            Assert.That(typeof(SceneTalkOrchestrator).GetMethod("TryEndControllerSpeechCapture"), Is.Null);
            Assert.That(typeof(SceneTalkOrchestrator).GetMethod("CanUseControllerSpeechCapture"), Is.Null);
            Assert.That(typeof(SceneTalkInteractionBootstrap).GetMethod(
                "TryBeginSpeechTriggerCapture",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(SceneTalkInteractionBootstrap).GetMethod(
                "TryEndSpeechTriggerCapture",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        }

        [Test]
        public void CorrectionSubtitleState_EarlyPlaybackCompletionDoesNotReturnToCorrectionState()
        {
            var host = new GameObject("CorrectionSubtitleStateTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var type = typeof(SceneTalkOrchestrator);
                var invokeFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var cue = new CorrectionSubtitleCue(
                    ExperimentConditionManager.AssistantAgentProvider,
                    "Try saying: I would like a table.");

                type.GetMethod("OnCorrectionSubtitleStarted", invokeFlags)
                    .Invoke(orchestrator, new object[] { cue });
                var payload = new SpringScenePayload
                {
                    correctionFeedback = new CorrectionFeedbackData
                    {
                        hasFeedback = true,
                        provider = ExperimentConditionManager.AssistantAgentProvider,
                        style = ExperimentConditionManager.ExplicitStyle,
                        feedbackText = "Try saying: I would like a table."
                    }
                };
                type.GetMethod("PrepareCorrectionReview", invokeFlags).Invoke(
                    orchestrator,
                    new object[] { payload, false });

                Assert.That(orchestrator.LastCorrectionSpokenProvider,
                    Is.EqualTo(ExperimentConditionManager.AssistantAgentProvider));
                Assert.That(orchestrator.LastCorrectionSpokenText,
                    Is.EqualTo("Try saying: I would like a table."));
                Assert.That(
                    type.GetMethod("ResolveReplyPlaybackState", invokeFlags).Invoke(orchestrator, null),
                    Is.EqualTo(SceneTalkState.CorrectionFeedbackSpeaking));

                type.GetMethod("OnCorrectionPlaybackCompleted", invokeFlags).Invoke(
                    orchestrator,
                    new object[]
                    {
                        new CorrectionPlaybackResult
                        {
                            provider = ExperimentConditionManager.AssistantAgentProvider,
                            outcome = "played"
                        }
                    });
                type.GetMethod("PrepareCorrectionReview", invokeFlags).Invoke(
                    orchestrator,
                    new object[] { payload, false });

                Assert.That(
                    type.GetMethod("ResolveReplyPlaybackState", invokeFlags).Invoke(orchestrator, null),
                    Is.EqualTo(SceneTalkState.DialogueSpeaking));

                type.GetMethod("BeginTurnSubtitleState", invokeFlags).Invoke(orchestrator, null);
                Assert.That(orchestrator.LastCorrectionSpokenText, Is.Empty);
                Assert.That(orchestrator.LastCorrectionSpokenProvider, Is.Empty);
                Assert.That(orchestrator.CurrentDialogueSubtitleText, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TurnSubtitleSynchronization_ReleasesCorrectionAndReplyTogether()
        {
            var host = new GameObject("TurnSubtitleSynchronizationTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var type = typeof(SceneTalkOrchestrator);
                var invokeFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var beginTurn = type.GetMethod("BeginTurnSubtitleState", invokeFlags);
                var resolvePlan = type.GetMethod("OnCorrectionPlanResolved", invokeFlags);
                var startCorrection = type.GetMethod("OnCorrectionSubtitleStarted", invokeFlags);
                var replySetter = type.GetProperty(nameof(SceneTalkOrchestrator.CurrentDialogueSubtitleText))
                    ?.GetSetMethod(true);
                var feedback = new CorrectionFeedbackData
                {
                    hasFeedback = true,
                    provider = ExperimentConditionManager.AssistantAgentProvider,
                    feedbackText = "Try saying: I would like a table."
                };
                var cue = new CorrectionSubtitleCue(
                    ExperimentConditionManager.AssistantAgentProvider,
                    feedback.feedbackText);

                beginTurn!.Invoke(orchestrator, null);
                replySetter!.Invoke(orchestrator, new object[] { "Here is the role reply." });
                resolvePlan!.Invoke(orchestrator, new object[] { feedback });
                Assert.That(orchestrator.AreTurnSubtitlesReady, Is.False,
                    "A reply must remain hidden while its correction subtitle is pending.");

                startCorrection!.Invoke(orchestrator, new object[] { cue });
                Assert.That(orchestrator.AreTurnSubtitlesReady, Is.True,
                    "The correction cue must release the buffered reply in the same update.");

                beginTurn.Invoke(orchestrator, null);
                startCorrection.Invoke(orchestrator, new object[] { cue });
                Assert.That(orchestrator.AreTurnSubtitlesReady, Is.False,
                    "An early correction cue must wait for dialogue text.");
                replySetter.Invoke(orchestrator, new object[] { "Here is the role reply." });
                Assert.That(orchestrator.AreTurnSubtitlesReady, Is.True,
                    "The first dialogue text must release an already buffered correction cue.");

                beginTurn.Invoke(orchestrator, null);
                replySetter.Invoke(orchestrator, new object[] { "No correction is required." });
                resolvePlan.Invoke(orchestrator, new object[] { null });
                Assert.That(orchestrator.AreTurnSubtitlesReady, Is.True,
                    "A resolved no-correction plan must not delay dialogue subtitles.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private sealed class FakeRecoveryVoice : MonoBehaviour,
            ISceneTalkAvatarRecoveryVoice,
            ISceneTalkStreamingAvatarVoice,
            ISceneTalkAvatarPlaybackDiagnostics
        {
            public int AbortCount { get; private set; }
            public int RecoveryCount { get; private set; }
            public int PresentReplyCount { get; private set; }
            public string LastPrompt { get; private set; }
            public SpringScenePayload LastReplyPayload { get; private set; }
            public AvatarReplyPlaybackFailureStage FailureStage { get; set; }
            public AvatarReplyPlaybackFailureStage LastFailureStage => FailureStage;

            public IEnumerator PresentRecoveryPrompt(
                string prompt,
                Action onComplete,
                Action<string> onError)
            {
                RecoveryCount++;
                LastPrompt = prompt;
                onComplete?.Invoke();
                yield break;
            }

            public IEnumerator PresentReply(
                SpringScenePayload payload,
                Action onComplete,
                Action<string> onError)
            {
                PresentReplyCount++;
                LastReplyPayload = payload;
                yield return null;
                onComplete?.Invoke();
            }

            public void PrepareStreaming(SpringScenePayload basePayload) { }
            public void EnqueueSentence(string sentence) { }
            public void CompleteStreaming(string expectedDialogueText) { }
            public void OpenDialogueGate() { }
            public void AbortStreaming() => AbortCount++;
        }

        private sealed class FakeManualSpeechInput : MonoBehaviour,
            ISceneTalkSpeechInput,
            ISceneTalkManualSpeechInput
        {
            public int StopRequestCount { get; private set; }

            public IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError)
            {
                while (StopRequestCount == 0)
                {
                    yield return null;
                }

                onComplete?.Invoke("test");
            }

            public void RequestStopCapture() => StopRequestCount++;
            public void CancelCapture() { }
        }
    }
}
