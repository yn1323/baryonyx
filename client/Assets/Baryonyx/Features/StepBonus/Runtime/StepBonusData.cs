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

    // 効果量が幅の中のどこに当たったか。ハクスラのように、入手ごとにばらつく値の良さを示す。
    public enum StepBonusRank
    {
        C,
        B,
        A,
        S,
    }

    /// <summary>One kind of UPT bonus and the range its value rolls in.</summary>
    [Serializable]
    public sealed class StepBonusDefinition
    {
        public string Id = "";
        public string Name = "";
        public StepBonusCategory Category;

        // 効果の文。{0} に効果量が入る（例：「ドロップ率 +{0}%」）。
        public string Effect = "{0}";

        // 効果量の幅。入手するたびに、この幅の中で値が決まる。
        public float Min;
        public float Max = 1f;

        // 効果量の小数の桁数。
        [Range(0, 2)]
        public int Decimals = 1;

        // 枠の倍率を掛けたあとの上限（確率の効果は100）。0なら上限なし。
        [Min(0f)]
        public float Cap;

        // 24×24のドット絵のアイコン。4倍で表示する。
        public Sprite Icon;
    }

    /// <summary>A bonus the player owns, with the value it rolled.</summary>
    [Serializable]
    public sealed class StepBonusRoll
    {
        public string Id = "";
        public float Value;

        // 前回の冒険で、効果量の高いものに置き換わった。
        public bool Updated;
    }
}
