using System.Linq;
using System.Threading;
using Baryonyx.Combat;
using Baryonyx.Economy;
using Baryonyx.Equipment;
using Baryonyx.Library;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 商会の「買う」：これまでに手に入れた物を品質の最低で売る品物と値段（doc/features/screens/shop.md#商会）。
    public sealed class WorkshopGoodsTests
    {
        private LibraryData data;

        [SetUp]
        public void CreateData()
        {
            data = ScriptableObject.CreateInstance<LibraryData>();
            data.Entries = new[]
            {
                Entry(LibraryKind.Character, "toma"),
                Entry(LibraryKind.Skill, "Hone"),
                Entry(LibraryKind.Skill, "Fire"),
                Entry(LibraryKind.Weapon, "flame-dagger"),
                Entry(LibraryKind.Weapon, "iron-sword"),
                Entry(LibraryKind.Weapon, "wooden-sword"),
                Entry(LibraryKind.Armor, "chainmail"),
                Entry(LibraryKind.Accessory, "moss-charm"),
                Entry(LibraryKind.Item, "herb"),
            };
        }

        [TearDown]
        public void DestroyData() => Object.DestroyImmediate(data);

        // 値段はサーバー（server/src/features/workshop/catalog.ts）と同じで、☆1の200から☆ごとに倍になる。
        [Test]
        public void PricesDoubleWithEachStarAsTheServers()
        {
            Assert.That(WorkshopGoods.PriceOf(LibraryKind.Skill, "Fire"), Is.EqualTo(200));
            Assert.That(WorkshopGoods.PriceOf(LibraryKind.Weapon, "iron-sword"), Is.EqualTo(400));
            Assert.That(
                WorkshopGoods.PriceOf(LibraryKind.Armor, "traveler-cloak"),
                Is.EqualTo(800)
            );
            Assert.That(WorkshopGoods.PriceOf(LibraryKind.Skill, "Hone"), Is.EqualTo(1600));
            Assert.That(
                WorkshopGoods.PriceOf(LibraryKind.Accessory, "kraken-pearl"),
                Is.EqualTo(3200)
            );
            // アイテムと、分類の違う物は売らない。
            Assert.That(WorkshopGoods.PriceOf(LibraryKind.Item, "herb"), Is.Zero);
            Assert.That(WorkshopGoods.PriceOf(LibraryKind.Weapon, "Fire"), Is.Zero);
        }

        // スキル・武器・防具・アクセサリーは、どれも冒険のドロップの一覧にあり、値段が付く。
        [Test]
        public void EverySkillAndEquipmentHasAPrice()
        {
            foreach (var skill in CardSkills.All)
                Assert.That(
                    WorkshopGoods.PriceOf(LibraryKind.Skill, skill.Id),
                    Is.GreaterThan(0),
                    skill.Id
                );
            foreach (var equipment in EquipmentCatalog.All)
            {
                var kind = equipment.Slot switch
                {
                    EquipmentSlot.Weapon => LibraryKind.Weapon,
                    EquipmentSlot.Armor => LibraryKind.Armor,
                    _ => LibraryKind.Accessory,
                };
                Assert.That(
                    WorkshopGoods.PriceOf(kind, equipment.Id),
                    Is.GreaterThan(0),
                    equipment.Id
                );
            }
        }

        // 見つけたスキル・武器・防具・アクセサリーだけを、分類ごとに安い順に並べる。キャラとアイテムは売らない。
        [Test]
        public void GoodsAreTheFoundGearAndSkillsKindByKindTheCheaperFirst()
        {
            var records = new LibraryRecords(
                new (LibraryKind, string, int)[]
                {
                    (LibraryKind.Character, "toma", 0),
                    (LibraryKind.Skill, "Hone", 0),
                    (LibraryKind.Skill, "Fire", 0),
                    (LibraryKind.Weapon, "flame-dagger", 0),
                    (LibraryKind.Weapon, "wooden-sword", 0),
                    (LibraryKind.Accessory, "moss-charm", 0),
                    (LibraryKind.Item, "herb", 0),
                },
                new LibraryStepDay[0]
            );
            var goods = WorkshopGoods.Of(data, records);
            Assert.That(
                goods.Select(good => good.Id),
                Is.EqualTo(new[] { "wooden-sword", "flame-dagger", "moss-charm", "Fire", "Hone" })
            );
            Assert.That(
                goods.Select(good => good.Price),
                Is.EqualTo(new[] { 200, 1600, 200, 200, 1600 })
            );
            Assert.That(WorkshopGoods.Of(data, LibraryRecords.Empty), Is.Empty);
        }

        // 選んだ品物は、ルーンが足りなければ足りない数を出す。残高を読めていなければ、足りないとは言わない。
        [Test]
        public void MissingTellsTheRunesShort()
        {
            var good = new WorkshopGood(Entry(LibraryKind.Weapon, "iron-sword"), 2, 400);
            Assert.That(WorkshopBuyView.MissingText(good, 1000), Is.Empty);
            Assert.That(
                WorkshopBuyView.MissingText(good, 150),
                Is.EqualTo("ルーンが 250 足りません")
            );
            Assert.That(WorkshopBuyView.MissingText(good, null), Is.Empty);
            Assert.That(WorkshopBuyView.PriceText(1600), Is.EqualTo("1,600 ルーン"));
        }

        // サーバーがないときは、仮の残高から値段を引き、足りなければ断る。
        [Test]
        public void TheMemorySourceSpendsAMockBalance()
        {
            // 画面の残高を読む前は、仮の残高を使う。
            RuneBalance.Reset();
            var source = new WorkshopMemorySource(500);
            var good = new WorkshopGood(Entry(LibraryKind.Weapon, "iron-sword"), 2, 400);
            var purchase = source.BuyAsync(good, CancellationToken.None).Result;
            Assert.That(purchase.Runes, Is.EqualTo(400));
            Assert.That(purchase.BalanceAfter, Is.EqualTo(100));
            Assert.That(source.BuyAsync(good, CancellationToken.None).IsFaulted, Is.True);
            Assert.That(source.Runes, Is.EqualTo(100));
        }

        private static LibraryEntryData Entry(LibraryKind kind, string id) =>
            new()
            {
                Kind = kind,
                Id = id,
                Name = id,
            };
    }
}
