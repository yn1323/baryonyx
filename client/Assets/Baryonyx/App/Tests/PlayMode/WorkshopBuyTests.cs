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
    public sealed class WorkshopBuyTests
    {
        // ☆5の品物を2つ買える残高。
        private const long Balance = 100000;

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
            yield return SceneTests.UnloadAll(nameof(WorkshopBuyTests));
        }

        // 商会の「買う」は、案内人を隠して画面いっぱいに、これまでに手に入れたスキル・武器・防具・アクセサリーを
        // 分類のタブごとに並べ、選んだ品物を右に大きく出す。開いたときとタブを替えたときは、先頭の品物を選んでおく。「買う」を押すと品質の最低で1つ買い、所持ルーンを
        // 減らして知らせる。続けて同じ物も買える。ルーンが足りない物は選べても買えず、足りない数を出す。
        [UnityTest]
        public IEnumerator BuyListsTheFoundGoodsAndBuysTheChosenOne()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            yield return SceneTests.WaitForTask(guide.RunesTask);
            RuneBalance.Set(Balance);
            var view = guide.View;
            int item = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == WorkshopSession.BuyItemKey
            );
            Assert.That(item, Is.EqualTo(0));
            var buy = view.PanelFor(item).GetComponent<WorkshopBuyView>();
            view.MenuItems[item].onClick.Invoke();
            Assert.That(buy.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            // 枠の中に、案内人を隠して画面いっぱいに出す。
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(view.Framed, Is.True);
            yield return SceneTests.WaitForTask(buy.LoadTask);
            // 読み終えたら伏せたカードをすべてめくり、品物のない場所のカードは消えていく。
            yield return SceneTests.WaitUntil(
                () =>
                    buy.Rows.All(row =>
                        row.GetComponent<GlassCardBack>() is not { } back
                        || !(back.FaceDown || back.Flipping || back.Leaving)
                    ),
                message: "The cards did not turn face up."
            );

            // サーバーがないときは、ライブラリの仮の記録で見つけた物を並べる。初めは武器のタブ。
            var all = WorkshopGoods.Of(buy.Data, LibraryMemorySource.Mock());
            Assert.That(buy.Goods.Select(good => good.Id), Is.EqualTo(all.Select(good => good.Id)));
            var expected = all.Where(good => good.Kind == LibraryKind.Weapon).ToArray();
            Assert.That(expected, Is.Not.Empty);
            Assert.That(
                buy.Shown.Select(good => good.Id),
                Is.EqualTo(expected.Select(good => good.Id))
            );
            var rows = buy.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(rows.Length, Is.EqualTo(expected.Length));
            Assert.That(rows[0].Name.text, Is.EqualTo(expected[0].Name));
            Assert.That(rows[0].Price.text, Is.EqualTo(expected[0].Price.ToString("N0")));
            Assert.That(rows[0].Art.enabled, Is.True);
            Assert.That(buy.Status.text, Is.Empty);
            SceneTests.AssertTouchSize(buy.Confirm.transform, 0.7f);

            // 開いたときは、右の面が空にならないよう先頭の品物を選んでおく。
            Assert.That(buy.Selected, Is.EqualTo(0));
            Assert.That(rows[0].Card.Selected, Is.True);
            Assert.That(buy.DetailName.text, Is.EqualTo(expected[0].Name));
            Assert.That(buy.DetailArt.transform.parent.gameObject.activeSelf, Is.True);
            Assert.That(buy.DetailArt.enabled, Is.True);
            Assert.That(buy.DetailLine.text, Does.Contain(WorkshopBuyView.QualityText));
            Assert.That(buy.PriceLabel.text, Is.EqualTo(expected[0].Price.ToString("N0")));
            // 仮の持ち物の数を添える。
            Assert.That(buy.DetailOwned.text, Does.StartWith("持っている数"));
            Assert.That(buy.Missing.text, Is.Empty);
            Assert.That(buy.Confirm.interactable, Is.True);
            int price = expected[0].Price;
            shop.Hold = true;
            buy.Confirm.onClick.Invoke();
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopBuyView.BoughtText(expected[0])));
            Assert.That(RuneBalance.Known, Is.EqualTo(Balance - price));
            Assert.That(buy.BuyTask.IsCompleted, Is.False);
            // 保存を待つ間も、同じ物を続けて買える。
            Assert.That(buy.Confirm.interactable, Is.True);
            shop.Hold = false;
            buy.Confirm.onClick.Invoke();
            Assert.That(RuneBalance.Known, Is.EqualTo(Balance - price * 2));
            shop.Answer();
            yield return SceneTests.WaitForTask(buy.BuyTask);
            Assert.That(shop.Bought, Is.EqualTo(new[] { expected[0].Id, expected[0].Id }));
            Assert.That(RuneBalance.Known, Is.EqualTo(Balance - price * 2));

            RuneBalance.Set(price - 1);
            Assert.That(buy.Confirm.interactable, Is.False);
            Assert.That(buy.Missing.text, Is.EqualTo("ルーンが 1 足りません"));

            // 保存できなかったときは、引いた値段を戻して知らせる。
            RuneBalance.Set(Balance);
            shop.Fail = true;
            string ownedBefore = buy.DetailOwned.text;
            buy.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(buy.BuyTask);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopBuyView.BuyFailedMessage));
            Assert.That(RuneBalance.Known, Is.EqualTo(Balance));
            // 手元で足した持っている数も戻す。
            Assert.That(buy.DetailOwned.text, Is.EqualTo(ownedBefore));
            Assert.That(buy.Confirm.interactable, Is.True);

            // 防具のタブに切り替えると、防具だけを並べ、先頭の品物を選び直す。
            buy.Tabs[1].Button.onClick.Invoke();
            Assert.That(buy.Shown, Is.Not.Empty);
            Assert.That(buy.Shown.All(good => good.Kind == LibraryKind.Armor), Is.True);
            Assert.That(buy.Selected, Is.EqualTo(0));
            Assert.That(buy.DetailName.text, Is.EqualTo(buy.Shown[0].Name));

            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(buy.gameObject.activeInHierarchy, Is.False);
            Assert.That(guide.Presenter.Left, Is.False);
        }

        // 買った物と残高を覚える商会の代役。Hold のあいだは答えを止め、Answer で答える。
        private sealed class FakeWorkshop : IWorkshopSource
        {
            public readonly List<string> Bought = new();
            private readonly Queue<(
                WorkshopGood Good,
                TaskCompletionSource<WorkshopPurchase> Answer
            )> held = new();
            public long Runes { get; set; } = Balance;
            public bool Hold { get; set; }
            public bool Fail { get; set; }

            public Task<WorkshopPurchase> BuyAsync(WorkshopGood good, CancellationToken token)
            {
                if (Fail)
                    return Task.FromException<WorkshopPurchase>(
                        new InvalidOperationException("買えない設定")
                    );
                if (!Hold)
                    return Task.FromResult(Spend(good));
                var answer = new TaskCompletionSource<WorkshopPurchase>();
                held.Enqueue((good, answer));
                return answer.Task;
            }

            public Task<WorkshopCrafted> CraftAsync(
                WorkshopRecipe recipe,
                IReadOnlyList<WorkshopOwned> materials,
                CancellationToken token
            ) => Task.FromException<WorkshopCrafted>(new NotSupportedException());

            public Task<WorkshopSale> SellAsync(
                IReadOnlyList<WorkshopOwned> things,
                CancellationToken token
            ) => Task.FromException<WorkshopSale>(new NotSupportedException());

            public void Answer()
            {
                while (held.Count > 0)
                {
                    var (good, answer) = held.Dequeue();
                    answer.SetResult(Spend(good));
                }
            }

            private WorkshopPurchase Spend(WorkshopGood good)
            {
                Bought.Add(good.Id);
                Runes -= good.Price;
                return new WorkshopPurchase(good.Price, Runes);
            }
        }
    }
}
