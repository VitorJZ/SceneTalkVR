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

    internal sealed class PreparedAvatarSpeech
    {
        public AudioClip clip;
        public TtsResponse ttsResponse;
        public string fallbackLevel = "none";
        public string error;
        public int audioDurationMs;
        public int preparationDurationMs;
        public bool useSilentWait;
        public bool ownsClip;

        public void Release()
        {
            if (ownsClip && clip != null)
            {
                UnityEngine.Object.Destroy(clip);
            }

            clip = null;
            ownsClip = false;
        }
    }

    internal sealed class AvatarSpeechPlayer
    {
        public IEnumerator Play(
            AvatarSpeechPlaybackContext context,
            SpringScenePayload payload,
            AvatarSpeechPlaybackRequest playbackRequest,
            Action<AvatarSpeechPlaybackResult> onComplete)
        {
            PreparedAvatarSpeech preparedSpeech = null;
            yield return Prepare(
                context,
                payload,
                playbackRequest,
                value => preparedSpeech = value);
            yield return PlayPrepared(
                context,
                playbackRequest,
                preparedSpeech,
                onComplete);
        }

        public IEnumerator Prepare(
            AvatarSpeechPlaybackContext context,
            SpringScenePayload payload,
            AvatarSpeechPlaybackRequest playbackRequest,
            Action<PreparedAvatarSpeech> onComplete)
        {
            var startedAt = Time.realtimeSinceStartup;
            var preparedSpeech = new PreparedAvatarSpeech();
            if (context == null || playbackRequest == null)
            {
                preparedSpeech.error = "Speech playback context or request is missing.";
                CompletePreparation(preparedSpeech, startedAt, onComplete);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(playbackRequest.text))
            {
                preparedSpeech.fallbackLevel = "empty_text";
                CompletePreparation(preparedSpeech, startedAt, onComplete);
                yield break;
            }

            var targetAudioSource = playbackRequest.audioSourceOverride != null
                ? playbackRequest.audioSourceOverride
                : context.defaultAudioSource;

            string gatewayError = null;
            if (context.useVoiceGatewayTts)
            {
                if (targetAudioSource == null)
                {
                    gatewayError = "AudioSource is not assigned.";
                }
                else if (context.gatewayClient == null)
                {
                    gatewayError = "Voice gateway client is not assigned.";
                }
                else
                {
                    var request = BuildTtsRequest(context, payload, playbackRequest);
                    yield return context.gatewayClient.RequestTtsAudioClip(
                        request,
                        (response, audioClip) =>
                        {
                            preparedSpeech.ttsResponse = response;
                            preparedSpeech.clip = audioClip;
                            preparedSpeech.ownsClip = audioClip != null;
                            preparedSpeech.audioDurationMs = audioClip == null
                                ? 0
                                : Mathf.RoundToInt(audioClip.length * 1000f);
                        },
                        message => gatewayError = message);
                }

                if (preparedSpeech.ttsResponse != null
                    && !string.IsNullOrWhiteSpace(preparedSpeech.ttsResponse.fallbackLevel)
                    && !string.Equals(
                        preparedSpeech.ttsResponse.fallbackLevel,
                        "none",
                        StringComparison.OrdinalIgnoreCase))
                {
                    preparedSpeech.fallbackLevel = AppendFallback(
                        preparedSpeech.fallbackLevel,
                        preparedSpeech.ttsResponse.fallbackLevel);
                }
            }

            if (context.useVoiceGatewayTts && preparedSpeech.clip == null)
            {
                gatewayError = string.IsNullOrWhiteSpace(gatewayError)
                    ? "Voice gateway returned no playable TTS clip."
                    : gatewayError;
                preparedSpeech.fallbackLevel = AppendFallback(
                    preparedSpeech.fallbackLevel,
                    "gateway_error");
                if (!context.fallbackToDemoVoiceOnGatewayError)
                {
                    preparedSpeech.error = gatewayError;
                    CompletePreparation(preparedSpeech, startedAt, onComplete);
                    yield break;
                }

                Debug.LogWarning(
                    $"[SceneTalkVR] {playbackRequest.logLabel} voice gateway TTS fallback: {gatewayError}",
                    context.logContext);
            }

            if (preparedSpeech.clip == null && targetAudioSource != null && context.demoReplyClip != null)
            {
                preparedSpeech.clip = context.demoReplyClip;
                preparedSpeech.audioDurationMs = Mathf.RoundToInt(context.demoReplyClip.length * 1000f);
                preparedSpeech.fallbackLevel = AppendFallback(
                    preparedSpeech.fallbackLevel,
                    "demo_clip");
            }

            if (preparedSpeech.clip == null)
            {
                preparedSpeech.useSilentWait = true;
                preparedSpeech.fallbackLevel = AppendFallback(
                    preparedSpeech.fallbackLevel,
                    "silent_wait");
            }

            CompletePreparation(preparedSpeech, startedAt, onComplete);
        }

        public IEnumerator PlayPrepared(
            AvatarSpeechPlaybackContext context,
            AvatarSpeechPlaybackRequest playbackRequest,
            PreparedAvatarSpeech preparedSpeech,
            Action<AvatarSpeechPlaybackResult> onComplete)
        {
            var result = CreatePlaybackResult(preparedSpeech);
            if (preparedSpeech == null)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(preparedSpeech.error)
                || string.Equals(preparedSpeech.fallbackLevel, "empty_text", StringComparison.OrdinalIgnoreCase))
            {
                preparedSpeech.Release();
                onComplete?.Invoke(result);
                yield break;
            }

            var targetAudioSource = playbackRequest != null && playbackRequest.audioSourceOverride != null
                ? playbackRequest.audioSourceOverride
                : context != null ? context.defaultAudioSource : null;
            var playedAudio = false;
            if (preparedSpeech.clip != null && targetAudioSource != null)
            {
                targetAudioSource.clip = preparedSpeech.clip;
                targetAudioSource.Play();
                playbackRequest?.playbackStarted?.Invoke();
                if (preparedSpeech.ttsResponse != null)
                {
                    Debug.Log(
                        $"[SceneTalkVR] Voice gateway TTS audio ({preparedSpeech.ttsResponse.provider}, "
                        + $"{preparedSpeech.ttsResponse.latencyMs} ms, cache={preparedSpeech.ttsResponse.cacheHit}, "
                        + $"prepare={preparedSpeech.preparationDurationMs} ms)",
                        context != null ? context.logContext : null);
                }

                yield return new WaitWhile(() => targetAudioSource != null && targetAudioSource.isPlaying);
                playbackRequest?.playbackEnded?.Invoke();
                playedAudio = targetAudioSource != null && !targetAudioSource.isPlaying;
                if (targetAudioSource != null && targetAudioSource.clip == preparedSpeech.clip)
                {
                    targetAudioSource.clip = null;
                }
            }
            else if (preparedSpeech.useSilentWait)
            {
                playbackRequest?.playbackStarted?.Invoke();
                yield return new WaitForSeconds(Mathf.Max(
                    0.1f,
                    context != null ? context.fallbackSpeakingSeconds : 0.1f));
                playbackRequest?.playbackEnded?.Invoke();
            }
            else if (preparedSpeech.clip != null)
            {
                result.error = "AudioSource was destroyed before prepared speech could be played.";
            }

            result.playbackCompleted = playedAudio;
            preparedSpeech.Release();
            onComplete?.Invoke(result);
        }

        private static TtsRequest BuildTtsRequest(
            AvatarSpeechPlaybackContext context,
            SpringScenePayload payload,
            AvatarSpeechPlaybackRequest playbackRequest)
        {
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

            return new TtsRequest
            {
                sessionId = context.sessionId,
                turnId = CreateTurnId(),
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
        }

        internal static string CreateTurnId()
        {
            return $"turn-{Time.frameCount}-{Guid.NewGuid():N}";
        }

        private static AvatarSpeechPlaybackResult CreatePlaybackResult(
            PreparedAvatarSpeech preparedSpeech)
        {
            if (preparedSpeech == null)
            {
                return new AvatarSpeechPlaybackResult
                {
                    error = "Prepared speech is missing."
                };
            }

            return new AvatarSpeechPlaybackResult
            {
                fallbackLevel = preparedSpeech.fallbackLevel,
                error = preparedSpeech.error,
                ttsProvider = preparedSpeech.ttsResponse != null
                    ? preparedSpeech.ttsResponse.provider
                    : preparedSpeech.clip != null && !preparedSpeech.ownsClip ? "demo" : string.Empty,
                ttsLatencyMs = preparedSpeech.ttsResponse != null
                    ? preparedSpeech.ttsResponse.latencyMs
                    : 0,
                audioDurationMs = preparedSpeech.audioDurationMs
            };
        }

        private static void CompletePreparation(
            PreparedAvatarSpeech preparedSpeech,
            float startedAt,
            Action<PreparedAvatarSpeech> onComplete)
        {
            preparedSpeech.preparationDurationMs = Mathf.RoundToInt(
                (Time.realtimeSinceStartup - startedAt) * 1000f);
            onComplete?.Invoke(preparedSpeech);
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
