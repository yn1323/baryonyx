using System;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The battle's shared formulas (doc/features/combat.md, how damage is worked out): how a
    /// defense softens a blow, with a defense constant that grows with the attacker's level as the
    /// stats do, so a fight between equal levels weighs the same at every level.
    /// </summary>
    public static class CombatFormula
    {
        /// <summary>The defense constant at level 100: a defense this high halves a blow.</summary>
        public const double DefenseConstantAt100 = 1600;

        /// <summary>
        /// The part of its level-100 value a stat has at <paramref name="level"/> (provisional:
        /// doc/features/progression.md, levels and stats): 0.1 ^ ((100 - level) / 99), 10% at
        /// level 1 and 100% at level 100.
        /// </summary>
        public static double GrowthRate(int level) => Math.Pow(0.1, (100 - level) / 99.0);

        /// <summary>The defense constant of an attacker at <paramref name="level"/> (about 200 at level 10).</summary>
        public static double DefenseConstant(int level) => DefenseConstantAt100 * GrowthRate(level);

        /// <summary>
        /// A blow of <paramref name="power"/> softened by the target's <paramref name="defense"/>
        /// (physical defense for a physical blow, magic defense for a spell): power × K / (K +
        /// defense), K being the defense constant of the attacker's level. Rounded down, and at
        /// least 1 for a blow with any power.
        /// </summary>
        public static int Defend(int power, int defense, int attackerLevel)
        {
            if (power <= 0)
                return 0;
            if (defense <= 0)
                return power;
            double k = DefenseConstant(attackerLevel);
            // A hair over the exact value keeps a whole result from rounding down past itself.
            int damage = (int)Math.Floor(power * k / (k + defense) + 1e-9);
            return Math.Max(1, damage);
        }
    }
}
