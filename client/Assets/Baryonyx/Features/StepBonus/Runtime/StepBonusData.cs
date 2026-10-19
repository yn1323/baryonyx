using System;
using Baryonyx.Inventory;
using UnityEngine;

namespace Baryonyx.StepBonus
{
    public enum StepBonusCategory
    {
        Explore,
        Drop,
        Battle,
    }

    /// <summary>
    /// One kind of ACT bonus: its base value, grown by each bonus's own multiplier
    /// (doc/features/step-bonus.md, the effect amount), and the upper limit of that multiplier.
    /// </summary>
    [Serializable]
    public sealed class StepBonusDefinition
    {
        public string Id = "";
        public string Name = "";
        public StepBonusCategory Category;

        // 効果の文。{0} に効果量が入る（例：「ドロップ率 +{0}%」）。
        public string Effect = "{0}";

        // 基本の効果量（倍率×1.00のとき）。
        public float BaseValue;

        // 1つずつ持つ倍率の上限（doc/features/inventory.md の品質）。サーバーの仮設定と同じ×1.5。
        [Min(1f)]
        public float MaxMultiplier = Quality.DefaultMaxMultiplier;

        // 効果量の小数の桁数。
        [Range(0, 2)]
        public int Decimals = 1;

        // 枠の倍率を掛けたあとの上限（確率の効果は100）。0なら上限なし。
        [Min(0f)]
        public float Cap;

        // 24×24のドット絵のアイコン。4倍で表示する。
        public Sprite Icon;

        // 倍率を掛けた効果量（枠に入れる前）。
        public float Value(float multiplier) => BaseValue * multiplier;

        public ItemQuality QualityOf(float multiplier) => Quality.Of(multiplier, MaxMultiplier);
    }

    /// <summary>A bonus the player owns, with the multiplier it came with.</summary>
    [Serializable]
    public sealed class StepBonusRoll
    {
        public string Id = "";

        [Min(1f)]
        public float Multiplier = Quality.MinMultiplier;
    }
}
