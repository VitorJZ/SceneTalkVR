using SceneTalkVR.AvatarSystem;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    public static class AvatarPlaceholderAssetBuilder
    {
        private const string AvatarRoot = "Assets/SceneTalkVR/Avatar";
        private const string MaterialFolder = AvatarRoot + "/Materials";
        private const string PrefabFolder = AvatarRoot + "/Prefabs/Placeholder";
        private const string CatalogFolder = AvatarRoot + "/Catalogs";
        private const string CatalogPath = CatalogFolder + "/AvatarCatalog.asset";

        [MenuItem("SceneTalkVR/Avatar/Generate Placeholder Avatars", false, 40)]
        public static void Generate()
        {
            EnsureFolders();

            var skin = CreateMaterial("Avatar_Skin_Warm", new Color(0.86f, 0.62f, 0.44f, 1f));
            var hairBlack = CreateMaterial("Avatar_Hair_Black", new Color(0.04f, 0.035f, 0.03f, 1f));
            var baristaGreen = CreateMaterial("Avatar_Barista_Green", new Color(0.05f, 0.42f, 0.25f, 1f));
            var teacherBlue = CreateMaterial("Avatar_Teacher_Blue", new Color(0.16f, 0.28f, 0.58f, 1f));
            var policeNavy = CreateMaterial("Avatar_Police_Navy", new Color(0.03f, 0.08f, 0.22f, 1f));
            var white = CreateMaterial("Avatar_White", new Color(0.9f, 0.92f, 0.86f, 1f));
            var black = CreateMaterial("Avatar_Black", new Color(0.02f, 0.02f, 0.025f, 1f));
            var gold = CreateMaterial("Avatar_Gold", new Color(1f, 0.72f, 0.16f, 1f));

            var baristaPrefab = CreateBaristaPrefab(skin, hairBlack, baristaGreen, white, black);
            var teacherPrefab = CreateTeacherPrefab(skin, hairBlack, teacherBlue, white, black, gold);
            var policePrefab = CreatePolicePrefab(skin, hairBlack, policeNavy, black, gold);

            CreateOrUpdateCatalog(baristaPrefab, teacherPrefab, policePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SceneTalkVR] Generated placeholder Avatar prefabs and catalog at {CatalogPath}.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/SceneTalkVR", "Avatar");
            EnsureFolder(AvatarRoot, "Materials");
            EnsureFolder(AvatarRoot, "Prefabs");
            EnsureFolder(AvatarRoot + "/Prefabs", "Placeholder");
            EnsureFolder(AvatarRoot, "Catalogs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var fullPath = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateBaristaPrefab(Material skin, Material hair, Material outfit, Material white, Material black)
        {
            var root = CreateBaseAvatar("barista_default", skin, hair, outfit, black);
            AddPart(root.transform, "GreenApron", PrimitiveType.Cube, new Vector3(0f, 1.08f, -0.27f), new Vector3(0.42f, 0.55f, 0.035f), outfit);
            AddPart(root.transform, "ApronPatch", PrimitiveType.Cube, new Vector3(0f, 1.23f, -0.31f), new Vector3(0.18f, 0.12f, 0.025f), white);
            AddPart(root.transform, "GlassesL", PrimitiveType.Cube, new Vector3(-0.11f, 1.88f, -0.29f), new Vector3(0.12f, 0.035f, 0.018f), black);
            AddPart(root.transform, "GlassesR", PrimitiveType.Cube, new Vector3(0.11f, 1.88f, -0.29f), new Vector3(0.12f, 0.035f, 0.018f), black);
            return SavePrefab(root);
        }

        private static GameObject CreateTeacherPrefab(Material skin, Material hair, Material outfit, Material white, Material black, Material gold)
        {
            var root = CreateBaseAvatar("teacher_default", skin, hair, outfit, black);
            AddPart(root.transform, "WhiteShirt", PrimitiveType.Cube, new Vector3(0f, 1.2f, -0.27f), new Vector3(0.32f, 0.34f, 0.035f), white);
            AddPart(root.transform, "Book", PrimitiveType.Cube, new Vector3(0.48f, 1.03f, -0.18f), new Vector3(0.22f, 0.3f, 0.04f), white);
            AddPart(root.transform, "NameTag", PrimitiveType.Cube, new Vector3(-0.16f, 1.28f, -0.31f), new Vector3(0.12f, 0.06f, 0.025f), gold);
            return SavePrefab(root);
        }

        private static GameObject CreatePolicePrefab(Material skin, Material hair, Material outfit, Material black, Material gold)
        {
            var root = CreateBaseAvatar("police_default", skin, hair, outfit, black);
            AddPart(root.transform, "Cap", PrimitiveType.Cube, new Vector3(0f, 2.15f, 0f), new Vector3(0.42f, 0.1f, 0.36f), outfit);
            AddPart(root.transform, "CapBrim", PrimitiveType.Cube, new Vector3(0f, 2.11f, -0.28f), new Vector3(0.34f, 0.045f, 0.18f), outfit);
            AddPart(root.transform, "Badge", PrimitiveType.Cube, new Vector3(-0.14f, 1.3f, -0.31f), new Vector3(0.1f, 0.1f, 0.025f), gold);
            AddPart(root.transform, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.88f, -0.26f), new Vector3(0.5f, 0.06f, 0.035f), black);
            return SavePrefab(root);
        }

        private static GameObject CreateBaseAvatar(string key, Material skin, Material hair, Material outfit, Material black)
        {
            var root = new GameObject(key);
            AddPart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f), new Vector3(0.48f, 0.82f, 0.48f), outfit);
            AddPart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0f), new Vector3(0.34f, 0.34f, 0.34f), skin);
            AddPart(root.transform, "Hair", PrimitiveType.Sphere, new Vector3(0f, 2.04f, -0.02f), new Vector3(0.36f, 0.15f, 0.36f), hair);
            AddPart(root.transform, "LeftArm", PrimitiveType.Capsule, new Vector3(-0.42f, 1.15f, 0f), new Vector3(0.16f, 0.42f, 0.16f), outfit).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            AddPart(root.transform, "RightArm", PrimitiveType.Capsule, new Vector3(0.42f, 1.15f, 0f), new Vector3(0.16f, 0.42f, 0.16f), outfit).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            AddPart(root.transform, "LeftLeg", PrimitiveType.Capsule, new Vector3(-0.17f, 0.36f, 0f), new Vector3(0.15f, 0.45f, 0.15f), black);
            AddPart(root.transform, "RightLeg", PrimitiveType.Capsule, new Vector3(0.17f, 0.36f, 0f), new Vector3(0.15f, 0.45f, 0.15f), black);
            return root;
        }

        private static GameObject AddPart(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }

        private static GameObject SavePrefab(GameObject root)
        {
            var path = PrefabFolder + "/" + root.name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateOrUpdateCatalog(GameObject baristaPrefab, GameObject teacherPrefab, GameObject policePrefab)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.defaultAvatarKey = "teacher_default";
            catalog.presets = new[]
            {
                CreateEntry(
                    "barista_default",
                    "Placeholder Barista",
                    baristaPrefab,
                    new[] { "barista", "clerk" },
                    new[] { "coffee_shop", "restaurant" },
                    new[] { "barista" },
                    new[] { "green" },
                    new[] { "round_black_glasses", "glasses" },
                    new[] { "green_apron" },
                    30),
                CreateEntry(
                    "teacher_default",
                    "Placeholder Teacher",
                    teacherPrefab,
                    new[] { "teacher" },
                    new[] { "classroom", "school" },
                    new[] { "teacher" },
                    new[] { "blue", "white" },
                    new string[0],
                    new string[0],
                    20),
                CreateEntry(
                    "police_default",
                    "Placeholder Police Officer",
                    policePrefab,
                    new[] { "police", "officer" },
                    new[] { "airport", "street", "station" },
                    new[] { "police" },
                    new[] { "navy", "blue" },
                    new[] { "badge", "cap" },
                    new[] { "badge" },
                    20)
            };

            EditorUtility.SetDirty(catalog);
        }

        private static AvatarPresetEntry CreateEntry(
            string key,
            string displayName,
            GameObject prefab,
            string[] roles,
            string[] environments,
            string[] outfits,
            string[] colors,
            string[] accessories,
            string[] mustHave,
            int priority)
        {
            return new AvatarPresetEntry
            {
                key = key,
                displayName = displayName,
                priority = priority,
                prefab = prefab,
                roles = roles,
                environmentTags = environments,
                styleIds = new[] { "semi_realistic_v1", "placeholder_v1" },
                ageBuckets = new[] { "young_adult", "adult", "middle_aged" },
                bodyBuilds = new[] { "average" },
                outfitRoles = outfits,
                outfitColors = colors,
                accessoryTags = accessories,
                mustHaveTags = mustHave,
                qualityTier = "placeholder",
                mobileReady = true
            };
        }
    }
}
