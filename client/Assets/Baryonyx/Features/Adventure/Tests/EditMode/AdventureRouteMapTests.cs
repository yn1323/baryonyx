using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 冒険の種から作る分岐ルート（doc/plans/2026-10-04-exploration-route-map.md）と、その地図。
    public sealed class AdventureRouteMapTests
    {
        private const int RoomCount = 8;

        private static IEnumerable<AdventureRouteMap> Routes(int count) =>
            Enumerable
                .Range(1, count)
                .Select(seed => AdventureRouteMap.Generate(seed * 7919, RoomCount));

        private static string Signature(AdventureRouteMap map) =>
            string.Join(
                "|",
                map.Rooms.Select(room => $"{room.Id}:{room.Kind}>{string.Join(",", room.Next)}")
            );

        [Test]
        public void TheSameSeedMakesTheSameRoute()
        {
            var first = AdventureRouteMap.Generate(20261004, RoomCount);
            var again = AdventureRouteMap.Generate(20261004, RoomCount);
            Assert.That(Signature(again), Is.EqualTo(Signature(first)));
            foreach (var room in first.Rooms)
            {
                Assert.That(again.PointOf(room.Id).T, Is.EqualTo(first.PointOf(room.Id).T));
                Assert.That(again.PointOf(room.Id).S, Is.EqualTo(first.PointOf(room.Id).S));
            }
        }

        [Test]
        public void EachAdventureGetsADifferentRoute()
        {
            var signatures = Routes(20).Select(Signature).Distinct().Count();
            Assert.That(signatures, Is.EqualTo(20));
            // 道の本数も、3本と4本の両方が出る。
            Assert.That(
                Routes(40).Select(map => map.LaneCount).Distinct(),
                Is.EquivalentTo(new[] { 3, 4 })
            );
        }

        // 入口から1階ずつ進み、どの部屋からも最奥の間へ着き、道は交差しない。
        [Test]
        public void EveryRoomLeadsOnToTheDeepestRoomWithoutCrossingRoads()
        {
            foreach (var map in Routes(200))
            {
                var entrance = map.Find(AdventureRouteMap.EntranceId);
                Assert.That(entrance.Floor, Is.EqualTo(1));
                Assert.That(entrance.Next.Count, Is.EqualTo(map.LaneCount));
                var boss = map.Find(AdventureRouteMap.BossId);
                Assert.That(boss.Floor, Is.EqualTo(RoomCount + 2));
                Assert.That(boss.Kind, Is.EqualTo(AdventureRoomKind.Boss));
                Assert.That(map.Rooms.Count, Is.EqualTo(RoomCount * map.LaneCount + 2));
                Assert.That(
                    map.Reachable(new[] { entrance.Id }).Count,
                    Is.EqualTo(map.Rooms.Count)
                );
                foreach (var room in map.Rooms.Where(room => room != boss))
                {
                    Assert.That(room.Next, Is.Not.Empty, room.Id);
                    Assert.That(map.Reachable(new[] { room.Id }), Does.Contain(boss.Id), room.Id);
                    foreach (var next in room.Next)
                        Assert.That(map.Find(next).Floor, Is.EqualTo(room.Floor + 1), room.Id);
                    // 道中の部屋から出る道は、まっすぐ先と隣の道への横道の2本まで。
                    if (room != entrance)
                        Assert.That(room.Next.Count, Is.LessThanOrEqualTo(2), room.Id);
                }
                for (int floor = 2; floor < RoomCount + 1; floor++)
                {
                    var roads = map
                        .Rooms.Where(room => room.Floor == floor)
                        .SelectMany(room =>
                            room.Next.Select(next =>
                                (map.PointOf(room.Id).Lane, map.PointOf(next).Lane)
                            )
                        )
                        .ToArray();
                    foreach (var a in roads)
                    foreach (var b in roads)
                        Assert.That(
                            a.Item1 < b.Item1 && a.Item2 > b.Item2,
                            Is.False,
                            $"seed {map.Seed}: the roads of floor {floor} cross"
                        );
                }
            }
        }

        // 最初の階は弱い魔物、強い魔物は第4層から、中ほどの階は宝箱、最奥の直前に強い魔物を置かない。
        [Test]
        public void TheFloorsFollowTheRouteRules()
        {
            foreach (var map in Routes(200))
            {
                IEnumerable<AdventureRoom> Floor(int floor) =>
                    map.Rooms.Where(room => room.Floor == floor);
                Assert.That(
                    Floor(2).Select(room => room.Kind),
                    Is.All.EqualTo(AdventureRoomKind.Battle)
                );
                Assert.That(
                    Floor(2 + RoomCount / 2).Select(room => room.Kind),
                    Is.All.EqualTo(AdventureRoomKind.Treasure)
                );
                foreach (int floor in new[] { 2, 3, RoomCount + 1 })
                    Assert.That(
                        Floor(floor).Select(room => room.Kind),
                        Has.None.EqualTo(AdventureRoomKind.Elite),
                        $"seed {map.Seed} floor {floor}"
                    );
                Assert.That(
                    map.Rooms.Count(room => room.Kind == AdventureRoomKind.Elite),
                    Is.GreaterThanOrEqualTo(2),
                    $"seed {map.Seed}"
                );
                Assert.That(
                    map.Rooms.Count(room => room.Kind == AdventureRoomKind.Boss),
                    Is.EqualTo(1)
                );
            }
        }

        // 道は順番を保ち、部屋の札が重ならないだけ離れて、地図からはみ出さない。
        [Test]
        public void TheRoadsKeepApartOnTheMap()
        {
            foreach (var map in Routes(100))
            {
                float gap = map.LaneCount == 3 ? 0.26f : 0.2f;
                for (float t = 0f; t <= 1f; t += 0.01f)
                for (int lane = 0; lane < map.LaneCount; lane++)
                {
                    float s = map.LaneS(lane, t);
                    Assert.That(s, Is.InRange(0.02f, 0.98f));
                    if (lane > 0)
                        Assert.That(
                            s - map.LaneS(lane - 1, t),
                            Is.GreaterThanOrEqualTo(gap - 0.001f),
                            $"seed {map.Seed} t {t}"
                        );
                }
            }
        }

        // 2階先までは詳しく、4階先までは行ける部屋の印だけ、その先は強い魔物の光と最奥の間だけを見せる。
        [Test]
        public void ThePartySeesNearRoomsInDetailAndFarRoomsThroughTheFog()
        {
            var map = AdventureRouteMap.Generate(4242, RoomCount);
            var run = new AdventureRun(
                "r",
                AdventureCatalog.ForestRuins,
                AdventureRouteMap.EntranceId,
                true,
                new[] { AdventureRouteMap.EntranceId },
                0,
                100,
                map,
                null
            );
            var sight = new ExplorationMapSight(run);
            foreach (var room in map.Rooms)
            {
                var seen = sight.Of(room);
                int ahead = room.Floor - 1;
                if (room.Kind == AdventureRoomKind.Boss)
                    Assert.That(seen, Is.EqualTo(ExplorationRoomSight.Detail));
                else if (ahead == 0)
                    Assert.That(seen, Is.EqualTo(ExplorationRoomSight.Hidden));
                else if (ahead <= 2)
                    Assert.That(seen, Is.EqualTo(ExplorationRoomSight.Detail), room.Id);
                else if (ahead <= 4)
                    Assert.That(seen, Is.EqualTo(ExplorationRoomSight.Mark), room.Id);
                else
                    Assert.That(
                        seen,
                        Is.EqualTo(
                            room.Kind == AdventureRoomKind.Elite
                                ? ExplorationRoomSight.Glow
                                : ExplorationRoomSight.Hidden
                        ),
                        room.Id
                    );
            }
        }

        // 地図の絵：同じ道と部屋なら同じ絵になり、木と最奥の遺跡が立ち、透明な画素を残さない。
        [Test]
        public void TheMapIsPaintedTheSameEveryTime()
        {
            var art = ScriptableObject.CreateInstance<ExplorationMapArt>();
            try
            {
                var tree = Stamp("tree", 9, 11, new Color32(10, 200, 30, 255));
                art.Trees = new[] { tree };
                art.TreesFar = new[] { Stamp("far", 5, 6, new Color32(10, 200, 30, 255)) };
                art.Ruin = Stamp("ruin", 12, 20, new Color32(250, 10, 250, 255));
                var map = AdventureRouteMap.Generate(99, RoomCount);
                var run = new AdventureRun(
                    "r",
                    AdventureCatalog.ForestRuins,
                    AdventureRouteMap.EntranceId,
                    true,
                    new[] { AdventureRouteMap.EntranceId },
                    0,
                    100,
                    map,
                    null
                );
                var first = Paint(run, art);
                var again = Paint(run, art);
                Assert.That(again, Is.EqualTo(first));
                Assert.That(first.All(pixel => pixel.a == 255), Is.True);
                Assert.That(
                    first.Count(pixel => pixel.r == 10 && pixel.b == 30),
                    Is.GreaterThan(500)
                );
                Assert.That(
                    first.Count(pixel => pixel.r == 250 && pixel.g == 10),
                    Is.GreaterThan(100)
                );
            }
            finally
            {
                Object.DestroyImmediate(art);
            }
        }

        private static Color32[] Paint(AdventureRun run, ExplorationMapArt art)
        {
            var pixels = new Color32[
                ExplorationMapProjection.Width * ExplorationMapProjection.Height
            ];
            ExplorationMapPainter.Paint(
                pixels,
                run,
                new ExplorationMapProjection(run.Map, run.RoomId),
                new ExplorationMapSight(run),
                art
            );
            return pixels;
        }

        private static PixelStamp Stamp(string name, int width, int height, Color32 color) =>
            PixelStamp.From(
                name,
                width,
                height,
                Enumerable.Repeat(color, width * height).ToArray()
            );

        // 部屋へ進む要求には、道の部屋の階と種類を付ける（server/src/features/adventure/schema.ts）。
        [Test]
        public void TheKindsAreSentWithTheServersNames()
        {
            Assert.That(
                new[]
                {
                    AdventureRoomKind.Battle,
                    AdventureRoomKind.Elite,
                    AdventureRoomKind.Treasure,
                    AdventureRoomKind.Boss,
                }.Select(AdventureApiClient.KindName),
                Is.EqualTo(new[] { "battle", "elite", "treasure", "boss" })
            );
        }
    }
}
