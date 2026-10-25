using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training;
using Baryonyx.Training.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Baryonyx.Tests.EditMode
{
    /// <summary>The parts of the automatic battles that need no battle: the request, the encounters and the sums.</summary>
    public sealed class BattleBalanceResultTests
    {
        [Test]
        public void ARequestLeftOutKeepsItsDefaults()
        {
            var request = BattleBalanceRequest.FromJson("{\"room\":\"boss\",\"levels\":[20,0,15]}");
            Assert.That(request.RoomKind, Is.EqualTo(AdventureRoomKind.Boss));
            Assert.That(request.destination, Is.EqualTo(AdventureCatalog.ForestRuins));
            Assert.That(request.repeats, Is.EqualTo(3));
            Assert.That(request.cardMultiplier, Is.EqualTo(1f));
            Assert.That(request.LevelOf(0), Is.EqualTo(20));
            Assert.That(request.LevelOf(1), Is.EqualTo(0));
            Assert.That(request.LevelOf(3), Is.EqualTo(0));
            Assert.That(
                BattleBalanceRequest.FromJson("").RoomKind,
                Is.EqualTo(AdventureRoomKind.Elite)
            );
        }

        // 森の小さな敵は4体、中くらいの敵は5体、最奥の主は1体。組み合わせの番号で、全部に1回ずつ会う。
        [Test]
        public void TheVariantsMeetEveryCombinationOnce()
        {
            var request = new BattleBalanceRequest { room = "elite", depthPercent = 106 };
            var encounters = BattleBalanceEncounters.Of(request);
            Assert.That(encounters.Count, Is.EqualTo(20));
            Assert.That(encounters[0], Is.EqualTo("forest-ruins/elite/0/106"));
            var pairs = encounters.Select(BattleBalanceEncounters.EnemiesOf).ToArray();
            Assert.That(pairs, Is.Unique);
            Assert.That(pairs, Does.Contain("dew-slime+nectar-bloom"));
            Assert.That(
                BattleBalanceEncounters.CombinationCount(
                    AdventureCatalog.ForestRuins,
                    AdventureRoomKind.Boss
                ),
                Is.EqualTo(20)
            );
            Assert.That(
                BattleBalanceEncounters.CombinationCount(
                    AdventureCatalog.ForestRuins,
                    AdventureRoomKind.ShadyChest
                ),
                Is.EqualTo(4)
            );
            // 最奥の間は深さを付けない。
            request.room = "boss";
            request.variants = new[] { 7 };
            Assert.That(
                BattleBalanceEncounters.Of(request),
                Is.EqualTo(new[] { "forest-ruins/boss/7" })
            );
        }

        [Test]
        public void TheSumsCountWinsTurnsAndTheHpLeft()
        {
            var battles = new[]
            {
                Battle("a", BattleBalanceRecord.Victory, 9, 0.6f),
                Battle("a", BattleBalanceRecord.Defeat, 6, 0f),
                Battle("a", BattleBalanceRecord.Victory, 7, 0.4f),
                Battle("b", BattleBalanceRecord.Timeout, 40, 0.2f),
            };
            var result = BattleBalanceResult.Of(new BattleBalanceRequest(), battles);

            var a = result.groups.Single(group => group.key == "a");
            Assert.That(a.battles, Is.EqualTo(3));
            Assert.That(a.winRate, Is.EqualTo(2f / 3f).Within(0.0001f));
            Assert.That(a.turnsAverage, Is.EqualTo(22f / 3f).Within(0.0001f));
            Assert.That(a.turnsMedian, Is.EqualTo(7));
            Assert.That(a.turnsMin, Is.EqualTo(6));
            Assert.That(a.turnsMax, Is.EqualTo(9));
            Assert.That(a.turnsHistogram, Is.EqualTo("6:1 7:1 9:1"));
            Assert.That(a.hpEndWhenWon, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(a.dealtPerTurn, Is.EqualTo(3000f / 22f).Within(0.01f));
            Assert.That(result.overall.timeouts, Is.EqualTo(1));
            Assert.That(result.overall.battles, Is.EqualTo(4));
            string table = result.ToMarkdown();
            Assert.That(table, Does.Contain("| a（x+y） | 3 | 67% |"));
            Assert.That(table, Does.Contain("| all | 4 | 50% |"));
        }

        // 通しの冒険は、部屋の種類（魔物・強い魔物・最奥）でまとめる。
        [Test]
        public void ThroughTheRouteTheSumsGoByKindOfRoom()
        {
            var battles = new[]
            {
                Battle("forest-ruins/pack/1/85", BattleBalanceRecord.Victory, 3, 0.8f),
                Battle("forest-ruins/pack/2/94", BattleBalanceRecord.Victory, 4, 0.7f),
                Battle("forest-ruins/elite/3/106", BattleBalanceRecord.Defeat, 6, 0f),
            };
            var result = BattleBalanceResult.Of(
                new BattleBalanceRequest { mode = BattleBalanceRequest.Through },
                battles,
                new[]
                {
                    new BattleBalanceRun
                    {
                        run = 0,
                        floor = 5,
                        lostIn = "f5-1",
                    },
                }
            );
            Assert.That(
                result.groups.Select(group => group.key),
                Is.EqualTo(new[] { "pack", "elite" })
            );
            Assert.That(result.ToMarkdown(), Does.Contain("| 0 | 0 | × | 5 | f5-1 |"));
        }

        // 依頼の装備は、プレイヤーの装備と同じく AdventureParty.Of に渡り、付けた仲間のステータスと2回発動の確率を上げる。
        [Test]
        public void TheEquipmentAskedForGoesTheBattlesWayToTheMembers()
        {
            var party = AssetDatabase.LoadAssetAtPath<PartyMockData>(PartyAssets.DataPath);
            var training = AssetDatabase.LoadAssetAtPath<TrainingMockData>(TrainingAssets.DataPath);
            var request = new BattleBalanceRequest
            {
                equipment = new[]
                {
                    new BattleBalanceGear
                    {
                        slot = 2,
                        equipment = "iron-sword",
                        multiplier = 1.5f,
                    },
                    new BattleBalanceGear { character = "toma", equipment = "flame-dagger" },
                    new BattleBalanceGear
                    {
                        slot = 0,
                        equipment = "dew-pendant",
                        accessory = 1,
                    },
                },
            };
            var slots = PartySession.Members(PartySession.Formation(party));
            Assert.That(slots[0].Id, Is.EqualTo("toma"), "The flame dagger goes to Toma by id.");
            var gear = BattleBalanceParty.Gear(request, slots);
            Assert.That(gear.ItemIn(slots[2].Id, EquipmentSlot.Weapon), Is.EqualTo("balance-0"));
            Assert.That(
                gear.ItemIn(slots[0].Id, EquipmentPlace.Accessory(1)),
                Is.EqualTo("balance-2")
            );

            var bare = BattleBalanceParty.Of(new BattleBalanceRequest(), party, training);
            var worn = BattleBalanceParty.Of(request, party, training);
            Assert.That(
                worn[2].Stats.PhysicalAttack - bare[2].Stats.PhysicalAttack,
                Is.EqualTo(EquipmentStats.Bonus(bare[2].Stats, gear, slots[2].Id).PhysicalAttack)
            );
            Assert.That(worn[2].Stats.PhysicalAttack, Is.GreaterThan(bare[2].Stats.PhysicalAttack));
            Assert.That(worn[0].Stats.MagicAttack, Is.GreaterThan(bare[0].Stats.MagicAttack));
            Assert.That(
                worn[0].TwiceChance,
                Is.EqualTo(EquipmentStats.TwiceChance(gear, slots[0].Id))
            );
            Assert.That(worn[0].TwiceChance, Is.GreaterThan(0));
            Assert.That(worn[1].Stats.PhysicalAttack, Is.EqualTo(bare[1].Stats.PhysicalAttack));
            Assert.That(BattleBalanceParty.Gear(new BattleBalanceRequest(), slots), Is.Null);
        }

        private static BattleBalanceRecord Battle(
            string encounter,
            string outcome,
            int turns,
            float hpEnd
        ) =>
            new()
            {
                encounter = encounter,
                enemies = "x+y",
                outcome = outcome,
                turns = turns,
                dealt = 1000,
                enemyHp = 1000,
                hpEnd = hpEnd,
                hpLowest = hpEnd / 2f,
            };
    }
}
