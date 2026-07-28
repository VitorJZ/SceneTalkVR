using System;
using System.Net;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.Demo;
using SceneTalkVR.Runtime.Services;
using SceneTalkVR.Voice;
using UnityEngine;

namespace SceneTalkVR.Runtime
{
    public sealed class SceneTalkRuntimeConfigApplier : MonoBehaviour
    {
        [SerializeField] private SceneTalkRuntimeConfig config;
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool logAppliedConfig = true;

        [Header("Scene Modules")]
        [SerializeField] private SceneTalkOrchestrator orchestrator;
        [SerializeField] private GatewaySpeechInputModule gatewaySpeechInput;
        [SerializeField] private DemoSpeechInputModule demoSpeechInput;
        [SerializeField] private RealLLMService realLlmService;
        [SerializeField] private DemoBrainModule demoBrainModule;
        [SerializeField] private HybridScenePresenter hybridScenePresenter;
        [SerializeField] private HolodeckSceneService holodeckSceneService;
        [SerializeField] private PanoramaSceneService panoramaSceneService;
        [SerializeField] private AvatarPresentationVoiceModule avatarVoiceModule;
        [SerializeField] private VoiceGatewayClient voiceGatewayClient;
        [SerializeField] private ExperimentConditionManager experimentConditionManager;
        [SerializeField] private GatewayTransportRouter gatewayTransportRouter;

        public SceneTalkRuntimeConfig Config => config;

        public bool TryConfigureHistoryBrainMode(SceneTalkBrainRuntimeMode mode, out string error)
        {
            error = string.Empty;
            if (mode == SceneTalkBrainRuntimeMode.KeepCurrent)
            {
                return true;
            }

            ResolveModules();
            MonoBehaviour brain = mode switch
            {
                SceneTalkBrainRuntimeMode.DemoBrain => demoBrainModule,
                SceneTalkBrainRuntimeMode.DirectRealLlm => realLlmService,
                _ => null
            };

            if (orchestrator == null || brain == null)
            {
                error = $"The scene does not contain the module required for Brain mode '{mode}'.";
                return false;
            }

            orchestrator.ConfigureModules(brain: brain);
            return true;
        }

        private void Awake()
        {
            if (applyOnAwake)
            {
                ApplyRuntimeConfig();
            }
        }

        public void ApplyRuntimeConfig()
        {
            if (config == null)
            {
                Debug.LogWarning("[SceneTalkVR] Runtime config is not assigned. Existing scene module settings will be used.", this);
                return;
            }

            ResolveModules();
            var formalLock = experimentConditionManager != null && experimentConditionManager.IsFormalExperiment;
            config.ConfigureFormalModeRuntimeLock(formalLock);
            if (formalLock && !experimentConditionManager.ValidateFormalProtocol(out var protocolError))
            {
                Debug.LogError($"[SceneTalkVR] Formal Mode startup blocked: {protocolError}", this);
                enabled = false;
                return;
            }
            ConfigureGatewayTransport();
            ConfigureVoiceGateway();
            ConfigureBrain();
            ConfigureSceneServices();
            ConfigureAvatarVoice();
            ConfigureOrchestratorModules();

            if (logAppliedConfig)
            {
                Debug.Log(BuildAppliedConfigLog(), this);
            }
        }

        private void ResolveModules()
        {
            orchestrator = Resolve(orchestrator);
            gatewaySpeechInput = Resolve(gatewaySpeechInput);
            demoSpeechInput = Resolve(demoSpeechInput);
            realLlmService = Resolve(realLlmService);
            demoBrainModule = Resolve(demoBrainModule);
            hybridScenePresenter = Resolve(hybridScenePresenter);
            holodeckSceneService = Resolve(holodeckSceneService);
            panoramaSceneService = Resolve(panoramaSceneService);
            avatarVoiceModule = Resolve(avatarVoiceModule);
            voiceGatewayClient = Resolve(voiceGatewayClient);
            experimentConditionManager = Resolve(experimentConditionManager);
            gatewayTransportRouter = Resolve(gatewayTransportRouter);
            if (gatewayTransportRouter == null)
            {
                gatewayTransportRouter = gameObject.AddComponent<GatewayTransportRouter>();
            }
        }

