using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Baryonyx.Editor
{
    public static class GameFontAssets
    {
        public const string SourcePath = "Assets/Baryonyx/Shared/UI/Fonts/KiwiMaru-Regular.ttf";
        public const string FontAssetPath = "Assets/Baryonyx/Shared/UI/Fonts/KiwiMaru.asset";

        public static TMP_FontAsset GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                CenterLinesOnGlyphs(existing);
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("The Kiwi Maru source font is required.");

            var font = TMP_FontAsset.CreateFontAsset(
                source,
                48,
                6,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true
            );
            font.name = "KiwiMaru";
            var serialized = new SerializedObject(font);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(font, FontAssetPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
                AssetDatabase.AddObjectToAsset(texture, font);
            CenterLinesOnGlyphs(font);
            return font;
        }

        // Kiwi Maru's ascent leaves a wide space above the kana and kanji, so text centred in its
        // box sat low. The ascent is set so the middle of a line (between the ascent and the
        // descent) falls at half the cap height, where the glyphs' middle is. The line height,
        // the spacing between lines, stays as it is.
        private static void CenterLinesOnGlyphs(TMP_FontAsset font)
        {
            var face = font.faceInfo;
            float ascent = face.capLine - face.descentLine;
            if (Mathf.Approximately(face.ascentLine, ascent))
                return;
            face.ascentLine = ascent;
            font.faceInfo = face;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        public static void SetAsDefault(TMP_FontAsset font)
        {
            if (TMP_Settings.instance == null)
                throw new InvalidOperationException("Import TMP Essential Resources first.");

            var settings = new SerializedObject(TMP_Settings.instance);
            var property = settings.FindProperty("m_defaultFontAsset");
            if (property == null)
                throw new InvalidOperationException("TMP default font property is missing.");
            property.objectReferenceValue = font;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
