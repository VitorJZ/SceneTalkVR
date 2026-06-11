using UnityEngine;

namespace SceneTalkVR.Voice
{
    [CreateAssetMenu(fileName = "VoiceGatewaySettings", menuName = "SceneTalkVR/Voice Gateway Settings")]
    public sealed class VoiceGatewaySettings : ScriptableObject
    {
        [SerializeField] private string gatewayBaseUrl = "http://127.0.0.1:8787";
        [SerializeField] private int requestTimeoutSeconds = 10;

        public string GatewayBaseUrl => string.IsNullOrWhiteSpace(gatewayBaseUrl)
            ? "http://127.0.0.1:8787"
            : gatewayBaseUrl.TrimEnd('/');

        public int RequestTimeoutSeconds => Mathf.Max(1, requestTimeoutSeconds);
    }
}
