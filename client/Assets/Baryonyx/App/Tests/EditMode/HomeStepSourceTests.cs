using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.Home;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // ACTパネルを押したときに同期できない場合（doc/features/startup-sync.md#homeのactパネル）。
    // 画面に前の値を残してトーストで知らせるのは HomePresenterStepTests が確かめる。
    public sealed class HomeStepSourceTests
    {
        private static readonly DateTime Today = new(2026, 10, 25);
        private Provider provider;
        private Server server;
        private Store store;
        private HomeStepSource source;

        [SetUp]
        public void SetUp()
        {
            provider = new Provider();
            server = new Server();
            store = new Store();
            source = new HomeStepSource(new HealthStepLink(provider, server, store, () => Today));
        }

        [Test]
        public async Task ADeclinedPermissionSavesAndClaimsNothing()
        {
            provider.Permission = HealthPermission.NotGranted;
            provider.Grants = false;

            var reading = await source.SyncAsync(CancellationToken.None);

            Assert.That(provider.Requests, Is.EqualTo(1), "未許可なら先に許可画面を開く。");
            Assert.That(reading.Result, Is.EqualTo(HomeStepResult.Unlinked));
            Assert.That(server.Saves, Is.Zero);
            Assert.That(server.Claims, Is.Zero);
        }

        [Test]
        public async Task WhenThePermissionScreenCannotOpenTheSettingsOpenInstead()
        {
            provider.Permission = HealthPermission.NotGranted;
            store.PermissionDenials = HealthStepLink.DenialsBeforeSettings;

            var reading = await source.SyncAsync(CancellationToken.None);

            Assert.That(provider.Requests, Is.Zero);
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            Assert.That(reading.Result, Is.EqualTo(HomeStepResult.SettingsOpened));
            Assert.That(server.Saves, Is.Zero);
            Assert.That(server.Claims, Is.Zero);
        }

        [Test]
        public async Task AFailedReadIsNeitherSavedNorClaimed()
        {
            provider.ReadStatus = HealthReadStatus.Failed;

            var reading = await source.SyncAsync(CancellationToken.None);

            Assert.That(reading.Result, Is.EqualTo(HomeStepResult.Failed));
            Assert.That(reading.Link, Is.EqualTo(HomeStepLink.Linked));
            Assert.That(server.Saves, Is.Zero);
            Assert.That(server.Claims, Is.Zero);
        }

        [Test]
        public void AFailedSaveReachesThePresenterWithoutClaiming()
        {
            server.FailSaves = true;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                source.SyncAsync(CancellationToken.None)
            );
            Assert.That(server.Claims, Is.Zero);
        }

        private sealed class Provider : IHealthStepProvider
        {
            public HealthPermission Permission = HealthPermission.Granted;
            public HealthReadStatus ReadStatus = HealthReadStatus.Success;
            public bool Grants = true;
            public int Requests;
            public int SettingsOpened;

            public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token) =>
                Task.FromResult(HealthAvailability.Available);

            public Task<HealthPermission> GetStepsPermissionAsync(CancellationToken token) =>
                Task.FromResult(Permission);

            public Task<HealthPermission> RequestStepsPermissionAsync(CancellationToken token)
            {
                Requests++;
                if (Grants)
                    Permission = HealthPermission.Granted;
                return Task.FromResult(Permission);
            }

            public Task<HealthReadResult> ReadRecentStepsAsync(CancellationToken token)
            {
                if (ReadStatus != HealthReadStatus.Success)
                    return Task.FromResult(new HealthReadResult(ReadStatus));
                var days = Enumerable
                    .Range(0, 7)
                    .Select(offset => new HealthDay
                    {
                        day = Today
                            .AddDays(offset - 6)
                            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        zone = "Asia/Tokyo",
                        hasValue = true,
                        steps = 1000 + offset,
                        stepsStatus = "success",
                    })
                    .ToArray();
                return Task.FromResult(new HealthReadResult(HealthReadStatus.Success, days));
            }

            public void OpenSettings() => SettingsOpened++;
        }

        private sealed class Server : IHealthStepServer
        {
            public bool FailSaves;
            public int Saves;
            public int Claims;

            public Task ConnectAsync(CancellationToken token) => Task.CompletedTask;

            public Task SaveAsync(HealthDay[] days, CancellationToken token)
            {
                if (FailSaves)
                    throw new InvalidOperationException("offline");
                Saves++;
                return Task.CompletedTask;
            }

            public Task<HealthDay[]> ReadAsync(CancellationToken token) =>
                Task.FromResult(Array.Empty<HealthDay>());

            public Task<long> ReadRunesAsync(CancellationToken token) => Task.FromResult(0L);

            public Task<HealthRuneClaim> ClaimRunesAsync(CancellationToken token)
            {
                Claims++;
                return Task.FromResult(new HealthRuneClaim(0, 0));
            }
        }

        private sealed class Store : IHealthLinkStore
        {
            public int PermissionDenials { get; set; }
        }
    }
}
