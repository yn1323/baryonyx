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

        // エリアマップの入口は地域に1つだけで、世界地図ではその地域のマスが街道の行き止まりになる
        // （地域を通り抜けずに、入るときだけ寄る）。
        [Test]
        public void EveryRegionHasOneEntranceAtADeadEnd()
        {
            var world = Load(WorldAtlasLayout.WorldKey).Build();
            foreach (var key in Regions)
            {
                Assert.That(
                    Load(key).nodes.Where(n => n.kind == "gate").Select(n => n.id),
                    Is.EqualTo(new[] { WorldAtlasLayout.GatePrefix + key }),
                    key
                );
                int region = world.IndexOf(WorldAtlasLayout.RegionPrefix + key);
                Assert.That(world.LinksOf(region), Has.Count.EqualTo(1), key);
            }
        }

        // マスの出来事は、何もないマスにだけ置き、道のりで2マス以上を空ける
        // （doc/features/world-atlas.md#マスに置いた出来事）。
        [Test]
        public void EventsSitOnEmptySquaresWithRoomBetween()
        {
            foreach (var key in new[] { WorldAtlasLayout.WorldKey, "marina" })
            {
                var layout = Load(key);
                var graph = layout.Build();
                var placed = new List<int>();
                for (int i = 0; i < layout.nodes.Length; i++)
                {
                    var node = layout.nodes[i];
                    if (string.IsNullOrEmpty(node.@event))
                        continue;
                    Assert.That(WorldAtlasLayout.EventOf(node.@event), Is.Not.Null, node.id);
                    Assert.That(node.kind, Is.EqualTo("empty"), node.id);
                    placed.Add(i);
                }
                Assert.That(placed, Is.Not.Empty, key);
                foreach (int at in placed)
                foreach (int next in graph.LinksOf(at))
                {
                    Assert.That(placed, Has.No.Member(next), key);
                    foreach (int beyond in graph.LinksOf(next))
                        if (beyond != at)
                            Assert.That(placed, Has.No.Member(beyond), key);
                }
            }
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
            Assert.That(nodes.Select(n => n.id), Does.Contain(WorldAtlasLayout.StartSquare));
        }
    }
}
