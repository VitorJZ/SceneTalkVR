using System;
using System.Collections;
using SceneTalkVR.Core;
using SceneTalkVR.Voice;
using UnityEngine;

namespace SceneTalkVR.AvatarSystem
{
    internal sealed class AvatarSpeechPlaybackContext
    {
        public MonoBehaviour logContext;
        public VoiceGatewayClient gatewayClient;
        public AudioSource defaultAudioSource;
        public AudioClip demoReplyClip;
        public bool useVoiceGatewayTts;
        public bool fallbackToDemoVoiceOnGatewayError;
        public string sessionId;
        public string language;
        public string defaultVoiceId;
        public string currentAvatarGenderPresentation;
        public int ttsSampleRate;
        public float fallbackSpeakingSeconds;
    }

    internal sealed class AvatarSpeechPlaybackRequest
    {
        public string text;
        public string logLabel;
        public string voiceIdOverride;
        public string speakingSpeedOverride;
        public string attitudeOverride;
        public AudioSource audioSourceOverride;
        public Action playbackStarted;
        public Action playbackEnded;
    }

    internal sealed class AvatarSpeechPlaybackResult
    {
        public bool playbackCompleted;
        public string fallbackLevel = "none";
        public string error;
        public string ttsProvider;
        public int ttsLatencyMs;
        public int audioDurationMs;
    }

    internal sealed class AvatarSpeechPlayer
    {
        public IEnumerator Play(
            AvatarSpeechPlaybackContext context,
            SpringScenePayload payload,
            AvatarSpeechPlaybackRequest playbackRequest,
            Action<AvatarSpeechPlaybackResult> onComplete)
        {
            var result = new AvatarSpeechPlaybackResult();
            if (context == null || playbackRequest == null)
            {
                result.error = "Speech playback context or request is missing.";
                onComplete?.Invoke(result);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(playbackRequest.text))
            {
                result.fallbackLevel = "empty_text";
                onComplete?.Invoke(result);
                yield break;
            }

            var playedAudio = false;
            if (context.useVoiceGatewayTts)
            {
                string gatewayError = null;
                var gatewayPlaybackCompleted = false;
                TtsResponse gatewayResponse = null;
                var gatewayAudioDurationMs = 0;
                yield return PlayGatewayTts(
                    context,
                    payload,
                    playbackRequest,
                    (completed, response, audioDurationMs) =>
                    {
                        gatewayPlaybackCompleted = completed;
                        gatewayResponse = response;
                        gatewayAudioDurationMs = audioDurationMs;
                    },
                    message => gatewayError = message);

                playedAudio = string.IsNullOrWhiteSpace(gatewayError) && gatewayPlaybackCompleted;
                result.ttsProvider = gatewayResponse != null ? gatewayResponse.provider : string.Empty;
                result.ttsLatencyMs = gatewayResponse != null ? gatewayResponse.latencyMs : 0;
                result.audioDurationMs = gatewayAudioDurationMs;
                if (gatewayResponse != null
                    && !string.IsNullOrWhiteSpace(gatewayResponse.fallbackLevel)
                    && !string.Equals(gatewayResponse.fallbackLevel, "none", StringComparison.OrdinalIgnoreCase))
                {
                    result.fallbackLevel = AppendFallback(
                        result.fallbackLevel,
                        gatewayResponse.fallbackLevel);
                }

                if (!playedAudio)
                {
                    if (!context.fallbackToDemoVoiceOnGatewayError)
                    {
                        result.fallbackLevel = "gateway_error";
                        result.error = string.IsNullOrWhiteSpace(gatewayError)
                            ? "Voice gateway playback did not complete."
                            : gatewayError;
                        onComplete?.Invoke(result);
                        yield break;
                    }

                    result.fallbackLevel = "gateway_error";
                    Debug.LogWarning(
                        $"[SceneTalkVR] {playbackRequest.logLabel} voice gateway TTS fallback: {gatewayError}",
                        context.logContext);
                }
            }

            var targetAudioSource = playbackRequest.audioSourceOverride != null
                ? playbackRequest.audioSourceOverride
                : context.defaultAudioSource;
            if (!playedAudio && targetAudioSource != null && context.demoReplyClip != null)
            {
                targetAudioSource.clip = context.demoReplyClip;
                targetAudioSource.Play();
                playbackRequest.playbackStarted?.Invoke();
                yield return new WaitWhile(() => targetAudioSource != null && targetAudioSource.isPlaying);
                playbackRequest.playbackEnded?.Invoke();

                playedAudio = targetAudioSource != null && !targetAudioSource.isPlaying;
                result.fallbackLevel = AppendFallback(result.fallbackLevel, "demo_clip");
                result.ttsProvider = "demo";
                result.audioDurationMs = Mathf.RoundToInt(context.demoReplyClip.length * 1000f);
            }

            if (!playedAudio)
            {
                playbackRequest.playbackStarted?.Invoke();
                yield return new WaitForSeconds(Mathf.Max(0.1f, context.fallbackSpeakingSeconds));
                playbackRequest.playbackEnded?.Invoke();
                result.fallbackLevel = AppendFallback(result.fallbackLevel, "silent_wait");
            }

            result.playbackCompleted = playedAudio;
            onComplete?.Invoke(result);
        }

