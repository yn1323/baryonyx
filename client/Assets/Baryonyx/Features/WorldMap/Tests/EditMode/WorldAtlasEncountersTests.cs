using System.Linq;
using Baryonyx.Combat;
using Baryonyx.WorldMap;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 入れ子の地図のマスの戦闘は、言葉で書いた敵・Lv・強さから敵を引く
    // （doc/features/world-atlas.md#戦闘の設定）。
    public sealed class WorldAtlasEncountersTests
    {
        private static string Slime => CombatEnemies.Find("moss-slime").Name;
        private static string Wolf => CombatEnemies.Find("moss-wolf").Name;

        [Test]
        public void WordsNameEnemiesAndCallForRandomOnes()
        {
            var named = WorldAtlasEncounters.Read($"{Slime}、{Wolf}");
            Assert.That(named.Named, Is.EqualTo(new[] { "moss-slime", "moss-wolf" }));
            Assert.That(named.Random, Is.EqualTo(-1));
            Assert.That(named.Errors, Is.Empty);

            Assert.That(WorldAtlasEncounters.Read($"{Slime}×2").Named, Has.Count.EqualTo(2));
            Assert.That(WorldAtlasEncounters.Read("ランダム2体").Random, Is.EqualTo(2));
            Assert.That(WorldAtlasEncounters.Read("").Random, Is.EqualTo(-1));
            Assert.That(WorldAtlasEncounters.Read("ランダム").Random, Is.EqualTo(-1));
            // 名前と並べた「ランダム」は1体。
            Assert.That(WorldAtlasEncounters.Read($"{Wolf}、ランダム").Random, Is.EqualTo(1));

            Assert.That(WorldAtlasEncounters.Read("ドラゴン").Errors, Has.Count.EqualTo(1));
            Assert.That(WorldAtlasEncounters.Read("ランダム4体").Errors, Is.Not.Empty);
        }

        [Test]
        public void TheBattleHasTheNamedEnemiesAtTheSquaresLevel()
        {
            var battle = WorldAtlasEncounters.Draw(
                "test",
                $"{Slime}、{Wolf}",
                17,
                WorldBattleStrength.Pack,
                3
            );
            Assert.That(
                battle.Kinds.Where(kind => kind != null),
                Is.EquivalentTo(new[] { "moss-slime", "moss-wolf" })
            );
            Assert.That(battle.Level, Is.EqualTo(17));
            Assert.That(battle.HpScale, Is.GreaterThan(0f));
            Assert.That(
                WorldAtlasEncounters.Draw("test", "ドラゴン", 10, WorldBattleStrength.Pack, 1),
                Is.Null
            );
        }

        [Test]
        public void RandomEnemiesAreDrawnTheSameWayFromTheSameSeed()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var two = WorldAtlasEncounters.Draw(
                    "t",
                    "ランダム2体",
                    10,
                    WorldBattleStrength.Pack,
                    seed
                );
                Assert.That(two.Kinds.Count(kind => kind != null), Is.EqualTo(2), seed.ToString());
                Assert.That(
                    WorldAtlasEncounters.Draw("t", "", 10, WorldBattleStrength.Pack, seed).Kinds,
                    Is.EqualTo(
                        WorldAtlasEncounters.Draw("t", "", 10, WorldBattleStrength.Pack, seed).Kinds
                    )
                );
                var boss = WorldAtlasEncounters.Draw("t", "", 30, WorldBattleStrength.Boss, seed);
                Assert.That(boss.Kinds.All(kind => kind != null), Is.True, seed.ToString());
                var mixed = WorldAtlasEncounters.Draw(
                    "t",
                    $"{Wolf}、ランダム",
                    10,
                    WorldBattleStrength.Pack,
                    seed
                );
                Assert.That(mixed.Kinds, Does.Contain("moss-wolf"));
                Assert.That(mixed.Kinds.Count(kind => kind != null), Is.EqualTo(2));
            }
        }

        // 世界地図とマリーナ海岸の戦闘のマスは、どれも敵を引ける。
        [Test]
        public void EveryPlacedBattleCanBeDrawn()
        {
            foreach (string key in new[] { WorldAtlasLayout.WorldKey, "marina" })
            {
                var layout = WorldAtlasLayout.Parse(
                    System.IO.File.ReadAllText(
                        $"Assets/Baryonyx/Features/WorldMap/Atlas/Layouts/{key}.json"
                    )
                );
                foreach (var node in layout.nodes.Where(node => node.@event == "battle"))
                    Assert.That(
                        WorldAtlasBattle.Of(key, node, layout.level, 1),
                        Is.Not.Null,
                        node.id
                    );
            }
        }
    }
}
