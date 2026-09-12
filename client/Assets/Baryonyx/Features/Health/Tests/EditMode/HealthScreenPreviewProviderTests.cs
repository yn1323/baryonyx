#if UNITY_EDITOR || !UNITY_ANDROID
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthScreenPreviewProviderTests
    {
        [Test]
        public async Task PreviewWeekHasConsecutiveDatesAndExplicitSampleJson()
        {
            var now = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.FromHours(9));
            var provider = new HealthScreenPreviewProvider(() => now);
            var result = await provider.ReadRecentDaysAsync(CancellationToken.None);
            var days = HealthDaySnapshot.ParseWeek(result.RawJson);

            Assert.That(result.Status, Is.EqualTo(HealthReadStatus.Success));
            Assert.That(days.Count, Is.EqualTo(7));
            Assert.That(days[0].Day, Is.EqualTo("2026-09-13"));
            Assert.That(days[6].Day, Is.EqualTo("2026-09-07"));
            Assert.That(days[0].Steps, Is.EqualTo(6432));
            Assert.That(days[1].HasValue, Is.False);
            Assert.That(days[1].Steps, Is.Zero);
            Assert.That(days[2].HasValue, Is.True);
            Assert.That(days[2].Steps, Is.Zero);
            for (int i = 0; i < days.Count; i++)
            {
                Assert.That(days[i].Zone, Is.EqualTo("Asia/Tokyo"));
                Assert.That(JObject.Parse(days[i].Json).Value<bool>("sample"), Is.True);
                Assert.That(days[i].Steps, Is.EqualTo(result.Days[i].steps));
            }
            Assert.That(
                DateTimeOffset.Parse(result.Days[0].endAt, CultureInfo.InvariantCulture),
                Is.EqualTo(now)
            );
        }

        [Test]
        public async Task RefreshUsesTheCurrentJapaneseDateAcrossMidnight()
        {
            var now = new DateTimeOffset(2026, 9, 13, 14, 59, 0, TimeSpan.Zero);
            var provider = new HealthScreenPreviewProvider(() => now);
            var first = await provider.ReadRecentDaysAsync(CancellationToken.None);
            now = now.AddMinutes(2);
            var next = await provider.ReadRecentDaysAsync(CancellationToken.None);

            Assert.That(first.Days[0].day, Is.EqualTo("2026-09-13"));
            Assert.That(next.Days[0].day, Is.EqualTo("2026-09-14"));
            Assert.That(next.Days.Select(day => day.day).Distinct().Count(), Is.EqualTo(7));
        }

        [Test]
        public async Task PreviewCanOpenDetailsRefreshAndRestartWithoutCredentials()
        {
            var preview = new HealthScreenPreviewProvider();
            using var presenter = new HealthScreenPresenter(preview, preview);
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.Phase, Is.EqualTo(HealthScreenPhase.Ready));
            presenter.SelectDay(6);
            Assert.That(JObject.Parse(presenter.SelectedDay.Json).Value<bool>("sample"), Is.True);
            presenter.CloseDetails();
            await presenter.RefreshAsync();
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
            await presenter.SignOutAsync();
            Assert.That(presenter.Days, Is.Empty);
            Assert.That(presenter.SignedIn, Is.False);
            await presenter.SignInAsync();
            await presenter.ConnectAsync();
            Assert.That(presenter.Days.Count, Is.EqualTo(7));
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
