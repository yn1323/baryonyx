using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 冒険の行き先と、行き先ごとの敵（doc/features/stage-progression.md の行き先と敵）。
    public sealed class AdventureAreasTests
    {
        [Test]
        public void TheAreasMatchTheServersDestinations()
        {
            var path = Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "server",
                "src",
                "features",
                "adventure",
                "catalog.ts"
            );
            var server = Regex
                .Matches(
                    File.ReadAllText(path),
                    "\\{ id: \"([a-z-]+)\", roomCount: ([0-9]+)(?:, town: \"[a-z]+\")? \\}"
                )
                .Cast<Match>()
                .Select(match => (match.Groups[1].Value, int.Parse(match.Groups[2].Value)))
                .ToArray();
            Assert.That(
                AdventureCatalog.Areas.Select(area => (area.Id, area.RoomCount)),
                Is.EqualTo(server)
            );
            Assert.That(AdventureCatalog.Areas.Count, Is.GreaterThanOrEqualTo(8));
        }

        // 奥の行き先ほど敵が強く、部屋が多い。
        [Test]
        public void DeeperAreasAreHarderAndLonger()
        {
            var areas = AdventureCatalog.Areas;
            for (int i = 1; i < areas.Count; i++)
            {
                Assert.That(areas[i].Level, Is.GreaterThan(areas[i - 1].Level), areas[i].Id);
                Assert.That(areas[i].RoomCount, Is.GreaterThanOrEqualTo(areas[i - 1].RoomCount));
            }
            Assert.That(
                areas.Select(area => area.Stage).Distinct().Count(),
                Is.EqualTo(areas.Count)
            );
        }

        // どの敵もどこかの行き先に出る。敵は大きさの合う場所に立つ。
        [Test]
        public void EveryEnemyLivesSomewhereInAPlaceOfItsSize()
        {
            var used = AdventureCatalog
                .Areas.SelectMany(area => area.Small.Concat(area.Medium).Concat(area.Bosses))
                .Distinct();
            Assert.That(used, Is.EquivalentTo(CombatEnemies.All.Select(enemy => enemy.Id)));
            foreach (var area in AdventureCatalog.Areas)
            {
                Assert.That(area.Small, Has.Length.GreaterThanOrEqualTo(2), area.Id);
                Assert.That(area.Medium, Has.Length.GreaterThanOrEqualTo(3), area.Id);
                Assert.That(area.Bosses, Is.Not.Empty, area.Id);
                foreach (var id in area.Small)
                    Assert.That(CombatEnemyStats.Find(id).Body, Is.EqualTo(EnemyBody.Small), id);
                foreach (var id in area.Medium)
                    Assert.That(CombatEnemyStats.Find(id).Body, Is.EqualTo(EnemyBody.Medium), id);
                foreach (var id in area.Bosses)
                    Assert.That(CombatEnemyStats.Find(id).Body, Is.EqualTo(EnemyBody.Large), id);
            }
        }

        [Test]
        public void EveryEnemyHasItsBattleNumbers()
        {
            Assert.That(
                CombatEnemyStats.All.Select(stats => stats.Id),
                Is.EqualTo(CombatEnemies.All.Select(enemy => enemy.Id))
            );
            foreach (var stats in CombatEnemyStats.All)
            {
                Assert.That(stats.Hp, Is.GreaterThan(0), stats.Id);
                Assert.That(stats.Skill, Is.Not.Empty, stats.Id);
            }
            // 苔むした狼は、Lv10で戦闘画面のモックと同じHPと攻撃になる。
            var wolf = CombatEnemyStats.Find("moss-wolf");
            Assert.That(EnemyStats.At(wolf.Hp, 10), Is.EqualTo(801));
            Assert.That(EnemyStats.At(wolf.PhysicalAttack, 10), Is.EqualTo(154));
            Assert.That(wolf.Magic, Is.False);
            Assert.That(CombatEnemyStats.Find("lich").Magic, Is.True);
        }

        // 道中の敵の強さは、最初の階の85%から最奥の直前の階の115%まで、階ごとに等しく上がる。
        [Test]
        public void TheEnemiesGrowStrongerFloorByFloor()
        {
            Assert.That(AdventureCatalog.DepthPercentOf("f2-0", 8), Is.EqualTo(85));
            Assert.That(AdventureCatalog.DepthPercentOf("f9-1", 8), Is.EqualTo(115));
            var way = Enumerable
                .Range(2, 8)
                .Select(floor => AdventureCatalog.DepthPercentOf($"f{floor}-0", 8))
                .ToArray();
            for (int i = 1; i < way.Length; i++)
                Assert.That(way[i], Is.GreaterThan(way[i - 1]));
            Assert.That(way.Average(), Is.EqualTo(100).Within(1));
            Assert.That(
                AdventureCatalog.DepthPercentOf(AdventureRouteMap.BossId, 8),
                Is.EqualTo(100)
            );

            // 道の敵の組み合わせに、その階の強さが付く。最奥の主には付かない。
            var map = AdventureCatalog.Route(AdventureCatalog.ForestRuins, 20261004, 8);
            foreach (var room in map.Rooms.Where(room => room.Kind == AdventureRoomKind.Battle))
                Assert.That(
                    AdventureCatalog.Encounter(room.Encounter).HpScale,
                    Is.EqualTo(
                            AdventureCatalog.PackHpScale
                                * FormationOf(room.Encounter).HpScale
                                * AdventureCatalog.DepthPercentOf(room.Id, 8)
                                / 100f
                        )
                        .Within(1e-4),
                    room.Id
                );
            var boss = map.Find(AdventureRouteMap.BossId);
            Assert.That(
                AdventureCatalog.Encounter(boss.Encounter).HpScale,
                Is.EqualTo(AdventureCatalog.BossHpScale)
            );
        }

        private static AdventureFormation FormationOf(string encounter) =>
            AdventureCatalog.PackFormationOf(AdventureCatalog.Encounter(encounter).Kinds);

        private static int Count(AdventureEncounter encounter) => encounter.Enemies.Length;

        [Test]
        public void AnEncounterPicksTheAreasEnemiesAtItsLevel()
        {
            var area = AdventureCatalog.Area("port-portoria");
            var pack = AdventureCatalog.Encounter("port-portoria/pack/4");
            var formation = FormationOf("port-portoria/pack/4");
            Assert.That(formation.Fits(pack.Kinds), Is.True);
            Assert.That(
                pack.Kinds.Where(kind => kind != null),
                Is.SubsetOf(area.Small.Concat(area.Medium))
            );
            Assert.That(pack.Kinds.Where(kind => kind != null), Is.Unique);
            Assert.That(pack.Level, Is.EqualTo(area.Level));
            Assert.That(
                pack.HpScale,
                Is.EqualTo(AdventureCatalog.PackHpScale * formation.HpScale).Within(1e-4)
            );
            Assert.That(
                pack.PowerScale,
                Is.EqualTo(AdventureCatalog.PackPowerScale * formation.PowerScale).Within(1e-4)
            );

            // 強い魔物の気配は中くらいの敵を2体にし、Lvを上げる。
            var elite = AdventureCatalog.Encounter("port-portoria/elite/4");
            var strong = AdventureCatalog.EliteFormations.Single(each => each.Fits(elite.Kinds));
            Assert.That(elite.Kinds.Count(kind => area.Medium.Contains(kind)), Is.EqualTo(2));
            Assert.That(elite.Level, Is.EqualTo(area.Level + AdventureCatalog.EliteLevelUp));
            Assert.That(
                elite.HpScale,
                Is.EqualTo(AdventureCatalog.EliteHpScale * strong.HpScale).Within(1e-4)
            );
            Assert.That(
                elite.PowerScale,
                Is.EqualTo(AdventureCatalog.ElitePowerScale * strong.PowerScale).Within(1e-4)
            );

            var boss = AdventureCatalog.Encounter("port-portoria/boss/4");
            Assert.That(area.Small, Does.Contain(boss.Kinds[AdventureCatalog.FrontPlace]));
            Assert.That(area.Medium, Does.Contain(boss.Kinds[AdventureCatalog.MiddlePlace]));
            Assert.That(area.Bosses, Does.Contain(boss.Kinds[AdventureCatalog.BackPlace]));
            Assert.That(boss.Enemies, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(boss.Level, Is.EqualTo(area.Level + AdventureCatalog.BossLevelUp));
            Assert.That(boss.HpScale, Is.EqualTo(AdventureCatalog.BossHpScale));
            Assert.That(boss.PowerScale, Is.EqualTo(AdventureCatalog.BossPowerScale));

            // 奥の階の敵ほど、HPと攻撃が少しずつ上がる（最奥の主には掛けない）。
            var deep = AdventureCatalog.Encounter("port-portoria/pack/4/115");
            var deepFormation = FormationOf("port-portoria/pack/4/115");
            Assert.That(
                deep.HpScale,
                Is.EqualTo(AdventureCatalog.PackHpScale * deepFormation.HpScale * 1.15f)
                    .Within(1e-4)
            );
            Assert.That(
                deep.PowerScale,
                Is.EqualTo(AdventureCatalog.PackPowerScale * deepFormation.PowerScale * 1.15f)
                    .Within(1e-4)
            );
            var shallowElite = AdventureCatalog.Encounter("port-portoria/elite/4/85");
            var shallowStrong = AdventureCatalog.EliteFormations.Single(each =>
                each.Fits(shallowElite.Kinds)
            );
            Assert.That(
                shallowElite.HpScale,
                Is.EqualTo(AdventureCatalog.EliteHpScale * shallowStrong.HpScale * 0.85f)
                    .Within(1e-4)
            );

            // 知らない組み合わせは、森の魔物の気配にする。
            var forest = AdventureCatalog.Area(AdventureCatalog.ForestRuins);
            var unknown = AdventureCatalog.Encounter("nowhere/pack/1");
            Assert.That(
                unknown.Kinds.Where(kind => kind != null),
                Is.SubsetOf(forest.Small.Concat(forest.Medium))
            );
        }

        // 魔物の気配は1体から3体まで散らばり、浅い階は少ない敵、奥の階は多い敵がよく出る。
        [Test]
        public void ThePacksHaveOneToThreeEnemiesAndMoreOnTheDeepFloors()
        {
            var shallow = Enumerable
                .Range(0, 400)
                .Select(variant => AdventureCatalog.Encounter($"hush-mire/pack/{variant}/85"))
                .ToArray();
            var deep = Enumerable
                .Range(0, 400)
                .Select(variant => AdventureCatalog.Encounter($"hush-mire/pack/{variant}/115"))
                .ToArray();
            Assert.That(shallow.Select(Count).Distinct(), Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(deep.Select(Count).Distinct(), Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(deep.Average(Count), Is.GreaterThan(shallow.Average(Count) + 0.3));
            foreach (var pack in shallow.Concat(deep))
                Assert.That(pack.Kinds.Where(kind => kind != null), Is.Unique, pack.Id);
        }

        // 同じ冒険（種）は、どの端末でも同じ敵に会い、部屋ごとに違う組み合わせが出る。
        [Test]
        public void TheRoomsOfARouteMeetVariedEnemiesTheSameWayEveryTime()
        {
            foreach (var area in AdventureCatalog.Areas)
            {
                var first = AdventureCatalog.Route(area.Id, 20261020, area.RoomCount);
                var again = AdventureCatalog.Route(area.Id, 20261020, area.RoomCount);
                Assert.That(
                    first.Rooms.Select(room => room.Encounter),
                    Is.EqualTo(again.Rooms.Select(room => room.Encounter))
                );
                var packs = first
                    .Rooms.Where(room => room.Kind == AdventureRoomKind.Battle)
                    .Select(room =>
                        string.Join(",", AdventureCatalog.Encounter(room.Encounter).Kinds)
                    )
                    .Distinct()
                    .Count();
                Assert.That(packs, Is.GreaterThan(1), area.Id);
                var boss = first.Rooms.Single(room => room.Kind == AdventureRoomKind.Boss);
                Assert.That(boss.Encounter, Does.StartWith(area.Id + "/boss/"));
            }
        }
    }
}
