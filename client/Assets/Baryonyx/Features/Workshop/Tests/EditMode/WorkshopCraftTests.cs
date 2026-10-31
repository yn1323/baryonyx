using System.Linq;
using System.Threading;
using Baryonyx.Economy;
using Baryonyx.Library;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 商会の「合成」の決まり（doc/features/synthesis.md のレシピ）。サーバー（server/src/features/workshop）と同じ。
    public sealed class WorkshopCraftTests
    {
        private LibraryData data;

        [SetUp]
        public void CreateData()
        {
            data = ScriptableObject.CreateInstance<LibraryData>();
            data.Entries = WorkshopRecipes
                .All.Select(recipe => Entry(recipe.Kind, recipe.Id))
                .Append(Entry(LibraryKind.Accessory, "guardian-circlet"))
                .ToArray();
        }

        [TearDown]
        public void DestroyData() => Object.DestroyImmediate(data);

        // レシピは、作れる☆2以上の物だけにあり、材料は多くて4種類で、同じ装備・スキルは1つまで。
        [Test]
        public void RecipesMakeTwoStarsOrMoreFromAtMostFourKinds()
        {
            foreach (var recipe in WorkshopRecipes.All)
            {
                Assert.That(
                    WorkshopStock.StarsOf(recipe.Kind, recipe.Id),
                    Is.GreaterThanOrEqualTo(2),
                    recipe.Id
                );
                Assert.That(WorkshopCraft.BossOnly(recipe.Kind, recipe.Id), Is.False, recipe.Id);
                Assert.That(recipe.Materials.Count, Is.InRange(1, 4), recipe.Id);
                Assert.That(
                    recipe
                        .Materials.Where(material => !material.IsItem)
                        .All(material => material.Count == 1),
                    Is.True,
                    recipe.Id
                );
            }
            Assert.That(WorkshopRecipes.Find(LibraryKind.Weapon, "wooden-sword"), Is.Null);
            Assert.That(WorkshopCraft.RunesOf(1), Is.Zero);
            Assert.That(WorkshopCraft.RunesOf(5), Is.EqualTo(1600));
        }

        // 一覧に出るのは、材料のどれか1つでも手に入れたことのある物。作る物を手に入れていなければ伏せる。
        [Test]
        public void RecipesShowOnceAnyMaterialIsFound()
        {
            var records = Records(
                (LibraryKind.Item, "dried-fish"),
                (LibraryKind.Weapon, "war-hammer")
            );
            var recipes = WorkshopCraft.Recipes(data, records);
            // 干し魚を使う物：戦鎚（武器）・旅人のマント（防具）・サンダー・偵察・マナの祈り（スキル）。
            Assert.That(
                recipes.Select(recipe => recipe.Id),
                Is.EquivalentTo(
                    new[] { "war-hammer", "traveler-cloak", "Thunder", "Scout", "ManaPrayer" }
                )
            );
            Assert.That(recipes.Single(recipe => recipe.Id == "war-hammer").Found, Is.True);
            Assert.That(recipes.Single(recipe => recipe.Id == "traveler-cloak").Found, Is.False);
            // 分類の順（武器・防具・アクセサリー・スキル）、同じ分類は☆の低い順。
            Assert.That(recipes[0].Id, Is.EqualTo("war-hammer"));
            Assert.That(recipes.Select(recipe => recipe.Runes).First(), Is.EqualTo(200));
        }

        // アイテムはデッキに入れている数を残した数、装備・スキルは誰も使っていない物の数が使える数。
        // 特別な材料は、初めは品質の低い物を選んでおく。材料がすべてそろえば作れる。
        [Test]
        public void MaterialsCountItemsLessTheDeckAndPiecesNobodyUses()
        {
            var stock = WorkshopStock.Mock();
            var herb = new RecipeMaterial(LibraryKind.Item, "herb", 1);
            Assert.That(WorkshopCraft.HaveOf(herb, stock), Is.EqualTo(4));
            var sword = new RecipeMaterial(LibraryKind.Weapon, "iron-sword", 1);
            Assert.That(WorkshopCraft.HaveOf(sword, stock), Is.EqualTo(2));
            Assert.That(
                WorkshopCraft.ChoicesOf(sword, stock).Select(thing => thing.RowId),
                Is.EqualTo(new[] { "mock-w9", "mock-w10" })
            );
            Assert.That(WorkshopCraft.PiecesOf(sword, stock).Count, Is.EqualTo(3));

            var recipes = WorkshopCraft.Recipes(data, LibraryMemorySource.Mock());
            var ironSword = recipes.Single(recipe => recipe.Id == "iron-sword");
            var dagger = recipes.Single(recipe => recipe.Id == "flame-dagger");
            Assert.That(WorkshopCraft.CanMake(ironSword, stock), Is.True);
            // 海賊のラム酒がない。
            Assert.That(WorkshopCraft.CanMake(dagger, stock), Is.False);
            Assert.That(
                WorkshopCraft.FirstChoice(dagger, stock).Select(thing => thing.RowId),
                Is.EqualTo(new[] { "mock-w9" })
            );
            Assert.That(WorkshopCraft.FirstChoice(ironSword, stock), Is.Empty);
        }

        // 出来上がる物の倍率は、特別な材料の倍率の幅の中の位置の平均を、出来上がる物の幅に当てはめる。
        // アイテムだけのレシピは×1.00（品質C）。
        [Test]
        public void TheMadeMultiplierIsTheSpecialMaterialsAveragePlace()
        {
            var recipes = WorkshopCraft.Recipes(data, LibraryMemorySource.Mock());
            var dagger = recipes.Single(recipe => recipe.Id == "flame-dagger");
            // 鉄の剣（上限×2.0）の×1.5は位置0.5。炎のダガーの幅に当てはめる。
            int width = Inventory.Quality.Percent(dagger.MaxMultiplier) - 100;
            float expected = (100 + Mathf.RoundToInt(0.5f * width)) / 100f;
            Assert.That(
                WorkshopCraft.Multiplier(dagger, new[] { Owned("iron-sword", 1.5f, 2.0f) }),
                Is.EqualTo(expected).Within(0.0001f)
            );
            var ironSword = recipes.Single(recipe => recipe.Id == "iron-sword");
            Assert.That(WorkshopCraft.Multiplier(ironSword, new WorkshopOwned[0]), Is.EqualTo(1f));
        }

        // 合成すると、アイテムは数が減り（使いきった種類は消え）、特別な材料が消え、出来た物が増える。
        [Test]
        public void ApplyingACraftTakesTheMaterialsAndAddsTheMadeThing()
        {
            var stock = WorkshopStock.Mock();
            var recipes = WorkshopCraft.Recipes(data, LibraryMemorySource.Mock());
            var ironSword = recipes.Single(recipe => recipe.Id == "iron-sword");
            var after = WorkshopCraftView.Applied(
                stock,
                ironSword,
                new WorkshopOwned[0],
                new WorkshopCrafted("new-sword", 1f, 200, 800)
            );
            // 貝殻のつぶて2つは使いきって消え、船乗りのロープは3つから1つ減る。
            Assert.That(after.Any(thing => thing.KindId == "shell-shard"), Is.False);
            Assert.That(after.Single(thing => thing.KindId == "sailor-rope").Count, Is.EqualTo(2));
            var made = after.Single(thing => thing.RowId == "new-sword");
            Assert.That(made.KindId, Is.EqualTo("iron-sword"));

            var dagger = recipes.Single(recipe => recipe.Id == "flame-dagger");
            var piece = stock.Single(thing => thing.RowId == "mock-w9");
            var withSword = WorkshopCraftView.Applied(
                stock,
                dagger,
                new[] { piece },
                new WorkshopCrafted("new-dagger", 1.4f, 800, 0)
            );
            Assert.That(withSword.Any(thing => thing.RowId == "mock-w9"), Is.False);
            Assert.That(
                withSword.Single(thing => thing.RowId == "new-dagger").Multiplier,
                Is.EqualTo(1.4f)
            );
        }

        // サーバーがないときは、仮の残高から費用を引き、特別な材料の数が合わなければ断る。
        [Test]
        public void TheMemorySourceCraftsWithAMockBalance()
        {
            RuneBalance.Reset();
            var source = new WorkshopMemorySource(1000);
            var recipes = WorkshopCraft.Recipes(data, LibraryMemorySource.Mock());
            var dagger = recipes.Single(recipe => recipe.Id == "flame-dagger");
            var used = new[] { Owned("iron-sword", 1.5f, 2.0f) };
            var crafted = source.CraftAsync(dagger, used, CancellationToken.None).Result;
            Assert.That(crafted.Runes, Is.EqualTo(800));
            Assert.That(crafted.BalanceAfter, Is.EqualTo(200));
            Assert.That(crafted.Multiplier, Is.EqualTo(WorkshopCraft.Multiplier(dagger, used)));
            Assert.That(source.CraftAsync(dagger, used, CancellationToken.None).IsFaulted, Is.True);
            Assert.That(
                new WorkshopMemorySource(1000)
                    .CraftAsync(dagger, new WorkshopOwned[0], CancellationToken.None)
                    .IsFaulted,
                Is.True
            );
        }

        private static LibraryRecords Records(params (LibraryKind Kind, string Id)[] found) =>
            new(found.Select(entry => (entry.Kind, entry.Id, 0)).ToArray(), new LibraryStepDay[0]);

        private static WorkshopOwned Owned(string id, float multiplier, float max) =>
            new()
            {
                Kind = LibraryKind.Weapon,
                RowId = "m" + multiplier,
                KindId = id,
                Stars = 2,
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
