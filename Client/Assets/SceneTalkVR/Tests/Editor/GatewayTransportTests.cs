using NUnit.Framework;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class GatewayTransportTests
    {
        [Test]
        public void UsbPreferred_ProbesAtomicUsbRouteBeforeLan()
        {
            var state = new GatewayTransportStateMachine();
            state.Configure(GatewayTransportPreference.UsbPreferred);

            Assert.That(
                state.PreferredProbeOrder(false),
                Is.EqualTo(new[] { GatewayTransportKind.Usb, GatewayTransportKind.Lan }));

            state.BeginProbe(GatewayTransportKind.Usb);
            Assert.That(state.State, Is.EqualTo(GatewayTransportState.ProbingUsb));
            state.MarkReady(Route(GatewayTransportKind.Usb));
            Assert.That(state.State, Is.EqualTo(GatewayTransportState.UsbReady));
            Assert.That(state.CurrentRoute.voiceBaseUrl, Does.Contain("8787"));
            Assert.That(state.CurrentRoute.llmApiUrl, Does.Contain("8788"));
        }

        [Test]
        public void UsbFailure_RecoveryCanOnlySelectLanOnce()
        {
            var state = new GatewayTransportStateMachine();
            state.Configure(GatewayTransportPreference.UsbPreferred);
            state.MarkReady(Route(GatewayTransportKind.Usb));

            Assert.That(
                state.RecoveryOrder(GatewayTransportKind.Usb),
                Is.EqualTo(new[] { GatewayTransportKind.Lan }));
            Assert.That(
                state.RecoveryOrder(GatewayTransportKind.Lan),
                Is.EqualTo(new[] { GatewayTransportKind.Usb }));
        }

        [Test]
        public void IncompleteRoute_CannotBecomeReady()
        {
            var state = new GatewayTransportStateMachine();
            state.Configure(GatewayTransportPreference.UsbPreferred);

            Assert.Throws<System.ArgumentException>(() => state.MarkReady(new GatewayRouteSnapshot
            {
                transport = GatewayTransportKind.Usb,
                voiceBaseUrl = "http://127.0.0.1:8787",
                llmApiUrl = string.Empty
            }));
        }

        [Test]
        public void OldRuntimeConfig_DefaultsToLanOnly()
        {
            var config = ScriptableObject.CreateInstance<SceneTalkRuntimeConfig>();
            try
            {
                Assert.That(config.TransportPreference, Is.EqualTo(GatewayTransportPreference.LanOnly));
                config.ConfigurePicoRealRunDefaults();
                Assert.That(config.TransportPreference, Is.EqualTo(GatewayTransportPreference.UsbPreferred));
                Assert.That(config.UsbVoiceGatewayBaseUrl, Is.EqualTo("http://127.0.0.1:8787"));
                Assert.That(config.UsbLlmApiUrl, Does.Contain("127.0.0.1:8788"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void PicoLoopback_RequiresExplicitUsbPreference()
        {
            var catalog = ScriptableObject.CreateInstance<ExperimentDeploymentCatalog>();
            try
            {
                var profile = PicoProfile(GatewayTransportPreference.LanOnly);
                catalog.EditorSet("test", new[] { profile });
                Assert.That(
                    catalog.ValidateForCollection(ExperimentDeploymentProfileId.PicoLab, out var lanError),
                    Is.False);
                Assert.That(lanError, Does.Contain("pico_endpoint_loopback_forbidden"));

                profile.transportPreference = GatewayTransportPreference.UsbPreferred;
                catalog.EditorSet("test", new[] { profile });
                Assert.That(
                    catalog.ValidateForCollection(ExperimentDeploymentProfileId.PicoLab, out var usbError),
                    Is.True,
                    usbError);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void HealthUrlsAndChineseStates_AreStable()
        {
            Assert.That(
                GatewayTransportRouter.BuildHealthUrl(
                    "http://127.0.0.1:8788/api/llm/chat/completions"),
                Is.EqualTo("http://127.0.0.1:8788/health"));
            Assert.That(
                GatewayTransportRouter.ResolveChineseStatus(GatewayTransportState.UsbReady),
                Is.EqualTo("USB 数据线"));
            Assert.That(
                GatewayTransportRouter.ResolveChineseStatus(GatewayTransportState.LanReady),
                Is.EqualTo("局域网备用"));
            Assert.That(
                GatewayTransportRouter.ResolveChineseStatus(GatewayTransportState.Unavailable),
                Is.EqualTo("不可用"));
        }

        [Test]
        public void TtsClipValidation_RejectsMetadataMismatch()
        {
            var clip = AudioClip.Create("gateway-test", 2400, 1, 24000, false);
            try
            {
                Assert.That(VoiceGatewayClient.ValidateDownloadedAudioClip(
                    new TtsResponse { format = "wav", sampleRate = 16000 },
                    clip,
                    out var error), Is.False);
                Assert.That(error, Does.Contain("sample rate"));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        private static GatewayRouteSnapshot Route(GatewayTransportKind transport)
        {
            var host = transport == GatewayTransportKind.Usb ? "127.0.0.1" : "192.168.137.1";
            return new GatewayRouteSnapshot
            {
                transport = transport,
                voiceBaseUrl = $"http://{host}:8787",
                llmApiUrl = $"http://{host}:8788/api/llm/chat/completions",
                selectedAtUtc = "2026-07-28T00:00:00Z"
            };
        }

        private static ExperimentDeploymentProfile PicoProfile(
            GatewayTransportPreference preference)
        {
            return new ExperimentDeploymentProfile
            {
                profileId = ExperimentDeploymentProfileId.PicoLab,
                voiceGatewayBaseUrl = "http://127.0.0.1:8787",
                llmGatewayApiUrl = "http://127.0.0.1:8788/api/llm/chat/completions",
                transportPreference = preference,
                requestTimeoutSeconds = 30,
                sttProvider = "tencent",
                ttsProvider = "tencent",
                networkRequired = true,
                approvedForCollection = true,
                collectionAllowed = true,
                target = ExperimentDeploymentTarget.Pico,
                picoRequired = true,
                evidenceReference = "usb-test"
            };
        }
    }
}
