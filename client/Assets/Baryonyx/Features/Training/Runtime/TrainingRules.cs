using System;

namespace Baryonyx.Training
{
    /// <summary>The runes levels cost (mock rule: Lv n → n+1 costs n × the cost per level).</summary>
    public static class TrainingRules
    {
        /// <summary>The runes to go from <paramref name="level"/> to the next level.</summary>
        public static long CostToNext(int level, int costPerLevel) =>
            (long)Math.Max(1, level) * Math.Max(1, costPerLevel);

        /// <summary>The runes to go from <paramref name="from"/> up to <paramref name="to"/>.</summary>
        public static long CostBetween(int from, int to, int costPerLevel)
        {
            long total = 0;
            for (int level = from; level < to; level++)
                total += CostToNext(level, costPerLevel);
            return total;
        }

        /// <summary>How many levels the runes buy from <paramref name="level"/>, up to the cap.</summary>
        public static int Affordable(int level, long runes, int maxLevel, int costPerLevel)
        {
            int count = 0;
            long left = runes;
            for (int next = level; next < maxLevel; next++)
            {
                left -= CostToNext(next, costPerLevel);
                if (left < 0)
                    break;
                count++;
            }
            return count;
        }
    }
}
