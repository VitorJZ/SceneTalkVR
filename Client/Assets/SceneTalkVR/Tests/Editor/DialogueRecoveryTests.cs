using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
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
                orchestrator.ConfigureModules(avatarVoice: voice);
                var method = typeof(SceneTalkOrchestrator).GetMethod(
                    "RecoverFromLlmFailure",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                var routine = (IEnumerator)method!.Invoke(
                    orchestrator,
                    new object[] { "HTTP 429", "Dialogue reply generation failed." });
                LogAssert.Expect(
                    LogType.Error,
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
                Assert.That(orchestrator.CanUseControllerSpeechCapture(), Is.True);
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
        public IEnumerator ControllerSpeechTrigger_AvatarPlaybackErrorRetriesCachedReply()
        {
            var host = new GameObject("AvatarPlaybackControllerRetryTests");
            try
            {
                var orchestrator = host.AddComponent<SceneTalkOrchestrator>();
                var voice = host.AddComponent<FakeRecoveryVoice>();
                orchestrator.ConfigureModules(avatarVoice: voice);
                var payload = new SpringScenePayload { dialogueReply = "Retry this reply." };
                var handler = typeof(SceneTalkOrchestrator).GetMethod(
                    "HandleAvatarVoiceErrorOrFinish",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Avatar voice playback failed: playback_stopped");
                LogAssert.Expect(
                    LogType.Error,
                    "[SceneTalkVR] Avatar voice playback failed. Please retry.");
                handler!.Invoke(orchestrator, new object[] { "playback_stopped", payload, false });

                Assert.That(orchestrator.TryBeginControllerSpeechCapture(), Is.False);
                yield return null;
                yield return null;

                Assert.That(voice.PresentReplyCount, Is.EqualTo(1));
                Assert.That(voice.LastReplyPayload.dialogueReply, Is.EqualTo(payload.dialogueReply));
                Assert.That(orchestrator.CurrentState, Is.EqualTo(SceneTalkState.TurnReview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private sealed class FakeRecoveryVoice : MonoBehaviour,
            ISceneTalkAvatarRecoveryVoice,
            ISceneTalkStreamingAvatarVoice
        {
            public int AbortCount { get; private set; }
            public int RecoveryCount { get; private set; }
            public int PresentReplyCount { get; private set; }
            public string LastPrompt { get; private set; }
            public SpringScenePayload LastReplyPayload { get; private set; }

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
