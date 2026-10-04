using System;
using UnityEngine;

namespace Baryonyx.StepBonus
{
    public enum StepBonusCategory
    {
        Explore,
        Drop,
        Battle,
    }

    // ボーナスのランク。ハクスラのように入手ごとに決まり、ランクごとに効果量は固定する。
    public enum StepBonusRank
    {
        E,
        D,
        C,
        B,
        A,
        S,
    }

    /// <summary>One kind of UPT bonus and its fixed value for each rank.</summary>
    [Serializable]
    public sealed class StepBonusDefinition
    {
        public string Id = "";
        public string Name = "";
        public StepBonusCategory Category;

        // 効果の文。{0} に効果量が入る（例：「ドロップ率 +{0}%」）。
        public string Effect = "{0}";

        // ランクごとの効果量。E・D・C・B・A・Sの順。
        public float[] Values = new float[6];

        // 効果量の小数の桁数。
        [Range(0, 2)]
        public int Decimals = 1;

        // 枠の倍率を掛けたあとの上限（確率の効果は100）。0なら上限なし。
        [Min(0f)]
        public float Cap;

        // 24×24のドット絵のアイコン。4倍で表示する。
        public Sprite Icon;

        public float Value(StepBonusRank rank) =>
            Values != null && (int)rank < Values.Length ? Values[(int)rank] : 0f;
    }

    /// <summary>A bonus the player owns, with the rank it came with.</summary>
    [Serializable]
    public sealed class StepBonusRoll
    {
        public string Id = "";
        public StepBonusRank Rank;
    }
}
