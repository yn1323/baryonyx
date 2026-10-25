using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.EditMode
{
    // 旅の案内所から冒険を始められなかったとき（doc/features/screens/adventure.md の出発）。
    public sealed class TravelDepartureTests
    {
        private const string PortPortoria = "port-portoria";

        private readonly List<string> notices = new();
        private AdventureLocalSource local;
        private FlakySource source;
        private int explored;
        private TravelDeparture departure;

        [SetUp]
        public void Open()
        {
            SaveQueue.Reset();
            AdventureSession.Reset();
            notices.Clear();
            explored = 0;
            local = new AdventureLocalSource(seed: 1);
            source = new FlakySource(local);
            departure = new TravelDeparture(
                notices.Add,
                source,
                () =>
                {
                    explored++;
                    return true;
                }
            );
            // 端末内の冒険はすぐに答えるため、開いたときの読み込みはここで済んでいる。
            Assert.That(departure.Work.IsCompleted, Is.True);
        }

        [TearDown]
        public void Close()
        {
            departure.Dispose();
            SaveQueue.Reset();
            AdventureSession.Reset();
        }

        [Test]
        public async Task AnAdventureStartedElsewhereTellsToResumeFromHome()
        {
            // 案内所を開いたあとに、ほかの端末で冒険を始めた。サーバーは409で断る。
            await local.StartAsync(PortPortoria, CancellationToken.None);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("冒険を始められませんでした")
            );

            Assert.That(departure.Depart(AdventureCatalog.ForestRuins), Is.True);
            await departure.Work;

            Assert.That(notices, Is.EqualTo(new[] { TravelDeparture.InProgressMessage }));
            Assert.That(explored, Is.Zero);
        }

        // 解放していない行き先は、冒険の途中ではないので、ホームからの再開を勧めない。
        [Test]
        public async Task ALockedDestinationDoesNotTellToResumeFromHome()
        {
            source.NextStartFailure = new ServerApiException(409, "destination_locked");
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(TravelDeparture.StartFailedMessage)
            );

            departure.Depart(AdventureCatalog.ForestRuins);
            await departure.Work;

            Assert.That(notices, Is.EqualTo(new[] { TravelDeparture.StartFailedMessage }));
            Assert.That(explored, Is.Zero);
        }

        [Test]
        public async Task AStartThatFailedCanBePressedAgain()
        {
            source.NextStartFailure = new ServerApiException(0);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("冒険を始められませんでした")
            );

            departure.Depart(AdventureCatalog.ForestRuins);
            await departure.Work;
            Assert.That(notices, Is.EqualTo(new[] { TravelDeparture.StartFailedMessage }));
            Assert.That(explored, Is.Zero);

            departure.Depart(AdventureCatalog.ForestRuins);
            await departure.Work;
            Assert.That(explored, Is.EqualTo(1));
            Assert.That(AdventureSession.Current.InProgress, Is.True);
        }

        [Test]
        public async Task TheStartWaitsForEarlierSavesEvenWhenOneFails()
        {
            var gate = new TaskCompletionSource<bool>();
            var earlier = SaveQueue.Enqueue<bool>(async _ =>
            {
                await gate.Task;
                throw new InvalidOperationException("offline");
            });

            departure.Depart(AdventureCatalog.ForestRuins);
            // 先に出した保存が済むまで、冒険を始めない。
            Assert.That(source.Starts, Is.Zero);

            gate.SetResult(true);
            await departure.Work;
            Assert.That(earlier.IsFaulted, Is.True);
            Assert.That(source.Starts, Is.EqualTo(1));
            Assert.That(explored, Is.EqualTo(1));
        }

        // 冒険を始める知らせだけを、1回失敗させられる代役。ほかは端末内の冒険へ渡す。
        private sealed class FlakySource : IAdventureSource
        {
            private readonly AdventureLocalSource inner;

            public FlakySource(AdventureLocalSource inner) => this.inner = inner;

            public Exception NextStartFailure;
            public int Starts;

            public Task<AdventureState> LoadAsync(CancellationToken token) =>
                inner.LoadAsync(token);

            public Task<AdventureState> StartAsync(string destinationId, CancellationToken token)
            {
                Starts++;
                var failure = NextStartFailure;
                NextStartFailure = null;
                return failure == null
                    ? inner.StartAsync(destinationId, token)
                    : Task.FromException<AdventureState>(failure);
            }

            public Task<AdventureState> MoveAsync(AdventureRoom room, CancellationToken token) =>
                inner.MoveAsync(room, token);

            public Task<AdventureState> ClearAsync(
                string roomId,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null,
                AdventureClearOptions options = null
            ) => inner.ClearAsync(roomId, token, enemies, options);

            public Task<AdventureState> OpenAsync(string roomId, CancellationToken token) =>
                inner.OpenAsync(roomId, token);

            public Task<AdventureState> BuyAsync(
                string roomId,
                int index,
                CancellationToken token
            ) => inner.BuyAsync(roomId, index, token);

            public Task<AdventureState> ReviveAsync(string roomId, CancellationToken token) =>
                inner.ReviveAsync(roomId, token);

            public Task<AdventureState> EndAsync(
                AdventureEndReason reason,
                CancellationToken token,
                IReadOnlyList<AdventureEnemyTally> enemies = null
            ) => inner.EndAsync(reason, token, enemies);
        }
    }
}
