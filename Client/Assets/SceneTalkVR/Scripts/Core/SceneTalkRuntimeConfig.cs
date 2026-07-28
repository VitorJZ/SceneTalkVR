using System;
using UnityEngine;

namespace SceneTalkVR.Core
{
    public enum SceneTalkBrainRuntimeMode
    {
        KeepCurrent = 0,
        DemoBrain = 1,
        DirectRealLlm = 2
    }

    [CreateAssetMenu(fileName = "SceneTalkRuntimeConfig", menuName = "SceneTalkVR/Runtime Config")]
    public sealed class SceneTalkRuntimeConfig : ScriptableObject
    {
        [Header("Module Selection")]
        [SerializeField] private SceneTalkBrainRuntimeMode brainMode = SceneTalkBrainRuntimeMode.KeepCurrent;
        [SerializeField] private bool useVoiceGatewaySpeech = true;
        [SerializeField] private bool useVoiceGatewayTts = true;
        [SerializeField] private bool useFixedExperimentMode = true;
        [SerializeField] private bool useDeveloperTextConsole = false;

        [Header("Gateway Transport")]
        [SerializeField] private int gatewayTransportConfigurationVersion;
        [SerializeField] private GatewayTransportPreference gatewayTransportPreference = GatewayTransportPreference.UsbPreferred;
        [SerializeField] private string usbVoiceGatewayBaseUrl = "http://127.0.0.1:8787";
        [SerializeField] private string usbLlmApiUrl = "http://127.0.0.1:8788/api/llm/chat/completions";
        [SerializeField, Range(1, 10)] private int gatewayProbeTimeoutSeconds = 3;

        [Header("LAN Services")]
        [SerializeField] private string voiceGatewayBaseUrl = string.Empty;
        [SerializeField, Min(1)] private int voiceGatewayRequestTimeoutSeconds = 30;
        [SerializeField] private string expectedTtsProvider = "tencent";
        [SerializeField] private bool allowMockTtsProvider;
        [SerializeField] private bool useHolodeckBackend;
        [SerializeField] private string holodeckBackendUrl = string.Empty;
        [SerializeField] private int holodeckTimeoutSeconds = 300;

        [Header("Mobile Safety")]
        [SerializeField] private bool onlyUsePanorama = true;
        [SerializeField] private bool forceFallbackPanorama = true;
        [SerializeField] private bool enableSpatialClipping = true;
        [SerializeField] private int maxSpawnCount = 2;

        [Header("Direct LLM Defaults")]
        [SerializeField] private string directLlmApiUrl = "https://models.sjtu.edu.cn/api/v1/chat/completions";
        [SerializeField] private string directLlmModelName = "deepseek-chat";

        [Header("Correction Policy")]
        [SerializeField] private CorrectionPolicySettings correctionPolicy = new CorrectionPolicySettings();

        [Header("Avatar Dialogue Pacing")]
        [Range(0f, 1f)] [SerializeField] private float temperature = 0.7f;
        [Min(0)] [SerializeField] private int maxNonGoalQuestionsPerTask = 3;

        [Header("Panorama Defaults")]
        [SerializeField] private string panoramaModelName = "Tongyi-MAI/Z-Image";
        [SerializeField] private string panoramaImageSize = "1024x1024";
        [NonSerialized] private bool formalModeRuntimeLock;

