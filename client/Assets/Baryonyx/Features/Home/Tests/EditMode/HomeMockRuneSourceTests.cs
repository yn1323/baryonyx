using System;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.EditMode
{
    public sealed class HomeMockRuneSourceTests
    {
        [Test]
        public async Task EveryTapGrantsTheMockAmountOnTopOfTheMockBalance()
        {
            var steps = new Steps
            {
                Next = new HomeStepReading(
                    HomeStepResult.Updated,
                    HomeStepLink.Linked,
                    4000,
                    runes: 99,
                    grantedRunes: 99
                ),
            };
            var source = new HomeMockRuneSource(steps, 12480, 1340);

            var loaded = await source.LoadAsync(CancellationToken.None);
            Assert.That(loaded.Steps, Is.EqualTo(4000));
            Assert.That(loaded.Runes, Is.EqualTo(12480), "The server balance is not shown.");
            Assert.That(loaded.GrantedRunes, Is.Zero);

            var first = await source.SyncAsync(CancellationToken.None);
            var second = await source.SyncAsync(CancellationToken.None);
            Assert.That(first.GrantedRunes, Is.EqualTo(1340));
            Assert.That(first.Runes, Is.EqualTo(13820));
            Assert.That(second.Runes, Is.EqualTo(15160));
            Assert.That(steps.Syncs, Is.EqualTo(2), "Steps are still synced.");
        }

        [Test]
        public async Task RunesAreGrantedEvenWhenTheServerIsDown()
        {
            var steps = new Steps { Fail = true };
            var source = new HomeMockRuneSource(steps, 100, 50);

            LogAssert.Expect(LogType.Warning, "歩数を更新できませんでした。offline");
            var loaded = await source.LoadAsync(CancellationToken.None);
            Assert.That(loaded.Result, Is.EqualTo(HomeStepResult.Failed));
            Assert.That(loaded.Runes, Is.EqualTo(100));

            LogAssert.Expect(LogType.Warning, "歩数を同期できませんでした。offline");
            var synced = await source.SyncAsync(CancellationToken.None);
            Assert.That(synced.GrantedRunes, Is.EqualTo(50));
            Assert.That(synced.Runes, Is.EqualTo(150));
        }

        [Test]
        public async Task UnlinkedStepsGetTheLinkPromptInsteadOfRunes()
        {
            var steps = new Steps
            {
                Next = new HomeStepReading(HomeStepResult.Unlinked, HomeStepLink.Unlinked, 0),
            };
            var source = new HomeMockRuneSource(steps, 100, 50);

            var synced = await source.SyncAsync(CancellationToken.None);
            Assert.That(synced.Result, Is.EqualTo(HomeStepResult.Unlinked));
            Assert.That(synced.GrantedRunes, Is.Zero);
            Assert.That(synced.Runes, Is.EqualTo(100));
        }

        private sealed class Steps : IHomeStepSource
        {
            public HomeStepReading Next = new(HomeStepResult.Updated, HomeStepLink.Linked, 0);
            public bool Fail;
            public int Syncs;

            public Task<HomeStepReading> LoadAsync(CancellationToken token) =>
                Fail
                    ? Task.FromException<HomeStepReading>(new InvalidOperationException("offline"))
                    : Task.FromResult(Next);

            public Task<HomeStepReading> SyncAsync(CancellationToken token)
            {
                Syncs++;
                return LoadAsync(token);
            }
        }
    }
}
