using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Health;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HealthStartupFlowTests
    {
        private static readonly DateTime Today = new(2026, 9, 24);
        private Provider provider;
        private Server server;
        private Store store;
        private HealthStepLink link;
        private HealthStartupFlow flow;

        [SetUp]
        public void SetUp()
        {
            provider = new Provider();
            server = new Server();
            store = new Store();
            link = new HealthStepLink(provider, server, store, () => Today);
            flow = new HealthStartupFlow(link);
        }

        [Test]
        public async Task LinkedStartupConnectsSyncsAndBecomesReady()
        {
            await flow.StartAsync(CancellationToken.None);

            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Ready));
            Assert.That(server.Connects, Is.EqualTo(1));
            Assert.That(server.Saved, Has.Length.EqualTo(7));
            Assert.That(provider.Requests, Is.Zero);
            Assert.That(await flow.ConfirmStartAsync(CancellationToken.None), Is.True);
            Assert.That(server.Saves, Is.EqualTo(1), "Starting does not sync twice.");
        }

        [Test]
        public async Task ServerFailureCanBeRetried()
        {
            server.Fail = true;
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Failed));
            Assert.That(flow.FailureMessage, Is.EqualTo("通信に失敗しました"));
            Assert.That(await flow.ConfirmStartAsync(CancellationToken.None), Is.False);

            server.Fail = false;
            await flow.RetryAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Ready));
            Assert.That(flow.FailureMessage, Is.Empty);
        }

        [Test]
        public async Task ReadFailureIsRetryableWithItsOwnMessage()
        {
            provider.ReadStatus = HealthReadStatus.Failed;
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Failed));
            Assert.That(flow.FailureMessage, Is.EqualTo("歩数を読み取れませんでした"));
            Assert.That(server.Saves, Is.Zero);
        }

        [Test]
        public async Task FailedDayIsNotSavedAsMissingSteps()
        {
            provider.TodayStepsStatus = "failed";
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Failed));
            Assert.That(flow.FailureMessage, Is.EqualTo("歩数を読み取れませんでした"));
            Assert.That(server.Saves, Is.Zero);
        }

        [Test]
        public async Task DayWithoutStepsPermissionReturnsToTheModal()
        {
            provider.TodayStepsStatus = "permission_required";
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.LinkRequired));
            Assert.That(flow.LinkStatus, Is.EqualTo(HealthLinkStatus.PermissionRequired));
            Assert.That(server.Saves, Is.Zero);
        }

        [Test]
        public async Task TwoDeclinedRequestsSwitchToOpeningSettings()
        {
            provider.Permission = HealthPermission.NotGranted;
            provider.Grants = false;
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.LinkRequired));
            Assert.That(flow.LinkStatus, Is.EqualTo(HealthLinkStatus.PermissionRequired));

            await flow.LinkAsync(CancellationToken.None);
            Assert.That(flow.LinkStatus, Is.EqualTo(HealthLinkStatus.PermissionRequired));
            await flow.LinkAsync(CancellationToken.None);
            Assert.That(flow.LinkStatus, Is.EqualTo(HealthLinkStatus.SettingsRequired));
            Assert.That(provider.Requests, Is.EqualTo(2));

            // 以降はOSの許可画面を要求せず、設定を開いて復帰時に確認し直す。
            await flow.LinkAsync(CancellationToken.None);
            Assert.That(provider.Requests, Is.EqualTo(2));
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            provider.Permission = HealthPermission.Granted;
            await flow.ResumeAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Ready));
            Assert.That(store.PermissionDenials, Is.Zero);
            Assert.That(server.Saves, Is.EqualTo(1));
        }

        [Test]
        public async Task ResumeWithoutOpeningSettingsDoesNothing()
        {
            provider.Permission = HealthPermission.NotGranted;
            await flow.StartAsync(CancellationToken.None);
            int checks = provider.PermissionChecks;
            await flow.ResumeAsync(CancellationToken.None);
            Assert.That(provider.PermissionChecks, Is.EqualTo(checks));
        }

        [TestCase(HealthAvailability.Unavailable, HealthLinkStatus.InstallRequired)]
        [TestCase(HealthAvailability.UpdateRequired, HealthLinkStatus.UpdateRequired)]
        public async Task MissingHealthConnectAsksToInstallOrUpdate(
            HealthAvailability availability,
            HealthLinkStatus expected
        )
        {
            provider.Availability = availability;
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.LinkStatus, Is.EqualTo(expected));
            await flow.LinkAsync(CancellationToken.None);
            Assert.That(provider.SettingsOpened, Is.EqualTo(1));
            Assert.That(provider.Requests, Is.Zero);
        }

        [Test]
        public async Task LaterAllowsStartingWithoutAskingAgain()
        {
            provider.Permission = HealthPermission.NotGranted;
            await flow.StartAsync(CancellationToken.None);
            flow.Dismiss();
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.Ready));
            Assert.That(flow.Dismissed, Is.True);
            Assert.That(await flow.ConfirmStartAsync(CancellationToken.None), Is.True);
            Assert.That(server.Saves, Is.Zero);
        }

        [Test]
        public async Task StartRechecksPermissionAndSyncsWhenGrantedLater()
        {
            provider.Permission = HealthPermission.NotGranted;
            await flow.StartAsync(CancellationToken.None);
            flow.Dismiss();

            provider.Permission = HealthPermission.Granted;
            Assert.That(await flow.ConfirmStartAsync(CancellationToken.None), Is.True);
            Assert.That(server.Saves, Is.EqualTo(1));
        }

        [Test]
        public async Task RevokedPermissionShowsTheModalAgainOnStart()
        {
            await flow.StartAsync(CancellationToken.None);
            provider.Permission = HealthPermission.NotGranted;
            Assert.That(await flow.ConfirmStartAsync(CancellationToken.None), Is.False);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.LinkRequired));
        }

        [Test]
        public async Task PermissionRevokedDuringReadReturnsToTheModal()
        {
            provider.ReadStatus = HealthReadStatus.PermissionRequired;
            await flow.StartAsync(CancellationToken.None);
            Assert.That(flow.Phase, Is.EqualTo(HealthStartupPhase.LinkRequired));
            Assert.That(flow.LinkStatus, Is.EqualTo(HealthLinkStatus.PermissionRequired));
            Assert.That(server.Saves, Is.Zero);
        }

        [Test]
        public async Task TodayStepsComeFromTheSavedDayOfToday()
        {
            Assert.That((await link.ReadTodayAsync(CancellationToken.None)).HasValue, Is.False);
            await link.SyncAsync(CancellationToken.None);
            var today = await link.ReadTodayAsync(CancellationToken.None);
            Assert.That(today.HasValue, Is.True);
            Assert.That(today.Steps, Is.EqualTo(1006));
        }

        [Test]
        public void EveryLinkStatusHasModalText()
        {
            foreach (HealthLinkStatus status in Enum.GetValues(typeof(HealthLinkStatus)))
            {
                var message = HealthLinkMessage.For(status);
                Assert.That(message.Body, Is.Not.Empty, status.ToString());
                Assert.That(message.Action, Is.Not.Empty, status.ToString());
            }
            Assert.That(
                HealthLinkMessage.For(HealthLinkStatus.SettingsRequired).Action,
                Is.EqualTo("Health Connectの設定を開く")
            );
        }

        private sealed class Provider : IHealthStepProvider
        {
            public HealthAvailability Availability = HealthAvailability.Available;
            public HealthPermission Permission = HealthPermission.Granted;
            public string TodayStepsStatus = "success";
            public HealthReadStatus ReadStatus = HealthReadStatus.Success;
            public bool Grants = true;
            public int Requests;
            public int PermissionChecks;
            public int SettingsOpened;

            public Task<HealthAvailability> GetAvailabilityAsync(CancellationToken token) =>
                Task.FromResult(Availability);

            public Task<HealthPermission> GetStepsPermissionAsync(CancellationToken token)
            {
                PermissionChecks++;
                return Task.FromResult(Permission);
            }

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
                        stepsStatus = offset == 6 ? TodayStepsStatus : "success",
                    })
                    .ToArray();
                return Task.FromResult(new HealthReadResult(HealthReadStatus.Success, days));
            }

            public void OpenSettings() => SettingsOpened++;
        }

        private sealed class Server : IHealthStepServer
        {
            public bool Fail;
            public int Connects;
            public int Saves;
            public HealthDay[] Saved = Array.Empty<HealthDay>();

            public Task ConnectAsync(CancellationToken token)
            {
                if (Fail)
                    throw new InvalidOperationException("offline");
                Connects++;
                return Task.CompletedTask;
            }

            public Task SaveAsync(HealthDay[] days, CancellationToken token)
            {
                if (Fail)
                    throw new InvalidOperationException("offline");
                Saves++;
                Saved = days;
                return Task.CompletedTask;
            }

            public Task<HealthDay[]> ReadAsync(CancellationToken token) => Task.FromResult(Saved);
        }

        private sealed class Store : IHealthLinkStore
        {
            public int PermissionDenials { get; set; }
        }
    }
}
