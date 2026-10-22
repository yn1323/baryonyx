using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 冒険の種から作る網の目の道（doc/features/stage-progression.md の冒険の道の作り方）と、その地図。
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
            // 入口から分かれる数も、2つと3つの両方が出る。
            Assert.That(
                Routes(40)
                    .Select(map => map.Find(AdventureRouteMap.EntranceId).Next.Count)
                    .Distinct(),
                Is.EquivalentTo(new[] { 2, 3 })
            );
        }

        // 網の目の道：入口から2〜3つに分かれ、階ごとに2〜6のマスが並び、5つ以上並ぶ階がある。
        // 入口から1階ずつ進み、どのマスにも前の階から来られて、どのマスからも最奥の間へ着き、道は交差しない。
        [Test]
        public void EveryRoomLeadsOnToTheDeepestRoomWithoutCrossingRoads()
        {
            foreach (var map in Routes(200))
            {
                var entrance = map.Find(AdventureRouteMap.EntranceId);
                Assert.That(entrance.Floor, Is.EqualTo(1));
                Assert.That(entrance.Next.Count, Is.InRange(2, 3));
                var boss = map.Find(AdventureRouteMap.BossId);
                Assert.That(boss.Floor, Is.EqualTo(RoomCount + 2));
                Assert.That(boss.Kind, Is.EqualTo(AdventureRoomKind.Boss));
                var widths = Enumerable
                    .Range(2, RoomCount)
                    .Select(floor => map.Rooms.Count(room => room.Floor == floor))
                    .ToArray();
                Assert.That(widths, Is.All.InRange(2, 6), $"seed {map.Seed}");
                Assert.That(widths.Max(), Is.GreaterThanOrEqualTo(5), $"seed {map.Seed}");
                Assert.That(
                    map.Reachable(new[] { entrance.Id }).Count,
                    Is.EqualTo(map.Rooms.Count)
                );
                var reached = new HashSet<string>(map.Rooms.SelectMany(room => room.Next));
                foreach (var room in map.Rooms.Where(room => room != boss))
                {
                    Assert.That(room.Next, Is.Not.Empty, room.Id);
                    Assert.That(map.Reachable(new[] { room.Id }), Does.Contain(boss.Id), room.Id);
                    foreach (var next in room.Next)
                        Assert.That(map.Find(next).Floor, Is.EqualTo(room.Floor + 1), room.Id);
                    if (room != entrance)
                        Assert.That(reached, Does.Contain(room.Id), $"seed {map.Seed}");
                }
                for (int floor = 2; floor < RoomCount + 1; floor++)
                {
                    var roads = map
                        .Rooms.Where(room => room.Floor == floor)
                        .SelectMany(room =>
                            room.Next.Select(next => (map.PointOf(room.Id).S, map.PointOf(next).S))
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

        // 道の決まり（doc/features/stage-progression.md）：第2層は魔物、真ん中の第6層は宝箱、最奥の直前の
        // 第9層は休憩。ほかの階には、その種類を置ける階から先にだけ置き、サーバーが受け付ける形にする。
        [Test]
        public void TheFloorsFollowTheRouteRules()
        {
            bool altar = false;
            foreach (var map in Routes(300))
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
                Assert.That(
                    Floor(RoomCount + 1).Select(room => room.Kind),
                    Is.All.EqualTo(AdventureRoomKind.Rest)
                );
                foreach (var room in map.Rooms.Where(room => room.Floor >= 2))
                {
                    var fixedKind = AdventureRouteMap.FixedKindOf(RoomCount, room.Floor);
                    if (fixedKind != null)
                        Assert.That(room.Kind, Is.EqualTo(fixedKind), room.Id);
                    else
                        Assert.That(
                            room.Floor - 1,
                            Is.GreaterThanOrEqualTo(AdventureRouteMap.EarliestOf(room.Kind)),
                            $"seed {map.Seed} {room.Id} {room.Kind}"
                        );
                }
                int Count(AdventureRoomKind kind) => map.Rooms.Count(room => room.Kind == kind);
                Assert.That(
                    Count(AdventureRoomKind.Elite),
                    Is.GreaterThanOrEqualTo(2),
                    $"seed {map.Seed}"
                );
                Assert.That(
                    Count(AdventureRoomKind.Merchant),
                    Is.InRange(1, 2),
                    $"seed {map.Seed}"
                );
                Assert.That(
                    Count(AdventureRoomKind.StepDoor),
                    Is.InRange(1, 2),
                    $"seed {map.Seed}"
                );
                Assert.That(Count(AdventureRoomKind.Altar), Is.LessThanOrEqualTo(1));
                Assert.That(Count(AdventureRoomKind.Boss), Is.EqualTo(1));
                altar |= Count(AdventureRoomKind.Altar) == 1;
            }
            Assert.That(altar, Is.True, "進化の祭壇が一度も出ない");
        }

        // 進化の祭壇は、冒険を始めたときに5%で1つだけ地図に置く（doc/features/stage-progression.md）。
        [Test]
        public void TheAltarIsOnAboutOneMapInTwenty()
        {
            var maps = Routes(2000).ToArray();
            int withAltar = maps.Count(map =>
                map.Rooms.Any(room => room.Kind == AdventureRoomKind.Altar)
            );
            Assert.That(
                maps.Select(map => map.Rooms.Count(room => room.Kind == AdventureRoomKind.Altar)),
                Is.All.LessThanOrEqualTo(1)
            );
            Assert.That(withAltar / (double)maps.Length, Is.InRange(0.03, 0.07));
        }

        // 道でつながった2つのマスに、魔物以外の同じ種類や、強い魔物と試練を続けない。真ん中の宝箱の階と
        // 最後の休憩の階の隣の階には、宝箱と休憩を置かない。
        [Test]
        public void ConnectedRoomsDoNotRepeat()
        {
            foreach (var map in Routes(300))
            foreach (var room in map.Rooms)
            {
                bool heavy = room.Kind is AdventureRoomKind.Elite or AdventureRoomKind.Trial;
                foreach (var next in room.Next.Select(map.Find))
                {
                    if (room.Kind == AdventureRoomKind.Start || next.Kind == AdventureRoomKind.Boss)
                        continue;
                    if (room.Kind != AdventureRoomKind.Battle)
                        Assert.That(
                            next.Kind,
                            Is.Not.EqualTo(room.Kind),
                            $"seed {map.Seed} {room.Id}"
                        );
                    if (heavy)
                        Assert.That(
                            new[] { AdventureRoomKind.Elite, AdventureRoomKind.Trial },
                            Has.No.Member(next.Kind),
                            $"seed {map.Seed} {room.Id}"
                        );
                }
                foreach (int floor in new[] { 1 + RoomCount / 2, 3 + RoomCount / 2, RoomCount })
                    if (room.Floor == floor)
                        Assert.That(
                            new[] { AdventureRoomKind.Treasure, AdventureRoomKind.Rest },
                            Has.No.Member(room.Kind),
                            $"seed {map.Seed} {room.Id}"
                        );
            }
        }

        // 同じ階のマスは順番を保ち、札が重ならないだけ離れて、地図からはみ出さない。
        [Test]
        public void TheRoomsOfAFloorKeepApartOnTheMap()
        {
            foreach (var map in Routes(200))
                for (int floor = 2; floor <= RoomCount + 1; floor++)
                {
                    var across = map
                        .Rooms.Where(room => room.Floor == floor)
                        .Select(room => map.PointOf(room.Id).S)
                        .ToArray();
                    Assert.That(across, Is.All.InRange(0.029f, 0.971f), $"seed {map.Seed}");
                    for (int i = 1; i < across.Length; i++)
                        Assert.That(
                            across[i] - across[i - 1],
                            Is.GreaterThanOrEqualTo(AdventureRouteMap.MinGap - 0.001f),
                            $"seed {map.Seed} floor {floor}"
                        );
                }
        }

        // マスへ進む要求には、道のマスの階と種類を付ける（server/src/features/adventure/schema.ts）。
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
                    AdventureRoomKind.Rest,
                    AdventureRoomKind.Merchant,
                    AdventureRoomKind.ShadyChest,
                    AdventureRoomKind.Trial,
                    AdventureRoomKind.StepDoor,
                    AdventureRoomKind.Altar,
                }.Select(AdventureApiClient.KindName),
                Is.EqualTo(
                    new[]
                    {
                        "battle",
                        "elite",
                        "treasure",
                        "boss",
                        "rest",
                        "merchant",
                        "shady-chest",
                        "trial",
                        "step-door",
                        "altar",
                    }
                )
            );
        }
    }
}
