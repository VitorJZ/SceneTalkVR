using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FixThirdPartyMaterials
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string[] Roots =
    {
        "Assets/ThirdParty/EnvironmentPacks/VisitorCenterOfficeSet",
        "Assets/ThirdParty/EnvironmentPacks/VNBHomeSet",
        "Assets/ThirdParty/EnvironmentPacks/ModularGym",
        "Assets/ThirdParty/EnvironmentPacks/ArtDecoHotelLobby"
    };

    private static readonly string[] SceneRootNames =
    {
        "TourOfficeScene",
        "FurnitureStoreScene",
        "GymScene",
        "HotelLobbyScene"
    };

    [MenuItem("SceneTalkVR/Repair Third-Party URP Materials")]
    public static void Repair()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("Could not find Universal Render Pipeline/Lit shader.");
            return;
        }

        var extracted = ExtractAndRemapEmbeddedMaterials(urpLit);
        var repaired = RepairExternalMaterials(urpLit, out var alreadyCompatible, out var missing);
        var restoredSlots = RestoreSceneMaterialReferences(urpLit);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Third-party URP material repair complete. Extracted/remapped: {extracted}, " +
            $"repaired: {repaired}, already compatible: {alreadyCompatible}, missing: {missing}, " +
            $"restored scene slots: {restoredSlots}.");
    }

    [MenuItem("SceneTalkVR/Validate Third-Party Scene Assets")]
    public static void ValidateSceneAssets()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"Validation requires the scene to be loaded: {ScenePath}");
            return;
        }

        var contentRoot = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "SceneContentRoot");
        if (contentRoot == null)
        {
            Debug.LogError("Validation failed: SceneContentRoot was not found.");
            return;
        }

        var problems = new List<string>();
        foreach (var rootName in SceneRootNames)
        {
            var root = contentRoot.transform.Find(rootName);
            if (root == null)
            {
                problems.Add($"Missing scene root: {rootName}");
                continue;
            }

            LogSceneStats(root, problems);
        }

        foreach (var dependency in AssetDatabase.GetDependencies(ScenePath, true))
        {
            if (!dependency.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.LoadMainAssetAtPath(dependency) != null)
                continue;

            problems.Add($"Missing scene dependency: {dependency}");
        }

        if (problems.Count == 0)
        {
            Debug.Log("Third-party scene validation passed: all four roots and their renderer materials are valid.");
            return;
        }

        var distinctProblems = problems.Distinct().ToArray();
        Debug.LogError(
            $"Third-party scene validation failed with {distinctProblems.Length} problem(s): " +
            string.Join(" | ", distinctProblems.Take(20)));
    }

    private static int ExtractAndRemapEmbeddedMaterials(Shader urpLit)
    {
        var remapped = 0;
        var modelGuids = Roots
            .SelectMany(root => AssetDatabase.FindAssets("t:Model", new[] { root }))
            .Distinct();

        foreach (var guid in modelGuids)
        {
            var modelPath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
                continue;

            var embeddedMaterials = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray();
            if (embeddedMaterials.Length == 0)
                continue;

            var materialFolder = GetMaterialFolder(modelPath);
            EnsureFolder(materialFolder);
            var importerChanged = false;
            var externalMap = importer.GetExternalObjectMap();

            foreach (var source in embeddedMaterials)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier
                {
                    type = typeof(Material),
                    name = source.name
                };

                if (externalMap.TryGetValue(identifier, out var mappedObject)
                    && mappedObject is Material mappedMaterial
                    && mappedMaterial.shader != null
                    && mappedMaterial.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = SanitizeFileName(source.name) + ".mat";
                var materialPath = AssetDatabase.GenerateUniqueAssetPath($"{materialFolder}/{fileName}");
                var target = new Material(urpLit) { name = source.name };
                CopyMaterialProperties(source, target);
                AssetDatabase.CreateAsset(target, materialPath);
                importer.AddRemap(identifier, target);
                importerChanged = true;
                remapped++;
            }

            if (importerChanged)
                importer.SaveAndReimport();
        }

        return remapped;
    }

    private static int RepairExternalMaterials(Shader urpLit, out int alreadyCompatible, out int missing)
    {
        var repaired = 0;
        alreadyCompatible = 0;
        missing = 0;
        var materialGuids = new HashSet<string>();

        foreach (var root in Roots)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { root }))
                materialGuids.Add(guid);
        }

        foreach (var guid in materialGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                continue;

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                missing++;
                continue;
            }

            if (material.shader == urpLit
                || (material.shader != null
                    && material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)))
            {
                alreadyCompatible++;
                continue;
            }

            var converted = new Material(urpLit) { name = material.name };
            CopyMaterialProperties(material, converted);
            material.shader = urpLit;
            material.CopyPropertiesFromMaterial(converted);
            UnityEngine.Object.DestroyImmediate(converted);
            EditorUtility.SetDirty(material);
            repaired++;
        }

        return repaired;
    }

    private static int RestoreSceneMaterialReferences(Shader urpLit)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning($"Skipped scene material restoration because the scene is not loaded: {ScenePath}");
            return 0;
        }

        var sourceMaterialsByMesh = BuildSourceMaterialMap();
        var fallback = GetOrCreateFallbackMaterial(urpLit);
        var restored = 0;

        foreach (var renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
        {
            var mesh = GetRendererMesh(renderer);
            if (mesh == null)
                continue;

            var current = renderer.sharedMaterials;
            if (current.All(IsUsableSceneMaterial))
                continue;

            sourceMaterialsByMesh.TryGetValue(mesh, out var sourceMaterials);
            var slotCount = Math.Max(mesh.subMeshCount, current.Length);
            var updated = new Material[slotCount];
            var rendererChanged = false;

            for (var slot = 0; slot < slotCount; slot++)
            {
                var material = slot < current.Length && IsUsableSceneMaterial(current[slot]) ? current[slot] : null;
                if (material == null && sourceMaterials != null && slot < sourceMaterials.Length
                    && IsUsableSceneMaterial(sourceMaterials[slot]))
                    material = sourceMaterials[slot];
                if (material == null && sourceMaterials != null)
                    material = sourceMaterials.FirstOrDefault(IsUsableSceneMaterial);
                if (material == null)
                    material = fallback;

                updated[slot] = material;
                if (slot >= current.Length || current[slot] != material)
                {
                    rendererChanged = true;
                    restored++;
                }
            }

            if (rendererChanged)
                renderer.sharedMaterials = updated;
        }

        if (restored > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        return restored;
    }

    private static Dictionary<Mesh, Material[]> BuildSourceMaterialMap()
    {
        var result = new Dictionary<Mesh, Material[]>();
        var modelGuids = Roots
            .SelectMany(root => AssetDatabase.FindAssets("t:Model", new[] { root }))
            .Distinct();

        foreach (var guid in modelGuids)
        {
            var modelPath = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                continue;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = GetRendererMesh(renderer);
                if (mesh != null && !result.ContainsKey(mesh))
                    result.Add(mesh, renderer.sharedMaterials);
            }
        }

        return result;
    }

    private static Material GetOrCreateFallbackMaterial(Shader urpLit)
    {
        const string folder = "Assets/ThirdParty/EnvironmentPacks/Generated";
        const string path = folder + "/FallbackURP.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        EnsureFolder(folder);
        material = new Material(urpLit) { name = "FallbackURP" };
        material.SetColor("_BaseColor", new Color(0.65f, 0.65f, 0.65f, 1f));
        material.SetFloat("_Smoothness", 0.25f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void CopyMaterialProperties(Material source, Material target)
    {
        var mainTexture = GetTexture(source, "_BaseMap", "_MainTex");
        var mainScale = GetTextureScale(source, "_BaseMap", "_MainTex");
        var mainOffset = GetTextureOffset(source, "_BaseMap", "_MainTex");
        var color = GetColor(source, Color.white, "_BaseColor", "_Color");

        target.SetColor("_BaseColor", color);
        target.SetColor("_Color", color);
        if (mainTexture != null)
        {
            target.SetTexture("_BaseMap", mainTexture);
            target.SetTexture("_MainTex", mainTexture);
            target.SetTextureScale("_BaseMap", mainScale);
            target.SetTextureOffset("_BaseMap", mainOffset);
        }

        CopyTexture(source, target, "_BumpMap", "_NORMALMAP");
        CopyFloat(source, target, "_BumpScale", 1f);
        CopyTexture(source, target, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP");
        CopyFloat(source, target, "_Metallic", 0f);
        target.SetFloat("_Smoothness", GetFloat(source, 0.5f, "_Smoothness", "_Glossiness"));
        CopyTexture(source, target, "_OcclusionMap", "_OCCLUSIONMAP");
        CopyFloat(source, target, "_OcclusionStrength", 1f);

        var emissionMap = GetTexture(source, "_EmissionMap");
        var emissionColor = GetColor(source, Color.black, "_EmissionColor");
        if (emissionMap != null)
            target.SetTexture("_EmissionMap", emissionMap);
        target.SetColor("_EmissionColor", emissionColor);
        if (emissionMap != null || emissionColor.maxColorComponent > 0f)
            target.EnableKeyword("_EMISSION");

        ConfigureSurface(source, target);
    }

    private static void ConfigureSurface(Material source, Material target)
    {
        var standardMode = source.HasProperty("_Mode") ? Mathf.RoundToInt(source.GetFloat("_Mode")) : 0;
        var alphaClip = source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f;
        var transparent = source.HasProperty("_Surface")
            ? source.GetFloat("_Surface") > 0.5f
            : standardMode >= 2;

        if (standardMode == 1 || alphaClip)
        {
            target.SetFloat("_AlphaClip", 1f);
            target.SetFloat("_Cutoff", GetFloat(source, 0.5f, "_Cutoff"));
            target.EnableKeyword("_ALPHATEST_ON");
            target.SetOverrideTag("RenderType", "TransparentCutout");
            target.renderQueue = (int)RenderQueue.AlphaTest;
            return;
        }

        if (!transparent)
            return;

        target.SetFloat("_Surface", 1f);
        target.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        target.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        target.SetFloat("_ZWrite", 0f);
        target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        target.SetOverrideTag("RenderType", "Transparent");
        target.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void LogSceneStats(Transform root, ICollection<string> problems)
    {
        var transforms = root.GetComponentsInChildren<Transform>(true);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var colliders = root.GetComponentsInChildren<Collider>(true);
        var materials = new HashSet<Material>();
        var meshes = new HashSet<Mesh>();
        long vertices = 0;
        long triangles = 0;

        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    problems.Add($"{root.name}: {GetPath(renderer.transform)} has a missing material.");
                    continue;
                }

                materials.Add(material);
                if (!IsUsableSceneMaterial(material))
                    problems.Add($"{root.name}: material '{material.name}' has an invalid shader.");
            }

            var mesh = GetRendererMesh(renderer);

            if (mesh == null)
                continue;

            meshes.Add(mesh);
            vertices += mesh.vertexCount;
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                triangles += (long)mesh.GetIndexCount(subMesh) / 3;
        }

        Debug.Log(
            $"Scene stats [{root.name}]: gameObjects={transforms.Length}, renderers={renderers.Length}, " +
            $"meshes={meshes.Count}, materials={materials.Count}, colliders={colliders.Length}, " +
            $"vertices={vertices}, triangles={triangles}.");
    }

    private static string GetMaterialFolder(string modelPath)
    {
        var root = Roots.First(path => modelPath.StartsWith(path + "/", StringComparison.Ordinal));
        return root + "/Materials";
    }

    private static Mesh GetRendererMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned)
            return skinned.sharedMesh;
        return renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
    }

    private static bool IsUsableSceneMaterial(Material material)
    {
        return material != null
            && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material))
            && material.shader != null
            && material.shader.isSupported
            && material.shader.name != "Hidden/InternalErrorShader";
    }

    private static void EnsureFolder(string folderPath)
    {
        var parts = folderPath.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "Material" : name;
    }

    private static string GetPath(Transform transform)
    {
        var parts = new Stack<string>();
        while (transform != null)
        {
            parts.Push(transform.name);
            transform = transform.parent;
        }
        return string.Join("/", parts);
    }

    private static Texture GetTexture(Material material, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (material.HasProperty(property))
                return material.GetTexture(property);
        }
        return null;
    }

    private static Vector2 GetTextureScale(Material material, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (material.HasProperty(property))
                return material.GetTextureScale(property);
        }
        return Vector2.one;
    }

    private static Vector2 GetTextureOffset(Material material, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (material.HasProperty(property))
                return material.GetTextureOffset(property);
        }
        return Vector2.zero;
    }

    private static Color GetColor(Material material, Color fallback, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (material.HasProperty(property))
                return material.GetColor(property);
        }
        return fallback;
    }

    private static float GetFloat(Material material, float fallback, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (material.HasProperty(property))
                return material.GetFloat(property);
        }
        return fallback;
    }

    private static void CopyTexture(Material source, Material target, string property, string keyword)
    {
        var texture = GetTexture(source, property);
        if (texture == null)
            return;

        target.SetTexture(property, texture);
        target.EnableKeyword(keyword);
    }

    private static void CopyFloat(Material source, Material target, string property, float fallback)
    {
        target.SetFloat(property, GetFloat(source, fallback, property));
    }
}
