using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // 街道の宝箱のマス（doc/features/stage-progression.md の街道の宝箱と街）を、実際のシーンで通す。
    // 進み具合は、ピーノの西の寄り道の宝箱の手前に立ったアプリ内の保存先から始める。
    public sealed class WorldMapChestTests
    {
        private const string Chest = "n8-5";
        private const string Before = "n9-5";

        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            WorldMapSession.Reset();
            WorldMapSession.Source = new WorldMapMemorySource(
                new WorldMapProgress(Before, WorldMapCatalog.MockCleared.Append(Before))
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            WorldMapSession.Reset();
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(WorldMapChestTests));
        }

        // 初めて着くと、宝箱の中身を冒険の宝箱と同じ札でまとめて見せる。札が閉じるまでマスを押しても歩かない。
        [UnityTest]
        public IEnumerator ReachingAChestShowsWhatItHeldOnce()
        {
            var map = default(WorldMapBootstrap);
            yield return SceneTests.LoadWorldMap(value => map = value);
            var view = map.View;
            int chest = view.Graph.IndexOf(Chest);
            int before = view.Graph.IndexOf(Before);
            Assert.That(view.States[chest], Is.EqualTo(WorldNodeState.Open));
            Assert.That(view.Tap(chest), Is.True);

            var reveal = view.Reveal;
            Assert.That(reveal, Is.Not.Null);
            yield return SceneTests.WaitUntil(
                () => reveal.Shown,
                10f,
                "The chest's plates did not show."
            );
            Assert.That(reveal.ShownCount, Is.EqualTo(WorldMapChests.Of(Chest).Count));
            Assert.That(view.Dialog.IsShown, Is.False);
            Assert.That(WorldMapSession.Known.Cleared, Has.Member(Chest));
            Assert.That(view.Opening, Is.True);
            Assert.That(view.Tap(before), Is.False);

            yield return SceneTests.WaitUntil(
                () => !view.Opening,
                10f,
                "The chest's plates did not close."
            );
            yield return SceneTests.WaitUntil(
                () => !reveal.Shown,
                2f,
                "The chest's plates stayed."
            );

            // クリアした宝箱へ戻っても、もう開けない。
            Assert.That(view.Tap(before), Is.True);
            yield return SceneTests.WaitUntil(() => !view.Walking, 10f, "The party did not stop.");
            Assert.That(view.Tap(chest), Is.True);
            yield return SceneTests.WaitUntil(() => !view.Walking, 10f, "The party did not stop.");
            yield return SceneTests.WaitForTask(view.SaveTask);
            Assert.That(reveal.Shown, Is.False);
            Assert.That(view.Opening, Is.False);
        }
    }
}
