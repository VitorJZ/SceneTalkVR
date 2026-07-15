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
        private bool isCapturing;
        private bool enableDeveloperConsole = true;

        public bool EnableDeveloperConsole
        {
            get => enableDeveloperConsole;
            set => enableDeveloperConsole = value;
        }

        public IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError)
        {
            stopRequested = false;
            cancelRequested = false;
            isCapturing = true;

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

            isCapturing = false;

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

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!isCapturing || !enableDeveloperConsole) return;

            // Centered developer text prompt window at the bottom of the screen
            Rect windowRect = new Rect((Screen.width - 550) / 2f, Screen.height - 180, 550, 140);
            if (Screen.width < 570)
            {
                windowRect = new Rect(10, Screen.height - 180, Screen.width - 20, 140);
            }

            GUILayout.BeginArea(windowRect, "⌨️ Developer Text Prompt Console (Demo Speech Mode)", GUI.skin.box);
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Text Prompt:", GUILayout.Width(90));
            demoTranscript = GUILayout.TextField(demoTranscript, GUILayout.Height(30));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Submit Custom Prompt (Simulate Speech)", GUILayout.Height(35)))
            {
                stopRequested = true;
            }
            if (GUILayout.Button("Cancel Capture", GUILayout.Width(120), GUILayout.Height(35)))
            {
                cancelRequested = true;
                stopRequested = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
#endif
    }
}
