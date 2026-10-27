using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.UI;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 世界地図を開くとき、読み込むまで覆ったまま待ち、今いるマスへ置いてから左右のシャッターで開く。
    public sealed class WorldMapOpeningTests
    {
        private const string At = "brezza";

        private TestGameServices services;
        private SlowSource source;

        // 読み込みを、テストが返すまで待たせる。
        private sealed class SlowSource : IWorldMapSource
        {
            public readonly TaskCompletionSource<WorldMapProgress> Read = new();

            public Task<WorldMapProgress> LoadAsync(CancellationToken token) => Read.Task;

            public Task<WorldMapProgress> MoveAsync(
                string squareId,
                bool clear,
                CancellationToken token
            ) => Task.FromResult(new WorldMapProgress(squareId, WorldMapCatalog.MockCleared));
        }

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            WorldMapSession.Reset();
            source = new SlowSource();
            WorldMapSession.Source = source;
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            source?.Read.TrySetCanceled();
            WorldMapSession.Reset();
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(WorldMapOpeningTests));
        }

        [UnityTest]
        public IEnumerator TheMapStaysCoveredUntilItKnowsWhereThePartyIs()
        {
            yield return SceneManager.LoadSceneAsync(SceneTests.WorldMapPath, LoadSceneMode.Single);
            var map = Object.FindAnyObjectByType<WorldMapBootstrap>();
            Assert.That(map, Is.Not.Null);
            for (int i = 0; i < 10; i++)
                yield return null;
            Assert.That(map.View.Loaded, Is.False);
            Assert.That(map.Transition.IsCovered, Is.True);
            Assert.That(map.Transition.IsPlaying, Is.False);

            source.Read.SetResult(new WorldMapProgress(At, WorldMapCatalog.MockCleared));
            yield return SceneTests.WaitUntil(() => map.Transition.IsPlaying);
            Assert.That(map.View.PartyAt, Is.EqualTo(map.View.Graph.IndexOf(At)));
            var enter = map.Transition.EnterSettings;
            Assert.That(enter.Type, Is.EqualTo(SceneTransitionType.Shutter));
            Assert.That(enter.ShutterAxis, Is.EqualTo(SceneTransitionShutterAxis.Horizontal));

            yield return SceneTests.WaitUntil(() => SceneTests.WorldMapReady(map));
        }
    }
}
