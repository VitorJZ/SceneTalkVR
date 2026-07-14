using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime.Services;

namespace SceneTalkVR.Editor
{
    public sealed class LLMPipelineTestRunner : EditorWindow
    {
        [MenuItem("SceneTalkVR/Diagnostics/Run LLM Manipulation Check")]
        public static void ShowWindow()
        {
            var window = GetWindow<LLMPipelineTestRunner>("LLM Test Runner");
            window.Show();
        }

        public class TestCase
        {
            public string id;
            public string scenarioId;
            public string input;
            public float sttConfidence;
            public float recordingDurationMs;
            public bool expectedHasFeedback;
            public string expectedErrorType;
            public string expectedExplicitContains;
            public string expectedRecastContains;
        }

        private List<TestCase> testCases = new List<TestCase>();
        private string statusMessage = "Idle";
        private bool isRunning;
        private int maxTestCases = 5;
        private int requestDelayMs = 6000;

        private void OnGUI()
        {
            GUILayout.Label("SceneTalkVR LLM Pipeline Manipulation Check", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Load Test Cases"))
            {
                LoadTestCases();
            }

            GUILayout.Label($"Loaded Cases: {testCases.Count}");

            maxTestCases = EditorGUILayout.IntField("Max Test Cases", maxTestCases);
            requestDelayMs = EditorGUILayout.IntField("Request Delay (ms)", requestDelayMs);

            if (isRunning)
            {
                GUI.enabled = false;
            }

            if (GUILayout.Button("Run Test Suite & Generate Report"))
            {
                RunSuiteAsync();
            }

            GUI.enabled = true;

            GUILayout.Space(10);
            GUILayout.Label($"Status: {statusMessage}");
        }

        private void LoadTestCases()
        {
            testCases.Clear();
            string path = Path.Combine(Application.dataPath, "SceneTalkVR/Docs/LLMPipelineTestCases.md");
            if (!File.Exists(path))
            {
                statusMessage = $"Test cases file not found at: {path}";
                return;
            }

            try
            {
                var lines = File.ReadAllLines(path);
                TestCase current = null;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("- id:"))
                    {
                        if (current != null) testCases.Add(current);
                        current = new TestCase { id = trimmed.Replace("- id:", "").Trim() };
                    }
                    else if (current != null)
                    {
                        if (trimmed.StartsWith("scenarioId:")) 
                            current.scenarioId = trimmed.Replace("scenarioId:", "").Trim();
                        else if (trimmed.StartsWith("input:")) 
                            current.input = trimmed.Replace("input:", "").Trim().Trim('"');
                        else if (trimmed.StartsWith("sttConfidence:")) 
                            float.TryParse(trimmed.Replace("sttConfidence:", "").Trim(), out current.sttConfidence);
                        else if (trimmed.StartsWith("recordingDurationMs:")) 
                            float.TryParse(trimmed.Replace("recordingDurationMs:", "").Trim(), out current.recordingDurationMs);
                        else if (trimmed.StartsWith("expectedHasFeedback:")) 
                            bool.TryParse(trimmed.Replace("expectedHasFeedback:", "").Trim(), out current.expectedHasFeedback);
                        else if (trimmed.StartsWith("expectedErrorType:")) 
                            current.expectedErrorType = trimmed.Replace("expectedErrorType:", "").Trim();
                        else if (trimmed.StartsWith("expectedExplicitContains:")) 
                            current.expectedExplicitContains = trimmed.Replace("expectedExplicitContains:", "").Trim().Trim('"');
                        else if (trimmed.StartsWith("expectedRecastContains:")) 
                            current.expectedRecastContains = trimmed.Replace("expectedRecastContains:", "").Trim().Trim('"');
                    }
                }
                if (current != null) testCases.Add(current);
                statusMessage = $"Successfully loaded {testCases.Count} test cases.";
            }
            catch (Exception ex)
            {
                statusMessage = $"Error loading test cases: {ex.Message}";
            }
        }

