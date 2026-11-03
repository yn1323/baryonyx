using System;

namespace Baryonyx.Combat
{
    /// <summary>The rounding the effects share: half away from zero, as the passives have it.</summary>
    public static class BattleMath
    {
        /// <summary><paramref name="value"/> grown by <paramref name="percent"/>, rounded half away from zero.</summary>
        public static int Grow(int value, int percent) =>
            percent == 0
                ? value
                : (int)Math.Round(value * (1 + percent / 100.0), MidpointRounding.AwayFromZero);

        /// <summary><paramref name="percent"/> of <paramref name="value"/>, rounded half away from zero.</summary>
        public static int Percent(int value, int percent) =>
            (int)Math.Round(value * percent / 100.0, MidpointRounding.AwayFromZero);

        /// <summary>
        /// <paramref name="value"/> times (1 + <paramref name="percent"/>/100) in single-precision,
        /// rounded half to even, as a status's multiplier has always been worked out
        /// (UnityEngine.Mathf.RoundToInt).
        /// </summary>
        public static int Scale(int value, int percent) =>
            percent == 0 ? value : (int)Math.Round(value * (1f + percent / 100f));

        /// <summary>True when <paramref name="hp"/> is above 0 and at most <paramref name="percent"/> of <paramref name="maxHp"/>.</summary>
        public static bool HpAtMost(int hp, int maxHp, int percent) =>
            hp > 0 && maxHp > 0 && hp * 100L <= maxHp * (long)percent;
    }
}
