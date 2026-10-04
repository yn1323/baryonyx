using UnityEngine;

namespace Baryonyx.StepBonus
{
    /// <summary>Colours shared by the bonus settings and its generator.</summary>
    public static class StepBonusArt
    {
        public static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        public static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        public static readonly Color TextFaint = new(0.604f, 0.580f, 0.514f);
        public static readonly Color Teal = new(0.498f, 0.890f, 0.839f);
        public static readonly Color Gold = new(1f, 0.843f, 0.4f);

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
                _ => new Color(0.7f, 0.68f, 0.64f),
            };
    }
}
