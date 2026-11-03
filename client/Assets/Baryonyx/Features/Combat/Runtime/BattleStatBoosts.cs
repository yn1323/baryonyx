using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>When a stat boost works in battle.</summary>
    public enum BattleBoostCondition
    {
        // HPが最大HPの半分以下で、立っている間。
        HalfHpOrLess,

        // 戦闘の最初の Turns ターンの間。
        FirstTurns,

        // 倒れている仲間1人につき。
        PerFallenAlly,
    }

    /// <summary>The stats a boost grows.</summary>
    public enum BattleBoostStats
    {
        Attacks,
        Speed,
    }

    /// <summary>
    /// A stat boost that works only while its condition holds during a battle: the adventure's
    /// boons after a won battle that grow with the battle (doc/features/stage-progression.md,
    /// 戦闘後の強化の中身: 背水・先手・弔い).
    /// </summary>
    public sealed class BattleStatBoost
    {
        public string Name = "";
        public BattleBoostCondition Condition;
        public BattleBoostStats Stats;

        // 上がる%。PerFallenAlly では倒れた仲間1人あたり。
        public int Percent;

        // FirstTurns のターン数。
        public int Turns = 2;
    }

    /// <summary>
    /// Works out an ally's stats at a moment of the battle with its boosts
    /// (<see cref="BattleStatBoost"/>). The boosts working now are added to the percents the
    /// stats were grown by before the battle and grown once from the stats before any boon, as
    /// the boons themselves are.
    /// </summary>
    public static class BattleStatBoosts
    {
        // 強化で上げる6つの項目の、%の並び（CharacterStats のうち会心と幸運を除く）。
        public const int Hp = 0;
        public const int PhysicalAttack = 1;
        public const int PhysicalDefense = 2;
        public const int MagicAttack = 3;
        public const int MagicDefense = 4;
        public const int Speed = 5;
        public const int Count = 6;

        // どの項目も、元の値のこの割合より下げない。
        public const float Floor = 0.5f;

        /// <summary><paramref name="value"/> grown by <paramref name="percent"/>, never under <see cref="Floor"/> of it.</summary>
        public static int Grow(int value, float percent) =>
            Math.Max(1, (int)Math.Round(value * Math.Max(Floor, 1f + percent / 100f)));

        /// <summary>Whether <paramref name="boost"/> works for an ally in this state.</summary>
        public static bool Works(
            BattleStatBoost boost,
            int hp,
            int maxHp,
            int battleTurn,
            int fallen
        ) =>
            boost != null
            && hp > 0
            && boost.Condition switch
            {
                BattleBoostCondition.HalfHpOrLess => hp * 2 <= maxHp,
                BattleBoostCondition.FirstTurns => battleTurn <= boost.Turns,
                BattleBoostCondition.PerFallenAlly => fallen > 0,
                _ => false,
            };

        /// <summary>
        /// The ally's stats now. <paramref name="stats"/> are those it starts the battle with,
        /// <paramref name="unboosted"/> those before any boon and <paramref name="percents"/> the
        /// percents (in the order of <see cref="Hp"/> and the rest) the boons grew them by; with
        /// no boost working, the stats stay as they started.
        /// </summary>
        public static CharacterStats Now(
            CharacterStats stats,
            CharacterStats unboosted,
            IReadOnlyList<float> percents,
            IReadOnlyList<BattleStatBoost> boosts,
            int hp,
            int maxHp,
            int battleTurn,
            int fallen
        )
        {
            if (boosts == null || boosts.Count == 0)
                return stats;
            var added = new float[Count];
            bool working = false;
            foreach (var boost in boosts)
            {
                if (!Works(boost, hp, maxHp, battleTurn, fallen))
                    continue;
                working = true;
                int percent =
                    boost.Condition == BattleBoostCondition.PerFallenAlly
                        ? boost.Percent * fallen
                        : boost.Percent;
                if (boost.Stats == BattleBoostStats.Speed)
                    added[Speed] += percent;
                else
                {
                    added[PhysicalAttack] += percent;
                    added[MagicAttack] += percent;
                }
            }
            if (!working)
                return stats;
            return Grown(
                stats,
                unboosted,
                percents,
                added[PhysicalAttack],
                added[MagicAttack],
                added[Speed]
            );
        }

        /// <summary>
        /// The ally's stats with the percents the effects add in battle
        /// (<see cref="BattleValue.StatPercent"/>): each grown once from the stats before any boon
        /// with the boons' percents and the added ones. With nothing added, the stats stay as they
        /// started; an ally without boons grows from the stats it started with.
        /// </summary>
        public static CharacterStats Grown(
            CharacterStats stats,
            CharacterStats unboosted,
            IReadOnlyList<float> percents,
            float physicalAttack,
            float magicAttack,
            float speed
        )
        {
            if (physicalAttack == 0f && magicAttack == 0f && speed == 0f)
                return stats;
            // 戦闘後の強化のない味方（強化の前の値がない）は、始めの値から伸ばす。
            bool boons = unboosted.Hp > 0;
            var from = boons ? unboosted : stats;
            float Before(int index) =>
                boons && percents != null && index < percents.Count ? percents[index] : 0f;
            var now = stats;
            now.PhysicalAttack = Grow(from.PhysicalAttack, Before(PhysicalAttack) + physicalAttack);
            now.MagicAttack = Grow(from.MagicAttack, Before(MagicAttack) + magicAttack);
            now.Speed = Grow(from.Speed, Before(Speed) + speed);
            return now;
        }

        /// <summary>
        /// The boost as an effect (doc/plans/2026-11-03-battle-effect-system.md): while its
        /// condition holds, it adds its percent to <see cref="BattleValue.StatPercent"/> of its
        /// stats, and its name shows when it starts to work or grows.
        /// </summary>
        public static BattleEffect EffectOf(BattleStatBoost boost)
        {
            var effect = new BattleEffect(
                "boost-" + boost.Condition + "-" + boost.Stats,
                boost.Name ?? ""
            );
            int[] stats =
                boost.Stats == BattleBoostStats.Speed
                    ? new[] { Speed }
                    : new[] { PhysicalAttack, MagicAttack };
            BattleAmount amount =
                boost.Condition == BattleBoostCondition.PerFallenAlly
                    ? BattleAmount.PerFallenAlly(boost.Percent)
                    : boost.Percent;
            var when = boost.Condition switch
            {
                BattleBoostCondition.HalfHpOrLess => BattleCondition.OwnerHpAtMost(50),
                BattleBoostCondition.FirstTurns => BattleCondition.TurnAtMost(boost.Turns),
                BattleBoostCondition.PerFallenAlly => BattleCondition.FallenAtLeast(1),
                _ => !BattleCondition.Always,
            };
            return effect
                .Add(BattleValue.StatPercent, amount)
                .When(BattleCondition.ActorIsOwner & BattleCondition.StatIs(stats) & when)
                .Shows(string.IsNullOrEmpty(boost.Name) ? BattleShow.Quiet : BattleShow.WhenGrows);
        }
    }
}
