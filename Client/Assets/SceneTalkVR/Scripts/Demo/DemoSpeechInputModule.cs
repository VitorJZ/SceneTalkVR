using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Demo
{
    public sealed class DemoSpeechInputModule : MonoBehaviour, ISceneTalkSpeechInput, ISceneTalkManualSpeechInput
    {
        [SerializeField]
        private string demoTranscript = "I want to practice ordering coffee with a fast-speaking foreign barista.";

        [SerializeField]
        private float simulatedListeningSeconds = 1.25f;

        [SerializeField]
        private bool autoCompleteAfterSimulatedDelay;

        private bool stopRequested;
        private bool cancelRequested;

        public IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError)
        {
            stopRequested = false;
            cancelRequested = false;

            var startAt = Time.realtimeSinceStartup;
            while (!cancelRequested && !stopRequested)
            {
                if (autoCompleteAfterSimulatedDelay
                    && Time.realtimeSinceStartup - startAt >= Mathf.Max(0f, simulatedListeningSeconds))
                {
                    break;
                }

                yield return null;
            }

            if (cancelRequested)
            {
                yield break;
            }

            onComplete?.Invoke(demoTranscript);
        }

        public void RequestStopCapture()
        {
            stopRequested = true;
        }

        public void CancelCapture()
        {
            cancelRequested = true;
            stopRequested = true;
        }
    }
}
