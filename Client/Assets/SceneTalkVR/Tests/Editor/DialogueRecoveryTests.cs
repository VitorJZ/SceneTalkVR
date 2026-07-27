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

        private sealed class FakeRecoveryVoice : MonoBehaviour,
            ISceneTalkAvatarRecoveryVoice,
            ISceneTalkStreamingAvatarVoice
        {
            public int AbortCount { get; private set; }
            public int RecoveryCount { get; private set; }
            public string LastPrompt { get; private set; }

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
                onComplete?.Invoke();
                yield break;
            }

            public void PrepareStreaming(SpringScenePayload basePayload) { }
            public void EnqueueSentence(string sentence) { }
            public void SignalStreamingComplete() { }
            public void OpenDialogueGate() { }
            public void AbortStreaming() => AbortCount++;
        }
    }
}
