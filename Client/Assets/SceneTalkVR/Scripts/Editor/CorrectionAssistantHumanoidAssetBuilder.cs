using SceneTalkVR.AvatarSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class CorrectionAssistantHumanoidAssetBuilder
    {
        private const string AvatarRoot = "Assets/SceneTalkVR/Avatar";
        private const string ModelFolder = AvatarRoot + "/Models/Humanoid/QuaterniusCorrectionAssistantWoman";
        private const string ModelPath = ModelFolder + "/correction_assistant_woman.fbx";
        private const string NativeIdleFolder = AvatarRoot + "/Animations/Common/NativeIdle";
        private const string NativeIdlePath = NativeIdleFolder + "/correction_assistant_woman_idle_neutral_loop.anim";
        private const string CommonControllerPath = AvatarRoot + "/Animations/Common/AvatarCommonHumanoid.controller";
        private const string OverrideFolder = AvatarRoot + "/Animations/Common/Overrides";
        private const string OverrideControllerPath = OverrideFolder + "/correction_assistant_woman.overrideController";
        private const string PrefabFolder = AvatarRoot + "/Prefabs/Humanoid";
        private const string PrefabPath = PrefabFolder + "/correction_assistant_woman.prefab";
        private const float TargetHeightMeters = 1.68f;

        [MenuItem("SceneTalkVR/Avatar/Build Correction Assistant Humanoid", false, 42)]
        public static void BuildCorrectionAssistantHumanoid()
        {
            EnsureFolders();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            AvatarHumanoidP1AssetBuilder.ConfigureHumanoidImporter(ModelPath);

            var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (sourceModel == null)
            {
                Debug.LogError($"[SceneTalkVR] Correction assistant source model not found at {ModelPath}.");
                return;
            }

            var idleClip = AvatarHumanoidP1AssetBuilder.CreateOrUpdateNativeIdleClip(
                ModelPath,
                NativeIdlePath);
            var commonController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CommonControllerPath);
            if (idleClip == null || commonController == null)
            {
                Debug.LogError("[SceneTalkVR] Correction assistant build stopped because the shared controller or native Idle is missing.");
                return;
            }

            var overrideController = AvatarHumanoidP1AssetBuilder.CreateOrUpdateCharacterOverrideController(
                OverrideControllerPath,
                commonController,
                ModelPath,
                idleClip);
            if (overrideController == null)
            {
                Debug.LogError("[SceneTalkVR] Correction assistant Animator override could not be created.");
                return;
            }

            var prefab = AvatarHumanoidP1AssetBuilder.CreateHumanoidPrefab(
                sourceModel,
                "correction_assistant_woman",
                "QuaterniusCorrectionAssistantWoman",
                PrefabPath,
                TargetHeightMeters,
                180f,
                overrideController);
            if (!ValidatePrefab(prefab))
            {
                return;
            }

            AssignOpenSceneReferences(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log($"[SceneTalkVR] Built correction assistant Humanoid prefab at {PrefabPath}.");
        }

        private static void EnsureFolders()
        {
            AvatarHumanoidP1AssetBuilder.EnsureFolder(AvatarRoot + "/Models/Humanoid", "QuaterniusCorrectionAssistantWoman");
            AvatarHumanoidP1AssetBuilder.EnsureFolder(AvatarRoot + "/Animations/Common", "NativeIdle");
            AvatarHumanoidP1AssetBuilder.EnsureFolder(AvatarRoot + "/Animations/Common", "Overrides");
            AvatarHumanoidP1AssetBuilder.EnsureFolder(AvatarRoot + "/Prefabs", "Humanoid");
        }

        private static bool ValidatePrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("[SceneTalkVR] Correction assistant Humanoid prefab creation failed.");
                return false;
            }

            var animator = prefab.GetComponentInChildren<Animator>(true);
            if (animator == null
                || animator.avatar == null
                || !animator.avatar.isValid
                || !animator.avatar.isHuman
                || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("[SceneTalkVR] Correction assistant prefab does not contain a valid configured Humanoid Animator.");
                return false;
            }

            return true;
        }

        private static void AssignOpenSceneReferences(GameObject prefab)
        {
            var presenters = Object.FindObjectsByType<CorrectionAgentPresenter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < presenters.Length; i++)
            {
                var presenter = presenters[i];
                if (presenter == null || !presenter.gameObject.scene.IsValid())
                {
                    continue;
                }

                var serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("humanoidPrefab").objectReferenceValue = prefab;
                var anchorProperty = serializedPresenter.FindProperty("humanoidPlacementAnchor");
                if (anchorProperty.objectReferenceValue == null)
                {
                    anchorProperty.objectReferenceValue = FindInScene(presenter.gameObject.scene, "AvatarRoot");
                }

                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(presenter);
                EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
                if (!string.IsNullOrWhiteSpace(presenter.gameObject.scene.path))
                {
                    EditorSceneManager.SaveScene(presenter.gameObject.scene);
                }
            }
        }

        private static Transform FindInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var match = FindRecursive(roots[i].transform, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindRecursive(Transform current, string objectName)
        {
            if (current.name == objectName)
            {
                return current;
            }

            for (var i = 0; i < current.childCount; i++)
            {
                var match = FindRecursive(current.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
