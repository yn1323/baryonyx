using System;
using Baryonyx.Health;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthWeekSummaryTests
    {
        private static HealthDaySnapshot[] Week(bool empty = false)
        {
            var json = new JArray();
            var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.FromHours(9));
            for (int i = 0; i < 7; i++)
            {
                var date = now.Date.AddDays(-i);
                bool has = !empty && i != 2;
                json.Add(
                    new JObject
                    {
                        ["day"] = date.ToString("yyyy-MM-dd"),
                        ["zone"] = "Asia/Tokyo",
                        ["startAt"] = new DateTimeOffset(date, now.Offset).ToString("O"),
                        ["endAt"] = new DateTimeOffset(date.AddDays(1), now.Offset).ToString("O"),
                        ["observedAt"] = now.ToString("O"),
                        ["hasValue"] = has,
                        ["steps"] = has ? i * 100 : 0,
                    }
                );
            }
            return HealthWeekSummary.Chronological(
                HealthDaySnapshot.ParseWeek(
                    new JObject { ["status"] = "success", ["days"] = json }.ToString()
                )
            );
        }

        [Test]
        public void BarsRunOldestToNewestAndDistinguishZeroFromMissing()
        {
            var days = Week();
            Assert.That(days[0].Day, Is.EqualTo("2026-09-13"));
            Assert.That(days[6].Day, Is.EqualTo("2026-09-19"));
            Assert.That(HealthWeekSummary.Total(days), Is.EqualTo(1900));
            Assert.That(HealthWeekSummary.Maximum(days), Is.EqualTo(600));
            Assert.That(HealthWeekSummary.Fraction(days[0], 600), Is.EqualTo(1));
            Assert.That(HealthWeekSummary.StepsLabel(days[6]), Is.EqualTo("0"));
            Assert.That(HealthWeekSummary.StepsLabel(days[4]), Is.EqualTo("記録なし"));
            Assert.That(HealthWeekSummary.Fraction(days[4], 600), Is.Zero);
        }

        [Test]
        public void EmptyWeekHasNoDivisionByZeroOrInventedSteps()
        {
            var days = Week(true);
            Assert.That(HealthWeekSummary.Maximum(days), Is.EqualTo(1));
            Assert.That(HealthWeekSummary.Total(days), Is.Zero);
            foreach (var day in days)
                Assert.That(HealthWeekSummary.Fraction(day, 0), Is.Zero);
            Assert.That(HealthWeekSummary.StepsLabel(null), Is.EqualTo("未取得"));
        }
    }
}
