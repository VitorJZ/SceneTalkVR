using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SceneTalkVR.Editor
{
    public static class ChineseFontSetup
    {
        private const string SourceFontPath = "Assets/SceneTalkVR/Fonts/NotoSansSC-VF.ttf";
        private const string FontAssetPath = "Assets/SceneTalkVR/Fonts/NotoSansSC-VF SDF.asset";
        private const string SmokeTestCharacters = "中文简体任务问卷最终排序正式实验试点退出确认取消继续非常同意不同意，。？！：《》（）【】";

        [MenuItem("SceneTalkVR/Setup Chinese TMP Fallback")]
        public static void RunFromMenu()
        {
            Setup();
        }

        public static void RunBatch()
        {
            try
            {
                Setup();
                Debug.Log("[ChineseFontSetup] Batch setup completed successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Setup()
        {
            AssetDatabase.ImportAsset(
                SourceFontPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException($"Unable to import Chinese source font at {SourceFontPath}.");
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = CreateFontAsset(sourceFont);
            }

            ConfigureFontAsset(fontAsset);
            VerifyChineseGlyphs(fontAsset);
            ConfigureFallbacks(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChineseFontSetup] Configured {fontAsset.name} as the global TMP Chinese fallback.");
        }

        private static TMP_FontAsset CreateFontAsset(Font sourceFont)
        {
            FontEngine.InitializeFontEngine();
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                throw new InvalidOperationException("TMP failed to create the Chinese font asset.");
            }

            fontAsset.name = "NotoSansSC-VF SDF";
            fontAsset.atlasTextures[0].name = "NotoSansSC-VF Atlas";
            fontAsset.material.name = "NotoSansSC-VF Material";

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            return fontAsset;
        }

        private static void ConfigureFontAsset(TMP_FontAsset fontAsset)
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            SerializedObject serializedFontAsset = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicData = serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicData == null)
            {
                throw new InvalidOperationException("TMP font asset is missing m_ClearDynamicDataOnBuild.");
            }

            clearDynamicData.boolValue = true;
            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontAsset);
        }

        private static void VerifyChineseGlyphs(TMP_FontAsset fontAsset)
        {
            if (!fontAsset.TryAddCharacters(SmokeTestCharacters, out string missingCharacters))
            {
                throw new InvalidOperationException(
                    $"Chinese font smoke test failed. Missing characters: {missingCharacters}");
            }

            fontAsset.ClearFontAssetData();
            EditorUtility.SetDirty(fontAsset);
        }

        private static void ConfigureFallbacks(TMP_FontAsset fontAsset)
        {
            TMP_Settings settings = TMP_Settings.LoadDefaultSettings();
            if (settings == null)
            {
                throw new InvalidOperationException("TMP Settings could not be loaded.");
            }

            List<TMP_FontAsset> globalFallbacks = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();
            globalFallbacks.RemoveAll(candidate => candidate == null);
            if (!globalFallbacks.Contains(fontAsset))
            {
                globalFallbacks.Insert(0, fontAsset);
            }

            TMP_Settings.fallbackFontAssets = globalFallbacks;
            EditorUtility.SetDirty(settings);
        }
    }
}
