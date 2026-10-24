using System.Collections.Generic;
using System.Linq;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 街道の世界地図のマスと道、進める範囲（doc/features/stage-progression.md#街道のマスと移動2026-10-24）。
    public sealed class WorldMapGraphTests
    {
        private static WorldMapGraph Graph() => WorldMapCatalog.Build();

        [Test]
        public void EveryTownCanBeReachedFromTheStart()
        {
            var graph = Graph();
            var reached = new HashSet<int> { graph.IndexOf(WorldMapCatalog.Start) };
            var queue = new Queue<int>(reached);
            while (queue.Count > 0)
                foreach (int next in graph.LinksOf(queue.Dequeue()))
                    if (reached.Add(next))
                        queue.Enqueue(next);
            Assert.That(reached.Count, Is.EqualTo(graph.Nodes.Count));
            Assert.That(
                WorldMapCatalog.Towns.Values.Select(town => graph.IndexOf(town.Id)),
                Has.None.EqualTo(-1)
            );
        }

        [Test]
        public void EveryLineIsStraightAcrossOrDown()
        {
            var graph = Graph();
            foreach (var (from, to) in graph.Lines)
            {
                var a = graph.Nodes[from].Point;
                var b = graph.Nodes[to].Point;
                Assert.That(
                    Mathf.Approximately(a.x, b.x) || Mathf.Approximately(a.y, b.y),
                    graph.Nodes[from].Id + " / " + graph.Nodes[to].Id
                );
            }
        }

        [Test]
        public void SquaresStayOnThePicture()
        {
            foreach (var node in Graph().Nodes)
            {
                Assert.That(node.Point.x, Is.InRange(20f, WorldMapCatalog.PictureSize.x - 20f));
                Assert.That(node.Point.y, Is.InRange(14f, WorldMapCatalog.PictureSize.y - 14f));
            }
        }

        [Test]
        public void TheWebHasLoopsAndDeadEnds()
        {
            var graph = Graph();
            // 輪があれば、線の数はマスの数より多い（木なら1少ない）。
            Assert.That(graph.Lines.Count, Is.GreaterThanOrEqualTo(graph.Nodes.Count));
            int deadEnds = Enumerable
                .Range(0, graph.Nodes.Count)
                .Count(i =>
                    graph.LinksOf(i).Count == 1 && graph.Nodes[i].Kind != WorldNodeKind.Town
                );
            Assert.That(deadEnds, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void NextToClearedIsOpenAndAnUnbeatenBossHidesWhatLiesBehind()
        {
            var graph = Graph();
            var states = graph.StatesOf(new[] { "porta" });
            Assert.That(states[graph.IndexOf("porta")], Is.EqualTo(WorldNodeState.Cleared));
            Assert.That(states[graph.IndexOf("n10-10")], Is.EqualTo(WorldNodeState.Open));
            Assert.That(states[graph.IndexOf("brezza")], Is.EqualTo(WorldNodeState.Seen));
            // 港町の手前の関所は見えるが、その先の港町は伏せる。
            Assert.That(states[graph.IndexOf("n4-8")], Is.EqualTo(WorldNodeState.Seen));
            Assert.That(states[graph.IndexOf("portoria")], Is.EqualTo(WorldNodeState.Hidden));
        }

        [Test]
        public void WalksOverClearedSquaresOnlyAndStopsAtAnOpenOne()
        {
            var graph = Graph();
            var states = graph.StatesOf(WorldMapCatalog.MockCleared.ToArray());
            int porta = graph.IndexOf("porta");

            var path = graph.PathTo(porta, graph.IndexOf("n4-8"), states);
            Assert.That(
                path.Select(index => graph.Nodes[index].Id),
                Is.EqualTo(
                    new[] { "porta", "n10-10", "n9-10", "n9-9", "n9-8", "brezza", "n6-8", "n4-8" }
                )
            );
            // 関所の先と、見えているだけのマスへは歩けない。
            Assert.That(graph.PathTo(porta, graph.IndexOf("portoria"), states), Is.Empty);
            Assert.That(graph.PathTo(porta, graph.IndexOf("n6-6"), states), Is.Not.Empty);
            Assert.That(graph.PathTo(porta, graph.IndexOf("n6-4"), states), Is.Empty);
        }

        [Test]
        public void LevelSignsSitOnThePicture()
        {
            var graph = Graph();
            Assert.That(graph.Levels.Count, Is.EqualTo(WorldMapCatalog.Levels.Count));
            foreach (var (_, point) in graph.Levels)
            {
                Assert.That(point.x, Is.InRange(0f, WorldMapCatalog.PictureSize.x));
                Assert.That(point.y, Is.InRange(0f, WorldMapCatalog.PictureSize.y));
            }
        }
    }
}
