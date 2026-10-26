using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Economy;
using Baryonyx.Library;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class WorkshopCraftTests
    {
        private TestGameServices services;
        private FakeWorkshop shop;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            LibrarySession.Reset();
            WorkshopSession.Reset();
            shop = new FakeWorkshop();
            WorkshopSession.Source = shop;
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            LibrarySession.Reset();
            WorkshopSession.Reset();
            yield return SceneTests.UnloadAll(nameof(WorkshopCraftTests));
        }

        // 商会の「合成」は、案内人を隠して画面いっぱいに、作れる物を分類のタブごとに並べる。選ぶと、☆が1つ低い
        // 同じ分類の使っていない物を材料に並べ、品質の低い3つを選んでおく。開いたときは先頭の物を選んでおく。「合成」を押すと、サーバーの答えを
        // 待ってから材料を減らし、出来た物を持ち物に足して知らせる。材料が3つでなければ押せない。
        [UnityTest]
        public IEnumerator CraftMakesTheChosenThingFromThreeMaterials()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            yield return SceneTests.WaitForTask(guide.RunesTask);
            RuneBalance.Set(1000);
            var view = guide.View;
            int item = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == WorkshopSession.CraftItemKey
            );
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            var craft = view.PanelFor(item).GetComponent<WorkshopCraftView>();
            view.MenuItems[item].onClick.Invoke();
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(view.Framed, Is.True);
            yield return SceneTests.WaitForTask(craft.LoadTask);
            // 読み終えたら伏せた行をすべてめくり、作れる物のない場所の行は消えていく。
            Assert.That(craft.Rows[0].GetComponent<GlassCardBack>(), Is.Not.Null);
            yield return SceneTests.WaitUntil(
                () =>
                    craft.Rows.All(row =>
                        row.GetComponent<GlassCardBack>() is not { } back
                        || !(back.FaceDown || back.Flipping || back.Leaving)
                    ),
                message: "The rows did not turn face up."
            );
            Assert.That(
                craft.Rows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(craft.Shown.Count)
            );
            // めくり終えても、材料の足りない物は薄いまま。
            for (int i = 0; i < craft.Shown.Count; i++)
                Assert.That(
                    craft.Rows[i].Card.Group.alpha,
                    Is.EqualTo(WorkshopCraft.CanMake(craft.Shown[i], craft.Owned) ? 1f : 0.5f)
                        .Within(0.001f),
                    craft.Shown[i].Name
                );

            // サーバーがないときは、ライブラリの仮の記録と仮の持ち物を使う。初めは武器のタブ。
            Assert.That(craft.Shown, Is.Not.Empty);
            Assert.That(
                craft.Shown.All(recipe => recipe.Kind == LibraryKind.Weapon && recipe.Stars >= 2),
                Is.True
            );
            // 右の面が空にならないよう、先頭の物を選んでおく。
            Assert.That(craft.Selected, Is.EqualTo(0));
            Assert.That(craft.Rows[0].Card.Selected, Is.True);
            Assert.That(craft.DetailName.text, Is.EqualTo(craft.Shown[0].Name));
            SceneTests.AssertTouchSize(craft.Confirm.transform, 0.7f);

            int index = craft
                .Shown.ToList()
                .FindIndex(recipe => WorkshopCraft.CanMake(recipe, craft.Owned));
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var chosen = craft.Shown[index];
            craft.Rows[index].Card.Click();
            Assert.That(craft.DetailName.text, Is.EqualTo(chosen.Name));
            // 材料は☆が1つ低い同じ分類で、使っている物は入らない。品質の低い3つを選んでおく。
            Assert.That(
                craft.Materials.Count,
                Is.GreaterThanOrEqualTo(WorkshopCraft.MaterialCount)
            );
            Assert.That(
                craft.Materials.All(thing =>
                    thing.Kind == chosen.Kind
                    && thing.Stars == chosen.Stars - 1
                    && thing.User == null
                ),
                Is.True
            );
            Assert.That(craft.Picked, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(craft.MaterialRows[0].Card.Selected, Is.True);
            Assert.That(craft.MaterialCount.text, Is.EqualTo("3 / 3"));
            Assert.That(craft.DetailQuality.text, Does.Contain("品質"));
            Assert.That(craft.PriceLabel.text, Is.EqualTo(chosen.Runes.ToString("N0")));
            Assert.That(craft.Confirm.interactable, Is.True);

            // 1つ外すと押せない。4つ目は選べない。
            craft.ToggleMaterial(0);
            Assert.That(craft.Picked.Count, Is.EqualTo(2));
            Assert.That(craft.Confirm.interactable, Is.False);
            Assert.That(craft.DetailQuality.text, Is.Empty);
            craft.ToggleMaterial(craft.Materials.Count - 1);
            Assert.That(craft.Picked.Count, Is.EqualTo(3));
            if (craft.Materials.Count > 3)
            {
                craft.ToggleMaterial(0);
                Assert.That(craft.Picked.Count, Is.EqualTo(3));
            }

            // 失敗したら知らせ、読み直す。持ち物は変わらない。
            shop.Fail = true;
            craft.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(craft.Work);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopCraftView.FailedMessage));
            yield return SceneTests.WaitForTask(craft.LoadTask);
            Assert.That(shop.Crafted, Is.Empty);
            shop.Fail = false;
            craft.Rows[index].Card.Click();
            Assert.That(craft.Picked.Count, Is.EqualTo(WorkshopCraft.MaterialCount));

            var used = craft.Picked.Select(i => craft.Materials[i]).ToArray();
            int before = craft.Owned.Count(thing => thing.Kind == chosen.Kind);
            craft.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(craft.Work);
            Assert.That(shop.Crafted.Single().Recipe, Is.EqualTo(chosen.Id));
            Assert.That(
                shop.Crafted.Single().Materials,
                Is.EqualTo(used.Select(thing => thing.RowId))
            );
            float made = WorkshopCraft.Multiplier(chosen, used);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopCraftView.CraftedText(chosen, made)));
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 - chosen.Runes));
            // 材料3つが消え、出来た物が1つ増える。
            Assert.That(
                craft.Owned.Count(thing => thing.Kind == chosen.Kind),
                Is.EqualTo(before - 2)
            );
            Assert.That(
                craft.Owned.Any(thing =>
                    thing.RowId == shop.LastItemId && thing.KindId == chosen.Id
                ),
                Is.True
            );
            Assert.That(
                craft.Owned.Any(thing => used.Any(material => material.RowId == thing.RowId)),
                Is.False
            );

            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(craft.gameObject.activeInHierarchy, Is.False);
        }

        // 商会の「売る」は、品質がそれぞれ違うため、持ち物を1つずつ別の札で並べる。使っている物には「E」を付けて
        // 選べなくする。いくつでも選べ、右に選んだ物と合計を出す。「売る」を押すと、サーバーの答えを待ってから
        // 売った物を一覧から外し、得たルーンを残高にして知らせる。売れなければ知らせて読み直す。
        [UnityTest]
        public IEnumerator SellListsEachHeldThingApartAndSumsTheChosen()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            var view = guide.View;
            int item = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == WorkshopSession.SellItemKey
            );
            var sell = view.PanelFor(item).GetComponent<WorkshopSellView>();
            view.MenuItems[item].onClick.Invoke();
            Assert.That(view.GuideArt.activeSelf, Is.False);
            yield return SceneTests.WaitForTask(sell.LoadTask);
            // 読み終えたら伏せたカードをすべてめくり、持ち物のない場所のカードは消えていく。
            yield return SceneTests.WaitUntil(
                () =>
                    sell.Rows.All(row =>
                        row.GetComponent<GlassCardBack>() is not { } back
                        || !(back.FaceDown || back.Flipping || back.Leaving)
                    ),
                message: "The cards did not turn face up."
            );

            var weapons = WorkshopStock.OfKind(WorkshopStock.Mock(), LibraryKind.Weapon);
            Assert.That(sell.Shown.Count, Is.EqualTo(weapons.Count));
            var rows = sell.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(rows.Length, Is.EqualTo(weapons.Count));
            // 同じ種類でも、1つずつ別の札にし、数は出さない。
            Assert.That(rows.All(row => row.Count.text == ""), Is.True);
            Assert.That(rows.All(row => row.Quality.text.Length == 1), Is.True);
            int worn = sell.Shown.ToList().FindIndex(thing => thing.User != null);
            Assert.That(rows[worn].InUse.activeSelf, Is.True);
            Assert.That(sell.Confirm.interactable, Is.False);

            // 使っている物は選べない。
            sell.Toggle(worn);
            Assert.That(sell.Chosen, Is.Empty);
            var free = Enumerable
                .Range(0, sell.Shown.Count)
                .Where(i => sell.Shown[i].User == null)
                .Take(2)
                .ToArray();
            foreach (int i in free)
                rows[i].Card.Click();
            Assert.That(sell.Chosen.Count, Is.EqualTo(2));
            Assert.That(sell.ChosenRows.Count(row => row.gameObject.activeSelf), Is.EqualTo(2));
            int total = free.Sum(i => WorkshopSellView.PriceOf(sell.Shown[i]));
            Assert.That(sell.Total.text, Is.EqualTo("+" + total.ToString("N0")));

            // 選び直すと、何も選んでいない状態に戻る。
            sell.Clear.onClick.Invoke();
            Assert.That(sell.Chosen, Is.Empty);
            Assert.That(sell.Total.text, Is.EqualTo("+0"));

            // 売れないときは知らせ、持ち物を読み直す。
            var picked = free.Select(i => sell.Shown[i]).ToArray();
            foreach (int i in free)
                sell.Toggle(i);
            shop.Fail = true;
            sell.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(sell.Work);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopSellView.FailedMessage));
            yield return SceneTests.WaitForTask(sell.LoadTask);
            Assert.That(sell.Shown.Count, Is.EqualTo(weapons.Count));

            // 売ると、売った物が一覧から外れ、得たルーンが残高になる。
            shop.Fail = false;
            RuneBalance.Set(1000);
            foreach (int i in free)
                sell.Toggle(i);
            sell.Confirm.onClick.Invoke();
            Assert.That(sell.Confirm.interactable, Is.False);
            yield return SceneTests.WaitForTask(sell.Work);
            Assert.That(shop.Sold, Is.EquivalentTo(picked.Select(thing => thing.RowId)));
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 + total));
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopSellView.SoldText(2, total)));
            Assert.That(sell.Chosen, Is.Empty);
            Assert.That(sell.Shown.Count, Is.EqualTo(weapons.Count - 2));
            Assert.That(
                sell.Owned.Any(thing => picked.Any(sold => sold.RowId == thing.RowId)),
                Is.False
            );
            Assert.That(
                sell.Rows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(weapons.Count - 2)
            );

            // アイテムには品質がないため、種類ごとに1枚で数を出す。
            sell.Tabs[WorkshopStock.Kinds.ToList().IndexOf(LibraryKind.Item)]
                .Button.onClick.Invoke();
            var items = sell.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(items, Is.Not.Empty);
            Assert.That(
                items.All(row => row.Count.text.StartsWith("×") && row.Quality.text == ""),
                Is.True
            );
        }

        // 合成した物・売った物を覚える商会の代役。Fail のあいだは失敗する。
        private sealed class FakeWorkshop : IWorkshopSource
        {
            public readonly List<(string Recipe, string[] Materials)> Crafted = new();
            public readonly List<string> Sold = new();
            public long Runes { get; set; } = 1000;
            public bool Fail { get; set; }
            public string LastItemId { get; private set; }

            public Task<WorkshopPurchase> BuyAsync(WorkshopGood good, CancellationToken token) =>
                Task.FromException<WorkshopPurchase>(new NotSupportedException());

            public Task<WorkshopSale> SellAsync(
                IReadOnlyList<WorkshopOwned> things,
                CancellationToken token
            )
            {
                if (Fail)
                    return Task.FromException<WorkshopSale>(
                        new InvalidOperationException("売れない設定")
                    );
                Sold.AddRange(things.Select(thing => thing.RowId));
                int runes = things.Sum(WorkshopSellView.PriceOf);
                return Task.FromResult(new WorkshopSale(runes, (RuneBalance.Known ?? 0) + runes));
            }

            public Task<WorkshopCrafted> CraftAsync(
                WorkshopRecipe recipe,
                IReadOnlyList<WorkshopOwned> materials,
                CancellationToken token
            )
            {
                if (Fail)
                    return Task.FromException<WorkshopCrafted>(
                        new InvalidOperationException("作れない設定")
                    );
                Crafted.Add((recipe.Id, materials.Select(thing => thing.RowId).ToArray()));
                Runes -= recipe.Runes;
                LastItemId = "made-" + Crafted.Count;
                return Task.FromResult(
                    new WorkshopCrafted(
                        LastItemId,
                        WorkshopCraft.Multiplier(recipe, materials),
                        recipe.Runes,
                        Runes
                    )
                );
            }
        }
    }
}
