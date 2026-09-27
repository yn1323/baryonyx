using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HomePresenterStepTests
    {
        private GameObject host;
        private HomeView view;
        private Source source;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("HomePresenterStepTests");
            view = host.AddComponent<HomeView>();
            source = new Source();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public async Task LoadingShowsSavedStepsAndRepeatedTapsSyncOnce()
        {
            using var presenter = new HomePresenter(view, Snapshot(), null, source);
            Assert.That(presenter.StepSyncing, Is.True);
            source.Load.SetResult(
                new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, 4321)
            );
            await presenter.StepTask;
            Assert.That(presenter.StepSyncing, Is.False);

            presenter.Handle(HomeAction.SyncSteps);
            presenter.Handle(HomeAction.SyncSteps);
            Assert.That(source.Syncs, Is.EqualTo(1));
            source.Sync.SetResult(
                new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, 5000)
            );
            await presenter.StepTask;
            Assert.That(presenter.StepSyncing, Is.False);
        }

        [Test]
        public async Task FailedSyncKeepsThePreviousSteps()
        {
            var snapshot = Snapshot();
            using var presenter = new HomePresenter(view, snapshot, null, source);
            source.Load.SetResult(
                new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, 1200)
            );
            await presenter.StepTask;

            presenter.Handle(HomeAction.SyncSteps);
            source.Sync.SetException(new InvalidOperationException("offline"));
            await presenter.StepTask;
            Assert.That(snapshot.Steps, Is.EqualTo(1200));
            Assert.That(snapshot.StepLink, Is.EqualTo(HomeStepLink.Linked));
            Assert.That(presenter.StepSyncing, Is.False);
        }

        [Test]
        public async Task StepsStayUnknownUntilTheFirstLoadSucceeds()
        {
            var snapshot = Snapshot();
            snapshot.Steps = 3820;
            using var presenter = new HomePresenter(view, snapshot, null, source);
            Assert.That(snapshot.StepsKnown, Is.False);
            Assert.That(HomeViewState.From(snapshot).WattsText, Is.EqualTo("--"));

            source.Load.SetException(new InvalidOperationException("offline"));
            await presenter.StepTask;
            Assert.That(snapshot.StepsKnown, Is.False, "A failed load must not reveal mock steps.");
            Assert.That(snapshot.Steps, Is.Zero);
        }

        [Test]
        public async Task RunesStayUnknownUntilLoadedAndFollowTheServerBalance()
        {
            var snapshot = Snapshot();
            snapshot.Runes = 12480;
            using var presenter = new HomePresenter(view, snapshot, null, source);
            Assert.That(snapshot.RunesKnown, Is.False);
            Assert.That(HomeViewState.From(snapshot).RunesText, Is.EqualTo("--"));

            source.Load.SetResult(
                new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, 100, runes: 500)
            );
            await presenter.StepTask;
            Assert.That(snapshot.RunesKnown, Is.True);
            Assert.That(snapshot.Runes, Is.EqualTo(500));

            presenter.Handle(HomeAction.SyncSteps);
            source.Sync.SetResult(
                new HomeStepReading(
                    HomeStepResult.Updated,
                    HomeStepLink.Linked,
                    400,
                    runes: 800,
                    grantedRunes: 300
                )
            );
            await presenter.StepTask;
            Assert.That(snapshot.Runes, Is.EqualTo(800));
        }

        [Test]
        public async Task FailedSyncKeepsTheKnownBalance()
        {
            var snapshot = Snapshot();
            using var presenter = new HomePresenter(view, snapshot, null, source);
            source.Load.SetResult(
                new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, 100, runes: 500)
            );
            await presenter.StepTask;

            presenter.Handle(HomeAction.SyncSteps);
            source.Sync.SetException(new InvalidOperationException("offline"));
            await presenter.StepTask;
            Assert.That(snapshot.RunesKnown, Is.True);
            Assert.That(snapshot.Runes, Is.EqualTo(500));
        }

        [Test]
        public async Task UpdatedStepsMoveHomeToTheirDay()
        {
            var snapshot = Snapshot();
            using var presenter = new HomePresenter(view, snapshot, null, source);
            var nextDay = snapshot.Today.AddDays(1);
            source.Load.SetResult(
                new HomeStepReading(HomeStepResult.Updated, HomeStepLink.Linked, 300, nextDay)
            );
            await presenter.StepTask;
            Assert.That(snapshot.Today, Is.EqualTo(nextDay));
            Assert.That(snapshot.Steps, Is.EqualTo(300));
            Assert.That(snapshot.StepsKnown, Is.True);
        }

        [Test]
        public void EveryStepResultHasAMessage()
        {
            foreach (HomeStepResult result in Enum.GetValues(typeof(HomeStepResult)))
                Assert.That(HomePresenter.MessageFor(result), Is.Not.Empty, result.ToString());
        }

        private static HomeSnapshot Snapshot() =>
            new() { Today = new DateTime(2026, 9, 24) };

        private sealed class Source : IHomeStepSource
        {
            public readonly TaskCompletionSource<HomeStepReading> Load = new();
            public readonly TaskCompletionSource<HomeStepReading> Sync = new();
            public int Syncs;

            public Task<HomeStepReading> LoadAsync(CancellationToken token) => Load.Task;

            public Task<HomeStepReading> SyncAsync(CancellationToken token)
            {
                Syncs++;
                return Sync.Task;
            }
        }
    }
}
