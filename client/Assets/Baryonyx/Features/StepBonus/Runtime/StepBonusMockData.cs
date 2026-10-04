using System;
using UnityEngine;

namespace Baryonyx.StepBonus
{
    /// <summary>
    /// The mock state of the UPT bonus until it is saved: the slots, the owned bonuses and
    /// today's UPT. The tavern's bonus settings read it. See doc/features/step-bonus.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Step Bonus Mock Data")]
    public sealed class StepBonusMockData : ScriptableObject
    {
        // ホームから開いたときはホームの今日のUPTを使う。単体で開いたときだけこの値を使う。
        [Min(0)]
        public int TodayUpt = 3240;

        // 段階のUPTと、その枠の倍率。doc/features/step-bonus.md の候補値で、未確定。
        public int[] Tiers = { 1000, 2000, 3000, 5000, 8000 };
        public float[] Multipliers = { 1f, 1.2f, 1.4f, 1.7f, 2f };

        // 用意したボーナスの全種類の数。「所持 7 / 20」の分母。
        [Min(0)]
        public int TotalKinds = 20;

        public StepBonusDefinition[] Bonuses = Array.Empty<StepBonusDefinition>();
        public StepBonusRoll[] Owned = Array.Empty<StepBonusRoll>();

        // 枠ごとに入れておくボーナスのID。空なら空いている枠。
        public string[] Loadout = Array.Empty<string>();

        public StepBonusDefinition Find(string id) => Array.Find(Bonuses, bonus => bonus.Id == id);
    }
}
