using System;
using System.Collections;
using SceneTalkVR.Core;
using UnityEngine;

namespace SceneTalkVR.Demo
{
    public sealed class DemoSpeechInputModule : MonoBehaviour, ISceneTalkSpeechInput
    {
        [SerializeField]
        private string demoTranscript = "I want to practice ordering coffee with a fast-speaking foreign barista.";

        [SerializeField]
        private float simulatedListeningSeconds = 1.25f;

        public IEnumerator CaptureSpeech(Action<string> onComplete, Action<string> onError)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, simulatedListeningSeconds));
            onComplete?.Invoke(demoTranscript);
        }
    }
}
