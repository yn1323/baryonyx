using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The numbers of an enemy's skills (doc/features/combat-enemy-behavior.md), the same for the
    /// battle screen and a battle without one (<see cref="BattleSimulation"/>): the power of its
    /// blow, the block, healing and regen it gives, and whom its blows and its care reach.
    /// </summary>
    public static class EnemyActs
    {
        /// <summary>The percent an enemy's attack up grows its blows by.</summary>
        public const int AttackUpPercent = 25;

        /// <summary>
        /// The power of an enemy's blow: <paramref name="percent"/>% of <paramref name="stat"/>,
        /// grown by its room (<paramref name="attackScale"/>), its attack up, the power it
        /// gathered, and weakened while it is paralysed. At least 1.
        /// </summary>
        public static int BlowPower(
            int stat,
            int percent,
            float attackScale,
            int? attackUp,
            int powerUp,
            bool paralysed
        )
        {
            float power = stat * percent / 100f * attackScale;
            if (attackUp.HasValue)
                power *= 1f + attackUp.Value / 100f;
            if (powerUp > 0)
                power *= 1f + powerUp / 100f;
            if (paralysed)
                power *= 1f - BattleBlows.ParalysisCut;
            return Math.Max(1, (int)Math.Round(power));
        }

        /// <summary>The block a skill gives: its part of <paramref name="stat"/>, grown by <paramref name="percent"/> while enraged. At least 1.</summary>
        public static int GuardBlock(int stat, int blockPercent, int percent) =>
            Math.Max(1, stat * blockPercent / 100 * percent / 100);

        /// <summary>The healing of a skill: its part of the max HP, or of the user's 属攻. At least 1.</summary>
        public static int Mend(EnemyMove move, int maxHp, int magicAttack) =>
            Math.Max(
                1,
                move.HealStat == CardStat.None
                    ? maxHp * move.HealPercent / 100
                    : magicAttack * move.HealPercent / 100
            );

        /// <summary>The healing each turn of a skill's regen. At least 1.</summary>
        public static int RegenPower(EnemyMove move, int magicAttack) =>
            Math.Max(1, magicAttack * move.RegenPercent / 100);

        /// <summary>
        /// The enemies a skill of <paramref name="care"/> by the enemy at <paramref name="index"/>
        /// reaches, among those standing (<paramref name="shares"/>: each enemy's share of its max
        /// HP, below 0 for one not standing).
        /// </summary>
        public static List<int> Cared(int index, EnemyCare care, IReadOnlyList<float> shares)
        {
            var others = new List<int>();
            for (int i = 0; i < shares.Count; i++)
                if (i != index && shares[i] > 0f)
                    others.Add(i);
            switch (care)
            {
                case EnemyCare.Others:
                    return others.Count > 0 ? others : new List<int> { index };
                case EnemyCare.Everyone:
                    others.Insert(0, index);
                    return others;
                case EnemyCare.WeakestOther:
                    if (others.Count == 0)
                        return new List<int> { index };
                    int weakest = others.Find(i =>
                        others.TrueForAll(other => shares[i] <= shares[other])
                    );
                    return new List<int> { weakest };
                default:
                    return new List<int> { index };
            }
        }

        /// <summary>
        /// The allies one hit of <paramref name="aim"/> lands on, among <paramref name="standing"/>:
        /// all of them, or one picked by <paramref name="pick"/> (a whole number below the one
        /// given), the weakest (<paramref name="share"/>) or the one with the most block; a
        /// taunting ally draws a blow on one.
        /// </summary>
        public static List<int> Aimed(
            EnemyAim aim,
            List<int> standing,
            Func<int, int> pick,
            Func<int, float> share,
            Func<int, int> block,
            Func<int, bool> taunting
        )
        {
            if (standing.Count == 0 || aim == EnemyAim.All)
                return standing;
            int target = standing[pick(standing.Count)];
            if (aim == EnemyAim.Weakest)
                target = standing.Find(i => standing.TrueForAll(other => share(i) <= share(other)));
            else if (aim == EnemyAim.MostBlock && standing.Exists(i => block(i) > 0))
                target = standing.Find(i => standing.TrueForAll(other => block(i) >= block(other)));
            return new List<int> { BattleBlows.Taunted(target, standing, taunting) };
        }
    }
}
