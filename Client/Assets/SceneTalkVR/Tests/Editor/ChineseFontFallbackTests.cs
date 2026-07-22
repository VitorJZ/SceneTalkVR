using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SceneTalkVR.Tests.Editor
{
    public sealed class ChineseFontFallbackTests
    {
        private const string SourceFontPath = "Assets/SceneTalkVR/Fonts/NotoSansSC-VF.ttf";
        private const string FontAssetPath = "Assets/SceneTalkVR/Fonts/NotoSansSC-VF SDF.asset";

        [Test]
        public void ChineseFont_IsPackagedAndConfiguredAsTmpFallback()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            TMP_FontAsset chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            Assert.That(sourceFont, Is.Not.Null, "The distributable Chinese source font must be packaged.");
            Assert.That(chineseFont, Is.Not.Null, "The TMP Chinese font asset must exist.");
            Assert.That(chineseFont.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(chineseFont.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(TMP_Settings.fallbackFontAssets, Does.Contain(chineseFont));

            SerializedObject serializedFont = new SerializedObject(chineseFont);
            Assert.That(
                serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue,
                Is.True,
                "Dynamic glyph cache must be cleared before builds so the source font remains authoritative.");

            Assert.That(chineseFont.HasCharacter('中', false, true), Is.True);
            Assert.That(chineseFont.HasCharacter('文', false, true), Is.True);
            Assert.That(chineseFont.HasCharacter('问', false, true), Is.True);
            Assert.That(chineseFont.HasCharacter('卷', false, true), Is.True);
            chineseFont.ClearFontAssetData();
        }
    }
}
