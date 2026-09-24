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
        public void EveryStepResultHasAMessage()
        {
            foreach (HomeStepResult result in Enum.GetValues(typeof(HomeStepResult)))
                Assert.That(HomePresenter.MessageFor(result), Is.Not.Empty, result.ToString());
        }

        private static HomeSnapshot Snapshot() =>
            new() { DailyGoal = 5000, Today = new DateTime(2026, 9, 24) };

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
