using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.App;
using Baryonyx.Economy;
using Baryonyx.Enhancement;
using Baryonyx.Inventory;
using Baryonyx.SkillEvolution;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class WorkshopEnhanceTests
    {
        private TestGameServices services;
        private FakeEnhancement source;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            EnhancementSession.Reset();
            UniqueSkillSession.Reset();
            source = new FakeEnhancement(
                new Dictionary<string, int>
                {
                    [EnhancementMaterials.White] = 3,
                    [EnhancementMaterials.Shard] = 1,
                }
            );
            EnhancementSession.Source = source;
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            EnhancementSession.Reset();
            UniqueSkillSession.Reset();
            yield return SceneTests.UnloadAll(nameof(WorkshopEnhanceTests));
        }

        // 商会の「強化」は、案内人を隠して画面いっぱいに、タブで持っているスキル・装備・固有スキルを並べ、選んだ物を右に出す。
        // 行と上げる段階の数を選んで押すと、サーバーの答えを待ってから品質を書き換え、素材と所持ルーンを減らして知らせる。
        // スキル覚醒の項目では1本の一覧から覚醒の場面を開いて覚醒させ、覚醒した段階は一覧から消える。失敗したら知らせて読み直す。
        [UnityTest]
        public IEnumerator EnhanceRaisesTheChosenQualityAndAwakensAUniqueSkill()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            yield return SceneTests.WaitForTask(guide.RunesTask);
            RuneBalance.Set(5000);
            var view = guide.View;
            int item = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == EnhancementSession.GuideItemKey
            );
            Assert.That(item, Is.EqualTo(3));
            var enhance = view.PanelFor(item).GetComponent<WorkshopEnhanceView>();
            view.MenuItems[item].onClick.Invoke();
            Assert.That(enhance.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            yield return SceneTests.WaitForTask(enhance.LoadTask);
            // 読み終えたら伏せた行をすべてめくり、強化できる物のない場所の行は消えていく。
            Assert.That(enhance.Rows[0].GetComponent<GlassCardBack>(), Is.Not.Null);
            yield return TurnedFaceUp(enhance);

            // 初めはスキルのタブ。サーバーがないときは、仮の編成のカードを×1.00で並べる。
            Assert.That(enhance.Current, Is.EqualTo(WorkshopEnhanceView.Tab.Skills));
            Assert.That(enhance.Targets, Is.Not.Empty);
            var rows = enhance.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(rows.Length, Is.EqualTo(enhance.Targets.Count));
            Assert.That(rows[0].Name.text, Is.EqualTo(enhance.Targets[0].Name));
            Assert.That(rows[0].Price.text, Does.Contain("品質C"));
            // 右の面が空にならないよう、先頭の行を選んでおく。強化する前と後をカードで並べる。
            Assert.That(enhance.Selected, Is.EqualTo(0));
            Assert.That(rows[0].Card.Selected, Is.True);
            Assert.That(enhance.DetailName.text, Is.EqualTo(enhance.Targets[0].Name));
            Assert.That(enhance.BeforeCard.Frame.transform.parent.gameObject.activeSelf, Is.True);
            Assert.That(enhance.AfterCard.Frame.transform.parent.gameObject.activeSelf, Is.True);
            Assert.That(enhance.BeforeGear.Root.activeSelf, Is.False);
            Assert.That(enhance.BeforeCard.Name.text, Is.EqualTo(enhance.Targets[0].Name));
            Assert.That(enhance.Gauge.activeSelf, Is.True);
            // 初めは石を選んでいないため、押せない。
            Assert.That(enhance.Confirm.interactable, Is.False);
            // 持っている数を札に出す。札は磨き石の1枚だけで、覚醒の欠片はスキル覚醒の画面で使う。
            Assert.That(enhance.Chips[0].Count.text, Is.EqualTo("×3"));
            Assert.That(enhance.Chips.Length, Is.EqualTo(1));
            SceneTests.AssertTouchSize(enhance.Confirm.transform, 0.7f);

            // 札を2回押すと2段階上げる。品質Cでは1段階に1つ使う。押すと、答えを待ってから倍率を上げる。
            rows[0].Card.Click();
            var target = enhance.Targets[0];
            Assert.That(enhance.Chips[0].Cancel.gameObject.activeSelf, Is.False);
            enhance.Chips[0].Card.Click();
            enhance.Chips[0].Card.Click();
            Assert.That(enhance.Steps, Is.EqualTo(2));
            Assert.That(enhance.Stones, Is.EqualTo(2));
            Assert.That(enhance.Chips[0].Count.text, Does.Contain("2</color>/3"));
            Assert.That(enhance.Chips[0].Card.Selected, Is.True);
            // 使っているときは札の右上の赤い×で、使うのをやめる。
            Assert.That(enhance.Chips[0].Cancel.gameObject.activeSelf, Is.True);
            enhance.Chips[0].Cancel.onClick.Invoke();
            Assert.That(enhance.Steps, Is.Zero);
            Assert.That(enhance.Chips[0].Cancel.gameObject.activeSelf, Is.False);
            Assert.That(enhance.Confirm.interactable, Is.False);
            enhance.Chips[0].Card.Click();
            enhance.Chips[0].Card.Click();
            Assert.That(enhance.Steps, Is.EqualTo(2));
            Assert.That(enhance.DetailName.text, Is.EqualTo(target.Name));
            Assert.That(enhance.Info.text, Does.Contain("200 ルーン"));
            // 右に、品質の前と後と、ゲージの今と上がった後を出す。
            Assert.That(enhance.BeforeQuality.text, Does.Contain("品質C"));
            Assert.That(enhance.AfterQuality.text, Does.Contain("品質"));
            Assert.That(enhance.Gauge.activeSelf, Is.True);
            Assert.That(
                enhance.GaugeAfter.anchorMax.x,
                Is.GreaterThan(enhance.GaugeNow.anchorMax.x)
            );
            Assert.That(enhance.Confirm.interactable, Is.True);
            source.Hold = true;
            enhance.Confirm.onClick.Invoke();
            Assert.That(enhance.Confirm.interactable, Is.False);
            // 押した瞬間に、サーバーの答えを待たずに強化の演出が始まる（品質強化の演出）。
            Assert.That(enhance.Flip.Playing, Is.True);
            Assert.That(RuneBalance.Known, Is.EqualTo(4800));
            source.Answer();
            yield return SceneTests.WaitForTask(enhance.Work);
            float expected = EnhancementMaterials.Polished(1f, 2, target.MaxMultiplier);
            Assert.That(enhance.Targets[0].Multiplier, Is.EqualTo(expected).Within(0.001f));
            Assert.That(rows[0].Price.text, Does.Contain(Quality.Times(expected)));
            Assert.That(
                view.ToastMessage,
                Is.EqualTo(WorkshopEnhanceView.PolishedText(enhance.Targets[0]))
            );
            Assert.That(enhance.Chips[0].Count.text, Is.EqualTo("×1"));
            Assert.That(RuneBalance.Known, Is.EqualTo(4800));

            // 持っている数より多くは選べない（残り1つで1段階まで）。
            enhance.Chips[0].Card.Click();
            enhance.Chips[0].Card.Click();
            Assert.That(enhance.Steps, Is.EqualTo(1));
            Assert.That(enhance.Chips[0].Count.text, Does.Contain("1</color>/1"));

            // 失敗したら知らせ、読み直す。
            source.Fail = true;
            enhance.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopEnhanceView.FailedMessage));
            yield return SceneTests.WaitForTask(enhance.LoadTask);
            source.Fail = false;

            // 装備のタブでは、カードの代わりに、装備の札で前と後を並べる。
            enhance.Tabs[(int)WorkshopEnhanceView.Tab.Equipment].Button.onClick.Invoke();
            Assert.That(enhance.Targets, Is.Not.Empty);
            Assert.That(enhance.Selected, Is.EqualTo(0));
            Assert.That(enhance.BeforeGear.Root.activeSelf, Is.True);
            Assert.That(enhance.AfterGear.Root.activeSelf, Is.True);
            Assert.That(enhance.BeforeCard.Frame.transform.parent.gameObject.activeSelf, Is.False);
            Assert.That(enhance.BeforeGear.Name.text, Is.EqualTo(enhance.Targets[0].Name));

            // 覚醒は別の項目「スキル覚醒」で、キャラごとに固有スキル・カスタムスキルを並べた1本の一覧を開く。
            // 行を押すと覚醒の場面（戦闘と同じ覚醒先の選択）が開き、選んでチェックを押すと覚醒し、
            // 覚醒しきった物は一覧から消える。
            view.Back.onClick.Invoke();
            int awakenItem = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == EnhancementSession.AwakeningItemKey
            );
            Assert.That(awakenItem, Is.EqualTo(4));
            var awaken = view.PanelFor(awakenItem).GetComponent<WorkshopAwakenView>();
            view.MenuItems[awakenItem].onClick.Invoke();
            Assert.That(awaken.gameObject.activeInHierarchy, Is.True);
            yield return SceneTests.WaitForTask(awaken.LoadTask);
            Assert.That(awaken.Entries, Is.Not.Empty);
            Assert.That(
                awaken.Rows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(awaken.Entries.Count)
            );
            // 先頭はパーティの先頭の人の固有スキル。
            var entry = awaken.Entries[0];
            Assert.That(entry.Character, Is.Not.Null);
            Assert.That(entry.IsSkillCard, Is.False);
            Assert.That(awaken.Rows[0].Name.text, Is.EqualTo(entry.Card.Name));
            var option = entry.Options[0];
            // 欠片が1つある間は、行の名前はいつもの色。
            Assert.That(awaken.Rows[0].Name.color, Is.EqualTo(UiPalette.TextMain));
            // カードが行から浮き上がってひびが入り、上に移ってから覚醒先が並ぶ（戦闘と同じ演出）。
            awaken.Rows[0].Button.onClick.Invoke();
            Assert.That(awaken.SceneOpen, Is.True);
            yield return SceneTests.WaitUntil(
                () => awaken.Choice.IsOpen,
                message: "The stages were not offered."
            );
            Assert.That(awaken.Choice.Count, Is.EqualTo(entry.Options.Count));
            awaken.Choice.Select(0);
            Assert.That(awaken.Choice.ConfirmButton.interactable, Is.True);
            awaken.Choice.ConfirmButton.onClick.Invoke();
            yield return SceneTests.WaitForTask(awaken.Work);
            var progress = UniqueSkillSession.ProgressOf(option.CharacterId, option.Index, null);
            Assert.That(progress.Has(option.Stage), Is.True);
            Assert.That(progress.Set, Is.EqualTo(option.Stage));
            Assert.That(awaken.SceneOpen, Is.False);
            Assert.That(
                awaken.Entries.Any(row =>
                    row.Options.Any(next =>
                        next.CharacterId == option.CharacterId
                        && next.Index == option.Index
                        && next.Stage == option.Stage
                    )
                ),
                Is.False
            );
            // 欠片を使いきると、一覧の名前が赤くなる。
            Assert.That(awaken.Rows[0].Name.color, Is.Not.EqualTo(UiPalette.TextMain));
            // 欠片を使いきると、覚醒の場面のチェックを押せない。もどるで場面を閉じ、もう1度でメニューへ戻る。
            awaken.Rows[0].Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => awaken.Choice.IsOpen,
                message: "The stages were not offered again."
            );
            awaken.Choice.Select(0);
            Assert.That(awaken.Choice.ConfirmButton.interactable, Is.False);
            // 払う物の札は、覚醒先を選ぶ間だけ出す。
            Assert.That(awaken.CostCard.activeSelf, Is.True);
            Assert.That(awaken.ShardCount.text, Is.EqualTo("1/0"));
            // 覚醒の場面は画面全体を暗く覆い、ヘッダーの「もどる」の所を押すと幕の上の「もどる」が受けて閉じる。
            Assert.That(
                TopHit(view.Back.transform).GetComponentInParent<Button>(),
                Is.EqualTo(awaken.SceneBack)
            );
            awaken.SceneBack.onClick.Invoke();
            Assert.That(awaken.SceneOpen, Is.False);
            Assert.That(awaken.gameObject.activeInHierarchy, Is.True);
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(awaken.gameObject.activeInHierarchy, Is.False);
        }

        // 1段階に使う石は品質が上がるほど増え、持っている数を超えては選べない。届かない段階は押しても増えず、
        // 1段階も上げられないときは札を暗くして「磨き石が足りません」と書く。
        [UnityTest]
        public IEnumerator StepsNeverUseMoreStonesThanHeld()
        {
            source = new FakeEnhancement(
                new Dictionary<string, int> { [EnhancementMaterials.White] = 9 }
            );
            EnhancementSession.Source = source;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Shop, value => guide = value);
            yield return SceneTests.WaitForTask(guide.RunesTask);
            RuneBalance.Set(5000);
            var view = guide.View;
            int item = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == EnhancementSession.GuideItemKey
            );
            var enhance = view.PanelFor(item).GetComponent<WorkshopEnhanceView>();
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.LoadTask);
            yield return TurnedFaceUp(enhance);

            var target = enhance.Targets[0];
            for (int i = 0; i < 30; i++)
                enhance.Chips[0].Card.Click();
            // 9つで上げられる所まで：Cは1つずつ、Bに入ると2つずつ。次の1段階は足りない。
            int steps = enhance.Steps;
            Assert.That(enhance.Stones, Is.LessThanOrEqualTo(9));
            Assert.That(
                enhance.Stones,
                Is.EqualTo(EnhancementMaterials.StonesFor(1f, steps, target.MaxMultiplier))
            );
            Assert.That(
                EnhancementMaterials.StonesFor(1f, steps + 1, target.MaxMultiplier),
                Is.GreaterThan(9)
            );
            Assert.That(enhance.Info.text, Does.Contain($"磨き石×{enhance.Stones}"));
            enhance.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(
                EnhancementSession.CountOf(EnhancementMaterials.White),
                Is.EqualTo(9 - EnhancementMaterials.StonesFor(1f, steps, target.MaxMultiplier))
            );
            Assert.That(
                enhance.Targets[0].Multiplier,
                Is.EqualTo(EnhancementMaterials.Polished(1f, steps, target.MaxMultiplier))
                    .Within(0.001f)
            );

            // 残りで1段階も上げられなければ、札を暗くして足りないと書く。
            if (
                EnhancementMaterials.StonesFor(
                    enhance.Targets[0].Multiplier,
                    1,
                    target.MaxMultiplier
                ) > EnhancementSession.CountOf(EnhancementMaterials.White)
            )
            {
                Assert.That(enhance.Chips[0].Card.Dimmed, Is.True);
                Assert.That(enhance.Info.text, Does.Contain("磨き石が足りません"));
            }
            enhance.Chips[0].Card.Click();
            Assert.That(
                enhance.Stones,
                Is.LessThanOrEqualTo(EnhancementSession.CountOf(EnhancementMaterials.White))
            );
        }

        // 画面のその位置を押したとき、いちばん手前で受け取るもの。
        private static GameObject TopHit(Transform target)
        {
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = SceneTests.ScreenRect(target).center,
            };
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            return hits[0].gameObject;
        }

        private static IEnumerator TurnedFaceUp(WorkshopEnhanceView enhance) =>
            SceneTests.WaitUntil(
                () =>
                    enhance.Rows.All(row =>
                        row.GetComponent<GlassCardBack>() is not { } back
                        || !(back.FaceDown || back.Flipping || back.Leaving)
                    ),
                message: "The rows did not turn face up."
            );

        // 素材とルーンを覚える強化の代役。Hold のあいだは答えを止め、Answer で答える。Fail で断る。
        private sealed class FakeEnhancement : IEnhancementSource
        {
            private readonly EnhancementMemorySource memory;
            private readonly Queue<(
                Func<Task<EnhancementOutcome>> Run,
                TaskCompletionSource<EnhancementOutcome> Answer
            )> held = new();

            public FakeEnhancement(IReadOnlyDictionary<string, int> materials) =>
                memory = new EnhancementMemorySource(materials, 5000, shownBalance: false);

            public bool Hold { get; set; }
            public bool Fail { get; set; }

            public Task<IReadOnlyDictionary<string, int>> LoadAsync(CancellationToken token) =>
                memory.LoadAsync(token);

            public Task<EnhancementOutcome> PolishAsync(
                EnhancementTarget target,
                int steps,
                CancellationToken token
            ) => Run(() => memory.PolishAsync(target, steps, token));

            public Task<EnhancementOutcome> AwakenAsync(
                AwakeningOption option,
                CancellationToken token
            ) => Run(() => memory.AwakenAsync(option, token));

            private Task<EnhancementOutcome> Run(Func<Task<EnhancementOutcome>> run)
            {
                if (Fail)
                    return Task.FromException<EnhancementOutcome>(
                        new InvalidOperationException("強化できない設定")
                    );
                if (!Hold)
                    return run();
                var answer = new TaskCompletionSource<EnhancementOutcome>();
                held.Enqueue((run, answer));
                return answer.Task;
            }

            public void Answer()
            {
                Hold = false;
                while (held.Count > 0)
                {
                    var (run, answer) = held.Dequeue();
                    run()
                        .ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                                answer.SetException(task.Exception.InnerExceptions);
                            else
                                answer.SetResult(task.Result);
                        });
                }
            }
        }
    }
}
