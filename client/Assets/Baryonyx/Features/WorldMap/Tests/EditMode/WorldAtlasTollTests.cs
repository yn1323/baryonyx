using System.Collections.Generic;
using System.Threading.Tasks;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 入れ子の地図のマスを開くルーン（doc/features/world-atlas.md#マスを開くルーン）：
    // 払うルーンがいちばん少ない道を選ぶことと、1マスずつ払うこと。
    public sealed class WorldAtlasTollTests
    {
        [SetUp]
        [TearDown]
        public void Clean() => WorldAtlasSession.Reset();

        // a─b─d と a─c─e─d のひし形。d へは、b が高ければ遠回りのほうが安い。
        private static WorldMapGraph Diamond()
        {
            var graph = new WorldMapGraph();
            foreach (var id in new[] { "a", "b", "c", "d", "e" })
                graph.Add(id, WorldNodeKind.Empty, Vector2.zero, "");
            graph.Link("a", "b");
            graph.Link("b", "d");
            graph.Link("a", "c");
            graph.Link("c", "e");
            graph.Link("e", "d");
            return graph;
        }

        private static List<string> Ids(WorldMapGraph graph, List<int> path) =>
            path.ConvertAll(i => graph.Nodes[i].Id);

        [Test]
        public void TheCheapestWayWinsAndFewerSquaresBreakATie()
        {
            var graph = Diamond();
            var states = graph.StatesOf(new[] { "a" });
            var prices = new Dictionary<string, int> { ["b"] = 900, ["c"] = 200, ["d"] = 200, ["e"] = 200 };
            int Cost(int i) => prices.TryGetValue(graph.Nodes[i].Id, out int p) ? p : 0;
            Assert.That(
                Ids(graph, graph.CheapestPathTo(0, graph.IndexOf("d"), states, Cost)),
                Is.EqualTo(new[] { "a", "c", "e", "d" })
            );
            // 同じ値段なら、マスの少ない道。
            prices["b"] = 200;
            prices["c"] = 0;
            prices["e"] = 200;
            Assert.That(
                Ids(graph, graph.CheapestPathTo(0, graph.IndexOf("d"), states, Cost)),
                Is.EqualTo(new[] { "a", "b", "d" })
            );
            Assert.That(graph.CheapestPathTo(0, 0, states, Cost), Is.Empty);
        }

        [Test]
        public async Task PayingTakesTheRunesOnceAndRefusesWhenShort()
        {
            WorldAtlasSession.Source = new WorldAtlasMemorySource(500, (map, square) => 300);
            await WorldAtlasSession.LoadAsync(default);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(500));

            var paid = await WorldAtlasSession.OpenAsync("world", "world-18");
            Assert.That(paid.Paid, Is.True);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(200));
            Assert.That(WorldAtlasSession.IsOpened("world", "world-18"), Is.True);

            // 開けたマスは払わない。足りないマスは開けず、足りないと知らせる。
            Assert.That((await WorldAtlasSession.OpenAsync("world", "world-18")).Paid, Is.True);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(200));
            var refused = await WorldAtlasSession.OpenAsync("world", "world-19");
            Assert.That(refused.Paid, Is.False);
            Assert.That(refused.Message, Is.EqualTo(WorldAtlasSession.ShortMessage));
            Assert.That(WorldAtlasSession.IsOpened("world", "world-19"), Is.False);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(200));
        }
    }
}
