using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Baryonyx.Editor
{
    public static class GameFontAssets
    {
        public const string SourcePath = "Assets/Baryonyx/Shared/UI/Fonts/DotGothic16-Regular.ttf";
        public const string FontAssetPath = "Assets/Baryonyx/Shared/UI/Fonts/DotGothic16.asset";

        public static TMP_FontAsset GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("The DotGothic16 source font is required.");

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
            font.name = "DotGothic16";
            AssetDatabase.CreateAsset(font, FontAssetPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
                AssetDatabase.AddObjectToAsset(texture, font);
            return font;
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
