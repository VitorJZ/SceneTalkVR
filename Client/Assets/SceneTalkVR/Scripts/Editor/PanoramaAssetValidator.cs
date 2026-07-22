using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SceneTalkVR.Core;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.EditorTools
{
    [Serializable]
    public sealed class PanoramaValidationRecord
    {
        public string resourceKey;
        public string assetPath;
        public bool exists;
        public bool resourcesLoadable;
        public int width;
        public int height;
        public float aspectRatio;
        public bool dimensionValid;
        public bool nativeEquirectangularProvenanceValid;
        public bool importerValid;
        public bool catalogReferenced;
        public string seamAssessment;
        public float seamMeanDifference;
        public long estimatedAndroidBytes;
        public string[] errors;
        public string[] warnings;
        public bool IsValid => exists && resourcesLoadable && dimensionValid && nativeEquirectangularProvenanceValid && importerValid && catalogReferenced && errors.Length == 0;
    }

    [Serializable]
    public sealed class PanoramaValidationReport
    {
        public string schemaVersion = "1.0";
        public string generatedAtUtc;
        public string result;
        public string generatorCapability;
        public PanoramaValidationRecord[] panoramas;
    }

    public static class PanoramaAssetValidator
    {
        public const string GeneratorCapability = "BLOCKED_GENERATOR_NOT_CAPABLE";
        public static readonly string[] RequiredResourceKeys =
        {
            "SceneTalkVR/Textures/hotel-lobby-360",
            "SceneTalkVR/Textures/furniture-store-360",
            "SceneTalkVR/Textures/gym-360",
            "SceneTalkVR/Textures/tourist-information-360",
            "SceneTalkVR/Textures/restaurant-360"
        };

        public static PanoramaValidationReport ValidateAll()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ExperimentTaskCatalog>("Assets/SceneTalkVR/ExperimentProtocol/ExperimentTaskCatalog.asset");
            var referenced = catalog == null ? new HashSet<string>() : new HashSet<string>(
                catalog.GetTasks(ExperimentTaskPhase.Formal).Concat(catalog.GetTasks(ExperimentTaskPhase.Pilot))
                    .Select(x => x.panoramaResourceKey), StringComparer.Ordinal);
            var records = RequiredResourceKeys.Select(x => Validate(x, referenced)).ToArray();
            return new PanoramaValidationReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("o"),
                result = records.All(x => x.IsValid) ? "PASS" : "FAIL",
                generatorCapability = GeneratorCapability,
                panoramas = records
            };
        }

        public static PanoramaValidationRecord Validate(string resourceKey, ISet<string> catalogKeys = null)
        {
            var errors = new List<string>(); var warnings = new List<string>();
            var relative = "Assets/Resources/" + resourceKey + ".png";
            var full = Path.GetFullPath(relative);
            var record = new PanoramaValidationRecord { resourceKey = resourceKey, assetPath = relative, exists = File.Exists(full) };
            if (!record.exists) errors.Add("asset_missing");
            record.nativeEquirectangularProvenanceValid = File.Exists(full + ".provenance.json");
            if (!record.nativeEquirectangularProvenanceValid) errors.Add("native_equirectangular_provenance_missing");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(relative);
            record.resourcesLoadable = Resources.Load<Texture2D>(resourceKey) != null;
            if (!record.resourcesLoadable) errors.Add("resources_load_failed");
            if (texture != null)
            {
                record.width = texture.width; record.height = texture.height;
                record.aspectRatio = texture.height == 0 ? 0f : texture.width / (float)texture.height;
                record.dimensionValid = texture.width >= 2048 && texture.height >= 1024 && texture.width == texture.height * 2;
                if (!record.dimensionValid) errors.Add("requires_true_2_to_1_minimum_2048x1024");
                record.estimatedAndroidBytes = EstimateAstc6x6Bytes(texture.width, texture.height, true);
                AssessSeam(full, record, warnings);
            }
            else errors.Add("texture_asset_missing");
            var importer = AssetImporter.GetAtPath(relative) as TextureImporter;
            if (importer == null) errors.Add("texture_importer_missing");
            else
            {
                var android = importer.GetPlatformTextureSettings("Android");
                record.importerValid = importer.textureType == TextureImporterType.Default
                    && importer.textureShape == TextureImporterShape.Texture2D
                    && importer.sRGBTexture && importer.mipmapEnabled
                    && importer.alphaSource == TextureImporterAlphaSource.None
                    && importer.wrapMode == TextureWrapMode.Repeat
                    && importer.filterMode == FilterMode.Trilinear
                    && importer.maxTextureSize == 4096
                    && android.overridden && android.maxTextureSize == 4096
                    && android.format == TextureImporterFormat.ASTC_6x6
                    && android.compressionQuality >= 100;
                if (!record.importerValid) errors.Add("import_settings_not_collection_ready");
            }
            record.catalogReferenced = catalogKeys == null || catalogKeys.Contains(resourceKey);
            if (!record.catalogReferenced) errors.Add("resource_key_not_referenced_by_task_catalog");
            record.errors = errors.ToArray(); record.warnings = warnings.ToArray();
            return record;
        }

        public static long EstimateAstc6x6Bytes(int width, int height, bool mipmaps)
        {
            var blocks = ((width + 5L) / 6L) * ((height + 5L) / 6L);
            var baseBytes = blocks * 16L;
            return mipmaps ? (long)Math.Ceiling(baseBytes * 4d / 3d) : baseBytes;
        }

        private static void AssessSeam(string fullPath, PanoramaValidationRecord record, List<string> warnings)
        {
            if (!File.Exists(fullPath)) { record.seamAssessment = "not_available"; return; }
            var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(temp, File.ReadAllBytes(fullPath), false)) { record.seamAssessment = "decode_failed"; return; }
                double total = 0;
                for (var y = 0; y < temp.height; y++)
                {
                    var a = temp.GetPixel(0, y); var b = temp.GetPixel(temp.width - 1, y);
                    total += (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 3d;
                }
                record.seamMeanDifference = temp.height == 0 ? 1f : (float)(total / temp.height);
                record.seamAssessment = record.seamMeanDifference <= 0.12f ? "edge_similarity_ok" : "warning_visible_seam_risk";
                if (record.seamMeanDifference > 0.12f) warnings.Add("left_right_edge_difference_exceeds_0.12");
            }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }

        [MenuItem("SceneTalkVR/Diagnostics/Validate Panoramas")]
        public static void ValidateFromMenu()
        {
            var report = ValidateAll();
            Debug.Log($"[SceneTalkVR] Panorama validation {report.result}: " +
                string.Join("; ", report.panoramas.Select(x => $"{x.resourceKey}={x.errors.Length} errors/{x.warnings.Length} warnings")));
        }

        [MenuItem("SceneTalkVR/Diagnostics/Apply Panorama Import Contract")]
        public static void ApplyRequiredImportSettings()
        {
            foreach (var key in RequiredResourceKeys)
            {
                var path = "Assets/Resources/" + key + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 4096;
                var android = importer.GetPlatformTextureSettings("Android");
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = 4096;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.compressionQuality = 100;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
            Debug.Log("[SceneTalkVR] Applied panorama import contract. Image provenance and equirectangular validation remain separate hard requirements.");
        }

        [MenuItem("SceneTalkVR/Diagnostics/Panorama Preview")]
        public static void OpenPreview() => PanoramaPreviewWindow.Open();
    }

    public sealed class PanoramaPreviewWindow : EditorWindow
    {
        private Vector2 scroll;
        public static void Open() { var window = GetWindow<PanoramaPreviewWindow>("Panorama Preview"); window.minSize = new Vector2(760, 520); window.Show(); }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Preview only. PASS requires native equirectangular provenance plus validator success; a 2:1 shape alone is not proof of a true 360 panorama.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var key in PanoramaAssetValidator.RequiredResourceKeys)
            {
                var texture = Resources.Load<Texture2D>(key);
                EditorGUILayout.LabelField(key, EditorStyles.boldLabel);
                var rect = GUILayoutUtility.GetAspectRect(2f, GUILayout.Height(180));
                if (texture != null) EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
                else EditorGUI.HelpBox(rect, "Missing", MessageType.Error);
                var result = PanoramaAssetValidator.Validate(key);
                EditorGUILayout.LabelField($"{result.width}x{result.height} | seam {result.seamMeanDifference:0.000} | {(result.IsValid ? "PASS" : "FAIL")}");
                EditorGUILayout.Space(12);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
