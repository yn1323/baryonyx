using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthSyncTests
    {
        private static HealthSession Session(string user = "user") =>
            new HealthSession(user, "token", DateTimeOffset.UtcNow.AddHours(1));

        private static HealthDay[] Days(bool hasValue = true, long steps = 100) =>
            Enumerable
                .Range(0, 7)
                .Select(_ => new HealthDay { hasValue = hasValue, steps = hasValue ? steps : 0 })
                .ToArray();

        [Test]
        public async Task PermissionDenialDoesNotUploadZeroSteps()
        {
            var provider = new Provider { Permission = HealthPermission.NotGranted };
            var api = new Api();
            using var service = new HealthSyncService(provider, api, "source");
            service.SetSession(Session());
            Assert.That(await service.SyncAsync(), Is.EqualTo(HealthSyncStatus.PermissionRequired));
            Assert.That(api.Saves, Is.Zero);
            provider.Permission = HealthPermission.Granted;
            Assert.That(await service.SyncAsync(), Is.EqualTo(HealthSyncStatus.Saved));
        }

        [TestCase(true, HealthSyncStatus.Saved)]
        [TestCase(false, HealthSyncStatus.NoValues)]
        public async Task UnknownPermissionSupportsIosAndPreservesMissingValues(
            bool hasValue,
            HealthSyncStatus expected
        )
        {
            var provider = new Provider
            {
                Permission = HealthPermission.Unknown,
                Result = new HealthReadResult(HealthReadStatus.Success, Days(hasValue, 0)),
            };
            var api = new Api();
            using var service = new HealthSyncService(provider, api, "source");
            service.SetSession(Session());
            Assert.That(await service.SyncAsync(), Is.EqualTo(expected));
            Assert.That(api.LastDays.All(d => d.hasValue == hasValue), Is.True);
            Assert.That(service.LastSavedAt, Is.Not.Null);
        }

        [Test]
        public async Task DuplicateSyncsShareOneOperationAndBackgroundCancelsIt()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<HealthReadResult>() };
            var api = new Api();
            using var service = new HealthSyncService(provider, api, "source");
            service.SetSession(Session());
            var first = service.SyncAsync();
            Assert.That(service.SyncAsync(), Is.SameAs(first));
            service.SetForeground(false);
            provider.Pending.SetResult(new HealthReadResult(HealthReadStatus.Success, Days()));
            Assert.That(await first, Is.EqualTo(HealthSyncStatus.Cancelled));
            Assert.That(api.Saves, Is.Zero);
        }

        [Test]
        public async Task LateReadAfterAccountSwitchIsDiscarded()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<HealthReadResult>() };
            var api = new Api();
            using var service = new HealthSyncService(provider, api, "source");
            service.SetSession(Session());
            var first = service.SyncAsync();
            service.SetSession(Session("other"));
            provider.Pending.SetResult(new HealthReadResult(HealthReadStatus.Success, Days()));
            await first;
            Assert.That(api.Saves, Is.Zero);
            Assert.That(service.Status, Is.EqualTo(HealthSyncStatus.Idle));
        }

        [Test]
        public async Task QuickResumeWaitsForCancellationThenStartsOneFreshSync()
        {
            var provider = new Provider { Pending = new TaskCompletionSource<HealthReadResult>() };
            var api = new Api();
            using var service = new HealthSyncService(provider, api, "source");
            service.SetSession(Session());
            var first = service.SyncAsync();
            service.SetForeground(false);
            service.SetForeground(true);
            var resumed = service.SyncAsync();
            Assert.That(service.SyncAsync(), Is.SameAs(resumed));
            provider.Pending.SetResult(new HealthReadResult(HealthReadStatus.Success, Days()));
            Assert.That(await first, Is.EqualTo(HealthSyncStatus.Cancelled));
            Assert.That(await resumed, Is.EqualTo(HealthSyncStatus.Saved));
            Assert.That(api.Leases, Is.EqualTo(2));
            Assert.That(api.Saves, Is.EqualTo(1));
        }

        [TestCase(HealthReadStatus.PermissionRequired, HealthSyncStatus.PermissionRequired)]
        [TestCase(HealthReadStatus.Unavailable, HealthSyncStatus.Unavailable)]
        [TestCase(HealthReadStatus.Failed, HealthSyncStatus.Failed)]
        public async Task UnsuccessfulReadDoesNotUpload(
            HealthReadStatus readStatus,
            HealthSyncStatus expected
        )
        {
            var api = new Api();
            using var service = new HealthSyncService(
                new Provider { Result = new HealthReadResult(readStatus) },
                api,
                "source"
            );
            service.SetSession(Session());
            Assert.That(await service.SyncAsync(), Is.EqualTo(expected));
            Assert.That(service.Status, Is.EqualTo(expected));
            Assert.That(api.Leases, Is.EqualTo(1));
            Assert.That(api.Saves, Is.Zero);
            Assert.That(service.LastSavedAt, Is.Null);
        }

        [TestCase(401, HealthSyncStatus.SignInRequired)]
        [TestCase(503, HealthSyncStatus.Failed)]
        public async Task FailedUploadDoesNotMarkSuccessfulSync(
            int statusCode,
            HealthSyncStatus expected
        )
        {
            using var service = new HealthSyncService(
                new Provider(),
                new Api { Failure = statusCode },
                "source"
            );
            service.SetSession(Session());
            Assert.That(await service.SyncAsync(), Is.EqualTo(expected));
            Assert.That(service.LastSavedAt, Is.Null);
        }

        [Test]
        public async Task SignedOutOrExpiredSessionDoesNotReadOrSend()
        {
            var api = new Api();
            using var service = new HealthSyncService(new Provider(), api, "source");
            Assert.That(await service.SyncAsync(), Is.EqualTo(HealthSyncStatus.SignInRequired));
            service.SetSession(
                new HealthSession("user", "token", DateTimeOffset.UtcNow.AddMinutes(-1))
            );
            Assert.That(await service.SyncAsync(), Is.EqualTo(HealthSyncStatus.SignInRequired));
            Assert.That(api.Leases, Is.Zero);
        }

        private sealed class Provider : IHealthDataProvider
        {
            public string ProviderId => "test";
            public HealthPermission Permission = HealthPermission.Granted;
            public HealthReadResult Result = new HealthReadResult(HealthReadStatus.Success, Days());
            public TaskCompletionSource<HealthReadResult> Pending;

            public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token) =>
                Task.FromResult(HealthAvailability.Available);

            public Task<HealthPermission> GetPermissionAsync(CancellationToken token) =>
                Task.FromResult(Permission);

            public Task<HealthPermission> RequestPermissionAsync(CancellationToken token) =>
                Task.FromResult(Permission);

            public Task<HealthReadResult> ReadRecentDaysAsync(CancellationToken token) =>
                Pending?.Task ?? Task.FromResult(Result);

            public void OpenSettings() { }
        }

        private sealed class Api : IHealthApi
        {
            public int Saves;
            public int Leases;
            public int Failure;
            public HealthDay[] LastDays;

            public Task<long> BeginSyncAsync(
                HealthSession session,
                string sourceId,
                string provider,
                CancellationToken token
            )
            {
                Leases++;
                return Task.FromResult(1L);
            }

            public Task SaveAsync(
                HealthSession session,
                string sourceId,
                long revision,
                HealthDay[] days,
                CancellationToken token
            )
            {
                if (Failure != 0)
                    throw new HealthApiException(Failure);
                Saves++;
                LastDays = days;
                return Task.CompletedTask;
            }
        }
    }
}
