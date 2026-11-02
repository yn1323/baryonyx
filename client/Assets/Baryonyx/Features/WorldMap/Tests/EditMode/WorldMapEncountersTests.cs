using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.WorldMap;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 街道の戦闘のマスの決まった敵（doc/features/stage-progression-world-map.md の街道のマスと移動）。
    public sealed class WorldMapEncountersTests
    {
        private static readonly AdventureArea[] Areas = AdventureCatalog.Areas.ToArray();

        // 戦闘と関所のボスのマスにだけ敵がいて、関所のボスだけが奥に主を置く。
        [Test]
        public void EveryBattleAndGateHasItsEnemies()
        {
            var graph = WorldMapCatalog.Build();
            foreach (var node in graph.Nodes)
            {
                var encounter = WorldMapEncounters.Of(node.Id);
                switch (node.Kind)
                {
                    case WorldNodeKind.Battle:
                        Assert.That(encounter, Is.Not.Null, node.Id);
                        Assert.That(
                            Areas.Any(area => area.Bosses.Contains(encounter.Enemies.Kinds[2])),
                            Is.False,
                            node.Id
                        );
                        break;
                    case WorldNodeKind.Boss:
                        Assert.That(encounter, Is.Not.Null, node.Id);
                        Assert.That(
                            Areas.Any(area => area.Bosses.Contains(encounter.Enemies.Kinds[2])),
                            node.Id
                        );
                        break;
                    default:
                        Assert.That(encounter, Is.Null, node.Id);
                        break;
                }
            }
            Assert.That(
                WorldMapEncounters.Squares.Where(id => graph.IndexOf(id) < 0),
                Is.Empty,
                "An encounter is set on a square that is not on the map."
            );
        }

        // 戦闘のマスは、ダンジョンの魔物の気配の編成のどれかで立ち、その編成の倍率で強くなる。
        // 関所のボスは、手前に小さな敵、中ほどに中くらいの敵、奥に主を置く。
        [Test]
        public void EachBattleStandsAsAFormationOfThePacks()
        {
            var formations = new[] { 0, 0, 0, 0 };
            foreach (string id in WorldMapEncounters.Squares)
            {
                var enemies = WorldMapEncounters.Of(id).Enemies;
                var kinds = enemies.Kinds;
                if (Areas.Any(area => area.Bosses.Contains(kinds[2])))
                {
                    Assert.That(Areas.Any(area => area.Small.Contains(kinds[0])), id);
                    Assert.That(Areas.Any(area => area.Medium.Contains(kinds[1])), id);
                    continue;
                }
                int formation = AdventureCatalog
                    .PackFormations.ToList()
                    .FindIndex(each => each.Fits(kinds));
                Assert.That(formation, Is.GreaterThanOrEqualTo(0), id);
                formations[formation]++;
                var scale = AdventureCatalog.PackFormations[formation];
                Assert.That(
                    enemies.HpScale,
                    Is.EqualTo(AdventureCatalog.PackHpScale * scale.HpScale).Within(1e-4),
                    id
                );
                Assert.That(kinds.Where(kind => kind != null), Is.Unique, id);
            }
            // 1体から3体まで、どの編成も街道のどこかに出る。
            Assert.That(formations, Has.All.GreaterThan(0));
        }

        [Test]
        public void ASquareWithoutABattleHasNone()
        {
            Assert.That(WorldMapEncounters.Of(WorldMapCatalog.Start), Is.Null);
            Assert.That(WorldMapEncounters.Of(null), Is.Null);
        }
    }
}
