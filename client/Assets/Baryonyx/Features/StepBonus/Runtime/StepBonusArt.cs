using Baryonyx.Inventory;
using Baryonyx.UI;
using UnityEngine;

namespace Baryonyx.StepBonus
{
    /// <summary>The slot colours shared by the bonus settings and its generator (text colours are in UiPalette).</summary>
    public static class StepBonusArt
    {
        // 枠の色は段階が上がるほど格を上げる（銅・銅・銀・金・水晶）。
        public static readonly Color[] Tiers =
        {
            new(0.85f, 0.55f, 0.33f),
            new(0.85f, 0.55f, 0.33f),
            new(0.82f, 0.85f, 0.9f),
            new(1f, 0.8f, 0.32f),
            new(0.62f, 0.92f, 1f),
        };

        // 品質の色は、ほかの持ち物の品質の表示と同じ（QualityPalette）。
        public static Color Quality(ItemQuality quality) => QualityPalette.Of(quality);
    }
}
