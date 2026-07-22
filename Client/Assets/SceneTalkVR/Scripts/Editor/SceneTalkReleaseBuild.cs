using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class SceneTalkReleaseBuild
    {
        private const string OutputEnvironmentVariable = "SCENETALK_ANDROID_VALIDATION_APK";

        [MenuItem("SceneTalkVR/Build/Build Android Validation APK", false, 200)]
        public static void BuildAndroidValidationApk()
        {
            var output = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(output))
            {
                output = Path.Combine(Path.GetTempPath(), "SceneTalkVR-validation.apk");
            }

            output = Path.GetFullPath(output);
            var outputDirectory = Path.GetDirectoryName(output);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Fail("Unable to resolve the Android validation APK output directory.");
                return;
            }

            Directory.CreateDirectory(outputDirectory);
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes are present in Editor Build Settings.");
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });

            var summary = report.summary;
            var message = $"Android validation build {summary.result}: {output}; "
                + $"errors={summary.totalErrors}; warnings={summary.totalWarnings}; "
                + $"size={summary.totalSize}; duration={summary.totalTime}.";
            if (summary.result != BuildResult.Succeeded || !File.Exists(output))
            {
                Fail(message);
                return;
            }

            Debug.Log("[SceneTalkVR] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[SceneTalkVR] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }

            throw new BuildFailedException(message);
        }
    }
}
