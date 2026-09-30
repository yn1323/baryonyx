using System;
using Baryonyx.Editor.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// The font and materials of the damage and heal numbers: Dela Gothic One, a very thick
    /// smooth gothic, drawn over the pixel art with a thick dark outline and a hard drop shadow.
    /// Only the digits are baked, so the font file does not ship in the build.
    /// </summary>
    public static class DamageNumberAssets
    {
        private const string Folder = "Assets/Baryonyx/Features/Combat/UI/Fonts";
        public const string SourcePath = Folder + "/DelaGothicOne-Regular.ttf";
        public const string FontAssetPath = Folder + "/DamageNumbers.asset";
        public const string NormalMaterialPath = Folder + "/DamageNormal.mat";
        public const string WeakMaterialPath = Folder + "/DamageWeak.mat";
        public const string HealMaterialPath = Folder + "/DamageHeal.mat";

        private const string Characters = "0123456789";

        /// <summary>How one kind of number looks: its size, the gradient from top to bottom, and the outline.</summary>
        public sealed class Style
        {
            public float Size;
            public Color Top;
            public Color Bottom;
            public Color Edge;
            public string MaterialPath;
        }

        // White to warm beige; a weakness hits bigger in yellow to orange; healing is smaller and green.
        // The gradient spans the glyph's quad with its padding, so the ends are set past the colours
        // seen on the glyph itself.
        public static readonly Style Normal = new()
        {
            Size = 40f,
            Top = new Color(1f, 1f, 1f),
            Bottom = new Color(0.85f, 0.76f, 0.6f),
            Edge = new Color(0.14f, 0.08f, 0.05f),
            MaterialPath = NormalMaterialPath,
        };
        public static readonly Style Weak = new()
        {
            Size = 44f,
            Top = new Color(1f, 1f, 0.35f),
            Bottom = new Color(0.95f, 0.45f, 0.02f),
            Edge = new Color(0.29f, 0.06f, 0.02f),
            MaterialPath = WeakMaterialPath,
        };
        public static readonly Style Heal = new()
        {
            Size = 36f,
            Top = new Color(0.9f, 1f, 0.82f),
            Bottom = new Color(0.36f, 0.8f, 0.35f),
            Edge = new Color(0.05f, 0.16f, 0.06f),
            MaterialPath = HealMaterialPath,
        };

        /// <summary>A centred number label in the style, made in the open <see cref="UiBuild"/> session.</summary>
        public static TMP_Text Label(RectTransform parent, string name, string text, Style style)
        {
            var font = GetOrCreateFont();
            var label = UiBuild.Label(
                parent,
                name,
                text,
                style.Size,
                Color.white,
                TextAlignmentOptions.Center,
                shadow: false
            );
            label.font = font;
            label.fontSharedMaterial = EnsureMaterial(font, style.MaterialPath, style.Edge);
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(
                style.Top,
                style.Top,
                style.Bottom,
                style.Bottom
            );
            UiBuild.Place(label.rectTransform, Vector2.zero, new Vector2(480f, style.Size * 1.6f));
            return label;
        }

        // Wide padding so the thick outline and the dropped shadow fit in the distance field.
        private const int SamplingSize = 90;
        private const int Padding = 20;
        private const float OutlineWidth = 0.18f;

        public static TMP_FontAsset GetOrCreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null)
                throw new InvalidOperationException("The Dela Gothic One source font is required.");

            var font = TMP_FontAsset.CreateFontAsset(
                source,
                SamplingSize,
                Padding,
                GlyphRenderMode.SDFAA,
                512,
                512,
                AtlasPopulationMode.Dynamic,
                false
            );
            font.name = "DamageNumbers";
            if (!font.TryAddCharacters(Characters, out string missing))
                throw new InvalidOperationException($"Dela Gothic One lacks \"{missing}\".");
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(font, FontAssetPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures)
                AssetDatabase.AddObjectToAsset(texture, font);
            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>
        /// Makes or refreshes a number material: the outline in <paramref name="edge"/> and a
        /// hard shadow dropped straight down under it.
        /// </summary>
        public static Material EnsureMaterial(TMP_FontAsset font, string path, Color edge)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = font.material.shader;
            material.CopyPropertiesFromMaterial(font.material);
            // The mobile shader centres the outline on the glyph's edge, so the face is dilated by
            // the same amount to keep the fill its own size and put the whole outline outside it.
            material.SetFloat("_FaceDilate", OutlineWidth);
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor("_OutlineColor", edge);
            material.SetFloat("_OutlineWidth", OutlineWidth);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", -0.5f);
            material.SetFloat("_UnderlayDilate", OutlineWidth);
            material.SetFloat("_UnderlaySoftness", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
    }
}
