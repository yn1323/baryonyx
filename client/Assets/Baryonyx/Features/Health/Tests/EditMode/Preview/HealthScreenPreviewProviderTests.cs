#if UNITY_EDITOR || !UNITY_ANDROID
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthScreenPreviewProviderTests
    {
        [Test]
        public async Task PreviewWeekHasConsecutiveDatesFromToday()
        {
            var now = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.FromHours(9));
            var provider = new HealthScreenPreviewProvider(() => now);
            var result = await provider.ReadRecentDaysAsync(CancellationToken.None);
            var days = result.Days;

            Assert.That(result.Status, Is.EqualTo(HealthReadStatus.Success));
            Assert.That(days.Length, Is.EqualTo(7));
            Assert.That(days[0].day, Is.EqualTo("2026-09-13"));
            Assert.That(days[6].day, Is.EqualTo("2026-09-07"));
            Assert.That(days[0].steps, Is.EqualTo(6432));
            Assert.That(days[1].hasValue, Is.False);
            Assert.That(days[1].steps, Is.Zero);
            Assert.That(days[2].hasValue, Is.True);
            Assert.That(days[2].steps, Is.Zero);
            Assert.That(days.Select(day => day.zone), Is.All.EqualTo("Asia/Tokyo"));
            Assert.That(
                DateTimeOffset.Parse(days[0].endAt, CultureInfo.InvariantCulture),
                Is.EqualTo(now)
            );
            Assert.That(
                DateTimeOffset.Parse(days[0].startAt, CultureInfo.InvariantCulture),
                Is.EqualTo(new DateTimeOffset(2026, 9, 13, 4, 0, 0, TimeSpan.FromHours(9)))
            );
        }

        [Test]
        public async Task RefreshSwitchesTheJapaneseDayAt4am()
        {
            // 日本時間9/14の3:59は、朝4時区切りではまだ9/13。
            var now = new DateTimeOffset(2026, 9, 13, 18, 59, 0, TimeSpan.Zero);
            var provider = new HealthScreenPreviewProvider(() => now);
            var first = await provider.ReadRecentDaysAsync(CancellationToken.None);
            now = now.AddMinutes(2);
            var next = await provider.ReadRecentDaysAsync(CancellationToken.None);

            Assert.That(first.Days[0].day, Is.EqualTo("2026-09-13"));
            Assert.That(next.Days[0].day, Is.EqualTo("2026-09-14"));
            Assert.That(next.Days.Select(day => day.day).Distinct().Count(), Is.EqualTo(7));
        }

        [Test]
        public void CanceledPreviewDoesNotReadTheClock()
        {
            bool clockRead = false;
            var preview = new HealthScreenPreviewProvider(() =>
            {
                clockRead = true;
                return DateTimeOffset.UtcNow;
            });
            Assert.Throws<OperationCanceledException>(() =>
                preview.ReadRecentDaysAsync(new CancellationToken(true))
            );
            Assert.That(clockRead, Is.False);
        }
    }
}
#endif
