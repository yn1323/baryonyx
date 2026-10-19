using UnityEngine;

namespace Baryonyx.Inventory
{
    /// <summary>
    /// The colour of each quality's letter, the same on every screen that shows one (the bonus
    /// settings, the equipment lists, the quality lab): S gold, A violet, B blue, C a dim grey.
    /// </summary>
    public static class QualityPalette
    {
        public static Color Of(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.S => new Color(1f, 0.8f, 0.3f),
                ItemQuality.A => new Color(0.8f, 0.62f, 1f),
                ItemQuality.B => new Color(0.5f, 0.76f, 1f),
                _ => new Color(0.66f, 0.64f, 0.6f),
            };

        /// <summary>"#ffcc4d": the colour for rich text.</summary>
        public static string Hex(ItemQuality quality) =>
            ColorUtility.ToHtmlStringRGB(Of(quality)).ToLowerInvariant();

        /// <summary>The quality's letter in its colour (rich text).</summary>
        public static string Painted(ItemQuality quality) =>
            $"<color=#{Hex(quality)}>{Quality.Letter(quality)}</color>";
    }
}
