#if UNITY_EDITOR
using System;
using System.Diagnostics;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    // Git is read only in the Editor/build pipeline; players consume the serialized asset.
    public sealed class ExperimentBuildInfoGenerator : IPreprocessBuildWithReport
    {
        private const string BuildInfoPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentBuildInfo.asset";
        private const string ProtocolPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset";
        public int callbackOrder => 0;

        [MenuItem("SceneTalkVR/Experiment/Refresh Build Info")]
        public static void RefreshBuildInfo()
        {
            var info = AssetDatabase.LoadAssetAtPath<ExperimentBuildInfo>(BuildInfoPath);
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>(ProtocolPath);
            if (info == null || protocol == null) throw new BuildFailedException("Experiment BuildInfo or protocol asset is missing.");
            var serialized = new SerializedObject(info);
            serialized.FindProperty("gitCommit").stringValue = Git("rev-parse HEAD");
            serialized.FindProperty("activeBranch").stringValue = Git("branch --show-current");
            serialized.FindProperty("buildVersion").stringValue = protocol.BuildVersion;
            serialized.FindProperty("buildTimestampUtc").stringValue = DateTime.UtcNow.ToString("o");
            serialized.FindProperty("unityVersion").stringValue = Application.unityVersion;
            serialized.FindProperty("protocolVersion").stringValue = protocol.ProtocolVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
        }

        public void OnPreprocessBuild(BuildReport report) => RefreshBuildInfo();

        private static string Git(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo("git", arguments) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using var process = Process.Start(start);
                return process == null ? string.Empty : process.StandardOutput.ReadToEnd().Trim();
            }
            catch { return string.Empty; }
        }
    }
}
#endif
