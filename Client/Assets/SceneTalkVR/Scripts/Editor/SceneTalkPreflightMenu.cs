using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SceneTalkVR.AvatarSystem;
using SceneTalkVR.Core;
using SceneTalkVR.History;
using SceneTalkVR.Runtime;
using SceneTalkVR.Runtime.Services;
using SceneTalkVR.Voice;
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
        private const string RuntimeConfigPath = "Assets/SceneTalkVR/RuntimeConfig/SceneTalkRuntimeConfig.asset";
        private const string ExperimentProtocolPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentV11Protocol.asset";
        private const string ExperimentTaskCatalogPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset";
        private const string QuestionnaireCatalogPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentQuestionnaireCatalog.asset";
        private const string PilotPresentationCatalogPath = "Assets/SceneTalkVR/ExperimentProtocol/PilotPresentationCatalog.asset";
        private const string VoiceProfileCatalogPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentVoiceProfileCatalog.asset";
        private const string DeploymentCatalogPath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentDeploymentCatalog.asset";
        private const string EditorCollectionResourcePath = "Assets/SceneTalkVR/ExperimentProtocol/ExperimentEditorCollectionResources.asset";
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
            if (Application.isBatchMode
                && SceneManager.GetActiveScene().path != MainScenePath
                && File.Exists(ToAbsolutePath(MainScenePath)))
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

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

            if (addedDefine)
            {
                AssetDatabase.SaveAssets();
                Debug.LogWarning(
                    "PICO_OPENXR_SDK was added. Wait for Unity to finish recompiling, then rerun " +
                    "`SceneTalkVR/Setup/Apply Recommended Project Settings` to register and enable PICO OpenXR features.");

                if (runPreflight)
                {
                    RunPreflightCheck();
                }

                return;
            }

            UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var enabledSupport = SetAndroidOpenXRFeatureEnabled(PicoXrSupportFeatureId, true);
            var enabledExtensions = SetAndroidOpenXRFeatureEnabled(PicoOpenXrFeaturesFeatureId, true);
            var enabledPico4 = SetAndroidOpenXRFeatureEnabled(Pico4ControllerFeatureId, true);
            var enabledPico4Ultra = SetAndroidOpenXRFeatureEnabled(Pico4UltraControllerFeatureId, true);

            if (enabledPico4 || enabledPico4Ultra)
            {
                SetAndroidOpenXRFeatureEnabled(KhrSimpleControllerFeatureId, false);
            }

            AssetDatabase.SaveAssets();

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
            AppendCheck(report, !EditorBuildSettings.scenes.Any(scene => scene.path.IndexOf("_Recovery/", StringComparison.OrdinalIgnoreCase) >= 0), "Recovery scenes are excluded from Build Settings");
            AppendCheck(report, SceneManager.GetActiveScene().path == MainScenePath, "Active scene is SampleScene");
            AppendCheck(report, !EditorSceneManager.GetActiveScene().isDirty, "Active scene has no unsaved changes");
            AppendCheck(report, !HasMissingScriptsInLoadedScene(), "Scene has no missing scripts");

            AppendSection(report, "Demo Rig");
            var orchestrators = FindAll<SceneTalkOrchestrator>();
            var bootstraps = FindAll<SceneTalkInteractionBootstrap>();
            var canvases = FindAll<Canvas>()
                .Where(canvas => canvas.gameObject.name.StartsWith("SceneTalkVR World UI", StringComparison.Ordinal))
                .ToArray();
            var eventSystems = FindAll<EventSystem>();
            var experimentManagers = FindAll<ExperimentConditionManager>();
            var learningMemoryServices = FindAll<LearningMemoryService>();
            var correctionFeedbackPresenters = FindAll<CorrectionFeedbackPresenter>();
            var correctionAgentPresenters = FindAll<CorrectionAgentPresenter>();
            AppendCheck(report, orchestrators.Length == 1, $"One SceneTalkOrchestrator in scene (found {orchestrators.Length})");
            AppendCheck(report, bootstraps.Length == 1, $"One SceneTalkInteractionBootstrap in scene (found {bootstraps.Length})");
            AppendCheck(report, canvases.Length == 1, $"One SceneTalkVR World UI canvas in scene (found {canvases.Length})");
            AppendCheck(report, eventSystems.Length == 1, $"One EventSystem in scene (found {eventSystems.Length})");
            AppendCheck(report, experimentManagers.Length == 1, $"One ExperimentConditionManager in scene (found {experimentManagers.Length})");
            AppendCheck(report, learningMemoryServices.Length <= 1, $"At most one LearningMemoryService in scene (found {learningMemoryServices.Length}; runtime auto-creates it when absent)");
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

            AppendSection(report, "PICO Real Service Routing");
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<SceneTalkRuntimeConfig>(RuntimeConfigPath);
            var protocol = AssetDatabase.LoadAssetAtPath<ExperimentV11ProtocolConfig>(ExperimentProtocolPath);
            var taskCatalog = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>(ExperimentTaskCatalogPath);
            var questionnaireCatalog = AssetDatabase.LoadAssetAtPath<QuestionnaireCatalog>(QuestionnaireCatalogPath);
            var pilotPresentations = AssetDatabase.LoadAssetAtPath<PilotPresentationCatalog>(PilotPresentationCatalogPath);
            var voiceProfiles = AssetDatabase.LoadAssetAtPath<ExperimentVoiceProfileCatalog>(VoiceProfileCatalogPath);
            var deployments = AssetDatabase.LoadAssetAtPath<ExperimentDeploymentCatalog>(DeploymentCatalogPath);
            var editorCollectionResources = AssetDatabase.LoadAssetAtPath<EditorCollectionResourceCatalog>(EditorCollectionResourcePath);
            var configAppliers = FindAll<SceneTalkRuntimeConfigApplier>();
            var voiceClients = FindAll<VoiceGatewayClient>();
            var holodeckServices = FindAll<HolodeckSceneService>();
            var effectiveVoiceUrl = ResolveEffectiveVoiceGatewayUrl(runtimeConfig, voiceClients.FirstOrDefault());
            var effectiveHolodeckUrl = ResolveEffectiveHolodeckUrl(runtimeConfig, holodeckServices.FirstOrDefault());
            var usesHolodeckBackend = runtimeConfig != null
                ? runtimeConfig.UseHolodeckBackend
                : holodeckServices.FirstOrDefault() != null && holodeckServices.First().UseLocalBackend;

            AppendCheck(report, runtimeConfig != null, "SceneTalkRuntimeConfig asset exists");
            AppendCheck(report, runtimeConfig != null && !EditorUtility.IsDirty(runtimeConfig), "RuntimeConfig asset has no unsaved changes");
            AppendCheck(report, protocol != null, "Experiment v1.1 protocol asset exists");
            AppendCheck(report, protocol != null && !EditorUtility.IsDirty(protocol), "Experiment protocol asset has no unsaved changes");
            AppendCheck(report, experimentManagers.Length == 1 && experimentManagers[0].ExperimentProtocol == protocol, "ExperimentConditionManager is bound to the protocol asset");
            AppendCheck(report, taskCatalog != null, "Experiment v1.1 Task Catalog asset exists");
            AppendCheck(report, taskCatalog != null && !EditorUtility.IsDirty(taskCatalog), "Experiment Task Catalog asset has no unsaved changes");
            AppendCheck(report, experimentManagers.Length == 1 && experimentManagers[0].TaskCatalog == taskCatalog, "ExperimentConditionManager is bound to the Task Catalog asset");
            var taskCatalogError = taskCatalog == null ? "Task Catalog asset is missing" : string.Empty;
            var taskCatalogValid = taskCatalog != null && taskCatalog.ValidateFormal(protocol, out taskCatalogError);
            AppendCheck(report, taskCatalogValid, taskCatalogValid
                ? "Formal Task Catalog is complete"
                : $"Formal Task Catalog is blocked: {taskCatalogError}");
            AppendCheck(report, questionnaireCatalog != null, "Experiment v1.1 Questionnaire Catalog asset exists");
            AppendCheck(report, questionnaireCatalog != null && !EditorUtility.IsDirty(questionnaireCatalog), "Questionnaire Catalog asset has no unsaved changes");
            AppendCheck(report, experimentManagers.Length == 1 && experimentManagers[0].QuestionnaireCatalog == questionnaireCatalog, "ExperimentConditionManager is bound to the Questionnaire Catalog asset");
            var questionnaireError = questionnaireCatalog == null ? "Questionnaire Catalog asset is missing" : string.Empty;
            var questionnaireValid = questionnaireCatalog != null && questionnaireCatalog.ValidateFormal(protocol, out questionnaireError);
            AppendCheck(report, questionnaireValid, questionnaireValid
                ? $"Questionnaire Catalog {questionnaireCatalog.CatalogVersion} is valid; Social Comfort follows protocol decision"
                : $"Questionnaire Catalog is blocked: {questionnaireError}");
            var socialValue = string.Empty;
            var socialConfirmed = protocol != null && protocol.TryGetConfirmedDecision("formal_social_comfort", out socialValue);
            AppendCheck(report, !socialConfirmed || !string.IsNullOrWhiteSpace(socialValue), socialConfirmed
                ? $"Social Comfort protocol decision is confirmed as `{socialValue}`"
                : "Social Comfort remains excluded because `formal_social_comfort` is unconfirmed");
            AppendCheck(report, questionnaireCatalog?.Find("formal_condition_v1") != null,
                "AwaitingQuestionnaire resolves to formal_condition_v1");

            AppendSection(report, "Pilot Embodiment Readiness");
            AppendCheck(report, pilotPresentations != null, "Pilot Presentation Catalog exists");
            AppendCheck(report, experimentManagers.Length == 1 && experimentManagers[0].PilotPresentationCatalog == pilotPresentations,
                "ExperimentConditionManager is bound to the Pilot Presentation Catalog");
            var profiles = pilotPresentations?.Profiles?.Where(x => x != null).ToArray() ?? Array.Empty<PilotPresentationProfile>();
            AppendCheck(report, profiles.Length == 3 && profiles.Select(x => x.embodimentCondition).Distinct().Count() == 3,
                "Voice Only, Floating Orb, and Humanoid Agent profiles are unique");
            var voiceOnly = pilotPresentations?.Find(PilotEmbodimentCondition.VoiceOnly);
            var orb = pilotPresentations?.Find(PilotEmbodimentCondition.FloatingOrb);
            var humanoid = pilotPresentations?.Find(PilotEmbodimentCondition.HumanoidAgent);
            AppendCheck(report, voiceOnly != null && voiceOnly.visualMode == PilotVisualMode.None,
                "Voice Only is an explicit no-visual condition, not a fallback");
            AppendCheck(report, orb != null && orb.visualMode == PilotVisualMode.FloatingOrb && !orb.developerPlaceholder,
                "Floating Orb has a non-placeholder fixed configuration");
            AppendCheck(report, humanoid != null && humanoid.visualMode == PilotVisualMode.Humanoid && humanoid.visualPrefab != null && !humanoid.developerPlaceholder,
                "Humanoid Agent has a non-placeholder prefab");
            AppendCheck(report, profiles.Length == 3 && profiles.Select(x => x.voiceProfileKey).Distinct().Count() == 1,
                "All Pilot embodiments share one voice profile");
            var pilotTaskError = taskCatalog == null ? "Task Catalog asset is missing" : string.Empty;
            var pilotTasksValid = taskCatalog != null && ExperimentTaskCatalog.ValidatePilotTasks(taskCatalog.GetTasks(ExperimentTaskPhase.Pilot).ToArray(), out pilotTaskError);
            AppendCheck(report, pilotTasksValid, pilotTasksValid ? "Three Pilot restaurant tasks are complete and unique" : $"Pilot tasks blocked: {pilotTaskError}");
            var pilotDecisionError = protocol == null ? "protocol asset is missing" : string.Empty;
            var pilotDecisionsValid = protocol != null && protocol.TryResolvePilotDecisions(out _, out _, out pilotDecisionError);
            AppendCheck(report, pilotDecisionsValid, pilotDecisionsValid ? "Pilot feedback style and Voice Only audio policy are confirmed" : $"Locked Pilot decisions blocked: {pilotDecisionError}");
            var pilotSequenceError = protocol == null ? "protocol asset is missing" : string.Empty;
            var pilotSequencesValid = protocol != null
                && protocol.TryResolvePilotSequences(out var pilotSequences, out pilotSequenceError)
                && pilotSequences.Length == 3
                && pilotSequences.All(x => x != null && x.confirmed);
            AppendCheck(report, pilotSequencesValid, pilotSequencesValid
                ? "Pilot a/b/c sequence mapping is confirmed in the protocol asset"
                : $"Pilot a/b/c sequence mapping is blocked: {pilotSequenceError}");
            AppendCheck(report, questionnaireCatalog?.Find("pilot_condition_v1") != null, "Stage 5 pilot_condition_v1 resolves");
            AppendCheck(report, questionnaireCatalog?.Find("pilot_final_v1") != null, "Stage 5 pilot_final_v1 resolves");
            var lockedPilotError = pilotPresentations == null ? "presentation catalog missing" : string.Empty;
            var lockedPilotValid = pilotPresentations != null && pilotPresentations.ValidateLocked(protocol, out lockedPilotError);
            AppendCheck(report, lockedPilotValid, lockedPilotValid ? "Locked Pilot has no placeholder or unresolved configuration" : $"Locked Pilot remains blocked: {lockedPilotError}");
            AppendCheck(report, protocol != null && !string.IsNullOrWhiteSpace(protocol.ProtocolVersion), "Experiment protocol version is non-empty");
            AppendCheck(report, protocol != null && protocol.FormalModeLocked, "Experiment protocol marks Formal Mode as locked");
            AppendSection(report, "Stage 7 Research Decisions and Evidence");
            foreach (var decision in protocol?.RequiredDecisions ?? Array.Empty<ExperimentProtocolDecision>())
            {
                var confirmed = decision != null && decision.status == ProtocolDecisionStatus.Confirmed && !string.IsNullOrWhiteSpace(decision.confirmedValue)
                    && !string.IsNullOrWhiteSpace(decision.confirmedBy) && !string.IsNullOrWhiteSpace(decision.confirmedAtUtc) && !string.IsNullOrWhiteSpace(decision.evidenceReference);
                AppendCheck(report, confirmed, $"{decision?.decisionId ?? "<null>"}: value=`{decision?.confirmedValue ?? ""}`, confirmedBy=`{decision?.confirmedBy ?? ""}`, evidence=`{decision?.evidenceReference ?? ""}`");
            }
            AppendSection(report, "Stage 7 Collection Assets");
            var dialogueVoiceKeys = taskCatalog?.GetTasks(ExperimentTaskPhase.Formal).Select(x => x.voiceProfileKey) ?? Enumerable.Empty<string>();
            var voiceError = voiceProfiles == null ? "voice_profile_catalog_missing" : string.Empty;
            var voiceValid = voiceProfiles != null && voiceProfiles.ValidateForLockedCollection(dialogueVoiceKeys, out voiceError);
            AppendCheck(report, voiceValid, voiceValid ? "Voice Profile Catalog is approved and complete" : $"Voice profiles blocked: {voiceError}");
            foreach (var deploymentId in new[] { ExperimentDeploymentProfileId.PicoLab, ExperimentDeploymentProfileId.PicoPortable })
            {
                var deploymentError = deployments == null ? "deployment_catalog_missing" : string.Empty;
                var deploymentValid = deployments != null && deployments.ValidateForCollection(deploymentId, out deploymentError);
                AppendCheck(report, deploymentValid, deploymentValid ? $"{deploymentId} deployment approved" : $"{deploymentId} blocked: {deploymentError}");
            }
            var avatarCatalog = FindAll<AvatarPresetResolver>().FirstOrDefault()?.Catalog;
            foreach (var task in taskCatalog?.GetTasks(ExperimentTaskPhase.Formal) ?? new System.Collections.Generic.List<ExperimentTaskDefinition>())
            {
                var avatarError = avatarCatalog == null ? "avatar_catalog_missing" : string.Empty;
                var avatarValid = avatarCatalog != null && avatarCatalog.ValidateEditorCollectionPreset(task.avatarPresetKey, out avatarError);
                AppendCheck(report, avatarValid, avatarValid ? $"{task.taskId} exact formal avatar valid" : $"{task.taskId} avatar blocked: {avatarError}");
                var texture = Resources.Load<Texture2D>(task.panoramaResourceKey);
                AppendCheck(report, texture != null && texture.width == texture.height * 2 && texture.width >= 2048, texture == null ? $"{task.taskId} panorama missing" : $"{task.taskId} panorama is collection-grade 2:1 (actual {texture.width}x{texture.height})");
            }
            var formalProtocolError = protocol == null ? "protocol asset is missing" : string.Empty;
            var formalProtocolValid = protocol != null && protocol.ValidateForFormalMode(out formalProtocolError);
            AppendCheck(report, formalProtocolValid, formalProtocolValid
                ? "Formal Mode decisions are confirmed"
                : $"Formal Mode is blocked until protocol decisions are confirmed: {formalProtocolError}");
            var editorResourceError = editorCollectionResources == null ? "editor_collection_resource_catalog_missing" : string.Empty;
            var editorResourcesValid = editorCollectionResources != null
                && editorCollectionResources.Validate(taskCatalog, voiceProfiles, deployments, out editorResourceError);
            var editorDeploymentError = deployments == null ? "deployment_catalog_missing" : string.Empty;
            var editorDeploymentValid = deployments != null
                && deployments.ValidateForCollection(ExperimentDeploymentProfileId.EditorCollection, out editorDeploymentError);
            AppendSection(report, "Deployment Readiness Summary");
            AppendCheck(report, formalProtocolValid && taskCatalogValid && questionnaireValid && voiceValid
                && editorResourcesValid && editorDeploymentValid,
                formalProtocolValid && taskCatalogValid && questionnaireValid && voiceValid && editorResourcesValid && editorDeploymentValid
                    ? "Editor Formal Collection: READY"
                    : $"Editor Formal Collection: BLOCKED ({formalProtocolError};{editorResourceError};{editorDeploymentError})");
            AppendCheck(report, lockedPilotValid, lockedPilotValid ? "Pilot Collection: READY" : $"Pilot Collection: BLOCKED ({lockedPilotError})");
            report.AppendLine("- [INFO] PICO Deployment: NOT VALIDATED (does not block Editor Collection)");
            AppendCheck(report, configAppliers.Length >= 1, $"Scene has runtime config applier (found {configAppliers.Length})");
            AppendCheck(report, HasRequiredSceneReferences(configAppliers, experimentManagers), "Required runtime, protocol, and avatar catalog references are assigned");
            AppendCheck(report, !string.IsNullOrWhiteSpace(effectiveVoiceUrl), "Voice gateway URL is configured");
            AppendCheck(report, !SceneTalkRuntimeConfig.IsLoopbackUrl(effectiveVoiceUrl), $"Voice gateway URL is not localhost for PICO: `{DisplayEndpoint(effectiveVoiceUrl)}`");
            var usbTransportConfigured = runtimeConfig == null
                || runtimeConfig.TransportPreference == GatewayTransportPreference.LanOnly
                || SceneTalkRuntimeConfig.IsLoopbackUrl(runtimeConfig.UsbVoiceGatewayBaseUrl)
                    && SceneTalkRuntimeConfig.IsLoopbackUrl(runtimeConfig.UsbLlmApiUrl);
            AppendCheck(report, usbTransportConfigured,
                runtimeConfig?.TransportPreference == GatewayTransportPreference.LanOnly
                    ? "USB ADB transport is disabled; LAN-only compatibility mode is active"
                    : "USB ADB transport uses explicit Voice and LLM loopback endpoints");
            AppendCheck(report,
                runtimeConfig == null || SceneTalkRuntimeConfig.IsLoopbackUrl(runtimeConfig.UsbHistoryExportBaseUrl),
                "PICO history export uses an explicit USB loopback endpoint");
            AppendCheck(report, !usesHolodeckBackend || !string.IsNullOrWhiteSpace(effectiveHolodeckUrl), usesHolodeckBackend
                ? "Holodeck backend URL is configured"
                : "Holodeck backend is disabled for this build");
            AppendCheck(report, !usesHolodeckBackend || !SceneTalkRuntimeConfig.IsLoopbackUrl(effectiveHolodeckUrl), usesHolodeckBackend
                ? $"Holodeck backend URL is not localhost for PICO: `{DisplayEndpoint(effectiveHolodeckUrl)}`"
                : "Holodeck backend LAN endpoint check is not applicable while backend mode is disabled");
            AppendCheck(report, UsesRealBrainProfile(orchestrators.FirstOrDefault(), runtimeConfig), "Brain module/profile is set to a real LLM path for real-device runs");

            AppendSection(report, "Packages");
            AppendPackageCheck(report, "com.unity.inputsystem", "Input System");
            AppendPackageCheck(report, "com.unity.ugui", "Unity UI");
            AppendPackageCheck(report, "com.unity.xr.interaction.toolkit", "XR Interaction Toolkit");
            AppendPackageCheck(report, "com.unity.xr.openxr", "OpenXR Plugin");
            AppendPackageCheck(report, "com.unity.xr.picoxr", "PICO Unity Integration SDK / PICO XR SDK");
            AppendPackageCheck(report, "com.gilzoide.sqlite-net", "SQLite-net history storage");

            AppendSection(report, "OpenXR");
            AppendCheck(report, HasAnyAndroidOpenXRInteractionProfileEnabled(), "At least one Android OpenXR interaction profile is enabled");
            AppendCheck(report, HasAndroidOpenXRControllerProfileEnabled(), "Android OpenXR has a controller interaction profile enabled");

            AppendSection(report, "PICO");
            AppendCheck(report, IsAndroidDefineEnabled(PicoOpenXrDefine), "`PICO_OPENXR_SDK` define is set for Android");
            AppendCheck(report, HasAndroidXRLoader(OpenXrLoaderTypeName), "Android XR loader uses OpenXR");
            AppendCheck(report, HasAndroidXRAutoStartEnabled(), "Android XR initializes and runs on startup");
            AppendCheck(report, AreRequiredPicoOpenXRFeaturesRegistered(), "Required PICO features are registered in Android OpenXR settings");
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
            report.AppendLine("- Start `Server/gateway-launcher/scenetalk_gateway_launcher.py` for USB-first PICO runs. Keep LAN gateway fields non-loopback for automatic fallback; only the explicit USB Voice/LLM fields use `127.0.0.1`.");
            report.AppendLine("- If Holodeck backend is enabled, set its URL to a LAN-reachable service; otherwise keep backend disabled and use mock layout / panorama fallback.");
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

        private static bool AreRequiredPicoOpenXRFeaturesRegistered()
        {
            return FindAndroidOpenXRFeature(PicoXrSupportFeatureId) != null
                && FindAndroidOpenXRFeature(PicoOpenXrFeaturesFeatureId) != null
                && FindAndroidOpenXRFeature(Pico4ControllerFeatureId) != null
                && FindAndroidOpenXRFeature(Pico4UltraControllerFeatureId) != null;
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
                Debug.LogWarning($"OpenXR feature '{featureId}' was not registered for Android after refreshing features.");
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
            if (feature == null)
            {
                return string.Empty;
            }

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

        private static string ResolveEffectiveVoiceGatewayUrl(
            SceneTalkRuntimeConfig config,
            VoiceGatewayClient voiceGatewayClient)
        {
            if (config != null && config.HasVoiceGatewayBaseUrl)
            {
                return config.VoiceGatewayBaseUrl;
            }

            return voiceGatewayClient == null ? string.Empty : voiceGatewayClient.GatewayBaseUrl;
        }

        private static string ResolveEffectiveHolodeckUrl(
            SceneTalkRuntimeConfig config,
            HolodeckSceneService holodeckService)
        {
            if (config != null && config.HasHolodeckBackendUrl)
            {
                return config.HolodeckBackendUrl;
            }

            return holodeckService == null ? string.Empty : holodeckService.BackendUrl;
        }

        private static bool UsesRealBrainProfile(
            SceneTalkOrchestrator orchestrator,
            SceneTalkRuntimeConfig config)
        {
            if (config != null && config.BrainMode == SceneTalkBrainRuntimeMode.DirectRealLlm)
            {
                return true;
            }

            var brainModule = GetSerializedModule(orchestrator, "brainModule");
            return brainModule is RealLLMService;
        }

        private static MonoBehaviour GetSerializedModule(MonoBehaviour target, string propertyName)
        {
            if (target == null)
            {
                return null;
            }

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            return property == null ? null : property.objectReferenceValue as MonoBehaviour;
        }

        private static string DisplayEndpoint(string endpoint)
        {
            return string.IsNullOrWhiteSpace(endpoint) ? "<empty>" : endpoint;
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

        private static bool HasMissingScriptsInLoadedScene()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasRequiredSceneReferences(
            SceneTalkRuntimeConfigApplier[] configAppliers,
            ExperimentConditionManager[] experimentManagers)
        {
            if (configAppliers.Length == 0 || experimentManagers.Length != 1 || experimentManagers[0].ExperimentProtocol == null)
            {
                return false;
            }

            var resolver = FindAll<AvatarPresetResolver>().FirstOrDefault();
            if (resolver == null)
            {
                return false;
            }

            var resolverObject = new SerializedObject(resolver);
            return resolverObject.FindProperty("catalog")?.objectReferenceValue != null;
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
