using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SceneTalkVR.Voice
{
    public sealed class MicrophoneRecorder : MonoBehaviour
    {
        private const int MinimumRecordingBufferSeconds = 60;

        [SerializeField] private string preferredDeviceName = string.Empty;
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private float recordingSeconds = 3.5f;
        [SerializeField] private int maxRecordingSeconds = 100;
        [SerializeField] private float minRecordingSeconds = 0.25f;

        public int LastSampleRate { get; private set; } = 16000;
        public int LastChannels { get; private set; } = 1;
        public int LastDurationMs { get; private set; }
        public bool IsRecording => !string.IsNullOrEmpty(activeDeviceName) && Microphone.IsRecording(activeDeviceName);

        private string activeDeviceName;
        private bool stopRequested;
        private bool cancelRequested;

        public IEnumerator RecordWavBase64(Action<string> onComplete, Action<string> onError)
        {
            var fixedStopAt = Time.realtimeSinceStartup + Mathf.Max(0.25f, recordingSeconds);
            yield return RecordWavBase64UntilStopped(
                () => Time.realtimeSinceStartup >= fixedStopAt,
                onComplete,
                onError);
        }

        public IEnumerator RecordWavBase64UntilStopped(
            Func<bool> shouldStop,
            Action<string> onComplete,
            Action<string> onError)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            yield return EnsureMicrophonePermission(onError);
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                yield break;
            }
#endif

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                onError?.Invoke("No microphone device is available.");
                yield break;
            }

            var deviceName = ResolveDeviceName();
            var requestedSampleRate = Mathf.Max(8000, sampleRate);
            var requestedMaxSeconds = Mathf.Max(0.25f, maxRecordingSeconds);
            var requestedBufferSeconds = Mathf.Max(
                MinimumRecordingBufferSeconds,
                Mathf.CeilToInt(requestedMaxSeconds));
            var requestedMinSeconds = Mathf.Clamp(minRecordingSeconds, 0f, requestedMaxSeconds);

            AudioClip clip = null;
            try
            {
                stopRequested = false;
                cancelRequested = false;
                clip = Microphone.Start(deviceName, true, requestedBufferSeconds, requestedSampleRate);
                activeDeviceName = deviceName;
            }
            catch (Exception exception)
            {
                ClearActiveRecording();
                onError?.Invoke($"Failed to start microphone recording: {exception.Message}");
                yield break;
            }

            if (clip == null)
            {
                ClearActiveRecording();
                onError?.Invoke("Microphone.Start returned no audio clip.");
                yield break;
            }

            var startTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (Microphone.GetPosition(deviceName) <= 0)
            {
                if (Time.realtimeSinceStartup >= startTimeoutAt)
                {
                    EndActiveRecording();
                    onError?.Invoke("Microphone did not start recording within 2 seconds.");
                    yield break;
                }

                yield return null;
            }

            var startedAt = Time.realtimeSinceStartup;
            while (!cancelRequested
                   && Time.realtimeSinceStartup - startedAt < requestedMinSeconds)
            {
                yield return null;
            }

            while (!cancelRequested
                   && !stopRequested
                   && Time.realtimeSinceStartup - startedAt < requestedMaxSeconds
                   && (shouldStop == null || !shouldStop()))
            {
                yield return null;
            }

            if (cancelRequested)
            {
                EndActiveRecording();
                yield break;
            }

            var elapsedSeconds = Time.realtimeSinceStartup - startedAt;
            var recordedSamples = Microphone.GetPosition(deviceName);
            var hasLooped = elapsedSeconds >= requestedBufferSeconds;
            EndActiveRecording();

            if (!hasLooped && recordedSamples <= 0)
            {
                onError?.Invoke("Microphone recording produced no samples.");
                yield break;
            }

            LastSampleRate = clip.frequency;
            LastChannels = clip.channels;
            LastDurationMs = hasLooped
                ? Mathf.RoundToInt(clip.samples / (float)clip.frequency * 1000f)
                : Mathf.RoundToInt(recordedSamples / (float)clip.frequency * 1000f);

            var wavBytes = hasLooped
                ? EncodeLoopedClipToWav(clip, recordedSamples)
                : EncodeRecordedClipToWav(clip, recordedSamples);
            onComplete?.Invoke(Convert.ToBase64String(wavBytes));
        }

        public void RequestStopRecording()
        {
            stopRequested = true;
        }

        public void CancelRecording()
        {
            cancelRequested = true;
            if (!string.IsNullOrEmpty(activeDeviceName) && Microphone.IsRecording(activeDeviceName))
            {
                Microphone.End(activeDeviceName);
            }
        }

        private string ResolveDeviceName()
        {
            if (!string.IsNullOrWhiteSpace(preferredDeviceName))
            {
                foreach (var device in Microphone.devices)
                {
                    if (string.Equals(device, preferredDeviceName, StringComparison.Ordinal))
                    {
                        return preferredDeviceName;
                    }
                }

                Debug.LogWarning(
                    $"[SceneTalkVR] Preferred microphone '{preferredDeviceName}' was not found. Falling back to default device.",
                    this);
            }

            return Microphone.devices[0];
        }

        private void OnDisable()
        {
            CancelRecording();
        }

        private void EndActiveRecording()
        {
            if (!string.IsNullOrEmpty(activeDeviceName) && Microphone.IsRecording(activeDeviceName))
            {
                Microphone.End(activeDeviceName);
            }

            ClearActiveRecording();
        }

        private void ClearActiveRecording()
        {
            activeDeviceName = string.Empty;
            stopRequested = false;
            cancelRequested = false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static IEnumerator EnsureMicrophonePermission(Action<string> onError)
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                yield break;
            }

            Permission.RequestUserPermission(Permission.Microphone);

            var timeoutAt = Time.realtimeSinceStartup + 30f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                onError?.Invoke("Microphone permission was not granted on this Android device.");
            }
        }
