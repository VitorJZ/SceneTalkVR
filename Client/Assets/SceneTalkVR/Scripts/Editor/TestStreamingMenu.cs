using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using SceneTalkVR.Runtime.Services;
using SceneTalkVR.Core;

namespace SceneTalkVR.Editor
{
    public static class TestStreamingMenu
    {
        [MenuItem("SceneTalkVR/Diagnostics/Test LLM Streaming Response")]
        public static void RunStreamingTest()
        {
            var llmService = UnityEngine.Object.FindFirstObjectByType<RealLLMService>();
            if (llmService == null)
            {
                Debug.LogError("[TestStreamingMenu] RealLLMService not found in active scene. Please open the main practice scene.");
                return;
            }

            // Set condition to test_runner to bypass active STT component scene checks
            llmService.SetExperimentCondition(new CorrectionExperimentCondition 
            { 
                participantId = "test_runner", 
                scenarioId = "restaurant_reservation",
                provider = "dialogue_avatar",
                style = "explicit"
            });

            // Force override private fields via reflection to bypass any STT suppression
            var sttConfField = typeof(RealLLMService).GetField("lastSttConfidence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sttConfField != null) sttConfField.SetValue(llmService, 1.0f);

            var recDurField = typeof(RealLLMService).GetField("lastRecordingDurationMs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (recDurField != null) recDurField.SetValue(llmService, 1000f);

            // Ensure key is loaded
            string envPath = System.IO.Path.Combine(Application.dataPath, "../../.env");
            if (System.IO.File.Exists(envPath))
            {
                var lines = System.IO.File.ReadAllLines(envPath);
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("OPENAI_API_KEY="))
                    {
                        var key = line.Split('=', 2)[1].Trim().Trim('"').Trim('\'');
                        var apiKeyField = typeof(RealLLMService).GetField("apiKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (apiKeyField != null)
                        {
                            apiKeyField.SetValue(llmService, key);
                        }
                    }
                }
            }

            Debug.Log("[TestStreamingMenu] Starting streaming test with deepseek-chat (bypassing STT)...");
            llmService.StartCoroutine(RunTestCoroutine(llmService));
        }

        private static IEnumerator RunTestCoroutine(RealLLMService service)
        {
            float startTime = Time.realtimeSinceStartup;
            bool isDone = false;

            yield return service.GenerateSceneAndReplyStreaming(
                "Hello Marcus, can you fix my steel sword? It has a crack.",
                sentence =>
                {
                    float elapsed = Time.realtimeSinceStartup - startTime;
                    Debug.Log($"<color=#FF9900>[TestStreamingMenu] [{elapsed:F3}s] [SENTENCE] -> \"{sentence}\"</color>");
                },
                payload =>
                {
                    float elapsed = Time.realtimeSinceStartup - startTime;
                    Debug.Log($"<color=#00FF00>[TestStreamingMenu] [{elapsed:F3}s] [COMPLETE] DialogueReply: \"{payload.dialogueReply}\"</color>");
                    isDone = true;
                },
                error =>
                {
                    Debug.LogError($"[TestStreamingMenu] Error: {error}");
                    isDone = true;
                }
            );

            while (!isDone)
            {
                yield return null;
            }
            Debug.Log("[TestStreamingMenu] Streaming test complete!");
        }
    }
}
