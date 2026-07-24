using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public static class ImportVisitorCenterLayout
{
    private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SourceScenePath = "Assets/ThirdParty/EnvironmentPacks/VisitorCenterOfficeSet/Scenes/Demo 1 - Office Set 1.unity";

    [MenuItem("SceneTalkVR/Import Visitor Center Demo 1 Layout")]
    public static void ImportLayout()
    {
        var targetScene = SceneManager.GetSceneByPath(TargetScenePath);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            Debug.LogError($"Target scene is not loaded: {TargetScenePath}");
            return;
        }

        var targetRoot = FindInScene(targetScene, "SceneContentRoot/TourOfficeScene");
        if (targetRoot == null)
        {
            Debug.LogError("Could not find SceneContentRoot/TourOfficeScene in the target scene.");
            return;
        }

        if (targetRoot.transform.childCount > 0)
        {
            Debug.LogError("TourOfficeScene already contains children; import was skipped to avoid duplicates.");
            return;
        }

        var sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
        var layoutRoot = sourceScene.GetRootGameObjects().FirstOrDefault(go => go.name == "Floor 0");
        if (layoutRoot == null)
        {
            EditorSceneManager.CloseScene(sourceScene, true);
            Debug.LogError("Could not find the Demo 1 layout root 'Floor 0'.");
            return;
        }

        SceneManager.MoveGameObjectToScene(layoutRoot, targetScene);
        layoutRoot.transform.SetParent(targetRoot.transform, false);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        EditorSceneManager.CloseScene(sourceScene, true);

        Debug.Log($"Imported Visitor Center Demo 1 layout into {TargetScenePath} under {targetRoot.name}. Child count: {layoutRoot.transform.childCount}.");
    }

    private static GameObject FindInScene(Scene scene, string path)
    {
        var parts = path.Split('/');
        var current = scene.GetRootGameObjects().FirstOrDefault(go => go.name == parts[0]);
        for (var i = 1; current != null && i < parts.Length; i++)
        {
            var child = current.transform.Find(parts[i]);
            current = child == null ? null : child.gameObject;
        }
        return current;
    }
}