        public SceneTalkBrainRuntimeMode BrainMode => brainMode;
        public bool UseVoiceGatewaySpeech => useVoiceGatewaySpeech;
        public bool UseVoiceGatewayTts => useVoiceGatewayTts;
        public bool UseFixedExperimentMode => useFixedExperimentMode;
        public bool UseDeveloperTextConsole => useDeveloperTextConsole;
        public GatewayTransportPreference TransportPreference =>
            gatewayTransportConfigurationVersion > 0
                ? gatewayTransportPreference
                : GatewayTransportPreference.LanOnly;
        public string UsbVoiceGatewayBaseUrl => NormalizeUrl(usbVoiceGatewayBaseUrl);
        public string UsbLlmApiUrl => NormalizeUrl(usbLlmApiUrl);
        public int GatewayProbeTimeoutSeconds => Mathf.Clamp(gatewayProbeTimeoutSeconds, 1, 10);
        public string VoiceGatewayBaseUrl => NormalizeUrl(voiceGatewayBaseUrl);
        public bool HasVoiceGatewayBaseUrl => !string.IsNullOrWhiteSpace(VoiceGatewayBaseUrl);
        public int VoiceGatewayRequestTimeoutSeconds => Mathf.Max(1, voiceGatewayRequestTimeoutSeconds);
        public string ExpectedTtsProvider => string.IsNullOrWhiteSpace(expectedTtsProvider)
            ? "tencent"
            : expectedTtsProvider.Trim().ToLowerInvariant();
        public bool AllowMockTtsProvider => allowMockTtsProvider;
        public bool UseHolodeckBackend => !formalModeRuntimeLock && useHolodeckBackend;
        public string HolodeckBackendUrl => NormalizeUrl(holodeckBackendUrl);
        public bool HasHolodeckBackendUrl => !string.IsNullOrWhiteSpace(HolodeckBackendUrl);
        public int HolodeckTimeoutSeconds => Mathf.Max(1, holodeckTimeoutSeconds);
        public bool OnlyUsePanorama => formalModeRuntimeLock || onlyUsePanorama;
        public bool ForceFallbackPanorama => formalModeRuntimeLock || forceFallbackPanorama;
        public bool IsFormalModeRuntimeLocked => formalModeRuntimeLock;
        public void ConfigureFormalModeRuntimeLock(bool enabled) => formalModeRuntimeLock = enabled;
        public bool EnableSpatialClipping => enableSpatialClipping;
        public int MaxSpawnCount => Mathf.Max(0, maxSpawnCount);
        public string DirectLlmApiUrl => NormalizeUrl(directLlmApiUrl);
        public string DirectLlmModelName => string.IsNullOrWhiteSpace(directLlmModelName)
            ? "deepseek-chat"
            : directLlmModelName.Trim();
        public CorrectionPolicySettings CorrectionPolicy =>
            CorrectionPolicySettings.CloneNormalized(correctionPolicy);
        public float Temperature => Mathf.Clamp01(temperature);
        public int MaxNonGoalQuestionsPerTask => Mathf.Max(0, maxNonGoalQuestionsPerTask);
        public string PanoramaModelName => string.IsNullOrWhiteSpace(panoramaModelName)
            ? "Tongyi-MAI/Z-Image"
            : panoramaModelName.Trim();
        public string PanoramaImageSize => string.IsNullOrWhiteSpace(panoramaImageSize)
            ? "1024x1024"
            : panoramaImageSize.Trim();

        public static bool IsLoopbackUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var host = uri.Host;
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
        }

#if UNITY_EDITOR
        public void ConfigurePicoRealRunDefaults()
        {
            brainMode = SceneTalkBrainRuntimeMode.DirectRealLlm;
            useVoiceGatewaySpeech = true;
            useVoiceGatewayTts = true;
            useFixedExperimentMode = true;
            gatewayTransportConfigurationVersion = 1;
            gatewayTransportPreference = GatewayTransportPreference.UsbPreferred;
            usbVoiceGatewayBaseUrl = "http://127.0.0.1:8787";
            usbLlmApiUrl = "http://127.0.0.1:8788/api/llm/chat/completions";
            gatewayProbeTimeoutSeconds = 3;
            voiceGatewayRequestTimeoutSeconds = 30;
            expectedTtsProvider = "tencent";
            allowMockTtsProvider = false;
            useHolodeckBackend = false;
            onlyUsePanorama = true;
            forceFallbackPanorama = true;
            enableSpatialClipping = true;
            maxSpawnCount = 2;
            holodeckTimeoutSeconds = 300;
            temperature = 0.7f;
            maxNonGoalQuestionsPerTask = 3;
            if (string.IsNullOrWhiteSpace(directLlmApiUrl))
            {
                directLlmApiUrl = "https://models.sjtu.edu.cn/api/v1/chat/completions";
            }

            if (string.IsNullOrWhiteSpace(directLlmModelName))
            {
                directLlmModelName = "deepseek-chat";
            }

            if (string.IsNullOrWhiteSpace(panoramaModelName))
            {
                panoramaModelName = "Tongyi-MAI/Z-Image";
            }

            if (string.IsNullOrWhiteSpace(panoramaImageSize))
            {
                panoramaImageSize = "1024x1024";
            }
        }
#endif
    }
}