#endif

        private static byte[] EncodeRecordedClipToWav(AudioClip clip, int recordedSamples)
        {
            var channels = Mathf.Max(1, clip.channels);
            var sampleCount = Mathf.Clamp(recordedSamples, 0, clip.samples);
            var samples = new float[sampleCount * channels];
            clip.GetData(samples, 0);

            return EncodeSamplesToWav(samples, clip.frequency, channels);
        }

        private static byte[] EncodeLoopedClipToWav(AudioClip clip, int writePosition)
        {
            var channels = Mathf.Max(1, clip.channels);
            var frameCount = Mathf.Max(0, clip.samples);
            if (frameCount == 0)
            {
                return EncodeSamplesToWav(Array.Empty<float>(), clip.frequency, channels);
            }

            var samples = new float[frameCount * channels];
            clip.GetData(samples, 0);

            var orderedSamples = new float[samples.Length];
            var startFrame = Mathf.Clamp(writePosition, 0, frameCount - 1);
            for (var frame = 0; frame < frameCount; frame++)
            {
                var sourceFrame = (startFrame + frame) % frameCount;
                for (var channel = 0; channel < channels; channel++)
                {
                    orderedSamples[frame * channels + channel] = samples[sourceFrame * channels + channel];
                }
            }

            return EncodeSamplesToWav(orderedSamples, clip.frequency, channels);
        }

        private static byte[] EncodeSamplesToWav(float[] samples, int sampleRate, int channels)
        {
            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream);
            WriteWavHeader(writer, sampleRate, channels, samples.Length);

            foreach (var sample in samples)
            {
                var clamped = Mathf.Clamp(sample, -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteWavHeader(BinaryWriter writer, int sampleRate, int channels, int sampleCount)
        {
            const short bitsPerSample = 16;
            var dataSize = sampleCount * bitsPerSample / 8;
            var byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
        }
    }
}
