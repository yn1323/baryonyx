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

        public static Color Rank(StepBonusRank rank) =>
            rank switch
            {
                StepBonusRank.S => new Color(1f, 0.8f, 0.3f),
                StepBonusRank.A => new Color(0.8f, 0.62f, 1f),
                StepBonusRank.B => new Color(0.5f, 0.76f, 1f),
                StepBonusRank.C => new Color(0.52f, 0.86f, 0.55f),
                StepBonusRank.D => new Color(0.78f, 0.75f, 0.68f),
                _ => new Color(0.55f, 0.53f, 0.5f),
            };
    }
}
