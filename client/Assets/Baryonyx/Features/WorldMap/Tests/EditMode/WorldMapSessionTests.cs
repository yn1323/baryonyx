using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.EditMode
{
    // 街道の進み具合の読み書き（doc/features/stage-progression.md の世界地図の形と試作）。
    public sealed class WorldMapSessionTests
    {
        private sealed class FakeSource : IWorldMapSource
        {
            public WorldMapProgress Kept = WorldMapProgress.Start;
            public bool Fail;
            public readonly List<(string, bool)> Moves = new();

            public Task<WorldMapProgress> LoadAsync(CancellationToken token) =>
                Task.FromResult(Kept);

            public Task<WorldMapProgress> MoveAsync(
                string squareId,
                bool clear,
                CancellationToken token
            )
            {
                Moves.Add((squareId, clear));
                if (Fail)
                    return Task.FromException<WorldMapProgress>(new InvalidOperationException());
                Kept = Kept.Moved(squareId, clear);
                return Task.FromResult(Kept);
            }
        }

        [SetUp]
        public void ResetSession()
        {
            WorldMapSession.Reset();
            SaveQueue.Reset();
        }

        [TearDown]
        public void Restore()
        {
            WorldMapSession.Reset();
            SaveQueue.Reset();
        }

        [Test]
        public async Task ReadsTheProgressTheServerKept()
        {
            var source = new FakeSource
            {
                Kept = new WorldMapProgress("n9-10", new[] { "n10-10", "n9-10" }),
            };
            WorldMapSession.Source = source;

            var progress = await WorldMapSession.LoadAsync(CancellationToken.None);
            Assert.That(progress.At, Is.EqualTo("n9-10"));
            Assert.That(progress.Cleared, Is.EqualTo(new[] { "porta", "n10-10", "n9-10" }));
            Assert.That(WorldMapSession.Known, Is.SameAs(progress));
        }

        [Test]
        public async Task KeepsAMoveAtOnceAndSavesIt()
        {
            var source = new FakeSource();
            WorldMapSession.Source = source;
            await WorldMapSession.LoadAsync(CancellationToken.None);

            var saving = WorldMapSession.MoveAsync("n10-10", clear: true);
            // 保存を待たずに、着いたマスとクリアを手元に残す。
            Assert.That(WorldMapSession.Known.At, Is.EqualTo("n10-10"));
            Assert.That(WorldMapSession.Known.Cleared, Does.Contain("n10-10"));
            Assert.That(await saving, Is.True);
            Assert.That(source.Moves, Is.EqualTo(new[] { ("n10-10", true) }));

            // クリアしたマスの上を戻るだけなら、クリアは増えない。
            Assert.That(await WorldMapSession.MoveAsync("porta", clear: false), Is.True);
            Assert.That(WorldMapSession.Known.At, Is.EqualTo("porta"));
            Assert.That(WorldMapSession.Known.Cleared, Is.EqualTo(new[] { "porta", "n10-10" }));
        }

        [Test]
        public async Task AFailedMoveGoesBackToWhatTheServerKept()
        {
            var source = new FakeSource { Fail = true };
            WorldMapSession.Source = source;
            await WorldMapSession.LoadAsync(CancellationToken.None);

            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(WorldMapSession.SaveFailedMessage)
            );
            Assert.That(await WorldMapSession.MoveAsync("n10-10", clear: true), Is.False);
            Assert.That(WorldMapSession.Known.At, Is.EqualTo("porta"));
            Assert.That(WorldMapSession.Known.Cleared, Is.EqualTo(new[] { "porta" }));
        }

        [Test]
        public async Task WithoutAServerUsesThePrototypesProgressWhileTheAppRuns()
        {
            var first = await WorldMapSession.LoadAsync(CancellationToken.None);
            Assert.That(first.Cleared, Is.EquivalentTo(WorldMapProgress.Mock.Cleared));

            await WorldMapSession.MoveAsync("n12-4", clear: true);
            var again = await WorldMapSession.LoadAsync(CancellationToken.None);
            Assert.That(again.At, Is.EqualTo("n12-4"));
            Assert.That(again.Cleared, Does.Contain("n12-4"));
        }

        [Test]
        public void TheServersAnswerAlwaysHasTheFirstTownCleared()
        {
            var progress = WorldMapServerSource.ToProgress(
                new WorldMapApiClient.State
                {
                    at = null,
                    cleared = new[] { "n10-10", "", "n10-10" },
                }
            );
            Assert.That(progress.At, Is.EqualTo("porta"));
            Assert.That(progress.Cleared, Is.EqualTo(new[] { "porta", "n10-10" }));
        }
    }
}