        private void ConfigureGatewayTransport()
        {
            if (gatewayTransportRouter == null)
            {
                return;
            }

            var preference = config.TransportPreference;
            var lanVoiceUrl = config.HasVoiceGatewayBaseUrl
                ? ResolveServiceUrlForRuntime(config.VoiceGatewayBaseUrl, 8787)
                : voiceGatewayClient?.GatewayBaseUrl ?? string.Empty;
            var lanLlmUrl = ResolveServiceUrlForRuntime(config.DirectLlmApiUrl, 8788);
            var allowMockProvider = config.AllowMockTtsProvider;

            if (TryResolveActiveVoiceDeployment(out var deployment))
            {
                preference = deployment.transportPreference;
                if (!string.IsNullOrWhiteSpace(deployment.voiceGatewayBaseUrl))
                {
                    lanVoiceUrl = ResolveServiceUrlForRuntime(deployment.voiceGatewayBaseUrl, 8787);
                }

                if (!string.IsNullOrWhiteSpace(deployment.llmGatewayApiUrl))
                {
                    lanLlmUrl = ResolveServiceUrlForRuntime(deployment.llmGatewayApiUrl, 8788);
                }

                allowMockProvider = deployment.profileId == ExperimentDeploymentProfileId.MockOffline;
            }

            gatewayTransportRouter.Configure(new GatewayTransportConfiguration
            {
                preference = preference,
                usbVoiceBaseUrl = config.UsbVoiceGatewayBaseUrl,
                usbLlmApiUrl = config.UsbLlmApiUrl,
                lanVoiceBaseUrl = lanVoiceUrl,
                lanLlmApiUrl = lanLlmUrl,
                probeTimeoutSeconds = config.GatewayProbeTimeoutSeconds,
                requireLiveTransport = config.UseVoiceGatewaySpeech
                    && config.UseVoiceGatewayTts
                    && config.BrainMode == SceneTalkBrainRuntimeMode.DirectRealLlm
                    && !allowMockProvider
            });

            voiceGatewayClient?.ConfigureTransportRouter(gatewayTransportRouter);
            realLlmService?.ConfigureTransportRouter(gatewayTransportRouter);
        }

        private void ConfigureVoiceGateway()
        {
            if (voiceGatewayClient == null)
            {
                return;
            }

            var baseUrl = config.HasVoiceGatewayBaseUrl
                ? ResolveServiceUrlForRuntime(config.VoiceGatewayBaseUrl, 8787)
                : voiceGatewayClient.GatewayBaseUrl;
            var timeoutSeconds = config.VoiceGatewayRequestTimeoutSeconds;
            var expectedProvider = config.ExpectedTtsProvider;
            var allowMockProvider = config.AllowMockTtsProvider;

            if (TryResolveActiveVoiceDeployment(out var deployment))
            {
                if (!string.IsNullOrWhiteSpace(deployment.voiceGatewayBaseUrl))
                {
                    baseUrl = ResolveServiceUrlForRuntime(deployment.voiceGatewayBaseUrl, 8787);
                }

                timeoutSeconds = Mathf.Max(1, deployment.requestTimeoutSeconds);
                expectedProvider = string.IsNullOrWhiteSpace(deployment.ttsProvider)
                    ? expectedProvider
                    : deployment.ttsProvider.Trim().ToLowerInvariant();
                allowMockProvider = deployment.profileId == ExperimentDeploymentProfileId.MockOffline;
            }

            voiceGatewayClient.ConfigureRuntime(new VoiceGatewayRuntimeOptions(
                baseUrl,
                timeoutSeconds,
                expectedProvider,
                allowMockProvider));
        }

        public void RefreshVoiceGatewayConfiguration()
        {
            ResolveModules();
            if (config != null)
            {
                ConfigureGatewayTransport();
                ConfigureVoiceGateway();
            }
        }

        private bool TryResolveActiveVoiceDeployment(out ExperimentDeploymentProfile deployment)
        {
            var rehearsal = RehearsalSessionCoordinator.Active;
            if (rehearsal != null && rehearsal.IsActive && rehearsal.DeploymentCatalog != null)
            {
                var profileId = rehearsal.IsDeviceValidation
                    ? ExperimentDeploymentProfileId.PicoDeviceValidation
                    : ExperimentDeploymentProfileId.RehearsalEditor;
                if (rehearsal.DeploymentCatalog.TryGet(profileId, out deployment))
                {
                    return true;
                }
            }

            if (experimentConditionManager != null
                && experimentConditionManager.DeploymentCatalog != null
                && experimentConditionManager.DeploymentCatalog.TryGet(
                    experimentConditionManager.DeploymentProfile,
                    out deployment))
            {
                return true;
            }

            deployment = null;
            return false;
        }

        private void ConfigureBrain()
        {
            if (realLlmService != null)
            {
                realLlmService.ConfigureApi(
                    ResolveServiceUrlForRuntime(config.DirectLlmApiUrl, 8788),
                    config.DirectLlmModelName);
                realLlmService.ConfigureCorrectionPolicy(config.CorrectionPolicy);
                realLlmService.ConfigureDialoguePacing(
                    config.Temperature,
                    config.MaxNonGoalQuestionsPerTask);
                realLlmService.ConfigureTransportRouter(gatewayTransportRouter);
            }
        }

