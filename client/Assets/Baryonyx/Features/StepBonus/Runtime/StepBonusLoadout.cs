using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Baryonyx.StepBonus
{
    // 枠にボーナスを入れたときの変わり方。
    public enum StepBonusChange
    {
        // すでにその枠に入っている。
        None,

        // 空いているボーナスを枠に入れる。
        Set,

        // ほかの枠に入っているボーナスを選び、2つの枠の中身を入れ替える。
        Swap,
    }

    // 冒険でボーナスを手に入れたときの結果。
    public enum StepBonusAcquired
    {
        Added,
        Updated,
        Discarded,
    }

    /// <summary>
    /// The UPT bonus slots and the bonuses the player owns, kept free of Unity objects so the
    /// rules can be tested directly. A slot opens when today's UPT reaches its tier, and the
    /// bonus in it works at its rolled value times the slot's multiplier. A bonus fits in one
    /// slot only, so choosing one already set swaps the two slots. Owning the same bonus again
    /// keeps the higher value. See doc/features/step-bonus.md.
    /// </summary>
    public sealed class StepBonusLoadout
    {
        // 効果量が幅のどこに当たったかで、ランクを分ける境目。
        public const float RankS = 0.85f;
        public const float RankA = 0.6f;
        public const float RankB = 0.3f;

        private readonly int[] tiers;
        private readonly float[] multipliers;
        private readonly string[] slots;
        private readonly Dictionary<string, StepBonusDefinition> definitions;
        private readonly List<StepBonusRoll> owned;

        public StepBonusLoadout(
            IReadOnlyList<int> tiers,
            IReadOnlyList<float> multipliers,
            IEnumerable<StepBonusDefinition> definitions,
            IEnumerable<StepBonusRoll> owned,
            IReadOnlyList<string> loadout,
            int totalKinds
        )
        {
            if (tiers == null)
                throw new ArgumentNullException(nameof(tiers));
            if (multipliers == null || multipliers.Count != tiers.Count)
                throw new ArgumentException("Each tier needs a multiplier.", nameof(multipliers));
            this.tiers = tiers.ToArray();
            this.multipliers = multipliers.ToArray();
            this.definitions = (definitions ?? Enumerable.Empty<StepBonusDefinition>())
                .Where(bonus => bonus != null && !string.IsNullOrEmpty(bonus.Id))
                .GroupBy(bonus => bonus.Id)
                .ToDictionary(group => group.Key, group => group.First());
            this.owned = new List<StepBonusRoll>();
            foreach (var roll in owned ?? Enumerable.Empty<StepBonusRoll>())
            {
                if (roll == null || !this.definitions.ContainsKey(roll.Id))
                    continue;
                var kept = Roll(roll.Id);
                if (kept == null)
                    this.owned.Add(roll);
                else if (roll.Value > kept.Value)
                    this.owned[this.owned.IndexOf(kept)] = roll;
            }
            slots = new string[this.tiers.Length];
            for (int i = 0; i < slots.Length && loadout != null && i < loadout.Count; i++)
                if (Owns(loadout[i]) && SlotOf(loadout[i]) < 0)
                    slots[i] = loadout[i];
            TotalKinds = Math.Max(totalKinds, this.owned.Count);
        }

        public static StepBonusLoadout From(StepBonusMockData data) =>
            data == null
                ? throw new ArgumentNullException(nameof(data))
                : new StepBonusLoadout(
                    data.Tiers,
                    data.Multipliers,
                    data.Bonuses,
                    data.Owned.Select(roll => new StepBonusRoll
                    {
                        Id = roll.Id,
                        Value = roll.Value,
                        Updated = roll.Updated,
                    }),
                    data.Loadout,
                    data.TotalKinds
                );

        public int SlotCount => slots.Length;
        public int TotalKinds { get; }
        public IReadOnlyList<StepBonusRoll> Owned => owned;

        public int Tier(int slot) => tiers[slot];

        public float Multiplier(int slot) => multipliers[slot];

        // 枠に入っているボーナスのID。空いていればnull。
        public string Bonus(int slot) => slots[slot];

        public StepBonusDefinition Definition(string id) =>
            id != null && definitions.TryGetValue(id, out var bonus) ? bonus : null;

        public StepBonusRoll Roll(string id) => owned.Find(roll => roll.Id == id);

        public bool Owns(string id) => Roll(id) != null;

        // ボーナスを入れている枠。どこにも入れていなければ-1。
        public int SlotOf(string id) => id == null ? -1 : Array.IndexOf(slots, id);

        public bool IsOpen(int slot, int upt) => upt >= tiers[slot];

        public int OpenCount(int upt) => tiers.Count(tier => upt >= tier);

        public StepBonusChange Preview(int slot, string id)
        {
            if (slot < 0 || slot >= slots.Length || !Owns(id) || slots[slot] == id)
                return StepBonusChange.None;
            return SlotOf(id) >= 0 ? StepBonusChange.Swap : StepBonusChange.Set;
        }

        public StepBonusChange Apply(int slot, string id)
        {
            var change = Preview(slot, id);
            if (change == StepBonusChange.Swap)
                slots[SlotOf(id)] = slots[slot];
            if (change != StepBonusChange.None)
                slots[slot] = id;
            return change;
        }

        // 同じボーナスをもう一度手に入れたら、効果量の高いほうだけを残す。
        public StepBonusAcquired Acquire(string id, float value)
        {
            if (Definition(id) == null)
                throw new ArgumentException($"Unknown bonus: {id}", nameof(id));
            var roll = Roll(id);
            if (roll == null)
            {
                owned.Add(new StepBonusRoll { Id = id, Value = value });
                return StepBonusAcquired.Added;
            }
            if (value <= roll.Value)
                return StepBonusAcquired.Discarded;
            roll.Value = value;
            roll.Updated = true;
            return StepBonusAcquired.Updated;
        }

        // 枠の倍率を掛けた効果量。確率の効果は上限で止める。
        public float Effective(string id, int slot)
        {
            var bonus = Definition(id);
            var roll = Roll(id);
            if (bonus == null || roll == null)
                return 0f;
            float value = roll.Value * multipliers[slot];
            return bonus.Cap > 0f ? Math.Min(value, bonus.Cap) : value;
        }

        public static StepBonusRank RankOf(StepBonusDefinition bonus, float value)
        {
            float position = Position(bonus, value);
            return position >= RankS ? StepBonusRank.S
                : position >= RankA ? StepBonusRank.A
                : position >= RankB ? StepBonusRank.B
                : StepBonusRank.C;
        }

        // 効果量が幅のどこにあるか（0〜1）。
        public static float Position(StepBonusDefinition bonus, float value)
        {
            if (bonus == null || bonus.Max <= bonus.Min)
                return 1f;
            return Math.Clamp((value - bonus.Min) / (bonus.Max - bonus.Min), 0f, 1f);
        }

        public static string Effect(StepBonusDefinition bonus, float value) =>
            bonus == null ? "" : string.Format(bonus.Effect, Number(bonus, value));

        // 「3〜8%」のような効果量の幅。
        public static string Range(StepBonusDefinition bonus) =>
            bonus == null
                ? ""
                : $"{bonus.Min.ToString("0.#", CultureInfo.InvariantCulture)}〜{bonus.Max.ToString("0.#", CultureInfo.InvariantCulture)}%";

        public static string Number(StepBonusDefinition bonus, float value) =>
            value.ToString("F" + bonus.Decimals, CultureInfo.InvariantCulture);

        // 「5,000」のような段階のUPT。
        public static string Upt(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        // 「×1.7」のような枠の倍率。
        public static string Times(float multiplier) =>
            "×" + multiplier.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
