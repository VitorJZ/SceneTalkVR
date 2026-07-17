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

        public SceneTalkRuntimeConfig Config => config;

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
        }

        private void ConfigureVoiceGateway()
        {
            if (voiceGatewayClient == null || !config.HasVoiceGatewayBaseUrl)
            {
                return;
            }

            voiceGatewayClient.ConfigureGatewayBaseUrl(config.VoiceGatewayBaseUrl);
        }

        private void ConfigureBrain()
        {
            if (realLlmService != null)
            {
                realLlmService.ConfigureApi(config.DirectLlmApiUrl, config.DirectLlmModelName);
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
            return "[SceneTalkVR] Runtime config applied. "
                + $"brain={config.BrainMode}, "
                + $"voiceGateway={(config.HasVoiceGatewayBaseUrl ? config.VoiceGatewayBaseUrl : "<scene default>")}, "
                + $"holodeck={(config.UseHolodeckBackend ? config.HolodeckBackendUrl : "mock layout")}, "
                + $"onlyUsePanorama={config.OnlyUsePanorama}, "
                + $"forceFallbackPanorama={config.ForceFallbackPanorama}, "
                + $"maxSpawnCount={config.MaxSpawnCount}.";
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
