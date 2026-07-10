using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Voice
{
    public sealed class GatewaySpeechInputModule : MonoBehaviour, ISceneTalkSpeechInput, ISceneTalkManualSpeechInput
    {
        [SerializeField] private VoiceGatewayClient gatewayClient;
        [SerializeField] private MicrophoneRecorder microphoneRecorder;
        [SerializeField] private string sessionId = "scenetalk-demo-session";
        [SerializeField] private string language = "en-US";
        [SerializeField] private string sceneType = "general";
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private int channels = 1;
        [SerializeField] private string format = "wav";
        [SerializeField] private bool useMockEmptyAudio;
        [SerializeField] private string fallbackTranscript = "I want to practice ordering coffee with a fast-speaking foreign barista.";
        [SerializeField] private bool useFallbackTranscriptOnError = true;

        private bool stopCaptureRequested;
        private bool cancelCaptureRequested;

        public SttResponse LastSttResponse { get; private set; }
        public float LastRecordingDurationMs { get; private set; }
        public string LastRecordingStopReason { get; private set; }

        public IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError)
        {
            stopCaptureRequested = false;
            cancelCaptureRequested = false;
            LastSttResponse = null;
            LastRecordingDurationMs = 0f;
            LastRecordingStopReason = "unknown";

            var client = ResolveGatewayClient();
            if (client == null)
            {
                onError?.Invoke("Voice gateway client is missing.");
                yield break;
            }

            var audioBase64 = string.Empty;
            var requestSampleRate = Mathf.Max(1, sampleRate);
            var requestChannels = Mathf.Max(1, channels);
            if (!useMockEmptyAudio)
            {
                var recorder = ResolveMicrophoneRecorder();
                if (recorder == null)
                {
                    onError?.Invoke("Microphone recorder is missing.");
                    yield break;
                }

                string recordingError = null;
                yield return recorder.RecordWavBase64UntilStopped(
                    () => stopCaptureRequested,
                    value => audioBase64 = value,
                    message => recordingError = message);

                LastRecordingDurationMs = recorder.LastDurationMs;
                if (cancelCaptureRequested)
                {
                    LastRecordingStopReason = "cancel";
                    yield break;
                }
                else if (stopCaptureRequested)
                {
                    LastRecordingStopReason = "button_end";
                }
                else
                {
                    LastRecordingStopReason = "timeout";
                }

                if (!string.IsNullOrWhiteSpace(recordingError))
                {
                    if (useFallbackTranscriptOnError && !string.IsNullOrWhiteSpace(fallbackTranscript))
                    {
                        Debug.LogWarning($"[SceneTalkVR] Microphone STT fallback: {recordingError}", this);
                        onComplete?.Invoke(fallbackTranscript);
                        yield break;
                    }

                    onError?.Invoke(recordingError);
                    yield break;
                }

                requestSampleRate = recorder.LastSampleRate;
                requestChannels = recorder.LastChannels;
            }

            var request = new SttRequest
            {
                sessionId = sessionId,
                sampleRate = requestSampleRate,
                channels = requestChannels,
                format = string.IsNullOrWhiteSpace(format) ? "wav" : format,
                language = string.IsNullOrWhiteSpace(language) ? "en-US" : language,
                sceneType = string.IsNullOrWhiteSpace(sceneType) ? "general" : sceneType,
                audioBase64 = audioBase64
            };

            SttResponse response = null;
            string error = null;
            yield return client.RequestStt(
                request,
                value => response = value,
                message => error = message);

            if (!string.IsNullOrWhiteSpace(error))
            {
                if (useFallbackTranscriptOnError && !string.IsNullOrWhiteSpace(fallbackTranscript))
                {
                    Debug.LogWarning($"[SceneTalkVR] Gateway STT fallback: {error}", this);
                    onComplete?.Invoke(fallbackTranscript);
                    yield break;
                }

                onError?.Invoke(error);
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.transcript))
            {
                onError?.Invoke("Voice gateway STT completed without a transcript.");
                yield break;
            }

            LastSttResponse = response;
            Debug.Log(
                $"[SceneTalkVR] Gateway STT transcript ({response.provider}, {response.latencyMs} ms): {response.transcript}",
                this);
            onComplete?.Invoke(response.transcript);
        }

        public void RequestStopCapture()
        {
            stopCaptureRequested = true;
            ResolveMicrophoneRecorder()?.RequestStopRecording();
        }

        public void CancelCapture()
        {
            cancelCaptureRequested = true;
            stopCaptureRequested = true;
            ResolveMicrophoneRecorder()?.CancelRecording();
        }

        private VoiceGatewayClient ResolveGatewayClient()
        {
            if (gatewayClient != null)
            {
                return gatewayClient;
            }

            gatewayClient = GetComponent<VoiceGatewayClient>();
            if (gatewayClient != null)
            {
                return gatewayClient;
            }

            gatewayClient = gameObject.AddComponent<VoiceGatewayClient>();
            return gatewayClient;
        }

        private MicrophoneRecorder ResolveMicrophoneRecorder()
        {
            if (microphoneRecorder != null)
            {
                return microphoneRecorder;
            }

            microphoneRecorder = GetComponent<MicrophoneRecorder>();
            if (microphoneRecorder != null)
            {
                return microphoneRecorder;
            }

            microphoneRecorder = gameObject.AddComponent<MicrophoneRecorder>();
            return microphoneRecorder;
        }
    }
}
