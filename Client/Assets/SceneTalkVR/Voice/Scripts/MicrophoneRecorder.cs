using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace SceneTalkVR.Voice
{
    public sealed class MicrophoneRecorder : MonoBehaviour
    {
        [SerializeField] private string preferredDeviceName = string.Empty;
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private float recordingSeconds = 3.5f;
        [SerializeField] private int maxRecordingSeconds = 10;

        public int LastSampleRate { get; private set; } = 16000;
        public int LastChannels { get; private set; } = 1;
        public int LastDurationMs { get; private set; }

        public IEnumerator RecordWavBase64(Action<string> onComplete, Action<string> onError)
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                onError?.Invoke("No microphone device is available.");
                yield break;
            }

            var deviceName = ResolveDeviceName();
            var requestedSampleRate = Mathf.Max(8000, sampleRate);
            var requestedMaxSeconds = Mathf.Max(1, maxRecordingSeconds);
            var waitSeconds = Mathf.Clamp(recordingSeconds, 0.25f, requestedMaxSeconds);

            AudioClip clip = null;
            try
            {
                clip = Microphone.Start(deviceName, false, requestedMaxSeconds, requestedSampleRate);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"Failed to start microphone recording: {exception.Message}");
                yield break;
            }

            if (clip == null)
            {
                onError?.Invoke("Microphone.Start returned no audio clip.");
                yield break;
            }

            var startTimeoutAt = Time.realtimeSinceStartup + 2f;
            while (Microphone.GetPosition(deviceName) <= 0)
            {
                if (Time.realtimeSinceStartup >= startTimeoutAt)
                {
                    Microphone.End(deviceName);
                    onError?.Invoke("Microphone did not start recording within 2 seconds.");
                    yield break;
                }

                yield return null;
            }

            yield return new WaitForSeconds(waitSeconds);

            var recordedSamples = Microphone.GetPosition(deviceName);
            Microphone.End(deviceName);

            if (recordedSamples <= 0)
            {
                onError?.Invoke("Microphone recording produced no samples.");
                yield break;
            }

            LastSampleRate = clip.frequency;
            LastChannels = clip.channels;
            LastDurationMs = Mathf.RoundToInt(recordedSamples / (float)clip.frequency * 1000f);

            var wavBytes = EncodeRecordedClipToWav(clip, recordedSamples);
            onComplete?.Invoke(Convert.ToBase64String(wavBytes));
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

        private static byte[] EncodeRecordedClipToWav(AudioClip clip, int recordedSamples)
        {
            var channels = Mathf.Max(1, clip.channels);
            var sampleCount = Mathf.Clamp(recordedSamples, 0, clip.samples);
            var samples = new float[sampleCount * channels];
            clip.GetData(samples, 0);

            using var stream = new MemoryStream(44 + samples.Length * 2);
            using var writer = new BinaryWriter(stream);
            WriteWavHeader(writer, clip.frequency, channels, samples.Length);

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
