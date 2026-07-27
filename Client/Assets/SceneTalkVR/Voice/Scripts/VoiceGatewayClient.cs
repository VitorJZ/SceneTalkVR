using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SceneTalkVR.Voice
{
    public sealed class VoiceGatewayRuntimeOptions
    {
        public VoiceGatewayRuntimeOptions(
            string baseUrl,
            int requestTimeoutSeconds,
            string expectedTtsProvider,
            bool allowMockProvider)
        {
            BaseUrl = baseUrl;
            RequestTimeoutSeconds = Mathf.Max(1, requestTimeoutSeconds);
            ExpectedTtsProvider = string.IsNullOrWhiteSpace(expectedTtsProvider)
                ? "tencent"
                : expectedTtsProvider.Trim().ToLowerInvariant();
            AllowMockProvider = allowMockProvider;
        }

        public string BaseUrl { get; }
        public int RequestTimeoutSeconds { get; }
        public string ExpectedTtsProvider { get; }
        public bool AllowMockProvider { get; }
    }

    public sealed class VoiceGatewayClient : MonoBehaviour
    {
        [SerializeField] private VoiceGatewaySettings settings;
        [SerializeField] private string gatewayBaseUrl = "http://127.0.0.1:8787";
        [SerializeField] private int requestTimeoutSeconds = 30;
        [SerializeField] private string expectedTtsProvider = "tencent";
        [SerializeField] private bool allowMockTtsProvider;
        [SerializeField, Min(0)] private int transientRetryCount = 1;
        private string runtimeGatewayBaseUrl;
        private int runtimeRequestTimeoutSeconds;
        private string runtimeExpectedTtsProvider;
        private bool? runtimeAllowMockTtsProvider;

        public string GatewayBaseUrl => !string.IsNullOrWhiteSpace(runtimeGatewayBaseUrl)
            ? NormalizeBaseUrl(runtimeGatewayBaseUrl)
            : settings != null
            ? settings.GatewayBaseUrl
            : NormalizeBaseUrl(gatewayBaseUrl);

        public int EffectiveRequestTimeoutSeconds => runtimeRequestTimeoutSeconds > 0
            ? runtimeRequestTimeoutSeconds
            : settings != null
                ? settings.RequestTimeoutSeconds
                : Mathf.Max(1, requestTimeoutSeconds);
        public string EffectiveExpectedTtsProvider => !string.IsNullOrWhiteSpace(runtimeExpectedTtsProvider)
            ? runtimeExpectedTtsProvider
            : settings != null
                ? settings.ExpectedTtsProvider
                : string.IsNullOrWhiteSpace(expectedTtsProvider)
                    ? "tencent"
                    : expectedTtsProvider.Trim().ToLowerInvariant();
        public bool EffectiveAllowMockProvider => runtimeAllowMockTtsProvider
            ?? (settings != null ? settings.AllowMockProvider : allowMockTtsProvider);

        public void ConfigureGatewayBaseUrl(string baseUrl)
        {
            runtimeGatewayBaseUrl = NormalizeBaseUrl(baseUrl);
        }

        public void ConfigureRuntime(VoiceGatewayRuntimeOptions options)
        {
            if (options == null)
            {
                return;
            }

            runtimeGatewayBaseUrl = NormalizeBaseUrl(options.BaseUrl);
            runtimeRequestTimeoutSeconds = Mathf.Max(1, options.RequestTimeoutSeconds);
            runtimeExpectedTtsProvider = options.ExpectedTtsProvider;
            runtimeAllowMockTtsProvider = options.AllowMockProvider;
        }

        public IEnumerator RequestStt(
            SttRequest request,
            Action<SttResponse> onComplete,
            Action<string> onError)
        {
            if (request == null)
            {
                onError?.Invoke("STT request is null.");
                yield break;
            }

            yield return PostJson(
                "/api/voice/stt",
                JsonUtility.ToJson(request),
                body =>
                {
                    var response = JsonUtility.FromJson<SttResponse>(body);
                    if (response == null || string.IsNullOrWhiteSpace(response.transcript))
                    {
                        onError?.Invoke("Voice gateway STT response did not include a transcript.");
                        return;
                    }

                    if (!EffectiveAllowMockProvider
                        && IsMockProvider(response.provider, response.fallbackLevel))
                    {
                        onError?.Invoke("Voice gateway returned mock STT data in a live voice profile.");
                        return;
                    }

                    onComplete?.Invoke(response);
                },
                onError);
        }

        public IEnumerator RequestTtsAudioClip(
            TtsRequest request,
            Action<TtsResponse, AudioClip> onComplete,
            Action<string> onError)
        {
            if (request == null)
            {
                onError?.Invoke("TTS request is null.");
                yield break;
            }

            TtsResponse response = null;
            string error = null;
            yield return PostJson(
                "/api/voice/tts",
                JsonUtility.ToJson(request),
                body =>
                {
                    response = JsonUtility.FromJson<TtsResponse>(body);
                    if (response == null || string.IsNullOrWhiteSpace(response.audioUrl))
                    {
                        error = "Voice gateway TTS response did not include an audioUrl.";
                    }
                    else if (!ValidateTtsProvider(response, out var providerError))
                    {
                        error = providerError;
                    }
                    else if (!ValidateTtsTextAcknowledgement(
                        request.text,
                        response.textCharacters,
                        out var textError))
                    {
                        error = textError;
                    }
                },
                message => error = message);

            if (!string.IsNullOrWhiteSpace(error))
            {
                onError?.Invoke(error);
                yield break;
            }

            AudioClip clip = null;
            yield return DownloadAudioClip(
                ToAbsoluteUrl(response.audioUrl),
                value => clip = value,
                message => error = message);

            if (!string.IsNullOrWhiteSpace(error))
            {
                onError?.Invoke(error);
                yield break;
            }

            if (clip == null || clip.samples <= 0 || clip.length <= 0.05f)
            {
                if (clip != null)
                {
                    Destroy(clip);
                }
                onError?.Invoke("Voice gateway TTS audio download did not contain a playable AudioClip.");
                yield break;
            }

            onComplete?.Invoke(response, clip);
        }

        private IEnumerator PostJson(
            string route,
            string json,
            Action<string> onComplete,
            Action<string> onError)
        {
            var url = $"{GatewayBaseUrl}{route}";
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            var maxAttempts = Mathf.Max(1, transientRetryCount + 1);
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = EffectiveRequestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "application/json");

                yield return request.SendWebRequest();

                var responseBody = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (attempt < maxAttempts && IsTransientFailure(request))
                    {
                        yield return new WaitForSecondsRealtime(0.25f * attempt);
                        continue;
                    }

                    onError?.Invoke(BuildGatewayRequestError(request, responseBody, attempt));
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    onError?.Invoke("Voice gateway returned an empty response.");
                    yield break;
                }

                onComplete?.Invoke(responseBody);
                yield break;
            }
        }

        private static bool IsTransientFailure(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return true;
            }

            var statusCode = request.responseCode;
            return statusCode == 408 || statusCode == 429 || statusCode >= 500;
        }

        private static string BuildGatewayRequestError(
            UnityWebRequest request,
            string responseBody,
            int attempts)
        {
            var requestError = request != null && !string.IsNullOrWhiteSpace(request.error)
                ? request.error
                : "unknown network error";
            var attemptSuffix = attempts > 1 ? $" after {attempts} attempts" : string.Empty;
            var gatewayDetail = ExtractGatewayErrorDetail(responseBody);
            return string.IsNullOrWhiteSpace(gatewayDetail)
                ? $"Voice gateway request failed{attemptSuffix}: {requestError}"
                : $"Voice gateway request failed{attemptSuffix}: {requestError}; {gatewayDetail}";
        }

        private static string ExtractGatewayErrorDetail(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            try
            {
                var error = JsonUtility.FromJson<VoiceGatewayErrorResponse>(responseBody);
                if (error != null && (!string.IsNullOrWhiteSpace(error.errorCode)
                    || !string.IsNullOrWhiteSpace(error.message)))
                {
                    var code = string.IsNullOrWhiteSpace(error.errorCode)
                        ? "gateway_error"
                        : error.errorCode.Trim();
                    var message = string.IsNullOrWhiteSpace(error.message)
                        ? "No error message was returned."
                        : error.message.Trim();
                    return $"{code}: {message}";
                }
            }
            catch (ArgumentException)
            {
                // Preserve a bounded raw response below when the body is not JSON.
            }

            var normalized = responseBody.Replace('\r', ' ').Replace('\n', ' ').Trim();
            const int maxDetailLength = 512;
            return normalized.Length <= maxDetailLength
                ? normalized
                : $"{normalized.Substring(0, maxDetailLength)}...";
        }

        private IEnumerator DownloadAudioClip(
            string url,
            Action<AudioClip> onComplete,
            Action<string> onError)
        {
            using var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
            request.timeout = EffectiveRequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Voice gateway audio download failed: {request.error}");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                onError?.Invoke("Downloaded voice gateway audio could not be decoded as WAV.");
                yield break;
            }

            onComplete?.Invoke(clip);
        }

        private string ToAbsoluteUrl(string routeOrUrl)
        {
            if (string.IsNullOrWhiteSpace(routeOrUrl))
            {
                return GatewayBaseUrl;
            }

            if (routeOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || routeOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return routeOrUrl;
            }

            return routeOrUrl.StartsWith("/", StringComparison.Ordinal)
                ? $"{GatewayBaseUrl}{routeOrUrl}"
                : $"{GatewayBaseUrl}/{routeOrUrl}";
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "http://127.0.0.1:8787";
            }

            return value.TrimEnd('/');
        }

        internal bool ValidateTtsProvider(TtsResponse response, out string error)
        {
            error = string.Empty;
            if (response == null)
            {
                error = "Voice gateway TTS response is null.";
                return false;
            }

            var provider = (response.provider ?? string.Empty).Trim().ToLowerInvariant();
            var isMock = IsMockProvider(provider, response.fallbackLevel);
            if (isMock && !EffectiveAllowMockProvider)
            {
                error = "Voice gateway returned mock TTS audio in a live voice profile.";
                return false;
            }

            var expectedProvider = EffectiveExpectedTtsProvider;
            if (!string.IsNullOrWhiteSpace(expectedProvider)
                && !string.Equals(provider, expectedProvider, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Voice gateway returned TTS provider '{provider}' while '{expectedProvider}' was required.";
                return false;
            }

            return true;
        }

        internal static bool ValidateTtsTextAcknowledgement(
            string requestedText,
            int acknowledgedCharacters,
            out string error)
        {
            error = string.Empty;
            if (CountUnicodeScalars((requestedText ?? string.Empty).Trim()) == acknowledgedCharacters)
            {
                return true;
            }

            error = "Voice gateway TTS response did not acknowledge the complete requested text.";
            return false;
        }

        private static int CountUnicodeScalars(string value)
        {
            var count = 0;
            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index])
                    && index + 1 < value.Length
                    && char.IsLowSurrogate(value[index + 1]))
                {
                    index++;
                }

                count++;
            }

            return count;
        }

        private static bool IsMockProvider(string provider, string fallbackLevel)
        {
            var normalizedProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedFallback = (fallbackLevel ?? string.Empty).Trim().ToLowerInvariant();
            return string.Equals(normalizedProvider, "mock", StringComparison.Ordinal)
                || normalizedFallback.StartsWith("mock_", StringComparison.Ordinal)
                || normalizedFallback.StartsWith("mock_after_", StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class SttRequest
    {
        public string sessionId;
        public int sampleRate = 16000;
        public int channels = 1;
        public string format = "wav";
        public string language = "en-US";
        public string sceneType = "general";
        public string audioBase64 = string.Empty;
    }

    [Serializable]
    public sealed class SttResponse
    {
        public string requestId;
        public string provider;
        public bool isFinal;
        public string transcript;
        public float confidence;
        public bool confidenceAvailable;
        public int durationMs;
        public int latencyMs;
        public string fallbackLevel;
    }

    [Serializable]
    public sealed class TtsRequest
    {
        public string sessionId;
        public string turnId;
        public string text;
        public string language = "en-US";
        public VoiceProfile voiceProfile = new VoiceProfile();
        public TtsOutput output = new TtsOutput();
    }

    [Serializable]
    public sealed class VoiceProfile
    {
        public string provider = "tencent";
        public string voiceId = "default_female_en";
        public string speakingSpeed;
        public string accent;
        public string attitude;
        public string role;
    }

    [Serializable]
    public sealed class TtsOutput
    {
        public string format = "wav";
        public int sampleRate = 24000;
    }

    [Serializable]
    public sealed class TtsResponse
    {
        public string requestId;
        public string provider;
        public string audioUrl;
        public string format;
        public int sampleRate;
        public int textCharacters;
        public int latencyMs;
        public bool cacheHit;
        public string fallbackLevel;
    }

    [Serializable]
    internal sealed class VoiceGatewayErrorResponse
    {
        public string errorCode;
        public string message;
        public bool retryable;
    }
}