        private void ConfigureSceneServices()
        {
            if (holodeckSceneService != null)
            {
                holodeckSceneService.ConfigureFormalModeLock(config.IsFormalModeRuntimeLocked);
                holodeckSceneService.ConfigureBackend(
                    config.UseHolodeckBackend,
                    config.HolodeckBackendUrl,
                    config.HolodeckTimeoutSeconds);
            }

            if (panoramaSceneService != null)
            {
                panoramaSceneService.ConfigureFormalModeLock(config.IsFormalModeRuntimeLocked);
                panoramaSceneService.ConfigureRuntime(
                    config.ForceFallbackPanorama,
                    config.PanoramaModelName,
                    config.PanoramaImageSize);
            }

            if (hybridScenePresenter != null)
            {
                hybridScenePresenter.ConfigureRuntime(
                    config.OnlyUsePanorama,
                    config.EnableSpatialClipping,
                    config.MaxSpawnCount);
            }
        }

        private void ConfigureAvatarVoice()
        {
            if (avatarVoiceModule == null)
            {
                return;
            }

            avatarVoiceModule.ConfigureVoiceGateway(
                config.UseVoiceGatewayTts,
                config.UseVoiceGatewayTts ? voiceGatewayClient : null);
        }

        private void ConfigureOrchestratorModules()
        {
            if (orchestrator == null)
            {
                return;
            }

            var speech = (!config.IsFormalModeRuntimeLocked && config.UseDeveloperTextConsole) || !config.UseVoiceGatewaySpeech
                ? demoSpeechInput as MonoBehaviour
                : gatewaySpeechInput as MonoBehaviour;

            if (demoSpeechInput is SceneTalkVR.Demo.DemoSpeechInputModule demoSpeech)
            {
                demoSpeech.EnableDeveloperConsole = !config.IsFormalModeRuntimeLocked && config.UseDeveloperTextConsole;
            }

            MonoBehaviour brain = null;
            switch (config.BrainMode)
            {
                case SceneTalkBrainRuntimeMode.DemoBrain:
                    brain = demoBrainModule;
                    break;
                case SceneTalkBrainRuntimeMode.DirectRealLlm:
                    brain = realLlmService;
                    break;
            }

            orchestrator.ConfigureModules(
                speechInput: speech,
                brain: brain,
                scenePresenter: hybridScenePresenter,
                avatarVoice: avatarVoiceModule);
        }

        private string BuildAppliedConfigLog()
        {
            var effectiveVoiceGatewayUrl = config.HasVoiceGatewayBaseUrl
                ? ResolveServiceUrlForRuntime(config.VoiceGatewayBaseUrl, 8787)
                : "<scene default>";
            var effectiveLlmUrl = ResolveServiceUrlForRuntime(config.DirectLlmApiUrl, 8788);
            return "[SceneTalkVR] Runtime config applied. "
                + $"brain={config.BrainMode}, "
                + $"voiceGateway={effectiveVoiceGatewayUrl}, "
                + $"voiceTimeout={voiceGatewayClient?.EffectiveRequestTimeoutSeconds ?? config.VoiceGatewayRequestTimeoutSeconds}s, "
                + $"ttsProvider={voiceGatewayClient?.EffectiveExpectedTtsProvider ?? config.ExpectedTtsProvider}, "
                + $"llm={effectiveLlmUrl}, "
                + $"transport={gatewayTransportRouter?.State.ToString() ?? "legacy"}, "
                + $"avatarPacingTemperature={config.Temperature:0.###}, "
                + $"maxNonGoalQuestionsPerTask={config.MaxNonGoalQuestionsPerTask}, "
                + $"holodeck={(config.UseHolodeckBackend ? config.HolodeckBackendUrl : "mock layout")}, "
                + $"onlyUsePanorama={config.OnlyUsePanorama}, "
                + $"forceFallbackPanorama={config.ForceFallbackPanorama}, "
                + $"maxSpawnCount={config.MaxSpawnCount}.";
        }

        internal static string ResolveServiceUrlForRuntime(
            string configuredUrl,
            int expectedLocalPort)
        {
            var normalized = SceneTalkRuntimeConfig.NormalizeUrl(configuredUrl);
            if (!Application.isEditor
                || string.IsNullOrWhiteSpace(normalized)
                || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || uri.IsLoopback
                || uri.Port != expectedLocalPort
                || !IsPrivateLanHost(uri.Host))
            {
                return normalized;
            }

            var builder = new UriBuilder(uri)
            {
                Host = "127.0.0.1"
            };
            return builder.Uri.AbsoluteUri.TrimEnd('/');
        }

        private static bool IsPrivateLanHost(string host)
        {
            if (!IPAddress.TryParse(host, out var address)
                || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        private T Resolve<T>(T current) where T : Component
        {
            if (current != null)
            {
                return current;
            }

            var component = GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }
    }
}
