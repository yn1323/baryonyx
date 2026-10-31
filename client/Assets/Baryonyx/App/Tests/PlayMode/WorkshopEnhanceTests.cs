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
                    [EnhancementMaterials.Blue] = 0,
                    [EnhancementMaterials.Purple] = 1,
                    [EnhancementMaterials.Gold] = 0,
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
        // 行と磨き石の色・数を選んで押すと、サーバーの答えを待ってから品質を書き換え、素材と所持ルーンを減らして知らせる。
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
            // 持っている数を札に出す。札は磨き石4色だけで、覚醒の欠片はスキル覚醒の画面で使う。
            Assert.That(enhance.Chips[0].Count.text, Is.EqualTo("×3"));
            Assert.That(enhance.Chips.Length, Is.EqualTo(EnhancementMaterials.Polishes.Count));
            SceneTests.AssertTouchSize(enhance.Confirm.transform, 0.7f);

            // 白い磨き石の札を2回押すと2つ使う。押すと、答えを待ってから倍率を上げる。
            rows[0].Card.Click();
            var target = enhance.Targets[0];
            Assert.That(enhance.Stone.Id, Is.EqualTo(EnhancementMaterials.White));
            enhance.Chips[0].Card.Click();
            enhance.Chips[0].Card.Click();
            Assert.That(enhance.Count, Is.EqualTo(2));
            Assert.That(enhance.Chips[0].Count.text, Does.Contain("2</color>/3"));
            Assert.That(enhance.Chips[0].Card.Selected, Is.True);
            // 使っている色の札の右上の赤い×で、その色をやめる。使っていない色には出さない。
            Assert.That(enhance.Chips[0].Cancel.gameObject.activeSelf, Is.True);
            Assert.That(enhance.Chips[1].Cancel.gameObject.activeSelf, Is.False);
            enhance.Chips[0].Cancel.onClick.Invoke();
            Assert.That(enhance.Count, Is.Zero);
            Assert.That(enhance.Chips[0].Cancel.gameObject.activeSelf, Is.False);
            Assert.That(enhance.Confirm.interactable, Is.False);
            enhance.Chips[0].Card.Click();
            enhance.Chips[0].Card.Click();
            Assert.That(enhance.Count, Is.EqualTo(2));
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
            Assert.That(RuneBalance.Known, Is.EqualTo(4800));
            source.Answer();
            yield return SceneTests.WaitForTask(enhance.Work);
            float expected = EnhancementMaterials.Polished(
                1f,
                EnhancementMaterials.Polishes[0],
                2,
                target.MaxMultiplier
            );
            Assert.That(enhance.Targets[0].Multiplier, Is.EqualTo(expected).Within(0.001f));
            Assert.That(rows[0].Price.text, Does.Contain(Quality.Times(expected)));
            Assert.That(
                view.ToastMessage,
                Is.EqualTo(WorkshopEnhanceView.PolishedText(enhance.Targets[0]))
            );
            Assert.That(enhance.Chips[0].Count.text, Is.EqualTo("×1"));
            Assert.That(RuneBalance.Known, Is.EqualTo(4800));

            // 持っていない色は押せない。持っている数より多くは選べない。
            enhance.ChooseStone(1);
            Assert.That(enhance.Confirm.interactable, Is.False);
            Assert.That(enhance.Info.text, Does.Contain("青い磨き石が足りません"));
            enhance.ChooseStone(0);
            enhance.Chips[0].Card.Click();
            Assert.That(enhance.Count, Is.EqualTo(1));

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
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopAwakenView.AwakenedText(option)));
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

        // 白い磨き石はBの入口で止まる。札を何度押しても入口までの数しか選べず、届いたら白の札を暗くし、
        // 押せなくして、白で上げられるのはBまでと書く。ゲージには上限の壁の線を立てる。選び直すと、上げられる色へ替える。
        [UnityTest]
        public IEnumerator WhiteStopsAtTheEntranceOfQualityB()
        {
            source = new FakeEnhancement(
                new Dictionary<string, int>
                {
                    [EnhancementMaterials.White] = 20,
                    [EnhancementMaterials.Blue] = 1,
                }
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

            var white = EnhancementMaterials.Polishes[0];
            var target = enhance.Targets[0];
            Assert.That(enhance.Stone, Is.EqualTo(white));
            Assert.That(enhance.GaugeWall.gameObject.activeSelf, Is.True);
            Assert.That(
                enhance.GaugeWall.rectTransform.anchorMin.x,
                Is.EqualTo(0.4f).Within(0.02f)
            );
            int useful = EnhancementMaterials.UsefulCount(1f, white, target.MaxMultiplier);
            for (int i = 0; i < 30; i++)
                enhance.Chips[0].Card.Click();
            Assert.That(enhance.Count, Is.EqualTo(useful));
            enhance.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(enhance.Targets[0].Quality, Is.EqualTo(ItemQuality.B));

            Assert.That(enhance.Stone, Is.EqualTo(white));
            Assert.That(enhance.Chips[0].Card.Dimmed, Is.True);
            Assert.That(enhance.Chips[1].Card.Dimmed, Is.False);
            Assert.That(enhance.Confirm.interactable, Is.False);
            Assert.That(enhance.Info.text, Is.EqualTo(WorkshopEnhanceView.CeilingText(white)));
            // 行を選び直すと、上げられる色（青）へ替え、壁の線をAの入口へ移す。
            enhance.Rows[0].Card.Click();
            Assert.That(enhance.Stone.Id, Is.EqualTo(EnhancementMaterials.Blue));
            Assert.That(
                enhance.GaugeWall.rectTransform.anchorMin.x,
                Is.EqualTo(0.7f).Within(0.02f)
            );
            enhance.ChooseStone(1);
            Assert.That(enhance.Count, Is.EqualTo(1));
            Assert.That(enhance.Confirm.interactable, Is.True);
        }

        // 選んだ色が足りなくても、下の色から作れる数までは選べる。「強化」で確かめの画面を開き、使う石と作る石、
        // 合成と強化のルーンの合計を見せる。暗い所を押すと何も使わずに閉じ、チェックで作ってから強化する。
        [UnityTest]
        public IEnumerator ShortStonesAreMadeFromTheColourBelowAfterTheCheck()
        {
            source = new FakeEnhancement(
                new Dictionary<string, int> { [EnhancementMaterials.White] = 6 }
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

            var blue = EnhancementMaterials.Polishes[1];
            var target = enhance.Targets[0];
            enhance.ChooseStone(1);
            Assert.That(enhance.Count, Is.EqualTo(1));
            // 白6個から青は2個まで作れる。
            for (int i = 0; i < 5; i++)
                enhance.Chips[1].Card.Click();
            Assert.That(enhance.Count, Is.EqualTo(2));
            int runes = blue.Runes * 2 + blue.CombineRunes * 2;
            Assert.That(enhance.Info.text, Does.Contain("（2個は合成）"));
            Assert.That(enhance.Info.text, Does.Contain($"{runes:N0} ルーン"));
            Assert.That(enhance.Confirm.interactable, Is.True);

            var prompt = enhance.Prompt;
            enhance.Confirm.onClick.Invoke();
            Assert.That(prompt.IsOpen, Is.True);
            Assert.That(
                prompt.Dim.GetComponent<UnityEngine.UI.Image>().color.a,
                Is.GreaterThan(0.5f)
            );
            Assert.That(prompt.Steps[0].activeSelf, Is.True);
            Assert.That(prompt.Steps[1].activeSelf, Is.False);
            Assert.That(prompt.From[0].Count.text, Is.EqualTo("×6"));
            Assert.That(prompt.To[0].Count.text, Is.EqualTo("×2"));
            Assert.That(prompt.Runes.text, Is.EqualTo(runes.ToString("N0")));
            // 暗い所を押すと、何も使わずに閉じる。
            prompt.Dim.onClick.Invoke();
            Assert.That(prompt.IsOpen, Is.False);
            Assert.That(EnhancementSession.CountOf(EnhancementMaterials.White), Is.EqualTo(6));

            enhance.Confirm.onClick.Invoke();
            prompt.Confirm.onClick.Invoke();
            Assert.That(prompt.IsOpen, Is.False);
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(EnhancementSession.CountOf(EnhancementMaterials.White), Is.Zero);
            Assert.That(EnhancementSession.CountOf(EnhancementMaterials.Blue), Is.Zero);
            Assert.That(RuneBalance.Known, Is.EqualTo(5000 - runes));
            Assert.That(
                enhance.Targets[0].Multiplier,
                Is.EqualTo(EnhancementMaterials.Polished(1f, blue, 2, target.MaxMultiplier))
                    .Within(0.001f)
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
                EnhancementMaterial stone,
                int count,
                CancellationToken token
            ) => Run(() => memory.PolishAsync(target, stone, count, token));

            public Task<EnhancementOutcome> AwakenAsync(
                AwakeningOption option,
                CancellationToken token
            ) => Run(() => memory.AwakenAsync(option, token));

            public Task<EnhancementOutcome> CombineAsync(
                EnhancementMaterial made,
                int count,
                CancellationToken token
            ) => Run(() => memory.CombineAsync(made, count, token));

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
