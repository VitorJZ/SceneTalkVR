using System.Collections;
using NUnit.Framework;
using SceneTalkVR.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneTalkVR.AvatarSystem.Tests
{
    public sealed class AvatarSpeechPlayerTests
    {
        [UnityTest]
        public IEnumerator Prepare_WithDemoClip_DoesNotStartPlayback()
        {
            var gameObject = new GameObject("AvatarSpeechPlayerTests");
            var demoClip = AudioClip.Create("demo", 1600, 1, 16000, false);
            try
            {
                var audioSource = gameObject.AddComponent<AudioSource>();
                var context = new AvatarSpeechPlaybackContext
                {
                    defaultAudioSource = audioSource,
                    demoReplyClip = demoClip,
                    useVoiceGatewayTts = false
                };
                var playbackStarted = false;
                var playbackEnded = false;
                var request = new AvatarSpeechPlaybackRequest
                {
                    text = "Prepared reply",
                    logLabel = "Test reply",
                    playbackStarted = () => playbackStarted = true,
                    playbackEnded = () => playbackEnded = true
                };
                PreparedAvatarSpeech preparedSpeech = null;

                yield return new AvatarSpeechPlayer().Prepare(
                    context,
                    new SpringScenePayload(),
                    request,
                    value => preparedSpeech = value);

                Assert.That(preparedSpeech, Is.Not.Null);
                Assert.That(preparedSpeech.clip, Is.SameAs(demoClip));
                Assert.That(preparedSpeech.ownsClip, Is.False);
                Assert.That(preparedSpeech.fallbackLevel, Is.EqualTo("demo_clip"));
                Assert.That(audioSource.isPlaying, Is.False);
                Assert.That(playbackStarted, Is.False);
                Assert.That(playbackEnded, Is.False);

                preparedSpeech.Release();
                Assert.That(demoClip, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(demoClip);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CreateTurnId_CalledInSameFrame_ReturnsUniqueValues()
        {
            var first = AvatarSpeechPlayer.CreateTurnId();
            var second = AvatarSpeechPlayer.CreateTurnId();

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Does.StartWith($"turn-{Time.frameCount}-"));
            Assert.That(second, Does.StartWith($"turn-{Time.frameCount}-"));
        }

        [TestCase("female", "male", "custom_voice", "default_female_en")]
        [TestCase("male", "female", "custom_voice", "default_male_en")]
        [TestCase("", "male", "custom_voice", "default_male_en")]
        [TestCase("unknown", "female", "custom_voice", "default_female_en")]
        [TestCase("unknown", "unknown", "custom_voice", "custom_voice")]
        [TestCase("", "", "", "default_female_en")]
        public void ResolveVoiceId_PrioritizesResolvedAvatarGender(
            string avatarGender,
            string requestedGender,
            string defaultVoiceId,
            string expectedVoiceId)
        {
            var context = new AvatarSpeechPlaybackContext
            {
                currentAvatarGenderPresentation = avatarGender,
                defaultVoiceId = defaultVoiceId
            };
            var payload = new SpringScenePayload
            {
                avatarRole = new AvatarRoleData
                {
                    appearance = new AvatarAppearanceData
                    {
                        genderPresentation = requestedGender
                    }
                }
            };

            Assert.That(
                AvatarSpeechPlayer.ResolveVoiceId(context, payload),
                Is.EqualTo(expectedVoiceId));
        }

        [Test]
        public void ResolveCorrectionVoiceIdOverride_DialogueAvatar_UsesAvatarVoice()
        {
            Assert.That(
                CorrectionFeedbackPresenter.ResolveCorrectionVoiceIdOverride(
                    useDialogueAvatar: true,
                    rehearsalFeedbackVoiceId: "feedback_voice",
                    experimentFeedbackVoiceId: "experiment_voice",
                    assistantAgentVoiceId: "assistant_voice"),
                Is.Null);
        }

        [TestCase("feedback_voice", "experiment_voice", "assistant_voice", "feedback_voice")]
        [TestCase("", "experiment_voice", "assistant_voice", "experiment_voice")]
        [TestCase("", "", "assistant_voice", "assistant_voice")]
        public void ResolveCorrectionVoiceIdOverride_AssistantAndPilotUseSharedExperimentVoice(
            string rehearsalFeedbackVoiceId,
            string experimentFeedbackVoiceId,
            string assistantAgentVoiceId,
            string expectedVoiceId)
        {
            Assert.That(
                CorrectionFeedbackPresenter.ResolveCorrectionVoiceIdOverride(
                    useDialogueAvatar: false,
                    rehearsalFeedbackVoiceId,
                    experimentFeedbackVoiceId,
                    assistantAgentVoiceId),
                Is.EqualTo(expectedVoiceId));
        }

        [TestCase("explicit")]
        [TestCase("recast")]
        public void ResolveExperimentFeedbackProfileKey_PilotAndFormalShareFormalProfile(string style)
        {
            var catalog = ScriptableObject.CreateInstance<ExperimentVoiceProfileCatalog>();
            try
            {
                catalog.EditorSet(
                    "test",
                    "shared_correction_voice",
                    "shared_correction_voice",
                    "shared_correction_voice",
                    new ExperimentVoiceProfile[0]);

                Assert.That(
                    CorrectionFeedbackPresenter.ResolveExperimentFeedbackProfileKey(catalog, style),
                    Is.EqualTo("shared_correction_voice"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
