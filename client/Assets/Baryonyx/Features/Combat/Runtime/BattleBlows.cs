using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>How an enemy's blow is aimed and softened before it lands.</summary>
    public static class BattleBlows
    {
        /// <summary>How much weaker a paralysed enemy hits.</summary>
        public const float ParalysisCut = 0.25f;

        /// <summary>An enemy's blow of <paramref name="power"/>, weakened while it is paralysed.</summary>
        public static int Weakened(int power, bool paralysed) =>
            // 半分ちょうどは偶数へ丸める（UnityのMathf.RoundToIntと同じ）。
            paralysed ? (int)Math.Round(power * (1f - ParalysisCut)) : power;

        /// <summary>How much of <paramref name="damage"/> a block of <paramref name="block"/> takes.</summary>
        public static int Soaked(int block, int damage) => Math.Min(block, damage);

        /// <summary>
        /// The ally a blow aimed at <paramref name="target"/> lands on: the first of those
        /// <paramref name="standing"/> who taunts draws it. With none taunting, the first ally of
        /// the party is looked at, standing or not, as the battle has always done.
        /// </summary>
        public static int Taunted(int target, IReadOnlyList<int> standing, Func<int, bool> taunting)
        {
            int first = 0;
            foreach (int ally in standing)
                if (taunting(ally))
                {
                    first = ally;
                    break;
                }
            return taunting(first) ? first : target;
        }
    }
}
