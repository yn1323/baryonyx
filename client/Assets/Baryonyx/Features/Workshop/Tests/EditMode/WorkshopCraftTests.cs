using System.Linq;
using System.Threading;
using Baryonyx.Economy;
using Baryonyx.Library;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 商会の「合成」の決まり（doc/features/synthesis.md の仮設定）。サーバー（server/src/features/workshop）と同じ。
    public sealed class WorkshopCraftTests
    {
        private LibraryData data;

        [SetUp]
        public void CreateData()
        {
            data = ScriptableObject.CreateInstance<LibraryData>();
            data.Entries = new[]
            {
                Entry(LibraryKind.Weapon, "wooden-sword"),
                Entry(LibraryKind.Weapon, "iron-sword"),
                Entry(LibraryKind.Weapon, "war-hammer"),
                Entry(LibraryKind.Accessory, "acorn-brooch"),
                Entry(LibraryKind.Accessory, "guardian-circlet"),
                Entry(LibraryKind.Item, "herb"),
                Entry(LibraryKind.Item, "moss-wad"),
            };
        }

        [TearDown]
        public void DestroyData() => Object.DestroyImmediate(data);

        // 作れるのは見つけた☆2以上の物で、主の物（最奥の間でだけ落ちる物）と☆1は作れない。
        [Test]
        public void RecipesAreTheFoundThingsOfTwoStarsOrMoreButNotTheLords()
        {
            var records = new LibraryRecords(
                new (LibraryKind, string, int)[]
                {
                    (LibraryKind.Weapon, "wooden-sword", 0),
                    (LibraryKind.Weapon, "iron-sword", 0),
                    (LibraryKind.Accessory, "acorn-brooch", 0),
                    (LibraryKind.Accessory, "guardian-circlet", 0),
                    (LibraryKind.Item, "moss-wad", 0),
                },
                new LibraryStepDay[0]
            );
            var recipes = WorkshopCraft.Recipes(data, records);
            Assert.That(
                recipes.Select(recipe => recipe.Id),
                Is.EqualTo(new[] { "iron-sword", "acorn-brooch", "moss-wad" })
            );
            Assert.That(
                recipes.Select(recipe => recipe.Runes),
                Is.EqualTo(new[] { 200, 200, 200 })
            );
            Assert.That(WorkshopCraft.BossOnly(LibraryKind.Accessory, "guardian-circlet"), Is.True);
            Assert.That(WorkshopCraft.RunesOf(1), Is.Zero);
            Assert.That(WorkshopCraft.RunesOf(5), Is.EqualTo(1600));
        }

        // 材料は同じ分類で☆が1つ低い、誰も使っていない物で、品質の低い順に並ぶ。
        // アイテムは1つずつの札にし、同じ種類は3つまで、デッキに入れている枠の数は残す。
        [Test]
        public void MaterialsAreUnusedThingsOneStarLowerTheWorseFirst()
        {
            var stock = WorkshopStock.Mock();
            var sword = Recipe(LibraryKind.Weapon, "iron-sword", 2);
            var materials = WorkshopCraft.Materials(sword, stock);
            Assert.That(
                materials.All(thing =>
                    thing.Kind == LibraryKind.Weapon && thing.Stars == 1 && thing.User == null
                ),
                Is.True
            );
            Assert.That(materials.Select(thing => thing.RowId), Does.Not.Contain("mock-w1"));
            Assert.That(
                materials.Select(thing => thing.Multiplier),
                Is.EqualTo(materials.Select(thing => thing.Multiplier).OrderBy(value => value))
            );
            Assert.That(WorkshopCraft.FirstChoice(materials), Is.EqualTo(new[] { 0, 1, 2 }));

            var moss = Recipe(LibraryKind.Item, "moss-wad", 2);
            var units = WorkshopCraft.Materials(moss, stock);
            // 薬草は5つのうちデッキの1つを残して3つまで、どんぐりは2つ。
            Assert.That(units.Count(thing => thing.KindId == "herb"), Is.EqualTo(3));
            Assert.That(units.Count(thing => thing.KindId == "acorn"), Is.EqualTo(2));
            Assert.That(units.All(thing => thing.Count == 1), Is.True);
            Assert.That(WorkshopCraft.CanMake(moss, stock), Is.True);
            Assert.That(
                WorkshopCraft.CanMake(Recipe(LibraryKind.Armor, "chainmail", 3), stock),
                Is.False
            );
        }

        // 出来上がる物の倍率は、材料の倍率の幅の中の位置の平均を、出来上がる物の幅に当てはめる。アイテムは×1.00。
        [Test]
        public void TheMadeMultiplierIsTheMaterialsAveragePlace()
        {
            var sword = Recipe(LibraryKind.Weapon, "iron-sword", 2);
            var materials = new[] { Owned(1.0f, 2.0f), Owned(1.5f, 2.0f), Owned(1.9f, 2.0f) };
            // 位置は 0・0.5・0.9 で平均0.4667。鉄の剣の幅に当てはめて四捨五入する。
            int width = Inventory.Quality.Percent(sword.MaxMultiplier) - 100;
            float expected = (100 + Mathf.RoundToInt(0.46666667f * width)) / 100f;
            Assert.That(
                WorkshopCraft.Multiplier(sword, materials),
                Is.EqualTo(expected).Within(0.0001f)
            );
            Assert.That(
                WorkshopCraft.Multiplier(
                    sword,
                    new[] { Owned(1f, 2f), Owned(1f, 2f), Owned(1f, 2f) }
                ),
                Is.EqualTo(1f)
            );
            Assert.That(
                WorkshopCraft.Multiplier(Recipe(LibraryKind.Item, "moss-wad", 2), materials),
                Is.EqualTo(1f)
            );
        }

        // 合成すると材料が消え（アイテムは数が減り）、出来た物が増える。
        [Test]
        public void ApplyingACraftTakesTheMaterialsAndAddsTheMadeThing()
        {
            var stock = WorkshopStock.Mock();
            var sword = Recipe(LibraryKind.Weapon, "iron-sword", 2);
            var used = WorkshopCraft.Materials(sword, stock).Take(3).ToArray();
            var after = WorkshopCraftView.Applied(
                stock,
                sword,
                used,
                new WorkshopCrafted("new-sword", 1.4f, 200, 800)
            );
            Assert.That(after.Count, Is.EqualTo(stock.Count - 2));
            Assert.That(
                after.Any(thing => used.Any(material => material.RowId == thing.RowId)),
                Is.False
            );
            var made = after.Single(thing => thing.RowId == "new-sword");
            Assert.That(made.KindId, Is.EqualTo("iron-sword"));
            Assert.That(made.Multiplier, Is.EqualTo(1.4f));

            var moss = Recipe(LibraryKind.Item, "moss-wad", 2);
            var units = WorkshopCraft
                .Materials(moss, stock)
                .Where(thing => thing.KindId == "herb")
                .ToArray();
            var items = WorkshopCraftView.Applied(
                stock,
                moss,
                units,
                new WorkshopCrafted("moss-wad", 1f, 200, 600)
            );
            Assert.That(items.Single(thing => thing.KindId == "herb").Count, Is.EqualTo(2));
            Assert.That(items.Single(thing => thing.KindId == "moss-wad").Count, Is.EqualTo(2));
        }

        // サーバーがないときは、仮の残高から費用を引き、材料が3つでなければ断る。
        [Test]
        public void TheMemorySourceCraftsWithAMockBalance()
        {
            RuneBalance.Reset();
            var source = new WorkshopMemorySource(300);
            var sword = Recipe(LibraryKind.Weapon, "iron-sword", 2);
            var used = WorkshopCraft.Materials(sword, WorkshopStock.Mock()).Take(3).ToArray();
            var crafted = source.CraftAsync(sword, used, CancellationToken.None).Result;
            Assert.That(crafted.Runes, Is.EqualTo(200));
            Assert.That(crafted.BalanceAfter, Is.EqualTo(100));
            Assert.That(crafted.Multiplier, Is.EqualTo(WorkshopCraft.Multiplier(sword, used)));
            Assert.That(source.CraftAsync(sword, used, CancellationToken.None).IsFaulted, Is.True);
            Assert.That(
                source.CraftAsync(sword, used.Take(2).ToArray(), CancellationToken.None).IsFaulted,
                Is.True
            );
        }

        private WorkshopRecipe Recipe(LibraryKind kind, string id, int stars) =>
            new(Entry(kind, id), stars, WorkshopCraft.MaxOf(kind, id));

        private static WorkshopOwned Owned(float multiplier, float max) =>
            new()
            {
                Kind = LibraryKind.Weapon,
                RowId = "m" + multiplier,
                Stars = 1,
                Multiplier = multiplier,
                MaxMultiplier = max,
            };

        private static LibraryEntryData Entry(LibraryKind kind, string id) =>
            new()
            {
                Kind = kind,
                Id = id,
                Name = id,
            };
    }
}
