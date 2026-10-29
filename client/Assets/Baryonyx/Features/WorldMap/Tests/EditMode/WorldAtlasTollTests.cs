using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Networking;
using Baryonyx.WorldMap;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 入れ子の地図のマスを開くルーン（doc/features/world-atlas.md#マスを開くルーン）：
    // 払うルーンがいちばん少ない道を選ぶことと、1マスずつ、サーバーを待たずに払うこと。
    public sealed class WorldAtlasTollTests
    {
        [SetUp]
        [TearDown]
        public void Clean()
        {
            WorldAtlasSession.Reset();
            SaveQueue.Reset();
        }

        // 世界地図の3マスを、どれも300ルーンで覚えさせる。
        private static void Prices()
        {
            var layout = new WorldAtlasLayout { key = "world" };
            layout.nodes = new[]
            {
                new WorldAtlasLayout.Node { id = "world-18", price = 300 },
                new WorldAtlasLayout.Node { id = "world-19", price = 300 },
                new WorldAtlasLayout.Node { id = "world-20", price = 300 },
            };
            WorldAtlasSession.Register(layout);
        }

        // 1回ずつ待ってから答えるサーバー。決めたマスの支払いだけを断る。
        private sealed class SlowSource : IWorldAtlasSource
        {
            private readonly WorldAtlasMemorySource memory;
            public string Refuse;
            public readonly List<string> Sent = new();

            public SlowSource(int runes) => memory = new WorldAtlasMemorySource(runes, (map, square) => 300);

            public Task<WorldAtlasState> LoadAsync(CancellationToken token) => memory.LoadAsync(token);

            public async Task<int> OpenAsync(string requestId, string map, string square, CancellationToken token)
            {
                await Task.Delay(20, token);
                Sent.Add(square);
                if (square == Refuse)
                    throw new ServerApiException(500, "server_error");
                return await memory.OpenAsync(requestId, map, square, token);
            }
        }

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
        public async Task PayingShowsAtOnceAndTheServerConfirmsIt()
        {
            Prices();
            var server = new SlowSource(500);
            WorldAtlasSession.Source = server;
            await WorldAtlasSession.LoadAsync(default);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(500));
            var spent = new List<int>();
            WorldAtlasSession.Spent += spent.Add;

            // サーバーの答えを待たずに、開けて引いた残高を出す。
            var paid = WorldAtlasSession.Open("world", "world-18");
            Assert.That(paid.Paid, Is.True);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(200));
            Assert.That(WorldAtlasSession.IsOpened("world", "world-18"), Is.True);
            Assert.That(spent, Is.EqualTo(new[] { 300 }));
            Assert.That(server.Sent, Is.Empty, "Nothing has reached the server yet.");

            // 開けたマスは払わない。足りないマスは、サーバーへ送らずに断る。
            Assert.That(WorldAtlasSession.Open("world", "world-18").Paid, Is.True);
            var refused = WorldAtlasSession.Open("world", "world-19");
            Assert.That(refused.Paid, Is.False);
            Assert.That(refused.Message, Is.EqualTo(WorldAtlasSession.ShortMessage));
            Assert.That(WorldAtlasSession.IsOpened("world", "world-19"), Is.False);

            await SaveQueue.IdleAsync(default);
            Assert.That(server.Sent, Is.EqualTo(new[] { "world-18" }));
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(200), "The server's balance is the one shown.");
            Assert.That(WorldAtlasSession.IsOpened("world", "world-18"), Is.True);
        }

        [Test]
        public async Task ARefusedPaymentIsTakenBackWithThePaymentsAfterIt()
        {
            Prices();
            var server = new SlowSource(1000) { Refuse = "world-19" };
            WorldAtlasSession.Source = server;
            await WorldAtlasSession.LoadAsync(default);
            var refused = new List<(string Square, string Message)>();
            WorldAtlasSession.Refused += (map, square, message) => refused.Add((square, message));

            // 続けて3マス払う。どれもすぐに開き、待ち行列の順にサーバーへ送る。
            foreach (var square in new[] { "world-18", "world-19", "world-20" })
                Assert.That(WorldAtlasSession.Open("world", square).Paid, Is.True);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(100));

            await SaveQueue.IdleAsync(default);
            // 取り消した支払いは、待ち行列の自分の番が過ぎてから知らせる。
            for (int i = 0; i < 20 && refused.Count < 2; i++)
                await Task.Delay(10);
            // 2マス目で断られると、それと後のマスを取り消し、後のマスは送らない。
            Assert.That(server.Sent, Is.EqualTo(new[] { "world-18", "world-19" }));
            Assert.That(WorldAtlasSession.IsOpened("world", "world-18"), Is.True);
            Assert.That(WorldAtlasSession.IsOpened("world", "world-19"), Is.False);
            Assert.That(WorldAtlasSession.IsOpened("world", "world-20"), Is.False);
            Assert.That(WorldAtlasSession.Runes, Is.EqualTo(700));
            Assert.That(
                refused,
                Is.EquivalentTo(new[] { ("world-19", WorldAtlasSession.FailedMessage), ("world-20", (string)null) }),
                "The first refusal is told; the one taken back after it is not told again."
            );
        }
    }
}
