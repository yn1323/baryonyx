using System.Collections.Generic;
using System.Linq;
using Baryonyx.WorldMap;
using Baryonyx.WorldMap.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 入れ子の地図の試作の、世界地図と地域のエリアマップの配置
    // （doc/plans/2026-10-27-world-map-rune-squares.md#2026-10-27地域の名前と細かく描いたエリアマップ）。
    public sealed class WorldAtlasLayoutTests
    {
        private static WorldAtlasLayout Load(string key)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                $"{WorldAtlasAssets.LayoutFolder}/{key}.json"
            );
            Assert.That(asset, Is.Not.Null, key);
            return WorldAtlasLayout.Parse(asset.text);
        }

        private static IEnumerable<string> Regions =>
            WorldAtlasAssets.Keys.Where(key => key != WorldAtlasLayout.WorldKey);

        [Test]
        public void EverySquareOfEveryMapCanBeReached()
        {
            foreach (var key in WorldAtlasAssets.Keys)
            {
                var graph = Load(key).Build();
                var reached = new HashSet<int> { 0 };
                var queue = new Queue<int>(reached);
                while (queue.Count > 0)
                    foreach (int next in graph.LinksOf(queue.Dequeue()))
                        if (reached.Add(next))
                            queue.Enqueue(next);
                Assert.That(reached.Count, Is.EqualTo(graph.Nodes.Count), key);
            }
        }

        [Test]
        public void SquaresStayOnTheirPicture()
        {
            foreach (var key in WorldAtlasAssets.Keys)
            {
                var layout = Load(key);
                foreach (var node in layout.nodes)
                {
                    Assert.That(node.x, Is.InRange(0f, layout.width), key + " " + node.id);
                    Assert.That(node.y, Is.InRange(0f, layout.height), key + " " + node.id);
                }
            }
        }

        [Test]
        public void EveryTownIsOnExactlyOneArea()
        {
            var towns = Regions
                .SelectMany(key => Load(key).nodes.Where(n => n.kind == "town"))
                .Select(n => n.id)
                .ToList();
            Assert.That(
                towns,
                Is.EquivalentTo(WorldMapCatalog.Towns.Values.Select(town => town.Id))
            );
        }

        // 入口は、つなぐ2つの地域のエリアマップに1つずつあり、世界地図でもその2つの地域が、
        // ほかの地域を通らない道でつながる。
        [Test]
        public void EveryGateJoinsTheTwoRegionsItNames()
        {
            var world = Load(WorldAtlasLayout.WorldKey).Build();
            var gates = new Dictionary<string, List<string>>();
            foreach (var key in Regions)
            foreach (var node in Load(key).nodes.Where(n => n.kind == "gate"))
            {
                if (!gates.TryGetValue(node.id, out var regions))
                    gates[node.id] = regions = new List<string>();
                regions.Add(key);
            }
            Assert.That(gates, Is.Not.Empty);
            foreach (var (gate, regions) in gates)
            {
                Assert.That(regions, Has.Count.EqualTo(2), gate);
                Assert.That(WorldAtlasLayout.OtherSide(gate, regions[0]), Is.EqualTo(regions[1]));
                Assert.That(
                    JoinedDirectly(
                        world,
                        WorldAtlasLayout.RegionPrefix + regions[0],
                        WorldAtlasLayout.RegionPrefix + regions[1]
                    ),
                    Is.True,
                    gate
                );
            }
        }

        // from から to まで、ほかの地域のマスを通らずに行けるか。
        private static bool JoinedDirectly(WorldMapGraph graph, string from, string to)
        {
            int start = graph.IndexOf(from);
            int goal = graph.IndexOf(to);
            var seen = new HashSet<int> { start };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
                foreach (int next in graph.LinksOf(queue.Dequeue()))
                {
                    if (next == goal)
                        return true;
                    if (graph.Nodes[next].Kind == WorldNodeKind.Region || !seen.Add(next))
                        continue;
                    queue.Enqueue(next);
                }
            return false;
        }

        [Test]
        public void TheWorldHasASquareForEveryRegionAndSquaresOnTheWay()
        {
            var nodes = Load(WorldAtlasLayout.WorldKey).nodes;
            Assert.That(
                nodes.Where(n => n.kind == "region").Select(n => n.id),
                Is.EquivalentTo(Regions.Select(key => WorldAtlasLayout.RegionPrefix + key))
            );
            Assert.That(nodes.Count(n => n.kind == "empty"), Is.GreaterThan(50));
        }
    }
}
