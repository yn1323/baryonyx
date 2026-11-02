using System;
using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // ドロップエディターの下書き（doc/features/screens/debug-rooms.md#ドロップエディター）。表から始まり、
    // 物を移す・外す・最奥だけにする・☆を変える、報酬の物・数・品質を変える。コピーは変えたダンジョンだけをJSONにする。
    public sealed class DropLabDraftsTests
    {
        private const string Forest = AdventureCatalog.ForestRuins;
        private const string Port = "port-portoria";

        // コピーしたJSONを読む形。
        [Serializable]
        private sealed class Copied
        {
            public string format;
            public Dungeon[] dungeons;
        }

        [Serializable]
        private sealed class Dungeon
        {
            public string id;
            public string name;
            public Drop[] drops;
            public Reward[] clearRewards;
        }

        [Serializable]
        private sealed class Drop
        {
            public string category;
            public string id;
            public string name;
            public int stars;
            public bool boss;
        }

        [Serializable]
        private sealed class Reward
        {
            public int clears;
            public string category;
            public string id;
            public string name;
            public int count;
            public string quality;
        }

        private static string[] Ids(DropLabDrafts.Draft draft, AdventureDropCategory category) =>
            draft
                .Drops.Where(entry => entry.Category == category)
                .Select(entry => entry.Id)
                .ToArray();

        [Test]
        public void TheDraftsStartAsTheTablesWithNothingChanged()
        {
            var drafts = new DropLabDrafts();
            Assert.That(
                drafts.All.Select(draft => draft.Id),
                Is.EqualTo(
                    AdventureCatalog
                        .Areas.Select(area => area.Id)
                        .Where(AdventureDrops.Pools.ContainsKey)
                )
            );
            foreach (var draft in drafts.All)
            {
                Assert.That(draft.Drops, Is.EqualTo(AdventureDrops.Pools[draft.Id]), draft.Id);
                var rewards = AdventureClearRewards.Of(draft.Id);
                Assert.That(draft.Rewards.Length, Is.EqualTo(rewards.Count), draft.Id);
                for (int i = 0; i < rewards.Count; i++)
                {
                    Assert.That(draft.Rewards[i].Id, Is.EqualTo(rewards[i].Drop.Id));
                    Assert.That(draft.Rewards[i].Count, Is.EqualTo(rewards[i].Count));
                    Assert.That(draft.Rewards[i].Quality, Is.EqualTo(rewards[i].Drop.Quality));
                }
            }
            Assert.That(drafts.ChangedDrafts, Is.Empty);
            var copied = JsonUtility.FromJson<Copied>(drafts.ChangesJson());
            Assert.That(copied.format, Is.EqualTo(DropLabDrafts.Format));
            Assert.That(copied.dungeons, Is.Empty);
        }

        // 物は1つのダンジョンにだけ落ちるため、ほかの一覧にある物を足すと、そこから移す。
        [Test]
        public void AddingAKindListedElsewhereMovesItIntoItsPlace()
        {
            var drafts = new DropLabDrafts();
            Assert.That(
                drafts.Add(Forest, AdventureDropCategory.Skill, "ManaPrayer", out string moved),
                Is.True
            );
            Assert.That(moved, Is.EqualTo("港町ポルトリアの一覧から移しました"));
            Assert.That(drafts.Find(Port).Has(AdventureDropCategory.Skill, "ManaPrayer"), Is.False);
            // スキルの☆は移す前の一覧のまま（☆4）で、分類の中の☆の順に入る。
            Assert.That(
                Ids(drafts.Find(Forest), AdventureDropCategory.Skill),
                Is.EqualTo(
                    new[]
                    {
                        "Slash",
                        "Fire",
                        "Heal",
                        "VitalThrust",
                        "ShieldBash",
                        "Embers",
                        "Hone",
                        "ManaPrayer",
                    }
                )
            );
            Assert.That(drafts.StarsOf(AdventureDropCategory.Skill, "ManaPrayer"), Is.EqualTo(4));
            Assert.That(
                drafts.ChangedDrafts.Select(draft => draft.Id),
                Is.EqualTo(new[] { Forest, Port })
            );

            Assert.That(
                drafts.Add(Forest, AdventureDropCategory.Skill, "ManaPrayer", out string twice),
                Is.False
            );
            Assert.That(twice, Is.EqualTo("すでに一覧にあります"));
            Assert.That(
                drafts.Add(Forest, AdventureDropCategory.Material, "polish-white", out _),
                Is.False
            );

            // 移した物を元の一覧へ戻すと、変えたダンジョンはなくなる。
            Assert.That(
                drafts.Add(Port, AdventureDropCategory.Skill, "ManaPrayer", out _),
                Is.True
            );
            Assert.That(drafts.ChangedDrafts, Is.Empty);
        }

        // 最奥の間では☆3〜5だけが落ちるため、最奥だけにできるのは☆3以上。最奥だけの物は分類の最後に並ぶ。
        [Test]
        public void OnlyKindsOfThreeStarsOrMoreFallOnlyAtTheBoss()
        {
            var drafts = new DropLabDrafts();
            Assert.That(
                drafts.ToggleBoss(Forest, AdventureDropCategory.Item, "herb", out string refused),
                Is.False
            );
            Assert.That(refused, Is.EqualTo("最奥だけで落ちる物は☆3以上にします"));
            Assert.That(
                drafts.ToggleBoss(Forest, AdventureDropCategory.Item, "spore-powder", out _),
                Is.True
            );
            var forest = drafts.Find(Forest);
            Assert.That(
                forest.Drops[forest.IndexOf(AdventureDropCategory.Item, "spore-powder")].BossOnly,
                Is.True
            );
            Assert.That(
                Ids(forest, AdventureDropCategory.Item).Skip(4),
                Is.EqualTo(new[] { "spore-powder", "guardian-seed" })
            );
            Assert.That(drafts.DropsChanged(Forest), Is.True);
            Assert.That(drafts.RewardsChanged(Forest), Is.False);
        }

        // スキルの☆は一覧で決まるため変えられ、☆5の次は☆1へ戻る。ほかの物の☆は定義のまま。
        [Test]
        public void ASkillsStarsTurnAndOthersStayAsDefined()
        {
            var drafts = new DropLabDrafts();
            var forest = drafts.Find(Forest);
            Assert.That(drafts.NextStars(Forest, AdventureDropCategory.Skill, "Slash"), Is.True);
            Assert.That(
                forest.Drops[forest.IndexOf(AdventureDropCategory.Skill, "Slash")].Stars,
                Is.EqualTo(2)
            );
            Assert.That(drafts.NextStars(Forest, AdventureDropCategory.Skill, "Hone"), Is.True);
            Assert.That(drafts.NextStars(Forest, AdventureDropCategory.Skill, "Hone"), Is.True);
            Assert.That(
                forest.Drops[forest.IndexOf(AdventureDropCategory.Skill, "Hone")].Stars,
                Is.EqualTo(1)
            );
            Assert.That(drafts.NextStars(Forest, AdventureDropCategory.Item, "herb"), Is.False);

            Assert.That(drafts.Remove(Forest, AdventureDropCategory.Skill, "Hone"), Is.True);
            Assert.That(forest.Has(AdventureDropCategory.Skill, "Hone"), Is.False);
            Assert.That(drafts.ListOf(AdventureDropCategory.Skill, "Hone"), Is.Null);
            Assert.That(drafts.Remove(Forest, AdventureDropCategory.Skill, "Hone"), Is.False);
        }

        // 報酬は、物を替えると1つになり、スキルと装備は品質を保つ。数は1〜99、品質はC→B→A→S→Cと回る。
        [Test]
        public void ARewardsKindCountAndQualityChange()
        {
            var drafts = new DropLabDrafts();
            Assert.That(
                drafts.SetReward(Forest, 1, AdventureDropCategory.Skill, "Meteor"),
                Is.True
            );
            var first = drafts.RewardOf(Forest, 1);
            Assert.That(
                (first.Category, first.Id, first.Count, first.Quality),
                Is.EqualTo((AdventureDropCategory.Skill, "Meteor", 1, (ItemQuality?)ItemQuality.A))
            );
            Assert.That(drafts.NextQuality(Forest, 1), Is.True);
            Assert.That(first.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(drafts.NextQuality(Forest, 1), Is.True);
            Assert.That(first.Quality, Is.EqualTo(ItemQuality.C));

            Assert.That(drafts.SetCount(Forest, 2, 15, out _), Is.True);
            Assert.That(drafts.RewardOf(Forest, 2).Count, Is.EqualTo(15));
            Assert.That(drafts.SetCount(Forest, 2, 0, out string low), Is.False);
            Assert.That(low, Is.EqualTo("数は1〜99にします"));
            Assert.That(drafts.SetCount(Forest, 2, 100, out _), Is.False);

            // 強化素材とアイテムは品質を持たない。
            Assert.That(drafts.SetReward(Forest, 5, AdventureDropCategory.Item, "herb"), Is.True);
            Assert.That(drafts.RewardOf(Forest, 5).Quality, Is.Null);
            Assert.That(drafts.NextQuality(Forest, 5), Is.False);
            Assert.That(drafts.RewardOf(Forest, 6), Is.Null);
            Assert.That(drafts.DropsChanged(Forest), Is.False);
            Assert.That(drafts.RewardsChanged(Forest), Is.True);
        }

        // コピーは、変えたダンジョンの、変えた方（一覧全体か5回分の報酬）だけを書く。
        [Test]
        public void TheCopyHoldsOnlyWhatChanged()
        {
            var drafts = new DropLabDrafts();
            drafts.ToggleBoss(Forest, AdventureDropCategory.Item, "spore-powder", out _);
            drafts.SetCount(Port, 2, 12, out _);
            string json = drafts.ChangesJson();
            var copied = JsonUtility.FromJson<Copied>(json);
            Assert.That(copied.format, Is.EqualTo(DropLabDrafts.Format));
            Assert.That(
                copied.dungeons.Select(dungeon => dungeon.id),
                Is.EqualTo(new[] { Forest, Port })
            );

            var forest = copied.dungeons[0];
            Assert.That(forest.name, Is.EqualTo("ミストラ遺跡"));
            Assert.That(forest.drops.Length, Is.EqualTo(AdventureDrops.Pools[Forest].Length));
            var spore = forest.drops.Single(drop => drop.id == "spore-powder");
            Assert.That(
                (spore.category, spore.name, spore.stars, spore.boss),
                Is.EqualTo(("item", "毒胞子の粉", 3, true))
            );
            Assert.That(forest.drops.Single(drop => drop.id == "herb").boss, Is.False);

            var port = copied.dungeons[1];
            Assert.That(
                port.clearRewards.Select(reward => reward.clears),
                Is.EqualTo(new[] { 1, 2, 3, 4, 5 })
            );
            var second = port.clearRewards[1];
            Assert.That(
                (second.category, second.id, second.count),
                Is.EqualTo(("material", "polish-white", 12))
            );
            Assert.That(second.quality, Is.Null.Or.Empty);
            Assert.That(port.clearRewards[0].quality, Is.EqualTo("A"));

            // 変えなかった方の項目は書かない。
            Assert.That(json.Split("\"drops\"").Length - 1, Is.EqualTo(1));
            Assert.That(json.Split("\"clearRewards\"").Length - 1, Is.EqualTo(1));
        }

        // 落ちる物1つごとの割合は、落ちうる物の分だけ足すと1になり、落ちない所では0。
        [Test]
        public void TheChancesOfAPoolAddUpToOne()
        {
            foreach (var pool in AdventureDrops.Pools.Values)
            foreach (
                var row in new[]
                {
                    AdventureRoomKind.Battle,
                    AdventureRoomKind.Elite,
                    AdventureRoomKind.Boss,
                }
            )
            {
                float sum = pool.Sum(entry => AdventureDrops.ChanceOf(pool, entry, row));
                Assert.That(sum, Is.EqualTo(1f).Within(0.0001f), row.ToString());
            }
            var forest = AdventureDrops.Pools[Forest];
            var seed = forest.Single(entry => entry.Id == "guardian-seed");
            var herb = forest.Single(entry => entry.Id == "herb");
            Assert.That(AdventureDrops.ChanceOf(forest, seed, AdventureRoomKind.Battle), Is.Zero);
            Assert.That(
                AdventureDrops.ChanceOf(forest, seed, AdventureRoomKind.Boss),
                Is.GreaterThan(0f)
            );
            Assert.That(AdventureDrops.ChanceOf(forest, herb, AdventureRoomKind.Boss), Is.Zero);
        }
    }
}