        private static IEnumerator PlayGatewayTts(
            AvatarSpeechPlaybackContext context,
            SpringScenePayload payload,
            AvatarSpeechPlaybackRequest playbackRequest,
            Action<bool, TtsResponse, int> onComplete,
            Action<string> onError)
        {
            var targetAudioSource = playbackRequest.audioSourceOverride != null
                ? playbackRequest.audioSourceOverride
                : context.defaultAudioSource;
            if (targetAudioSource == null)
            {
                onError?.Invoke("AudioSource is not assigned.");
                yield break;
            }

            if (context.gatewayClient == null)
            {
                onError?.Invoke("Voice gateway client is not assigned.");
                yield break;
            }

            var role = payload != null ? payload.avatarRole : null;
            var voiceId = string.IsNullOrWhiteSpace(playbackRequest.voiceIdOverride)
                ? ResolveVoiceId(context, payload)
                : playbackRequest.voiceIdOverride;
            var speakingSpeed = string.IsNullOrWhiteSpace(playbackRequest.speakingSpeedOverride)
                ? role != null ? role.speakingSpeed : string.Empty
                : playbackRequest.speakingSpeedOverride;
            var attitude = string.IsNullOrWhiteSpace(playbackRequest.attitudeOverride)
                ? role != null ? role.attitude : string.Empty
                : playbackRequest.attitudeOverride;
            var request = new TtsRequest
            {
                sessionId = context.sessionId,
                turnId = $"turn-{Time.frameCount}",
                text = playbackRequest.text,
                language = string.IsNullOrWhiteSpace(context.language) ? "en-US" : context.language,
                voiceProfile = new VoiceProfile
                {
                    provider = "tencent",
                    voiceId = voiceId,
                    speakingSpeed = speakingSpeed,
                    accent = role != null ? role.accent : string.Empty,
                    attitude = attitude,
                    role = role != null ? role.role : string.Empty
                },
                output = new TtsOutput
                {
                    format = "wav",
                    sampleRate = Mathf.Max(8000, context.ttsSampleRate)
                }
            };

            AudioClip clip = null;
            TtsResponse response = null;
            string requestError = null;
            yield return context.gatewayClient.RequestTtsAudioClip(
                request,
                (value, audioClip) =>
                {
                    response = value;
                    clip = audioClip;
                },
                message => requestError = message);

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                onError?.Invoke(requestError);
                yield break;
            }

            if (clip == null)
            {
                onError?.Invoke("Voice gateway returned no playable TTS clip.");
                yield break;
            }

            targetAudioSource.clip = clip;
            targetAudioSource.Play();
            playbackRequest.playbackStarted?.Invoke();
            Debug.Log(
                $"[SceneTalkVR] Voice gateway TTS audio ({response?.provider}, {response?.latencyMs} ms, cache={response?.cacheHit})",
                context.logContext);
            yield return new WaitWhile(() => targetAudioSource != null && targetAudioSource.isPlaying);
            playbackRequest.playbackEnded?.Invoke();

            if (targetAudioSource == null)
            {
                onError?.Invoke("AudioSource was destroyed before TTS playback completed.");
                yield break;
            }

            onComplete?.Invoke(true, response, Mathf.RoundToInt(clip.length * 1000f));
        }

        private static string ResolveVoiceId(
            AvatarSpeechPlaybackContext context,
            SpringScenePayload payload)
        {
            var role = payload != null ? payload.avatarRole : null;
            var appearance = role != null ? role.appearance : null;
            var requestedGender = appearance != null ? appearance.genderPresentation : string.Empty;

            if (IsGender(context.currentAvatarGenderPresentation, "male") || IsGender(requestedGender, "male"))
            {
                return "default_male_en";
            }

            if (IsGender(context.currentAvatarGenderPresentation, "female") || IsGender(requestedGender, "female"))
            {
                return "default_female_en";
            }

            return string.IsNullOrWhiteSpace(context.defaultVoiceId)
                ? "default_female_en"
                : context.defaultVoiceId;
        }

        private static string AppendFallback(string current, string next)
        {
            return string.IsNullOrWhiteSpace(current) || string.Equals(current, "none", StringComparison.OrdinalIgnoreCase)
                ? next
                : $"{current}+{next}";
        }

        private static bool IsGender(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
