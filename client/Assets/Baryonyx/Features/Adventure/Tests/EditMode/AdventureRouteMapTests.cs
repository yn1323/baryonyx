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

        // 列の上の道：入口から2〜3つに分かれ、階ごとに2〜4のマスが並ぶ。入口から1階ずつ進み、
        // どのマスにも前の階から来られて、どのマスからも最奥の間へ着き、道は交差しない。
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
                Assert.That(widths, Is.All.InRange(2, 4), $"seed {map.Seed}");
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
            Assert.That(altar, Is.True, "覚醒の祭壇が一度も出ない");
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

        // 同じ階のマスは縦に揃って列の上に立ち、順番を保ち、札が重ならないだけ離れて、地図からはみ出さない。
        [Test]
        public void TheRoomsOfAFloorStandOnTheLanes()
        {
            foreach (var map in Routes(200))
                for (int floor = 2; floor <= RoomCount + 1; floor++)
                {
                    var rooms = map.Rooms.Where(room => room.Floor == floor).ToArray();
                    var across = rooms.Select(room => map.PointOf(room.Id).S).ToArray();
                    Assert.That(across, Is.All.InRange(0.029f, 0.971f), $"seed {map.Seed}");
                    Assert.That(
                        rooms.Select(room => map.PointOf(room.Id).T).Distinct().Count(),
                        Is.EqualTo(1),
                        $"seed {map.Seed} floor {floor}"
                    );
                    Assert.That(
                        rooms.Select(room => map.LaneOf(room.Id)),
                        Is.All.InRange(0, AdventureRouteMap.LaneCount - 1)
                    );
                    for (int i = 1; i < across.Length; i++)
                        Assert.That(
                            across[i] - across[i - 1],
                            Is.GreaterThanOrEqualTo(AdventureRouteMap.LaneGap - 0.001f),
                            $"seed {map.Seed} floor {floor}"
                        );
                }
        }

        // 道中の道は、同じ列を真っすぐ進むか、隣の列へ斜めに移るだけで、列を飛ばさない。
        // 真っすぐな道がいちばん多く、分かれ道（2つ以上へ続くマス）と合流（2つ以上から来るマス）もある。
        [Test]
        public void TheRoadsGoStraightOrToTheNextLane()
        {
            int straight = 0;
            int slanted = 0;
            bool splits = false;
            bool joins = false;
            foreach (var map in Routes(200))
            {
                var way = map
                    .Rooms.Where(room => room.Floor >= 2 && room.Floor <= RoomCount)
                    .ToArray();
                foreach (var room in way)
                foreach (var next in room.Next)
                {
                    int step = System.Math.Abs(map.LaneOf(next) - map.LaneOf(room.Id));
                    Assert.That(step, Is.LessThanOrEqualTo(1), $"seed {map.Seed} {room.Id}");
                    if (step == 0)
                        straight++;
                    else
                        slanted++;
                }
                splits |= way.Any(room => room.Next.Count >= 2);
                joins |= way.SelectMany(room => room.Next)
                    .GroupBy(id => id)
                    .Any(g => g.Count() >= 2);
            }
            Assert.That(straight, Is.GreaterThan(slanted));
            Assert.That(splits, Is.True);
            Assert.That(joins, Is.True);
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
                    AdventureRoomKind.Mystery,
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
                        "mystery",
                    }
                )
            );
            // サーバーが返す種類の名前を読み、知らない名前は読まない。
            Assert.That(
                AdventureApiClient.ParseKind("treasure"),
                Is.EqualTo(AdventureRoomKind.Treasure)
            );
            Assert.That(AdventureApiClient.ParseKind("nowhere"), Is.Null);
        }

        // 「？」のマスは道に出て、正体を明かすと、同じ場所と道のまま中身の種類のマスになる。
        [Test]
        public void AMysteryRoomTurnsIntoWhatItHolds()
        {
            var maps = Routes(300).ToArray();
            Assert.That(
                maps.Count(map => map.Rooms.Any(room => room.Kind == AdventureRoomKind.Mystery)),
                Is.GreaterThan(150)
            );
            var map = maps.First(map =>
                map.Rooms.Any(room => room.Kind == AdventureRoomKind.Mystery)
            );
            var mystery = map.Rooms.First(room => room.Kind == AdventureRoomKind.Mystery);
            Assert.That(mystery.Floor - 1, Is.GreaterThanOrEqualTo(3));
            var battle = map.Reveal(mystery.Id, AdventureRoomKind.Battle);
            Assert.That(battle.Kind, Is.EqualTo(AdventureRoomKind.Battle));
            Assert.That(battle.Floor, Is.EqualTo(mystery.Floor));
            Assert.That(battle.Next, Is.EqualTo(mystery.Next));
            // ほかのマスは、明かしても変わらない。
            var other = map.Rooms.First(room => room.Kind == AdventureRoomKind.Battle);
            Assert.That(map.Reveal(other.Id, AdventureRoomKind.Treasure), Is.SameAs(other));
            Assert.That(AdventureRouteMap.FloorOf(mystery.Id), Is.EqualTo(mystery.Floor));
            Assert.That(AdventureRouteMap.FloorOf(AdventureRouteMap.BossId), Is.Zero);
        }
    }
}
