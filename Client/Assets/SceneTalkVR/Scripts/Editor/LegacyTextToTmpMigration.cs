using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SceneTalkVR.EditorTools
{
    internal static class LegacyTextToTmpMigration
    {
        private const string MenuPath = "SceneTalkVR/Maintenance/Migrate Sample Scene Legacy Text To TMP";
        private const string ValidateMenuPath = "SceneTalkVR/Maintenance/Validate Sample Scene TMP Migration";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string DefaultFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string EssentialResourcesPackageName = "TMP Essential Resources.unitypackage";
        private const string PendingMigrationKey = "SceneTalkVR.LegacyTextToTmpMigration.Pending";

        private static bool importInProgress;

        [InitializeOnLoadMethod]
        private static void ResumePendingMigrationAfterReload()
        {
            if (SessionState.GetBool(PendingMigrationKey, false))
            {
                EditorApplication.delayCall += ContinueAfterEssentialResourcesImport;
            }
        }

        [MenuItem(MenuPath)]
        private static void MigrateSampleScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SceneTalkVR] Exit Play Mode before migrating Legacy Text components.");
                return;
            }

            if (!HasEssentialResources())
            {
                ImportEssentialResourcesAndContinue();
                return;
            }

            RunMigration();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanMigrateSampleScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !importInProgress;
        }

        [MenuItem(ValidateMenuPath)]
        private static void ValidateSampleSceneMigration()
        {
            var scene = FindLoadedSampleScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning(
                    $"[SceneTalkVR] Open {SampleScenePath} before validating its TMP components in the Editor.");
                return;
            }

            var legacyCount = FindSceneComponents<Text>(scene).Count;
            var tmpCount = FindSceneComponents<TextMeshProUGUI>(scene).Count;
            var resourcesReady = HasEssentialResources();
            var message =
                $"[SceneTalkVR] TMP migration validation: legacy={legacyCount}, tmp={tmpCount}, " +
                $"essentialResources={(resourcesReady ? "ready" : "missing")}.";

            if (legacyCount == 0 && resourcesReady)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }

        private static void ImportEssentialResourcesAndContinue()
        {
            var packagePath = FindEssentialResourcesPackage();
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError(
                    "[SceneTalkVR] TMP Essential Resources package was not found in the installed com.unity.ugui package.");
                return;
            }

            importInProgress = true;
            SessionState.SetBool(PendingMigrationKey, true);
            SubscribeToPackageImportEvents();

            Debug.Log($"[SceneTalkVR] Importing TMP Essential Resources from {packagePath}.");
            try
            {
                AssetDatabase.ImportPackage(packagePath, false);
            }
            catch (Exception exception)
            {
                FinishPackageImport(false);
                Debug.LogException(exception);
            }
        }

        private static string FindEssentialResourcesPackage()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (packageInfo != null)
            {
                var packagePath = Path.Combine(
                    packageInfo.resolvedPath,
                    "Package Resources",
                    EssentialResourcesPackageName);
                if (File.Exists(packagePath))
                {
                    return packagePath;
                }
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var packageCache = string.IsNullOrEmpty(projectRoot)
                ? string.Empty
                : Path.Combine(projectRoot, "Library", "PackageCache");
            if (!Directory.Exists(packageCache))
            {
                return null;
            }

            var matches = Directory.GetFiles(packageCache, EssentialResourcesPackageName, SearchOption.AllDirectories);
            return matches.Length > 0 ? matches[0] : null;
        }

        private static void SubscribeToPackageImportEvents()
        {
            AssetDatabase.importPackageCompleted -= OnPackageImportCompleted;
            AssetDatabase.importPackageCancelled -= OnPackageImportCancelled;
            AssetDatabase.importPackageFailed -= OnPackageImportFailed;
            AssetDatabase.importPackageCompleted += OnPackageImportCompleted;
            AssetDatabase.importPackageCancelled += OnPackageImportCancelled;
            AssetDatabase.importPackageFailed += OnPackageImportFailed;
        }

        private static void UnsubscribeFromPackageImportEvents()
        {
            AssetDatabase.importPackageCompleted -= OnPackageImportCompleted;
            AssetDatabase.importPackageCancelled -= OnPackageImportCancelled;
            AssetDatabase.importPackageFailed -= OnPackageImportFailed;
        }

        private static void OnPackageImportCompleted(string packageName)
        {
            if (!importInProgress && !SessionState.GetBool(PendingMigrationKey, false))
            {
                return;
            }

            UnsubscribeFromPackageImportEvents();
            importInProgress = false;
            EditorApplication.delayCall += ContinueAfterEssentialResourcesImport;
        }

        private static void OnPackageImportCancelled(string packageName)
        {
            FinishPackageImport(false);
            Debug.LogError($"[SceneTalkVR] TMP Essential Resources import was cancelled: {packageName}.");
        }

        private static void OnPackageImportFailed(string packageName, string errorMessage)
        {
            FinishPackageImport(false);
            Debug.LogError(
                $"[SceneTalkVR] TMP Essential Resources import failed: {packageName}. {errorMessage}");
        }

        private static void FinishPackageImport(bool keepPendingMigration)
        {
            UnsubscribeFromPackageImportEvents();
            importInProgress = false;
            if (!keepPendingMigration)
            {
                SessionState.EraseBool(PendingMigrationKey);
            }
        }

        private static void ContinueAfterEssentialResourcesImport()
        {
            if (!SessionState.GetBool(PendingMigrationKey, false))
            {
                return;
            }

            if (!HasEssentialResources())
            {
                FinishPackageImport(false);
                Debug.LogError(
                    "[SceneTalkVR] TMP Essential Resources import completed, but TMP Settings or LiberationSans SDF is missing.");
                return;
            }

            FinishPackageImport(false);
            RunMigration();
        }

        private static bool HasEssentialResources()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath) != null &&
                   AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontAssetPath) != null;
        }

        private static void RunMigration()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontAssetPath);
            if (fontAsset == null)
            {
                Debug.LogError($"[SceneTalkVR] Required SDF font asset is missing: {DefaultFontAssetPath}.");
                return;
            }

            var scene = GetOrOpenSampleScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var legacyTexts = FindSceneComponents<Text>(scene);
            if (legacyTexts.Count == 0)
            {
                Debug.Log($"[SceneTalkVR] {SampleScenePath} already contains no Legacy Text components.");
                return;
            }

            var legacyIds = new HashSet<int>();
            foreach (var legacyText in legacyTexts)
            {
                legacyIds.Add(legacyText.GetInstanceID());
            }

            var referenceLocations = CaptureReferences(scene, legacyIds);
            var replacements = new Dictionary<int, TextMeshProUGUI>(legacyTexts.Count);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Migrate SampleScene Legacy Text To TMP");

            try
            {
                foreach (var legacyText in legacyTexts)
                {
                    var legacyId = legacyText.GetInstanceID();
                    var settings = LegacyTextSettings.Capture(legacyText);
                    var gameObject = legacyText.gameObject;

                    Undo.DestroyObjectImmediate(legacyText);
                    var tmpText = Undo.AddComponent<TextMeshProUGUI>(gameObject);
                    if (tmpText == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not add TextMeshProUGUI to {GetHierarchyPath(gameObject.transform)}.");
                    }

                    settings.ApplyTo(tmpText, fontAsset);
                    replacements.Add(legacyId, tmpText);
                    EditorUtility.SetDirty(tmpText);
                }

                var repairedReferenceCount = RepairReferences(referenceLocations, replacements);
                var remainingLegacyCount = FindSceneComponents<Text>(scene).Count;
                if (remainingLegacyCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Migration left {remainingLegacyCount} Legacy Text components in {SampleScenePath}.");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"Unity could not save {SampleScenePath}.");
                }

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log(
                    $"[SceneTalkVR] TMP migration complete: converted {replacements.Count} Legacy Text components, " +
                    $"repaired {repairedReferenceCount} serialized references, font={DefaultFontAssetPath}.");
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("[SceneTalkVR] TMP migration failed and scene changes were reverted.");
                Debug.LogException(exception);
            }
        }

        private static Scene GetOrOpenSampleScene()
        {
            var loadedScene = FindLoadedSampleScene();
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                return loadedScene;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) == null)
            {
                Debug.LogError($"[SceneTalkVR] Sample scene was not found: {SampleScenePath}.");
                return default;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[SceneTalkVR] TMP migration cancelled before opening SampleScene.");
                return default;
            }

            return EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        private static Scene FindLoadedSampleScene()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (string.Equals(scene.path, SampleScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }

            return default;
        }

        private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static List<ReferenceLocation> CaptureReferences(Scene scene, HashSet<int> legacyIds)
        {
            var references = new List<ReferenceLocation>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Text)
                    {
                        continue;
                    }

                    try
                    {
                        var serializedObject = new SerializedObject(component);
                        var property = serializedObject.GetIterator();
                        if (!property.Next(true))
                        {
                            continue;
                        }

                        do
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference)
                            {
                                continue;
                            }

                            var referencedInstanceId = property.objectReferenceInstanceIDValue;
                            if (legacyIds.Contains(referencedInstanceId))
                            {
                                references.Add(new ReferenceLocation(
                                    component,
                                    property.propertyPath,
                                    referencedInstanceId));
                            }
                        }
                        while (property.Next(true));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            $"[SceneTalkVR] Could not inspect serialized references on {component.GetType().Name}: " +
                            exception.Message,
                            component);
                    }
                }
            }

            return references;
        }

        private static int RepairReferences(
            IReadOnlyList<ReferenceLocation> referenceLocations,
            IReadOnlyDictionary<int, TextMeshProUGUI> replacements)
        {
            var repairedCount = 0;
            foreach (var location in referenceLocations)
            {
                if (location.Target == null ||
                    !replacements.TryGetValue(location.LegacyInstanceId, out var replacement))
                {
                    continue;
                }

                var serializedObject = new SerializedObject(location.Target);
                serializedObject.UpdateIfRequiredOrScript();
                var property = serializedObject.FindProperty(location.PropertyPath);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    Debug.LogWarning(
                        $"[SceneTalkVR] Could not restore serialized reference {location.PropertyPath} on " +
                        $"{location.Target.GetType().Name}.",
                        location.Target);
                    continue;
                }

                Undo.RecordObject(location.Target, "Restore TMP text reference");
                property.objectReferenceValue = replacement;
                if (serializedObject.ApplyModifiedProperties())
                {
                    repairedCount++;
                }
            }

            return repairedCount;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private readonly struct ReferenceLocation
        {
            public ReferenceLocation(Object target, string propertyPath, int legacyInstanceId)
            {
                Target = target;
                PropertyPath = propertyPath;
                LegacyInstanceId = legacyInstanceId;
            }

            public Object Target { get; }

            public string PropertyPath { get; }

            public int LegacyInstanceId { get; }
        }

        private readonly struct LegacyTextSettings
        {
            private readonly string text;
            private readonly Color color;
            private readonly bool enabled;
            private readonly bool raycastTarget;
            private readonly Vector4 raycastPadding;
            private readonly bool maskable;
            private readonly int fontSize;
            private readonly FontStyle fontStyle;
            private readonly bool bestFit;
            private readonly int minimumFontSize;
            private readonly int maximumFontSize;
            private readonly TextAnchor alignment;
            private readonly bool richText;
            private readonly HorizontalWrapMode horizontalOverflow;
            private readonly VerticalWrapMode verticalOverflow;
            private readonly float lineSpacing;

            private LegacyTextSettings(Text legacyText)
            {
                text = legacyText.text;
                color = legacyText.color;
                enabled = legacyText.enabled;
                raycastTarget = legacyText.raycastTarget;
                raycastPadding = legacyText.raycastPadding;
                maskable = legacyText.maskable;
                fontSize = legacyText.fontSize;
                fontStyle = legacyText.fontStyle;
                bestFit = legacyText.resizeTextForBestFit;
                minimumFontSize = legacyText.resizeTextMinSize;
                maximumFontSize = legacyText.resizeTextMaxSize;
                alignment = legacyText.alignment;
                richText = legacyText.supportRichText;
                horizontalOverflow = legacyText.horizontalOverflow;
                verticalOverflow = legacyText.verticalOverflow;
                lineSpacing = legacyText.lineSpacing;
            }

            public static LegacyTextSettings Capture(Text legacyText)
            {
                return new LegacyTextSettings(legacyText);
            }

            public void ApplyTo(TextMeshProUGUI tmpText, TMP_FontAsset fontAsset)
            {
                tmpText.font = fontAsset;
                tmpText.text = text;
                tmpText.color = color;
                tmpText.enabled = enabled;
                tmpText.raycastTarget = raycastTarget;
                tmpText.raycastPadding = raycastPadding;
                tmpText.maskable = maskable;
                tmpText.fontSize = fontSize;
                tmpText.fontStyle = ToTmpFontStyle(fontStyle);
                tmpText.enableAutoSizing = bestFit;
                tmpText.fontSizeMin = minimumFontSize;
                tmpText.fontSizeMax = maximumFontSize;
                tmpText.alignment = ToTmpAlignment(alignment);
                tmpText.richText = richText;
                tmpText.textWrappingMode = horizontalOverflow == HorizontalWrapMode.Wrap
                    ? TextWrappingModes.Normal
                    : TextWrappingModes.NoWrap;
                tmpText.overflowMode = verticalOverflow == VerticalWrapMode.Overflow
                    ? TextOverflowModes.Overflow
                    : TextOverflowModes.Truncate;
                tmpText.lineSpacing = (lineSpacing - 1f) * fontSize;
            }

            private static FontStyles ToTmpFontStyle(FontStyle style)
            {
                return style switch
                {
                    FontStyle.Bold => FontStyles.Bold,
                    FontStyle.Italic => FontStyles.Italic,
                    FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
                    _ => FontStyles.Normal
                };
            }

            private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
            {
                return anchor switch
                {
                    TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                    TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                    TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                    TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                    TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                    TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                    TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                    TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                    TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                    _ => TextAlignmentOptions.Center
                };
            }
        }
    }
}
