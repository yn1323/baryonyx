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
                .Matches(File.ReadAllText(path), "\\{ id: \"([a-z-]+)\", roomCount: ([0-9]+) \\}")
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

        [Test]
        public void AnEncounterPicksTheAreasEnemiesAtItsLevel()
        {
            var area = AdventureCatalog.Area("port-portoria");
            var pack = AdventureCatalog.Encounter("port-portoria/pack/4");
            Assert.That(area.Small, Does.Contain(pack.Kinds[AdventureCatalog.SmallPlace]));
            Assert.That(area.Medium, Does.Contain(pack.Kinds[AdventureCatalog.MediumPlace]));
            Assert.That(pack.Kinds[AdventureCatalog.LargePlace], Is.Null);
            Assert.That(pack.Enemies, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(pack.Level, Is.EqualTo(area.Level));
            Assert.That(pack.HpScale, Is.EqualTo(1f));

            var elite = AdventureCatalog.Encounter("port-portoria/elite/4");
            Assert.That(elite.Kinds.Take(2), Is.EqualTo(pack.Kinds.Take(2)));
            Assert.That(elite.Level, Is.EqualTo(area.Level + AdventureCatalog.EliteLevelUp));
            Assert.That(elite.HpScale, Is.EqualTo(AdventureCatalog.EliteHpScale));
            Assert.That(elite.PowerScale, Is.EqualTo(AdventureCatalog.ElitePowerScale));
            Assert.That(pack.PowerScale, Is.EqualTo(1f));

            var boss = AdventureCatalog.Encounter("port-portoria/boss/4");
            Assert.That(area.Bosses, Does.Contain(boss.Kinds[AdventureCatalog.LargePlace]));
            Assert.That(boss.Enemies, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(boss.Level, Is.EqualTo(area.Level + AdventureCatalog.BossLevelUp));
            Assert.That(boss.HpScale, Is.EqualTo(AdventureCatalog.BossHpScale));
            Assert.That(boss.PowerScale, Is.EqualTo(AdventureCatalog.BossPowerScale));

            // 知らない組み合わせは、森の魔物の気配にする。
            var unknown = AdventureCatalog.Encounter("nowhere/pack/1");
            Assert.That(
                AdventureCatalog.Area(AdventureCatalog.ForestRuins).Small,
                Does.Contain(unknown.Kinds[0])
            );
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
