using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.WorldMap;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 街道の戦闘のマスの決まった敵（doc/features/stage-progression.md の街道のマスと移動）。
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
                        Assert.That(encounter.Enemies.Kinds[2], Is.Null, node.Id);
                        break;
                    case WorldNodeKind.Boss:
                        Assert.That(encounter, Is.Not.Null, node.Id);
                        Assert.That(encounter.Enemies.Kinds[2], Is.Not.Null, node.Id);
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

        // 戦闘画面の場所の大きさに合う敵を置く：手前は小さな敵、中ほどは中くらいの敵、奥は主。
        [Test]
        public void EachEnemyStandsInThePlaceOfItsSize()
        {
            foreach (string id in WorldMapEncounters.Squares)
            {
                var kinds = WorldMapEncounters.Of(id).Enemies.Kinds;
                Assert.That(Areas.Any(area => area.Small.Contains(kinds[0])), id + " " + kinds[0]);
                Assert.That(Areas.Any(area => area.Medium.Contains(kinds[1])), id + " " + kinds[1]);
                if (kinds[2] != null)
                    Assert.That(
                        Areas.Any(area => area.Bosses.Contains(kinds[2])),
                        id + " " + kinds[2]
                    );
            }
        }

        [Test]
        public void ASquareWithoutABattleHasNone()
        {
            Assert.That(WorldMapEncounters.Of(WorldMapCatalog.Start), Is.Null);
            Assert.That(WorldMapEncounters.Of(null), Is.Null);
        }
    }
}
