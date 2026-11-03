using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Inventory;
using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// What the item views show of one thing (doc/rules/ui-design.md の装備の見せ方): its name,
    /// stars, quality and multiplier, its effect at that multiplier, its attributes and its art.
    /// The same face fills the card, the rows and the detail, so a thing reads the same on every
    /// screen. Things without a quality (items, library entries) leave <see cref="HasQuality"/>
    /// false; things without stars leave <see cref="Rarity"/> 0.
    /// </summary>
    public sealed class ItemFace
    {
        public string Name = "";
        public int Rarity;

        public bool HasQuality;
        public ItemQuality Quality;
        public float Multiplier = Inventory.Quality.MinMultiplier;

        // 品質を掛けたあとの効果（リッチテキスト）。
        public string Effect = "";
        public IReadOnlyList<CardElement> Elements = Array.Empty<CardElement>();

        // 絵（アトラスから切り出す範囲と色）。まだ見つけていない物は影の色にする。
        public Texture Art;
        public Rect ArtUv = new(0f, 0f, 1f, 1f);
        public Color ArtTint = Color.white;
        public Vector2 ArtDots;

        /// <summary>
        /// Paints the art on <paramref name="image"/> at the largest whole scale that fits
        /// <paramref name="box"/>, so the dots stay square; no art hides the image.
        /// </summary>
        public void PaintArt(UnityEngine.UI.RawImage image, float box)
        {
            if (image == null)
                return;
            image.texture = Art;
            image.enabled = Art != null;
            if (Art == null)
                return;
            image.uvRect = ArtUv;
            image.color = ArtTint;
            float dot = Mathf.Max(1f, Mathf.Floor(box / Mathf.Max(1f, Mathf.Max(ArtDots.x, ArtDots.y))));
            image.rectTransform.sizeDelta = ArtDots * dot;
        }
    }

    /// <summary>
    /// The texts of the item views (doc/rules/ui-design.md の装備の見せ方): the stars, the quality
    /// with its multiplier ("C ×1.00"), and the row's heading with the stars and the quality right
    /// after the name.
    /// </summary>
    public static class ItemText
    {
        private static readonly string GoldHex = ColorUtility.ToHtmlStringRGB(UiPalette.Gold);
        private static readonly string SubHex = ColorUtility.ToHtmlStringRGB(UiPalette.TextSub);
        private static readonly string RiseHex = ColorUtility.ToHtmlStringRGB(UiPalette.Rise);
        private static readonly string FallHex = ColorUtility.ToHtmlStringRGB(UiPalette.Fall);

        public static string Stars(int rarity) => new('★', Math.Max(0, rarity));

        /// <summary>The stars in gold (rich text), or empty for none.</summary>
        public static string GoldStars(int rarity) =>
            rarity > 0 ? $"<color=#{GoldHex}>{Stars(rarity)}</color>" : "";

        /// <summary>"C ×1.00": the quality's letter in its colour and the multiplier.</summary>
        public static string QualityLine(ItemQuality quality, float multiplier) =>
            $"{QualityPalette.Painted(quality)} <color=#{SubHex}>{Inventory.Quality.Times(multiplier)}</color>";

        /// <summary>
        /// The detail's quality line: the letter large in its colour, the multiplier beside it
        /// (the buy panel's shape).
        /// </summary>
        public static string DetailQuality(ItemQuality quality, float multiplier) =>
            $"<size=300%>{QualityPalette.Painted(quality)}</size><voffset=0.3em>　{Inventory.Quality.Times(multiplier)}</voffset>";

        /// <summary>
        /// A row's first line: the name, then right after it the stars and the quality with its
        /// multiplier in a smaller size ("鉄の剣 ★★ A ×1.76").
        /// </summary>
        public static string Heading(string name, int rarity, bool hasQuality, ItemQuality quality, float multiplier)
        {
            string tail = GoldStars(rarity);
            if (hasQuality)
                tail = (tail.Length > 0 ? tail + " " : "") + QualityLine(quality, multiplier);
            return tail.Length > 0 ? $"{name}<space=0.6em><size=80%>{tail}</size>" : name ?? "";
        }

        /// <summary>The row's first line of <paramref name="face"/>.</summary>
        public static string Heading(ItemFace face) =>
            face == null ? "" : Heading(face.Name, face.Rarity, face.HasQuality, face.Quality, face.Multiplier);

        /// <summary>A value that went up in green, down in red (rich text).</summary>
        public static string Rise(string text) => $"<color=#{RiseHex}>{text}</color>";

        public static string Fall(string text) => $"<color=#{FallHex}>{text}</color>";
    }
}
