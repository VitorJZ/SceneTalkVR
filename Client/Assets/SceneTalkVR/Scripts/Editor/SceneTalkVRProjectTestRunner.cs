using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    [Serializable]
    public sealed class SceneTalkVRProjectTestLeafResult
    {
        public string assembly; public string fullName; public string status; public double durationSeconds; public string message;
    }

    [Serializable]
    public sealed class SceneTalkVRProjectTestSummary
    {
        public string schemaVersion="1.0"; public string runId; public string testMode; public string gitCommit; public string unityVersion; public string protocolVersion;
        public string startedAtUtc; public string completedAtUtc; public string[] testAssemblies=Array.Empty<string>();
        public int testCount; public int passed; public int failed; public int skipped; public int inconclusive; public double durationSeconds;
        public bool unitySkillsPackageTestsExcluded; public SceneTalkVRProjectTestLeafResult[] tests=Array.Empty<SceneTalkVRProjectTestLeafResult>();
    }

    [Serializable]
    public sealed class SceneTalkVRProjectTestCollection
    {
        public string schemaVersion = "1.1";
        public string gitCommit;
        public string unityVersion;
        public string protocolVersion;
        public bool unitySkillsPackageTestsExcluded = true;
        public int testCount;
        public int passed;
        public int failed;
        public int skipped;
        public double durationSeconds;
        public SceneTalkVRProjectTestSummary[] runs = Array.Empty<SceneTalkVRProjectTestSummary>();
    }

    [InitializeOnLoad]
    public static class SceneTalkVRProjectTestRunner
    {
        private const string ActiveKey="SceneTalkVR.ProjectTests.Active";
        private const string ModeKey="SceneTalkVR.ProjectTests.Mode";
        private const string StartKey="SceneTalkVR.ProjectTests.Started";
        private const string OutputFolder="Library/SceneTalkVR/ProjectTestResults";
        private const string DeliveryXml="EXPERIMENT_V1_1_STAGE8_PROJECT_TEST_RESULTS.xml";
        private const string DeliveryJson="EXPERIMENT_V1_1_STAGE8_PROJECT_TEST_RESULTS.json";
        private static readonly ProjectCallbacks Callbacks=new ProjectCallbacks();

        static SceneTalkVRProjectTestRunner() { TestRunnerApi.RegisterTestCallback(Callbacks, 1000); }

        [MenuItem("SceneTalkVR/Tests/Run Project EditMode", false, 10)] public static void RunEditMode() => Run(TestMode.EditMode);
        [MenuItem("SceneTalkVR/Tests/Run Project PlayMode", false, 11)] public static void RunPlayMode() => Run(TestMode.PlayMode);

        public static Filter BuildProjectFilter(TestMode mode) => mode==TestMode.EditMode
            ? new Filter{testMode=TestMode.EditMode,groupNames=new[]{"^SceneTalkVR\\.Tests\\."}}
            : new Filter{testMode=TestMode.PlayMode,assemblyNames=new[]{"SceneTalkVR.Stage2.PlayModeTests"},groupNames=new[]{"^SceneTalkVR\\.Tests\\.PlayMode\\."}};

        public static bool IsProjectTestName(string fullName) => !string.IsNullOrWhiteSpace(fullName) && fullName.StartsWith("SceneTalkVR.Tests.",StringComparison.Ordinal) && fullName.IndexOf("UnitySkills",StringComparison.OrdinalIgnoreCase)<0;

        private static void Run(TestMode mode)
        {
            if(SessionState.GetBool(ActiveKey,false)){UnityEngine.Debug.LogError("A SceneTalkVR project test run is already active.");return;}
            Directory.CreateDirectory(ProjectPath(OutputFolder));SessionState.SetBool(ActiveKey,true);SessionState.SetString(ModeKey,mode.ToString());SessionState.SetString(StartKey,DateTime.UtcNow.ToString("o"));
            var api=ScriptableObject.CreateInstance<TestRunnerApi>();var job=api.Execute(new ExecutionSettings(BuildProjectFilter(mode)));
            SessionState.SetString("SceneTalkVR.ProjectTests.JobId",job);UnityEngine.Debug.Log($"SceneTalkVR project-only {mode} started: {job}. UnitySkills package assemblies are excluded by namespace/assembly filter.");
        }

        private sealed class ProjectCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(ActiveKey,false))return;
                try
                {
                    var mode=SessionState.GetString(ModeKey,"Unknown");var commit=ResolveGitCommit();var protocol=AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset");
                    var leaves=new List<SceneTalkVRProjectTestLeafResult>();Flatten(result,leaves);
                    leaves=leaves.Where(x=>IsProjectTestName(x.fullName)).ToList();
                    var summary=new SceneTalkVRProjectTestSummary{runId=SessionState.GetString("SceneTalkVR.ProjectTests.JobId",""),testMode=mode,gitCommit=commit,unityVersion=Application.unityVersion,protocolVersion=protocol?.ProtocolVersion??"",startedAtUtc=SessionState.GetString(StartKey,""),completedAtUtc=DateTime.UtcNow.ToString("o"),testAssemblies=leaves.Select(x=>x.assembly).Distinct().OrderBy(x=>x).ToArray(),testCount=leaves.Count,passed=leaves.Count(x=>x.status=="Passed"),failed=leaves.Count(x=>x.status.StartsWith("Failed",StringComparison.Ordinal)),skipped=leaves.Count(x=>x.status.StartsWith("Skipped",StringComparison.Ordinal)),inconclusive=leaves.Count(x=>x.status=="Inconclusive"),durationSeconds=leaves.Sum(x=>x.durationSeconds),unitySkillsPackageTestsExcluded=true,tests=leaves.ToArray()};
                    var raw=ProjectPath(Path.Combine(OutputFolder,$"{commit}_{mode.ToLowerInvariant()}_raw.xml"));TestRunnerApi.SaveResultToFile(result,raw);
                    File.WriteAllText(ProjectPath(Path.Combine(OutputFolder,$"{mode.ToLowerInvariant()}_summary.json")),JsonUtility.ToJson(summary,true),Encoding.UTF8);
                    WriteCombinedDelivery(summary);
                    SessionState.SetString("SceneTalkVR.ProjectTests.LastSummary",JsonUtility.ToJson(summary));UnityEngine.Debug.Log($"SceneTalkVR project-only {mode}: {summary.passed}/{summary.testCount} passed, {summary.failed} failed, {summary.skipped} skipped. Raw XML: {raw}");
                }
                finally{SessionState.SetBool(ActiveKey,false);}
            }
        }

        private static void Flatten(ITestResultAdaptor node,List<SceneTalkVRProjectTestLeafResult> output)
        {
            if(node==null)return;if(!node.HasChildren){var unique=node.Test?.UniqueName??"";var assembly=unique.Contains("/")?unique.Substring(0,unique.IndexOf('/')):"Assembly-CSharp-Editor";output.Add(new SceneTalkVRProjectTestLeafResult{assembly=assembly,fullName=node.FullName,status=node.ResultState,durationSeconds=node.Duration,message=node.Message??""});return;}
            foreach(var child in node.Children)Flatten(child,output);
        }
        public static string BuildAggregateXml(SceneTalkVRProjectTestSummary s,string raw) => $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<project-test-results schema-version=\"1.0\" mode=\"{Escape(s.testMode)}\" commit=\"{Escape(s.gitCommit)}\" unity=\"{Escape(s.unityVersion)}\" protocol=\"{Escape(s.protocolVersion)}\" total=\"{s.testCount}\" passed=\"{s.passed}\" failed=\"{s.failed}\" skipped=\"{s.skipped}\" duration=\"{s.durationSeconds:F3}\" unityskills-package-tests-excluded=\"true\">\n  <raw-unity-test-runner-xml>{Escape(raw)}</raw-unity-test-runner-xml>\n</project-test-results>\n";
        private static void WriteCombinedDelivery(SceneTalkVRProjectTestSummary current)
        {
            var summaries = new List<SceneTalkVRProjectTestSummary>();
            foreach (var mode in new[] { "editmode", "playmode" })
            {
                var path = ProjectPath(Path.Combine(OutputFolder, mode + "_summary.json"));
                if (!File.Exists(path)) continue;
                var value = JsonUtility.FromJson<SceneTalkVRProjectTestSummary>(File.ReadAllText(path));
                if (value != null && value.gitCommit == current.gitCommit && value.unityVersion == current.unityVersion && value.protocolVersion == current.protocolVersion)
                    summaries.Add(value);
            }
            var collection = new SceneTalkVRProjectTestCollection
            {
                gitCommit = current.gitCommit,
                unityVersion = current.unityVersion,
                protocolVersion = current.protocolVersion,
                runs = summaries.OrderBy(x => x.testMode).ToArray(),
                testCount = summaries.Sum(x => x.testCount),
                passed = summaries.Sum(x => x.passed),
                failed = summaries.Sum(x => x.failed),
                skipped = summaries.Sum(x => x.skipped),
                durationSeconds = summaries.Sum(x => x.durationSeconds)
            };
            File.WriteAllText(ProjectPath(DeliveryJson), JsonUtility.ToJson(collection, true), Encoding.UTF8);
            var runs = string.Join(Environment.NewLine, collection.runs.Select(x =>
                $"  <run mode=\"{Escape(x.testMode)}\" total=\"{x.testCount}\" passed=\"{x.passed}\" failed=\"{x.failed}\" skipped=\"{x.skipped}\" duration=\"{x.durationSeconds:F3}\" raw-unity-test-runner-xml=\"{Escape(ProjectPath(Path.Combine(OutputFolder,$"{x.gitCommit}_{x.testMode.ToLowerInvariant()}_raw.xml")))}\" />"));
            var xml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<project-test-results schema-version=\"1.1\" commit=\"{Escape(collection.gitCommit)}\" unity=\"{Escape(collection.unityVersion)}\" protocol=\"{Escape(collection.protocolVersion)}\" total=\"{collection.testCount}\" passed=\"{collection.passed}\" failed=\"{collection.failed}\" skipped=\"{collection.skipped}\" duration=\"{collection.durationSeconds:F3}\" unityskills-package-tests-excluded=\"true\">\n{runs}\n</project-test-results>\n";
            File.WriteAllText(ProjectPath(DeliveryXml), xml, Encoding.UTF8);
        }
        private static string Escape(string value)=>SecurityElement.Escape(value??"");
        private static string ResolveGitCommit(){try{var p=Process.Start(new ProcessStartInfo("git","rev-parse HEAD"){WorkingDirectory=Directory.GetParent(Application.dataPath).FullName,UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true});var value=p.StandardOutput.ReadToEnd().Trim();p.WaitForExit(3000);return value;}catch{return "unknown";}}
        private static string ProjectPath(string relative)=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,relative.Replace('/',Path.DirectorySeparatorChar));
    }
}
