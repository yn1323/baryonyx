using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Health
{
    public static class HealthWeekSummary
    {
        public static HealthDaySnapshot[] Chronological(IReadOnlyList<HealthDaySnapshot> days) =>
            days.OrderBy(day => day.Day, StringComparer.Ordinal).ToArray();

        public static long Maximum(IReadOnlyList<HealthDaySnapshot> days) =>
            Math.Max(
                1,
                days.Where(day => day.HasValue).Select(day => day.Steps).DefaultIfEmpty(0).Max()
            );

        public static long Total(IReadOnlyList<HealthDaySnapshot> days) =>
            days.Where(day => day.HasValue).Sum(day => day.Steps);

        public static string StepsLabel(HealthDaySnapshot day) =>
            day == null ? "未取得"
            : day.HasValue ? $"{day.Steps:N0}"
            : day.StepsStatus == "permission_required" ? "未許可"
            : day.StepsStatus == "failed" ? "取得失敗"
            : "記録なし";

        public static float Fraction(HealthDaySnapshot day, long maximum) =>
            day != null && day.HasValue ? (float)((double)day.Steps / Math.Max(1, maximum)) : 0;
    }
}
