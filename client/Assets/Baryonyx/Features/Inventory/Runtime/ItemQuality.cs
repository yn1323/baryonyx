using System;

namespace Baryonyx.Inventory
{
    /// <summary>
    /// The quality of one skill, piece of equipment or ACT bonus (doc/features/inventory.md, quality):
    /// where its multiplier falls in its kind's range, from ×1.00 to the kind's upper limit.
    /// Lowest first, so a higher quality compares greater.
    /// </summary>
    public enum ItemQuality
    {
        C,
        B,
        A,
        S,
    }

    /// <summary>
    /// The rules of quality, the same as the server's (server/src/shared/quality.ts): the top 10%
    /// of the range is S, the next 20% A, the next 30% B and the rest C, a multiplier right on a
    /// border going to the higher quality. Multipliers are compared in whole hundredths (×1.37 is
    /// 137), as the server keeps them, so both sides grade a multiplier the same way. Every number
    /// of an effect grows with the multiplier, each the way that helps its owner
    /// (doc/features/inventory.md, how the multiplier is applied).
    /// </summary>
    public static class Quality
    {
        /// <summary>The lowest multiplier: the kind's own effect, never weaker.</summary>
        public const float MinMultiplier = 1f;

        /// <summary>The upper limit every kind uses until each kind's own is decided (provisional).</summary>
        public const float DefaultMaxMultiplier = 1.5f;

        /// <summary>The lower edge of each quality, as a place in the range from 0 (×1.00) to 1 (the limit).</summary>
        private static readonly double[] Floors = { 0, 0.4, 0.7, 0.9 };

        // 小数の誤差で境目の下へ落ちないよう、わずかに足して比べる。
        private const double Epsilon = 1e-9;

        public static readonly ItemQuality[] All =
        {
            ItemQuality.C,
            ItemQuality.B,
            ItemQuality.A,
            ItemQuality.S,
        };

        /// <summary>A multiplier in whole hundredths (×1.37 is 137).</summary>
        public static int Percent(float multiplier) =>
            (int)Math.Round(multiplier * 100.0, MidpointRounding.AwayFromZero);

        /// <summary>The quality of <paramref name="multiplier"/> in a range up to <paramref name="max"/>.</summary>
        public static ItemQuality Of(float multiplier, float max)
        {
            int percent = Percent(multiplier);
            int maxPercent = Percent(max);
            if (maxPercent <= 100)
                return ItemQuality.C;
            double place = (percent - 100) / (double)(maxPercent - 100);
            for (int i = All.Length - 1; i > 0; i--)
                if (place + Epsilon >= Floors[i])
                    return All[i];
            return ItemQuality.C;
        }

        /// <summary>Where <paramref name="multiplier"/> falls in its range, from 0 (×1.00) to 1 (the limit).</summary>
        public static float Place(float multiplier, float max)
        {
            int maxPercent = Percent(max);
            if (maxPercent <= 100)
                return 0f;
            float place = (Percent(multiplier) - 100) / (float)(maxPercent - 100);
            return Math.Clamp(place, 0f, 1f);
        }

        /// <summary>The lowest and highest multipliers (whole hundredths) of <paramref name="quality"/> up to <paramref name="max"/>.</summary>
        public static (float Min, float Max) Range(ItemQuality quality, float max)
        {
            int maxPercent = Percent(max);
            int width = maxPercent - 100;
            int At(double floor) => (int)Math.Ceiling(100 + width * floor - Epsilon);
            int index = (int)quality;
            int low = At(Floors[index]);
            int high = quality == ItemQuality.S ? maxPercent : At(Floors[index + 1]) - 1;
            return (low / 100f, Math.Max(low, high) / 100f);
        }

        /// <summary>A multiplier in the middle of <paramref name="quality"/>'s range, for showing that quality.</summary>
        public static float Middle(ItemQuality quality, float max)
        {
            var (low, high) = Range(quality, max);
            return (float)Math.Round((low + high) / 2.0, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>The letter shown for a quality ("S").</summary>
        public static string Letter(ItemQuality quality) => quality.ToString();

        /// <summary>A quality from its letter as the server sends it ("S"); false for anything else.</summary>
        public static bool TryParse(string letter, out ItemQuality quality)
        {
            quality = ItemQuality.C;
            if (string.IsNullOrEmpty(letter) || letter.Length != 1)
                return false;
            foreach (var each in All)
            {
                if (Letter(each) == letter)
                {
                    quality = each;
                    return true;
                }
            }
            return false;
        }

        /// <summary>"×1.37".</summary>
        public static string Times(float multiplier) => $"×{Percent(multiplier) / 100f:0.00}";

        // 掛け算と割り算は、倍率を百分率の整数にしてから行う（×1.3 の float は 1.2999… のため）。

        /// <summary>An amount (damage, healing, block, a stat's change) grown by the multiplier.</summary>
        public static int Amount(int value, float multiplier) =>
            Round(value * (double)Percent(multiplier) / 100.0);

        /// <summary>A count of hits or turns grown by the multiplier, rounded (1 at ×1.5 is 2).</summary>
        public static int Count(int value, float multiplier) => Amount(value, multiplier);

        /// <summary>A chance in percent grown by the multiplier, up to 100%.</summary>
        public static int Chance(int percent, float multiplier) =>
            Math.Min(100, Amount(percent, multiplier));

        /// <summary>A cost lowered by the multiplier; a cost of 1 or more stays at 1 or more.</summary>
        public static int Cost(int cost, float multiplier) =>
            cost <= 0 ? cost : Math.Max(1, Divide(cost, multiplier));

        /// <summary>An amount against the owner (HP lost) lowered by the multiplier.</summary>
        public static int Adverse(int value, float multiplier) => Divide(value, multiplier);

        private static int Divide(int value, float multiplier) =>
            Round(value * 100.0 / Math.Max(100, Percent(multiplier)));

        private static int Round(double value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
