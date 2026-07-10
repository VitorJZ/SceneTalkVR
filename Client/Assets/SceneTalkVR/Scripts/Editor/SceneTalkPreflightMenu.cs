using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace SceneTalkVR.EditorTools
{
    public static class SceneTalkPreflightMenu
    {
        private const string MainScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ReportPath = "Assets/SceneTalkVR/Docs/VitorPreflightReport.md";
        private const string AndroidPackageName = "com.scenetalkvr.demo";
        private const string PicoOpenXrDefine = "PICO_OPENXR_SDK";
        private const string OpenXrLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string KhrSimpleControllerFeatureId = "com.unity.openxr.feature.input.khrsimpleprofile";
        private const string PicoXrSupportFeatureId = "com.unity.openxr.feature.pico";
        private const string PicoOpenXrFeaturesFeatureId = "com.unity.openxr.pico.features";
        private const string Pico4ControllerFeatureId = "com.unity.openxr.feature.input.PICO4touch";
        private const string Pico4UltraControllerFeatureId = "com.unity.openxr.feature.input.PICO4Ultratouch";

        [MenuItem("SceneTalkVR/Diagnostics/Run Preflight Check", false, 50)]
        public static void RunPreflightCheck()
        {
            var report = BuildReport();
            WriteReport(report);
            Debug.Log(report);
            AssetDatabase.Refresh();
        }

        [MenuItem("SceneTalkVR/Setup/Apply Recommended Project Settings", false, 20)]
        public static void ApplyRecommendedProjectSettings()
        {
            ConfigureAndroidBuildDefaults(false);
            ConfigurePicoOpenXRDefaults(false);
            RunPreflightCheck();
        }

        public static void ConfigureAndroidBuildDefaults()
        {
            ConfigureAndroidBuildDefaults(true);
        }

        private static void ConfigureAndroidBuildDefaults(bool runPreflight)
        {
            PlayerSettings.productName = "SceneTalkVR";
            PlayerSettings.companyName = "SceneTalkVR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });

            EnsureMainSceneInBuildSettings();

            if (IsAndroidBuildSupportInstalled())
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            else
            {
                Debug.LogWarning(
                    "Android Build Support is not installed for this Unity editor. " +
                    "Install Android Build Support, Android SDK & NDK Tools, and OpenJDK in Unity Hub, then rerun this menu.");
            }

            AssetDatabase.SaveAssets();
            if (runPreflight)
            {
                RunPreflightCheck();
            }
        }

        [MenuItem("SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile", false, 120)]
        public static void ConfigureOpenXRAndroidInteractionDefaults()
        {
            ConfigureOpenXRAndroidInteractionDefaults(true);
        }

        private static void ConfigureOpenXRAndroidInteractionDefaults(bool runPreflight)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);

            if (settings == null)
            {
                Debug.LogWarning("OpenXR Android settings are not available. Enable OpenXR under XR Plug-in Management first.");
                return;
            }

            var khrSimpleController = settings.GetFeature<KHRSimpleControllerProfile>();

            if (khrSimpleController == null)
            {
                Debug.LogWarning("Khronos Simple Controller Profile was not found in Android OpenXR settings.");
                return;
            }

            khrSimpleController.enabled = true;
            EditorUtility.SetDirty(khrSimpleController);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            if (runPreflight)
            {
                RunPreflightCheck();
            }
        }

        public static void ConfigurePicoOpenXRDefaults()
        {
            ConfigurePicoOpenXRDefaults(true);
        }

        private static void ConfigurePicoOpenXRDefaults(bool runPreflight)
        {
            var addedDefine = EnsureAndroidDefine(PicoOpenXrDefine);
            var enabledSupport = SetAndroidOpenXRFeatureEnabled(PicoXrSupportFeatureId, true);
            var enabledExtensions = SetAndroidOpenXRFeatureEnabled(PicoOpenXrFeaturesFeatureId, true);
            var enabledPico4 = SetAndroidOpenXRFeatureEnabled(Pico4ControllerFeatureId, true);
            var enabledPico4Ultra = SetAndroidOpenXRFeatureEnabled(Pico4UltraControllerFeatureId, true);

            if (enabledPico4 || enabledPico4Ultra)
            {
                SetAndroidOpenXRFeatureEnabled(KhrSimpleControllerFeatureId, false);
            }

            AssetDatabase.SaveAssets();

            if (addedDefine && (!enabledSupport || !enabledExtensions || (!enabledPico4 && !enabledPico4Ultra)))
            {
                Debug.LogWarning(
                    "PICO_OPENXR_SDK was added. Wait for Unity to finish recompiling, then rerun " +
                    "`SceneTalkVR/Setup/Apply Recommended Project Settings` to enable PICO OpenXR features.");
            }

            if (runPreflight)
            {
                RunPreflightCheck();
            }
        }

        private static string BuildReport()
        {
            var report = new StringBuilder();
            report.AppendLine("# Vitor Preflight Report");
            report.AppendLine();
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"Active Build Target: {EditorUserBuildSettings.activeBuildTarget}");
            report.AppendLine($"Android Build Support Path: `{GetAndroidPlaybackEnginePath()}`");
            report.AppendLine();
            var hasAndroidBuildSupport = IsAndroidBuildSupportInstalled();

            AppendSection(report, "Client Scene");
            AppendCheck(report, File.Exists(ToAbsolutePath(MainScenePath)), $"Main scene exists: `{MainScenePath}`");
            AppendCheck(report, IsSceneInBuildSettings(MainScenePath), "Main scene is included in Build Settings");
            AppendCheck(report, SceneManager.GetActiveScene().path == MainScenePath, "Active scene is SampleScene");

            AppendSection(report, "Demo Rig");
            var orchestrators = FindAll<SceneTalkOrchestrator>();
            var bootstraps = FindAll<SceneTalkInteractionBootstrap>();
            var canvases = FindAll<Canvas>()
                .Where(canvas => canvas.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal))
                .ToArray();
            var eventSystems = FindAll<EventSystem>();
            var experimentManagers = FindAll<ExperimentConditionManager>();
            var correctionFeedbackPresenters = FindAll<CorrectionFeedbackPresenter>();
            var correctionAgentPresenters = FindAll<CorrectionAgentPresenter>();
            AppendCheck(report, orchestrators.Length == 1, $"One SceneTalkOrchestrator in scene (found {orchestrators.Length})");
            AppendCheck(report, bootstraps.Length == 1, $"One SceneTalkInteractionBootstrap in scene (found {bootstraps.Length})");
            AppendCheck(report, canvases.Length == 1, $"One SceneTalkVR World UI canvas in scene (found {canvases.Length})");
            AppendCheck(report, eventSystems.Length == 1, $"One EventSystem in scene (found {eventSystems.Length})");
            AppendCheck(report, experimentManagers.Length == 1, $"One ExperimentConditionManager in scene (found {experimentManagers.Length})");
            AppendCheck(report, correctionFeedbackPresenters.Length == 1, $"One CorrectionFeedbackPresenter in scene (found {correctionFeedbackPresenters.Length})");
            AppendCheck(report, correctionAgentPresenters.Length == 1, $"One CorrectionAgentPresenter in scene (found {correctionAgentPresenters.Length})");
            AppendCheck(report, HasTrackedPoseDriver(Camera.main), "Main Camera uses XR tracked pose on device");

            if (canvases.Length > 0)
            {
                var canvas = canvases[0];
                AppendCheck(report, canvas.renderMode == RenderMode.WorldSpace, "World UI canvas uses World Space render mode");
                AppendCheck(report, canvas.worldCamera != null, "World UI canvas has an interaction camera");
                AppendCheck(report, Mathf.Abs(canvas.transform.eulerAngles.y) < 0.01f, "World UI canvas is not mirrored on Y axis");
                AppendCheck(report, canvas.GetComponent<GraphicRaycaster>() != null, "World UI canvas has GraphicRaycaster");
            }

            AppendSection(report, "Packages");
            AppendPackageCheck(report, "com.unity.inputsystem", "Input System");
            AppendPackageCheck(report, "com.unity.ugui", "Unity UI");
            AppendPackageCheck(report, "com.unity.xr.interaction.toolkit", "XR Interaction Toolkit");
            AppendPackageCheck(report, "com.unity.xr.openxr", "OpenXR Plugin");
            AppendPackageCheck(report, "com.unity.xr.picoxr", "PICO Unity Integration SDK / PICO XR SDK");

            AppendSection(report, "OpenXR");
            AppendCheck(report, HasAnyAndroidOpenXRInteractionProfileEnabled(), "At least one Android OpenXR interaction profile is enabled");
            AppendCheck(report, HasAndroidOpenXRControllerProfileEnabled(), "Android OpenXR has a controller interaction profile enabled");

            AppendSection(report, "PICO");
            AppendCheck(report, IsAndroidDefineEnabled(PicoOpenXrDefine), "`PICO_OPENXR_SDK` define is set for Android");
            AppendCheck(report, HasAndroidXRLoader(OpenXrLoaderTypeName), "Android XR loader uses OpenXR");
            AppendCheck(report, HasAndroidXRAutoStartEnabled(), "Android XR initializes and runs on startup");
            AppendCheck(report, IsAndroidOpenXRFeatureIdEnabled(PicoXrSupportFeatureId), "PICO XR Support feature is enabled for Android OpenXR");
            AppendCheck(report, IsAndroidOpenXRFeatureIdEnabled(PicoOpenXrFeaturesFeatureId), "PICO OpenXR Features extension is enabled");
            AppendCheck(report, IsAndroidOpenXRFeatureIdEnabled(Pico4ControllerFeatureId) || IsAndroidOpenXRFeatureIdEnabled(Pico4UltraControllerFeatureId), "PICO 4 controller profile is enabled for Android OpenXR");

            AppendSection(report, "Android/PICO Build");
            AppendCheck(report, hasAndroidBuildSupport, "Unity Android Build Support module is installed");
            AppendCheck(report, EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Active build target is Android");
            AppendCheck(report, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) == AndroidPackageName, $"Android package id is `{AndroidPackageName}`");
            AppendCheck(report, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP, "Android scripting backend is IL2CPP");
            AppendCheck(report, PlayerSettings.Android.targetArchitectures == AndroidArchitecture.ARM64, "Android target architecture is ARM64");
            AppendCheck(report, (int)PlayerSettings.Android.minSdkVersion >= (int)AndroidSdkVersions.AndroidApiLevel29, "Android minimum SDK is 29 or higher for PICO");
            AppendCheck(report, !PlayerSettings.Android.useCustomKeystore, "Android development builds use Unity debug signing");
            AppendCheck(report, UsesAndroidOpenGLES3Only(), "Android graphics API is OpenGLES3 only");

            AppendSection(report, "Manual Steps Still Required");
            if (!hasAndroidBuildSupport)
            {
                report.AppendLine("- Install Unity Hub modules: Android Build Support, Android SDK & NDK Tools, and OpenJDK.");
            }
            report.AppendLine("- Run `SceneTalkVR/Setup/Apply Recommended Project Settings` after package import or Unity recompilation.");
            report.AppendLine("- If OpenXR validation still reports no interaction profile, run `SceneTalkVR/Advanced/Enable OpenXR Fallback Controller Profile` or add `Khronos Simple Controller Profile` on the Android OpenXR page.");
            report.AppendLine("- In Unity Project Settings, keep exactly one Android XR provider path active: OpenXR + PICO features, or PICO native loader.");
            report.AppendLine("- Keep XR automatic loading and automatic running enabled for Android unless a custom startup script explicitly initializes XR.");
            report.AppendLine("- Keep Android Graphics APIs set to OpenGLES3 only for PICO 4 debug builds; Vulkan can crash on startup with this project stack.");
            report.AppendLine("- For local Build & Run, keep custom keystore disabled. Enable a private keystore only for release builds.");
            report.AppendLine("- Connect PICO 4 with developer mode enabled, then build and run the Android APK.");
            report.AppendLine("- Replace demo Spring/Edwin adapters with real LLM, STT, TTS, Avatar, and scene-generation modules.");

            return report.ToString();
        }

        private static void AppendSection(StringBuilder report, string title)
        {
            report.AppendLine();
            report.AppendLine($"## {title}");
            report.AppendLine();
        }

        private static void AppendPackageCheck(StringBuilder report, string packageName, string label)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName);
            var installed = package != null;
            var suffix = installed ? $" `{package.version}`" : string.Empty;
            AppendCheck(report, installed, $"{label} installed{suffix}");
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static void AppendCheck(StringBuilder report, bool passed, string text)
        {
            report.AppendLine($"- [{(passed ? "x" : " ")}] {text}");
        }

        private static bool IsSceneInBuildSettings(string scenePath)
        {
            return EditorBuildSettings.scenes.Any(scene => scene.path == scenePath && scene.enabled);
        }

        private static void EnsureMainSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existingIndex = scenes.FindIndex(scene => scene.path == MainScenePath);

            if (existingIndex >= 0)
            {
                scenes[existingIndex].enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(MainScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool IsAndroidBuildSupportInstalled()
        {
            return Directory.Exists(GetAndroidPlaybackEnginePath());
        }

        private static bool HasAnyAndroidOpenXRInteractionProfileEnabled()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            return settings != null && settings.GetFeatures<OpenXRInteractionFeature>().Any(feature => feature.enabled);
        }

        private static bool IsAndroidOpenXRFeatureEnabled<T>() where T : OpenXRFeature
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var feature = settings?.GetFeature<T>();
            return feature != null && feature.enabled;
        }

        private static bool HasAndroidOpenXRControllerProfileEnabled()
        {
            return IsAndroidOpenXRFeatureIdEnabled(KhrSimpleControllerFeatureId)
                || IsAndroidOpenXRFeatureIdEnabled(Pico4ControllerFeatureId)
                || IsAndroidOpenXRFeatureIdEnabled(Pico4UltraControllerFeatureId);
        }

        private static bool IsAndroidOpenXRFeatureIdEnabled(string featureId)
        {
            var feature = FindAndroidOpenXRFeature(featureId);
            return feature != null && feature.enabled;
        }

        private static bool SetAndroidOpenXRFeatureEnabled(string featureId, bool enabled)
        {
            var feature = FindAndroidOpenXRFeature(featureId);

            if (feature == null)
            {
                return false;
            }

            feature.enabled = enabled;
            EditorUtility.SetDirty(feature);
            return true;
        }

        private static OpenXRFeature FindAndroidOpenXRFeature(string featureId)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);

            if (settings == null)
            {
                return null;
            }

            return settings.GetFeatures()
                .FirstOrDefault(feature => string.Equals(GetOpenXRFeatureId(feature), featureId, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetOpenXRFeatureId(OpenXRFeature feature)
        {
            var field = typeof(OpenXRFeature).GetField("featureIdInternal", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(feature) as string ?? string.Empty;
        }

        private static bool HasAndroidXRLoader(string loaderTypeName)
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var managerSettings = generalSettings?.AssignedSettings;
            return managerSettings != null
                && managerSettings.activeLoaders.Any(loader => loader != null && loader.GetType().FullName == loaderTypeName);
        }

        private static bool HasAndroidXRAutoStartEnabled()
        {
            var generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var managerSettings = generalSettings?.AssignedSettings;
            return generalSettings != null
                && managerSettings != null
                && generalSettings.InitManagerOnStart
                && managerSettings.automaticLoading
                && managerSettings.automaticRunning;
        }

        private static bool UsesAndroidOpenGLES3Only()
        {
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            return apis.Length == 1 && apis[0] == GraphicsDeviceType.OpenGLES3;
        }

        private static bool HasTrackedPoseDriver(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
            {
                var typeName = behaviour == null ? string.Empty : behaviour.GetType().FullName;
                if (!string.IsNullOrEmpty(typeName) && typeName.Contains("TrackedPoseDriver"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EnsureAndroidDefine(string define)
        {
            var currentDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            var defines = currentDefines
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (defines.Contains(define))
            {
                return false;
            }

            defines.Add(define);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", defines));
            return true;
        }

        private static bool IsAndroidDefineEnabled(string define)
        {
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Contains(define);
        }

        private static string GetAndroidPlaybackEnginePath()
        {
            return Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
        }

        private static void WriteReport(string report)
        {
            var absolutePath = ToAbsolutePath(ReportPath);
            var directory = Path.GetDirectoryName(absolutePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(absolutePath, report, Encoding.UTF8);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return projectRoot == null ? assetPath : Path.Combine(projectRoot, assetPath);
        }
    }
}