        private async void RunSuiteAsync()
        {
            isRunning = true;
            statusMessage = "Initializing test environment...";
            LoadTestCases();

            if (testCases.Count == 0)
            {
                statusMessage = "Cannot run: No test cases loaded.";
                isRunning = false;
                return;
            }

            var llmService = FindFirstObjectByType<RealLLMService>();
            if (llmService == null)
            {
                statusMessage = "Cannot run: RealLLMService not found in active scene. Please open the main practice scene.";
                isRunning = false;
                return;
            }

            int limitCount = Mathf.Min(testCases.Count, maxTestCases);
            int totalTests = limitCount * 4; // 4 conditions per test case
            int completedTests = 0;
            int passCount = 0;
            int failCount = 0;
            int jsonSuccessCount = 0;
            int leakageCount = 0;
            int recastViolationCount = 0;

            var reportBuilder = new System.Text.StringBuilder();
            reportBuilder.AppendLine("# LLM Pipeline Manipulation Check Report");
            reportBuilder.AppendLine($"Date: {DateTime.Now.ToString(\"g\")}");
            reportBuilder.AppendLine($"Total Test Cases: {limitCount}");
            reportBuilder.AppendLine($"Total Executed Variations: {totalTests}");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("## Summary Metrics");
            
            var conditions = new string[] 
            { 
                "dialogue_avatar_explicit", 
                "dialogue_avatar_recast", 
                "assistant_agent_explicit", 
                "assistant_agent_recast" 
            };

            var resultsList = new List<string>();

            // Temporarily backup existing state on RealLLMService
            var backupCondition = llmService.CurrentCondition;

            try
            {
                for (int i = 0; i < limitCount; i++)
                {
                    var tc = testCases[i];
                    statusMessage = $"Running test {i + 1}/{limitCount}: {tc.id}...";
                    Repaint();

                    foreach (var cond in conditions)
                    {
                        var parts = cond.Split('_');
                        string provider = parts[0] + "_" + parts[1];
                        string style = parts[2];

                        // Create fake experiment condition
                        var testCond = new CorrectionExperimentCondition
                        {
                            participantId = "test_runner",
                            sessionId = "test_session",
                            conditionId = cond,
                            scenarioId = tc.scenarioId,
                            provider = provider,
                            style = style,
                            formalExperiment = true, // Force locked mode
                            turnIndex = 1
                        };

                        llmService.SetExperimentCondition(testCond);
                        
                        // Inject speech metadata
                        // RealLLMService uses lastRecordingDurationMs and lastSttConfidence fields. Let's use reflection to inject them directly!
                        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
                        llmService.GetType().GetField("lastRecordingDurationMs", flags)?.SetValue(llmService, tc.recordingDurationMs);
                        llmService.GetType().GetField("lastSttConfidence", flags)?.SetValue(llmService, tc.sttConfidence);
                        llmService.GetType().GetField("lastRecordingStopReason", flags)?.SetValue(llmService, "test_complete");

                        bool parsedOk = false;
                        string error = null;
                        SpringScenePayload payload = null;

                        // Call RealLLMService's GenerateSceneAndReply method synchronously/asynchronously
                        var tcs = new TaskCompletionSource<SpringScenePayload>();
                        llmService.StartCoroutine(llmService.GenerateSceneAndReply(
                            tc.input,
                            val => tcs.SetResult(val),
                            err => tcs.SetException(new Exception(err))));

                        try
                        {
                            payload = await tcs.Task;
                            parsedOk = true;
                            jsonSuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                        }

                        completedTests++;
                        bool isPass = true;
                        string failReason = "";

                        if (parsedOk && payload != null)
                        {
                            // 1. STT Suppression validation
                            bool sttSuppressed = (tc.sttConfidence < 0.5f) || (tc.recordingDurationMs < 500f);
                            if (sttSuppressed)
                            {
                                if (payload.correctionFeedback.hasFeedback)
                                {
                                    isPass = false;
                                    failReason += "STT should be suppressed but correction occurred. ";
                                }
                            }
                            else
                            {
                                // Normal correction validation
                                if (payload.correctionFeedback.hasFeedback != tc.expectedHasFeedback)
                                {
                                    isPass = false;
                                    failReason += $"Expected hasFeedback={tc.expectedHasFeedback} but got {payload.correctionFeedback.hasFeedback}. ";
                                }
                            }

                            // 2. Dialogue Reply Leakage validation
                            if (provider == "assistant_agent" && CorrectionTextGuards.LooksLikeCorrection(payload.dialogueReply))
                            {
                                leakageCount++;
                                isPass = false;
                                failReason += "Correction leaked into dialogueReply under assistant_agent. ";
                            }

                            // 3. Recast Purity validation
                            if (style == "recast" && payload.correctionFeedback.hasFeedback)
                            {
                                if (CorrectionTextGuards.ViolatesRecastPurity(payload.correctionFeedback.feedbackText))
                                {
                                    recastViolationCount++;
                                    isPass = false;
                                    failReason += "Recast purity violated in feedbackText. ";
                                }
                            }
                        }
                        else
                        {
                            isPass = false;
                            failReason += $"LLM Pipeline Error: {error}. ";
                        }

                        if (isPass) passCount++;
                        else failCount++;

                        string outcomeSymbol = isPass ? "✅ PASS" : "❌ FAIL";
                        string resultLine = $"| {tc.id} | {cond} | {tc.input} | {outcomeSymbol} | {failReason} |";
                        resultsList.Add(resultLine);

                        if (requestDelayMs > 0 && completedTests < totalTests)
                        {
                            statusMessage = $"Pacing... Waiting {requestDelayMs}ms to avoid Rate Limit (Completed {completedTests}/{totalTests})...";
                            Repaint();
                            await Task.Delay(requestDelayMs);
                        }
                    }
                }

                float passRate = ((float)passCount / totalTests) * 100f;
                float jsonRate = ((float)jsonSuccessCount / totalTests) * 100f;

                reportBuilder.AppendLine($"- **Pass Rate**: {passRate:F1}% ({passCount}/{totalTests} passed)");
                reportBuilder.AppendLine($"- **JSON Parse Success Rate**: {jsonRate:F1}% ({jsonSuccessCount}/{totalTests} parsed)");
                reportBuilder.AppendLine($"- **Assistant Dialogue Leakage Count**: {leakageCount}");
                reportBuilder.AppendLine($"- **Recast Purity Violation Count**: {recastViolationCount}");
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("## Detailed Test Results");
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("| Case ID | Condition | Input | Result | Details |");
                reportBuilder.AppendLine("|---|---|---|---|---|");
                foreach (var line in resultsList)
                {
                    reportBuilder.AppendLine(line);
                }

                // Write report file
                string reportPath = Path.Combine(Application.dataPath, "SceneTalkVR/Docs/LLMPipelineManipulationCheckReport.md");
                File.WriteAllText(reportPath, reportBuilder.ToString());
                AssetDatabase.Refresh();

                statusMessage = $"Manipulation check complete! Pass Rate: {passRate:F1}%. Report saved to: Assets/SceneTalkVR/Docs/LLMPipelineManipulationCheckReport.md";
            }
            catch (Exception ex)
            {
                statusMessage = $"Test run exception: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                // Restore state
                llmService.SetExperimentCondition(backupCondition);
                isRunning = false;
                Repaint();
            }
        }
    }
}
