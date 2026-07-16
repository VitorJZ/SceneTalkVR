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
    }
}
