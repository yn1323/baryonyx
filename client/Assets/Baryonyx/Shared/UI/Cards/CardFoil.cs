using Baryonyx.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI.Cards
{
    /// <summary>
    /// The sparkle laid over a card's art (doc/art/direction.md, card sparkle): a raw image of the
    /// same art drawn with the CardFoil shader, which adds a sweep of light, tiny specks that
    /// brighten and dim, a motif and a rim by the card's quality, only where the art is opaque:
    /// everything at S, a lighter sparkle at A, nothing at B and C. The shader draws five levels
    /// (made for the stars before quality); S takes its fifth and A its third. How it sparkles,
    /// one of <see cref="StyleCount"/> styles (a motif and its colours), belongs to the kind of
    /// skill or equipment, so each kind sparkles its own way. The level, a seed of this
    /// card's own (so cards side by side do not flash together) and the style go to the shader in
    /// the vertex colour, so every card shares one material.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CardFoil : MonoBehaviour
    {
        /// <summary>The shader's levels: 1 draws nothing, 5 everything.</summary>
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        /// <summary>How many ways to sparkle the shader has: 7 motifs times 9 sets of colours.</summary>
        public const int StyleCount = 63;

        /// <summary>The motifs, in the shader's order (the style modulo 7).</summary>
        public static readonly string[] MotifNames =
        {
            "星屑",
            "コスモ",
            "氷の結晶",
            "光沢",
            "波紋",
            "オーロラ",
            "格子",
        };

        /// <summary>The sets of colours, in the shader's order (the style divided by 7).</summary>
        public static readonly string[] PaletteNames =
        {
            "金",
            "銀",
            "薔薇",
            "水",
            "翠",
            "菫",
            "焔",
            "虹",
            "薄氷と桜",
        };

        public ItemQuality Quality = ItemQuality.C;

        /// <summary>How this one sparkles (0 to <see cref="StyleCount"/> - 1).</summary>
        public int Style;

        private RawImage image;

        public RawImage Image => image != null ? image : image = GetComponent<RawImage>();

        /// <summary>The shader's level for <paramref name="quality"/>: S 5, A 3, B and C none.</summary>
        public static int LevelOf(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.S => MaxLevel,
                ItemQuality.A => 3,
                _ => MinLevel,
            };

        /// <summary>Whether <paramref name="quality"/> sparkles at all.</summary>
        public static bool Sparkles(ItemQuality quality) => LevelOf(quality) > MinLevel;

        /// <summary>The name of a style: its motif and its colours ("コスモ・金").</summary>
        public static string NameOf(int style)
        {
            int s = Wrap(style);
            return $"{MotifNames[s % MotifNames.Length]}・{PaletteNames[s / MotifNames.Length]}";
        }

        /// <summary>
        /// The style of the <paramref name="index"/>th thing of a list: things next to each other
        /// differ in motif first. A list longer than <see cref="StyleCount"/> starts over.
        /// </summary>
        public static int StyleAt(int index) => Wrap(index);

        /// <summary>
        /// The style of the <paramref name="index"/>th thing of another list than the skills
        /// (equipment), stepped by <paramref name="step"/> from
        /// <paramref name="offset"/> so its first things do not take the skills' first styles.
        /// A step without a common factor with <see cref="StyleCount"/> keeps the list's styles apart.
        /// </summary>
        public static int StyleAt(int index, int step, int offset) => Wrap(index * step + offset);

        private static int Wrap(int style) => ((style % StyleCount) + StyleCount) % StyleCount;

        /// <summary>
        /// Shows the sparkle of <paramref name="quality"/> in <paramref name="style"/> over
        /// <paramref name="art"/>; none at B and C.
        /// </summary>
        public void Show(Texture art, Rect uvRect, ItemQuality quality, int style)
        {
            Quality = quality;
            Style = Wrap(style);
            int level = LevelOf(quality);
            var raw = Image;
            raw.texture = art;
            raw.uvRect = uvRect;
            raw.raycastTarget = false;
            raw.enabled = art != null && level > MinLevel;
            raw.color = Encode(
                raw,
                level / (float)MaxLevel,
                SeedOf(transform),
                (Style + 0.5f) / StyleCount
            );
        }

        /// <summary>
        /// The vertex colour that reaches the shader as <paramref name="level"/>,
        /// <paramref name="seed"/> and <paramref name="style"/> (all 0 to 1). In the linear colour
        /// space a canvas turns vertex colours from gamma to linear on their way, which made A's
        /// third level arrive as the second (no specks, motif or rim) and mixed up the styles; the
        /// values are turned the other way first so that they arrive as they are.
        /// </summary>
        public static Color Encode(Graphic graphic, float level, float seed, float style)
        {
            var canvas = graphic != null ? graphic.canvas : null;
            if (
                QualitySettings.activeColorSpace != ColorSpace.Linear
                || (canvas != null && canvas.vertexColorAlwaysGammaSpace)
            )
                return new Color(level, seed, style, 1f);
            return new Color(
                Mathf.LinearToGammaSpace(level),
                Mathf.LinearToGammaSpace(seed),
                Mathf.LinearToGammaSpace(style),
                1f
            );
        }

        public void Show(Texture art, ItemQuality quality, int style) =>
            Show(art, new Rect(0f, 0f, 1f, 1f), quality, style);

        /// <summary>
        /// Shows the sparkle over an icon drawn as a sprite (equipment): the part
        /// of its texture the sprite takes.
        /// </summary>
        public void Show(Sprite art, ItemQuality quality, int style) =>
            Show(art != null ? art.texture : null, UvOf(art), quality, style);

        /// <summary>
        /// Darkens the sparkle as much as the art under it (1 as shown, 0 none), as on a slot
        /// whose art is darkened. Call it after <see cref="Show(Texture, Rect, ItemQuality, int)"/>.
        /// </summary>
        public void Fade(float strength)
        {
            var color = Image.color;
            color.a = Mathf.Clamp01(strength);
            Image.color = color;
        }

        /// <summary>The part of its texture <paramref name="sprite"/> takes, as a raw image's uv rect.</summary>
        public static Rect UvOf(Sprite sprite)
        {
            var texture = sprite != null ? sprite.texture : null;
            if (texture == null)
                return new Rect(0f, 0f, 1f, 1f);
            var rect = sprite.textureRect;
            return new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height
            );
        }

        /// <summary>
        /// A seed of the card's own (0 to 1) from where <paramref name="transform"/> sits, so cards
        /// side by side do not flash together.
        /// </summary>
        public static float SeedOf(Transform transform)
        {
            unchecked
            {
                int hash = 17;
                for (var t = transform; t != null; t = t.parent)
                    hash = hash * 31 + t.GetSiblingIndex() * 7 + t.name.Length;
                return Mathf.Repeat(hash * 0.6180339f, 1f);
            }
        }
    }
}
