using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
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
            EnhancementSession.Reset();
            yield return SceneTests.UnloadAll(nameof(WorkshopCraftTests));
        }

        // 合成の最後のタブ「磨き石」は、合成で作れる青・紫・金を並べ、選ぶと1つ下の色の札を材料に1枚出す。
        // 下の色3個とルーンがそろうと押せ、演出が流れ、サーバーの答えで石の数と残高を書き換える。
        [UnityTest]
        public IEnumerator StonesTabMakesOneStoneFromThreeOfTheColourBelow()
        {
            EnhancementSession.Source = new EnhancementMemorySource(
                new Dictionary<string, int> { [EnhancementMaterials.White] = 7 },
                1000,
                shownBalance: false
            );
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            yield return SceneTests.WaitForTask(guide.RunesTask);
            RuneBalance.Set(1000);
            var view = guide.View;
            int item = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == WorkshopSession.CraftItemKey
            );
            var craft = view.PanelFor(item).GetComponent<WorkshopCraftView>();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitForTask(craft.LoadTask);

            craft.Tabs[WorkshopCraft.Kinds.Count].Button.onClick.Invoke();
            Assert.That(craft.StonesTab, Is.True);
            Assert.That(
                craft.Rows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(WorkshopCraftView.Stones.Count)
            );
            Assert.That(craft.Rows[0].Name.text, Is.EqualTo("青い磨き石"));
            // 青がないので、紫は作れず薄い。先頭の青を選んでおく。
            Assert.That(craft.Rows[1].Card.Dimmed, Is.True);
            Assert.That(craft.Selected, Is.EqualTo(0));
            Assert.That(craft.DetailName.text, Is.EqualTo("青い磨き石"));
            Assert.That(craft.MaterialRows.Count(row => row.gameObject.activeSelf), Is.EqualTo(1));
            Assert.That(craft.MaterialRows[0].Count.text, Is.EqualTo("7/3"));
            Assert.That(craft.MaterialRows[0].Name.text, Is.EqualTo("白い磨き石"));
            var blue = EnhancementMaterials.Find(EnhancementMaterials.Blue);
            Assert.That(craft.PriceLabel.text, Is.EqualTo(blue.CombineRunes.ToString("N0")));
            Assert.That(craft.Confirm.interactable, Is.True);

            craft.Confirm.onClick.Invoke();
            Assert.That(craft.Swirl.Playing, Is.True);
            yield return SceneTests.WaitForTask(craft.Work);
            craft.Swirl.Finish();
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopCraftView.CombinedText(blue)));
            Assert.That(EnhancementSession.CountOf(EnhancementMaterials.White), Is.EqualTo(4));
            Assert.That(EnhancementSession.CountOf(EnhancementMaterials.Blue), Is.EqualTo(1));
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 - blue.CombineRunes));
            Assert.That(craft.MaterialRows[0].Count.text, Is.EqualTo("4/3"));

            // 紫は、青が2つ足りない。
            craft.Rows[1].Card.Click();
            Assert.That(craft.Confirm.interactable, Is.False);
            Assert.That(craft.Missing.text, Is.EqualTo("青い磨き石が 2 つ足りません"));
        }

        // 商会の「合成」は、案内人を隠して画面いっぱいに、レシピの材料を1つでも見つけた物を分類のタブごとに並べる。
        // 選ぶと、レシピの材料を札で横に並べ、持っている数と要る数を出す。特別な材料（装備）の札を押すと使う物を
        // 選ぶ画面を開き、品質の違う物に替えられる。「合成」を押すと、サーバーの答えを待たずに材料を減らし、
        // 出来た物を持ち物に足して知らせる。保存に失敗したら知らせて読み直す。材料がそろわなければ押せない。
        [UnityTest]
        public IEnumerator CraftMakesTheChosenThingFromItsRecipe()
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
            yield return SceneTests.WaitUntil(
                () =>
                    craft.Rows.All(row =>
                        row.GetComponent<GlassCardBack>() is not { } back
                        || !(back.FaceDown || back.Flipping || back.Leaving)
                    ),
                message: "The rows did not turn face up."
            );
            Assert.That(craft.Tabs.Length, Is.EqualTo(WorkshopCraft.TabLabels.Length));
            Assert.That(
                craft.Rows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(craft.Shown.Count)
            );
            // めくり終えても、材料の足りない物は薄いまま。まだ手に入れていない物は名前を伏せる。
            for (int i = 0; i < craft.Shown.Count; i++)
            {
                Assert.That(
                    craft.Rows[i].Card.Group.alpha,
                    Is.EqualTo(WorkshopCraft.CanMake(craft.Shown[i], craft.Owned) ? 1f : 0.5f)
                        .Within(0.001f),
                    craft.Shown[i].Name
                );
                Assert.That(
                    craft.Rows[i].Name.text,
                    Is.EqualTo(
                        craft.Shown[i].Found ? craft.Shown[i].Name : WorkshopCraftView.Hidden
                    )
                );
            }
            Assert.That(craft.Shown.All(recipe => recipe.Kind == LibraryKind.Weapon), Is.True);
            Assert.That(craft.Selected, Is.EqualTo(0));
            SceneTests.AssertTouchSize(craft.Confirm.transform, 0.7f);

            // 炎のダガー：鉄の剣（特別）と、海賊のラム酒が足りない。
            int dagger = craft.Shown.ToList().FindIndex(recipe => recipe.Id == "flame-dagger");
            Assert.That(dagger, Is.GreaterThanOrEqualTo(0));
            craft.Rows[dagger].Card.Click();
            Assert.That(
                craft.MaterialRows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(craft.Shown[dagger].Materials.Count)
            );
            Assert.That(craft.Confirm.interactable, Is.False);
            Assert.That(craft.Missing.text, Does.Contain("足りません"));
            // 鉄の剣は、使っていない物のうち品質の低い物を選んでおき、札の左上に品質を出す。
            Assert.That(craft.Special.Single().RowId, Is.EqualTo("mock-w9"));
            Assert.That(
                craft.MaterialRows[0].Quality.transform.parent.gameObject.activeSelf,
                Is.True
            );
            craft.MaterialRows[0].Card.Click();
            var picker = craft.Picker;
            Assert.That(picker.IsOpen, Is.True);
            // 持っている3つを並べ、付けている物は選べない。
            Assert.That(picker.Pieces.Count, Is.EqualTo(3));
            int worn = picker.Pieces.ToList().FindIndex(piece => piece.User != null);
            Assert.That(picker.Rows[worn].InUse.activeSelf, Is.True);
            picker.Rows[worn].Card.Click();
            Assert.That(picker.Selected, Is.Not.EqualTo(worn));
            string before = craft.DetailQuality.text;
            int best = picker.Pieces.ToList().FindIndex(piece => piece.RowId == "mock-w10");
            picker.Rows[best].Card.Click();
            picker.Confirm.onClick.Invoke();
            Assert.That(picker.IsOpen, Is.False);
            Assert.That(craft.Special.Single().RowId, Is.EqualTo("mock-w10"));
            Assert.That(craft.DetailQuality.text, Is.Not.EqualTo(before));
            // 暗い所を押すと、替えずに閉じる。
            craft.MaterialRows[0].Card.Click();
            picker.Rows[0].Card.Click();
            picker.Dim.onClick.Invoke();
            Assert.That(picker.IsOpen, Is.False);
            Assert.That(craft.Special.Single().RowId, Is.EqualTo("mock-w10"));
            // アイテムの札は押しても開かない。
            craft.MaterialRows[1].Card.Click();
            Assert.That(picker.IsOpen, Is.False);

            // 鉄の剣：アイテムだけのレシピ。材料がそろうと押せ、品質Cで出来上がる。
            int sword = craft.Shown.ToList().FindIndex(recipe => recipe.Id == "iron-sword");
            var chosen = craft.Shown[sword];
            craft.Rows[sword].Card.Click();
            Assert.That(craft.Special, Is.Empty);
            Assert.That(craft.DetailQuality.text, Does.Contain("C").And.Contain("×1.00"));
            Assert.That(craft.DetailQuality.text, Does.Not.Contain("品質"));
            Assert.That(craft.MaterialRows[0].Count.text, Is.EqualTo("2/2"));
            Assert.That(craft.PriceLabel.text, Is.EqualTo(chosen.Runes.ToString("N0")));
            Assert.That(craft.Confirm.interactable, Is.True);

            // 押すとすぐ出来上がりを見せ、保存に失敗したら知らせて読み直す。持ち物は元に戻る。
            shop.Fail = true;
            shop.Hold = new TaskCompletionSource<bool>();
            craft.Confirm.onClick.Invoke();
            // 押すと画面全体の演出が流れ、出来た物が右上へ戻ってから知らせる。
            Assert.That(craft.Swirl.Playing, Is.True);
            Assert.That(craft.Swirl.Dim.gameObject.activeInHierarchy, Is.True);
            craft.Swirl.Finish();
            Assert.That(craft.Swirl.gameObject.activeSelf, Is.False);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopCraftView.CraftedText(chosen, 1f)));
            Assert.That(craft.Owned.Any(thing => thing.KindId == "shell-shard"), Is.False);
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 - chosen.Runes));
            shop.Hold.SetResult(true);
            yield return SceneTests.WaitForTask(craft.Work);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopCraftView.FailedMessage));
            yield return SceneTests.WaitForTask(craft.LoadTask);
            Assert.That(shop.Crafted, Is.Empty);
            Assert.That(craft.Owned.Any(thing => thing.KindId == "shell-shard"), Is.True);
            Assert.That(RuneBalance.Known, Is.EqualTo(1000));
            shop.Fail = false;
            sword = craft.Shown.ToList().FindIndex(recipe => recipe.Id == "iron-sword");
            craft.Rows[sword].Card.Click();

            // サーバーの答えを待たずに、材料を消し、出来た物を足し、ルーンを引いて見せる。
            shop.Hold = new TaskCompletionSource<bool>();
            craft.Confirm.onClick.Invoke();
            // 押すと画面全体の演出が流れ、出来た物が右上へ戻ってから知らせる。
            Assert.That(craft.Swirl.Playing, Is.True);
            Assert.That(craft.Swirl.Dim.gameObject.activeInHierarchy, Is.True);
            craft.Swirl.Finish();
            Assert.That(craft.Swirl.gameObject.activeSelf, Is.False);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopCraftView.CraftedText(chosen, 1f)));
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 - chosen.Runes));
            // 貝殻のつぶて2つを使いきり、出来た物が1つ増える。もう材料がそろわない。
            Assert.That(craft.Owned.Any(thing => thing.KindId == "shell-shard"), Is.False);
            Assert.That(
                craft.Owned.Count(thing => thing.KindId == "iron-sword"),
                Is.GreaterThan(0)
            );
            Assert.That(craft.Confirm.interactable, Is.False);
            shop.Hold.SetResult(true);
            yield return SceneTests.WaitForTask(craft.Work);
            Assert.That(shop.Crafted.Single().Recipe, Is.EqualTo("iron-sword"));
            Assert.That(shop.Crafted.Single().Materials, Is.Empty);
            Assert.That(RuneBalance.Known, Is.EqualTo(1000 - chosen.Runes));
            // 答えが届いたら、出来た物の行IDをサーバーの物にする。
            Assert.That(
                craft.Owned.Any(thing =>
                    thing.RowId == shop.LastItemId && thing.KindId == "iron-sword"
                ),
                Is.True
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
            // 札は買うと同じく、名前・☆・ルーンの値段を出す。
            Assert.That(rows[0].Name.text, Is.EqualTo(sell.Shown[0].Name));
            Assert.That(
                rows[0].Price.text,
                Is.EqualTo(WorkshopSellView.PriceOf(sell.Shown[0]).ToString("N0"))
            );
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

            // 右の行は、品質を「品質」と書かずに文字と倍率で出し、値段は数だけ（ルーンはアイコン）。
            var line = sell.ChosenRows[0];
            Assert.That(line.Detail.text, Does.Not.Contain("品質"));
            Assert.That(
                line.Price.text,
                Is.EqualTo(WorkshopSellView.PriceOf(sell.Chosen[0]).ToString("N0"))
            );

            // もう一度押すと、選ぶのをやめる。
            foreach (int i in free)
                rows[i].Card.Click();
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

            // 置くと、合成の答えをこれが終わるまで待たせる。
            public TaskCompletionSource<bool> Hold { get; set; }

            public async Task<WorkshopCrafted> CraftAsync(
                WorkshopRecipe recipe,
                IReadOnlyList<WorkshopOwned> materials,
                CancellationToken token
            )
            {
                if (Hold != null)
                    await Hold.Task;
                if (Fail)
                    throw new InvalidOperationException("作れない設定");
                Crafted.Add((recipe.Id, materials.Select(thing => thing.RowId).ToArray()));
                Runes -= recipe.Runes;
                LastItemId = "made-" + Crafted.Count;
                return new WorkshopCrafted(
                    LastItemId,
                    WorkshopCraft.Multiplier(recipe, materials),
                    recipe.Runes,
                    Runes
                );
            }
        }
    }
}
