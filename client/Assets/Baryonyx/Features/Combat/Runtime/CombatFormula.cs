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

        /// <summary>The chance of a blow to land as a critical hit with no critical stat at all.</summary>
        public const double CriticalBaseChance = 0.05;

        /// <summary>The level-100 critical stat that adds 1% to the chance.</summary>
        public const double CriticalPerPercent = 40;

        /// <summary>The highest chance of a critical hit.</summary>
        public const double CriticalMaxChance = 0.5;

        /// <summary>A critical hit's damage is this many times a normal one's.</summary>
        public const double CriticalMultiplier = 1.5;

        /// <summary>
        /// The chance (0 to 1) that a blow of an attacker at <paramref name="level"/> with
        /// <paramref name="critical"/> lands as a critical hit (doc/features/combat.md, critical
        /// hits): 5% and 1% more for every 40 of the stat at level 100, up to 50%. The stat is
        /// brought back to its level-100 value first, so the chance does not grow with the level.
        /// </summary>
        public static double CriticalChance(int critical, int level)
        {
            double at100 = Math.Max(0, critical) / GrowthRate(Math.Max(1, level));
            return Math.Min(
                CriticalMaxChance,
                CriticalBaseChance + at100 / CriticalPerPercent / 100.0
            );
        }

        /// <summary>A blow's damage as a critical hit: 1.5 times, rounded half away from zero.</summary>
        public static int Critical(int damage) =>
            (int)Math.Round(damage * CriticalMultiplier, MidpointRounding.AwayFromZero);

        /// <summary>The level-100 luck gap that moves the chance of a status by 1%.</summary>
        public const double LuckPerPercent = 20;

        /// <summary>The lowest chance of a status that is not sure to land.</summary>
        public const int StatusMinPercent = 5;

        /// <summary>
        /// The chance in percent that a status of <paramref name="basePercent"/> lands
        /// (doc/features/combat.md, how easily statuses land): the base, plus the gap between the
        /// user's and the target's luck brought back to level 100 by the user's level, 1% for
        /// every 20, kept between 5% and 100%. A base of 100% or more starts sure, and only luck
        /// can lower it.
        /// </summary>
        public static int StatusChance(int basePercent, int userLuck, int userLevel, int targetLuck)
        {
            double gap = (userLuck - targetLuck) / GrowthRate(Math.Max(1, userLevel));
            int percent = (int)
                Math.Round(basePercent + gap / LuckPerPercent, MidpointRounding.AwayFromZero);
            return Math.Clamp(percent, StatusMinPercent, 100);
        }
    }

    /// <summary>
    /// The rolls of a battle's chances: whether a blow lands as a critical hit, and whether a
    /// status lands. Both roll at random; tests set them to a fixed outcome
    /// (<see cref="Fix"/>) so a battle plays the same every time.
    /// </summary>
    public static class BattleOdds
    {
        private static Func<double> criticalRoll;
        private static Func<double> statusRoll;
        private static readonly Random Shared = new();

        /// <summary>True when a blow with <paramref name="chance"/> (0 to 1) of a critical hit lands as one.</summary>
        public static bool Critical(double chance) =>
            chance > 0 && (criticalRoll?.Invoke() ?? Shared.NextDouble()) < chance;

        /// <summary>True when a status with <paramref name="percent"/>% of landing lands.</summary>
        public static bool Lands(int percent) =>
            percent >= 100
            || (percent > 0 && (statusRoll?.Invoke() ?? Shared.NextDouble()) * 100 < percent);

        /// <summary>Makes every roll come out the same: critical hits or none, statuses landing or missing.</summary>
        public static void Fix(bool critical, bool lands)
        {
            criticalRoll = () => critical ? 0 : 1;
            statusRoll = () => lands ? 0 : 1;
        }

        /// <summary>Rolls at random again.</summary>
        public static void Unfix()
        {
            criticalRoll = null;
            statusRoll = null;
        }

        /// <summary>
        /// Rolls at random from <paramref name="seed"/>: the same seed rolls the same chances in
        /// the same order, so an automatic battle plays the same again. <see cref="Unfix"/> goes
        /// back to the shared rolls.
        /// </summary>
        public static void Seed(int seed)
        {
            var random = new Random(seed);
            criticalRoll = random.NextDouble;
            statusRoll = random.NextDouble;
        }
    }
}
