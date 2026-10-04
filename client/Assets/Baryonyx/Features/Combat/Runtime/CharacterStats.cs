using System;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The eight stats a party member and an enemy both have (doc/features/party.md, the stats).
    /// The data holds them at level 100, and a level's stats are those times the growth rate
    /// (doc/features/progression.md, levels and stats).
    /// </summary>
    [Serializable]
    public struct CharacterStats
    {
        public const int Count = 8;

        // 表示の順。
        public static readonly string[] Labels =
        {
            "HP",
            "物攻",
            "物防",
            "属攻",
            "属防",
            "速度",
            "会心",
            "幸運",
        };

        public int Hp;
        public int PhysicalAttack;
        public int PhysicalDefense;
        public int MagicAttack;
        public int MagicDefense;
        public int Speed;
        public int Critical;
        public int Luck;

        public CharacterStats(
            int hp,
            int physicalAttack,
            int physicalDefense,
            int magicAttack,
            int magicDefense,
            int speed,
            int critical,
            int luck
        )
        {
            Hp = hp;
            PhysicalAttack = physicalAttack;
            PhysicalDefense = physicalDefense;
            MagicAttack = magicAttack;
            MagicDefense = magicDefense;
            Speed = speed;
            Critical = critical;
            Luck = luck;
        }

        public int this[int index] =>
            index switch
            {
                0 => Hp,
                1 => PhysicalAttack,
                2 => PhysicalDefense,
                3 => MagicAttack,
                4 => MagicDefense,
                5 => Speed,
                6 => Critical,
                7 => Luck,
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };

        /// <summary>These level-100 stats at <paramref name="level"/>.</summary>
        public CharacterStats At(int level) => Scaled(CombatFormula.GrowthRate(Math.Max(1, level)));

        /// <summary>Every stat times <paramref name="rate"/>, rounded half up and at least 1.</summary>
        public CharacterStats Scaled(double rate) =>
            new(
                Scale(Hp, rate),
                Scale(PhysicalAttack, rate),
                Scale(PhysicalDefense, rate),
                Scale(MagicAttack, rate),
                Scale(MagicDefense, rate),
                Scale(Speed, rate),
                Scale(Critical, rate),
                Scale(Luck, rate)
            );

        public static CharacterStats operator -(CharacterStats a, CharacterStats b) =>
            new(
                a.Hp - b.Hp,
                a.PhysicalAttack - b.PhysicalAttack,
                a.PhysicalDefense - b.PhysicalDefense,
                a.MagicAttack - b.MagicAttack,
                a.MagicDefense - b.MagicDefense,
                a.Speed - b.Speed,
                a.Critical - b.Critical,
                a.Luck - b.Luck
            );

        private static int Scale(int value, double rate) =>
            Math.Max(1, (int)Math.Floor(value * rate + 0.5));
    }
}
