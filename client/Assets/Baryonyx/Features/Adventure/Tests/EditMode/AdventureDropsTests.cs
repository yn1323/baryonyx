using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Adventure;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.Items;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 冒険のドロップ（doc/features/drops.md）。一覧はサーバー（server/src/features/adventure/drops.ts）と同じにする。
    public sealed class AdventureDropsTests
    {
        private static string ServerFile(params string[] path) =>
            File.ReadAllText(
                Path.Combine(
                    new[] { Application.dataPath, "..", "..", "server", "src", "features" }
                        .Concat(path)
                        .ToArray()
                )
            );

        // 品質のあるドロップは、倍率がその品質の幅に入る。冒険の状態のテストからも使う。
        internal static void AssertQualityFitsMultiplier(AdventureDrop drop)
        {
            if (drop.Quality is { } quality)
                Assert.That(
                    Quality.Of(
                        drop.Multiplier,
                        AdventureTexts.MaxMultiplierOf(drop.Category, drop.Id)
                    ),
                    Is.EqualTo(quality),
                    drop.Id
                );
        }

        private static (AdventureDropCategory, string, int, bool)[] Tuples(
            IEnumerable<AdventureDrops.Entry> entries
        ) =>
            entries
                .Select(entry => (entry.Category, entry.Id, entry.Stars, entry.BossOnly))
                .ToArray();

        [Test]
        public void TheListsMatchTheServer()
        {
            var source = ServerFile("adventure", "drops.ts");
            var pools = Regex
                .Matches(source, "\"([a-z-]+)\": \\[(.*?)\\n  \\],", RegexOptions.Singleline)
                .Cast<Match>()
                .ToDictionary(
                    match => match.Groups[1].Value,
                    match =>
                        Regex
                            .Matches(
                                match.Groups[2].Value,
                                "(item|accessory|weapon|armor|skill)\\(\"([^\"]+)\", ([1-5])(, true)?\\)"
                            )
                            .Cast<Match>()
                            .Select(entry =>
                                (
                                    Parse(entry.Groups[1].Value),
                                    entry.Groups[2].Value,
                                    int.Parse(entry.Groups[3].Value),
                                    entry.Groups[4].Success
                                )
                            )
                            .ToArray()
                );
            Assert.That(pools.Keys, Is.EquivalentTo(AdventureDrops.Pools.Keys));
            foreach (var pair in AdventureDrops.Pools)
                Assert.That(Tuples(pair.Value), Is.EqualTo(pools[pair.Key]), pair.Key);
        }

        private static AdventureDropCategory Parse(string key)
        {
            Assert.That(AdventureDrops.TryParse(key, out var category), Is.True, key);
            return category;
        }

        // 一覧の物はどれも定義があり、アイテムと装備の☆は定義のレア度と同じ。
        [Test]
        public void EveryListedKindHasADefinitionWithTheSameStars()
        {
            var listed = AdventureDrops.Pools.Values.SelectMany(pool => pool).ToArray();
            foreach (var entry in listed)
            {
                switch (entry.Category)
                {
                    case AdventureDropCategory.Item:
                        Assert.That(
                            ItemCards.Find(entry.Id)?.Rarity,
                            Is.EqualTo(entry.Stars),
                            entry.Id
                        );
                        break;
                    case AdventureDropCategory.Skill:
                        Assert.That(CardSkills.Find(entry.Id), Is.Not.Null, entry.Id);
                        break;
                    default:
                        var gear = EquipmentCatalog.Find(entry.Id);
                        Assert.That(gear?.Rarity, Is.EqualTo(entry.Stars), entry.Id);
                        Assert.That(
                            EquipmentCatalog.KeyOf(gear.Slot),
                            Is.EqualTo(AdventureDrops.KeyOf(entry.Category)),
                            entry.Id
                        );
                        break;
                }
            }
            Assert.That(
                listed
                    .Where(entry => entry.Category == AdventureDropCategory.Skill)
                    .Select(entry => entry.Id),
                Is.EquivalentTo(CardSkills.All.Select(card => card.Id))
            );
            Assert.That(
                listed
                    .Where(entry => entry.Category == AdventureDropCategory.Item)
                    .Select(entry => entry.Id),
                Is.EquivalentTo(ItemCards.All.Select(item => item.Id))
            );
        }

        // アイテムの種類はサーバー（server/src/features/items/catalog.ts）と同じ。
        [Test]
        public void TheItemsMatchTheServer()
        {
            var block = Regex.Match(
                ServerFile("items", "catalog.ts"),
                "ITEM_IDS = \\[(.*?)\\] as const",
                RegexOptions.Singleline
            );
            var server = Regex
                .Matches(block.Groups[1].Value, "\"([a-z-]+)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value);
            Assert.That(ItemCards.All.Select(item => item.Id), Is.EqualTo(server));
        }

        // マスごとの数だけ落とし、最奥の間では☆3以上だけが落ちる。品質は倍率の幅に合う。
        // 休憩と行商人は落とさず、怪しい宝箱（本物）は宝箱より1つ多い（サーバーの rewardOf と同じ）。
        [Test]
        public void ARoomDropsItsCountAndQualitiesFitTheirMultipliers()
        {
            var random = new System.Random(3);
            var counts = new Dictionary<AdventureRoomKind, HashSet<int>>();
            foreach (
                var kind in new[]
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
                }
            )
            {
                counts[kind] = new HashSet<int>();
                for (int i = 0; i < 300; i++)
                {
                    var drops = AdventureDrops.Roll(
                        AdventureCatalog.ForestRuins,
                        kind,
                        "room",
                        AdventureTexts.MaxMultiplierOf,
                        random
                    );
                    counts[kind].Add(drops.Count);
                    foreach (var drop in drops)
                    {
                        if (kind == AdventureRoomKind.Boss)
                            Assert.That(drop.Stars, Is.GreaterThanOrEqualTo(3), drop.Id);
                        Assert.That(
                            drop.Quality.HasValue,
                            Is.EqualTo(drop.Category != AdventureDropCategory.Item)
                        );
                        AssertQualityFitsMultiplier(drop);
                    }
                }
            }
            Assert.That(counts[AdventureRoomKind.Battle], Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(counts[AdventureRoomKind.Elite], Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(counts[AdventureRoomKind.Boss], Is.EquivalentTo(new[] { 2, 3, 4 }));
            Assert.That(counts[AdventureRoomKind.Rest], Is.EquivalentTo(new[] { 0 }));
            Assert.That(counts[AdventureRoomKind.Merchant], Is.EquivalentTo(new[] { 0 }));
            Assert.That(counts[AdventureRoomKind.ShadyChest], Is.EquivalentTo(new[] { 2, 3, 4 }));
            Assert.That(counts[AdventureRoomKind.Trial], Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(counts[AdventureRoomKind.StepDoor], Is.EquivalentTo(new[] { 1, 2 }));
            Assert.That(counts[AdventureRoomKind.Altar], Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ADropReadsAsALineOfStarsNameCategoryAndQuality()
        {
            Assert.That(
                AdventureTexts.DropLine(
                    new AdventureDrop(
                        "",
                        AdventureDropCategory.Armor,
                        "iron-helm",
                        1.2f,
                        ItemQuality.C
                    )
                ),
                Is.EqualTo("★★ 鉄の兜（防具・品質C）")
            );
            Assert.That(
                AdventureTexts.DropLine(new AdventureDrop("", AdventureDropCategory.Item, "herb")),
                Is.EqualTo("★ 薬草（アイテム）")
            );
            Assert.That(
                AdventureTexts.DropsBody(Array.Empty<AdventureDrop>()),
                Is.EqualTo("何も見つからなかった")
            );
        }
    }
}
