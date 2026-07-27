using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;

namespace SceneTalkVR.Tests
{
    public sealed class VoiceGatewayPolicyTests
    {
        [Test]
        public void LiveRuntimeOptions_ApplyTimeoutAndRejectMockTts()
        {
            var host = new GameObject("VoiceGatewayPolicyTests");
            try
            {
                var client = host.AddComponent<VoiceGatewayClient>();
                client.ConfigureRuntime(new VoiceGatewayRuntimeOptions(
                    "http://192.168.137.1:8787",
                    30,
                    "tencent",
                    false));

                var allowed = client.ValidateTtsProvider(new TtsResponse
                {
                    provider = "mock",
                    fallbackLevel = "mock_after_tencent_error:timeout",
                    audioUrl = "/api/voice/audio/test.wav"
                }, out var error);

                Assert.That(client.EffectiveRequestTimeoutSeconds, Is.EqualTo(30));
                Assert.That(allowed, Is.False);
                Assert.That(
                    GatewaySpeechInputModule.AllowsFallbackTranscript(
                        true,
                        false,
                        "fixed fallback transcript",
                        client),
                    Is.False);
                StringAssert.Contains("mock", error.ToLowerInvariant());
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MockOfflineRuntimeOptions_AllowExplicitMockTts()
        {
            var host = new GameObject("VoiceGatewayMockPolicyTests");
            try
            {
                var client = host.AddComponent<VoiceGatewayClient>();
                client.ConfigureRuntime(new VoiceGatewayRuntimeOptions(
                    "http://127.0.0.1:8787",
                    5,
                    "mock",
                    true));

                var allowed = client.ValidateTtsProvider(new TtsResponse
                {
                    provider = "mock",
                    fallbackLevel = "mock_audio",
                    audioUrl = "/api/voice/audio/test.wav"
                }, out var error);

                Assert.That(allowed, Is.True, error);
                Assert.That(client.EffectiveRequestTimeoutSeconds, Is.EqualTo(5));
                Assert.That(
                    GatewaySpeechInputModule.AllowsFallbackTranscript(
                        true,
                        false,
                        "fixed fallback transcript",
                        client),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TtsTextAcknowledgement_CountsUnicodeScalarsLikeGateway()
        {
            Assert.That(
                VoiceGatewayClient.ValidateTtsTextAcknowledgement(
                    " A\U0001F600B ",
                    3,
                    out var error),
                Is.True,
                error);
        }

        [Test]
        public void PicoRealDefaults_ForceLiveVoicePolicy()
        {
            var config = ScriptableObject.CreateInstance<SceneTalkRuntimeConfig>();
            try
            {
                SetPrivateField(config, "voiceGatewayRequestTimeoutSeconds", 5);
                SetPrivateField(config, "expectedTtsProvider", "mock");
                SetPrivateField(config, "allowMockTtsProvider", true);

                config.ConfigurePicoRealRunDefaults();

                Assert.That(config.VoiceGatewayRequestTimeoutSeconds, Is.EqualTo(30));
                Assert.That(config.ExpectedTtsProvider, Is.EqualTo("tencent"));
                Assert.That(config.AllowMockTtsProvider, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field!.SetValue(target, value);
        }
    }
}
