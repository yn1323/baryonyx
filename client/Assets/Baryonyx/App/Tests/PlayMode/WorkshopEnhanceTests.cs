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
using Baryonyx.UI.GuideMenu;
using Baryonyx.Workshop;
using NUnit.Framework;
using UnityEngine.TestTools;

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

        // 商会の「強化」は、案内人を隠して画面いっぱいに、タブで持っているスキル・装備・固有スキルと覚醒できる段階を
        // 並べ、選んだ物を右に出す。行と磨き石の色・数を選んで押すと、サーバーの答えを待ってから品質を書き換え、素材と所持ルーンを減らして知らせる。
        // スキル覚醒の項目では欠片だけを出し、覚醒した段階は一覧から消える。失敗したら知らせて読み直す。
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
            Assert.That(enhance.Confirm.interactable, Is.True);
            // 持っている数を札に出す。欠片は覚醒のタブでだけ出す。
            Assert.That(enhance.Chips[0].Count.text, Is.EqualTo("×3"));
            Assert.That(enhance.Chips[4].gameObject.activeSelf, Is.False);
            SceneTests.AssertTouchSize(enhance.Confirm.transform, 0.7f);

            // 白い磨き石を2つ選んで押すと、答えを待ってから倍率を上げる。
            rows[0].Card.Click();
            var target = enhance.Targets[0];
            Assert.That(enhance.Stone.Id, Is.EqualTo(EnhancementMaterials.White));
            enhance.More.onClick.Invoke();
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
            enhance.More.onClick.Invoke();
            Assert.That(enhance.Count, Is.EqualTo(1));

            // 失敗したら知らせ、読み直す。
            source.Fail = true;
            enhance.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopEnhanceView.FailedMessage));
            yield return SceneTests.WaitForTask(enhance.LoadTask);
            source.Fail = false;

            // 装備のタブでは、カードの代わりに、縁が品質の色の札で前と後を並べる。
            enhance.Tabs[(int)WorkshopEnhanceView.Tab.Equipment].Button.onClick.Invoke();
            Assert.That(enhance.Targets, Is.Not.Empty);
            Assert.That(enhance.Selected, Is.EqualTo(0));
            Assert.That(enhance.BeforeGear.Root.activeSelf, Is.True);
            Assert.That(enhance.AfterGear.Root.activeSelf, Is.True);
            Assert.That(enhance.BeforeCard.Frame.transform.parent.gameObject.activeSelf, Is.False);
            Assert.That(enhance.BeforeGear.Name.text, Is.EqualTo(enhance.Targets[0].Name));
            Assert.That(
                enhance.BeforeGear.Rim.color,
                Is.EqualTo(QualityPalette.Of(enhance.Targets[0].Quality))
            );

            // 覚醒は別の項目「スキル覚醒」で、タブのない覚醒だけのパネルを開く。磨き石の札と数の段を隠し、欠片だけを出す。
            view.Back.onClick.Invoke();
            int awakenItem = Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == EnhancementSession.AwakeningItemKey
            );
            Assert.That(awakenItem, Is.EqualTo(4));
            enhance = view.PanelFor(awakenItem).GetComponent<WorkshopEnhanceView>();
            view.MenuItems[awakenItem].onClick.Invoke();
            Assert.That(enhance.gameObject.activeInHierarchy, Is.True);
            yield return SceneTests.WaitForTask(enhance.LoadTask);
            Assert.That(enhance.Tabs, Is.Empty);
            Assert.That(enhance.Current, Is.EqualTo(WorkshopEnhanceView.Tab.Awakening));
            Assert.That(enhance.Chips[0].gameObject.activeSelf, Is.False);
            Assert.That(enhance.Chips[4].gameObject.activeSelf, Is.True);
            Assert.That(enhance.Stepper.activeSelf, Is.False);
            Assert.That(enhance.ConfirmLabel.text, Is.EqualTo(WorkshopEnhanceView.AwakenLabel));
            Assert.That(enhance.Awakenings, Is.Not.Empty);
            var option = enhance.Awakenings[0];
            int before = enhance.Awakenings.Count;
            enhance.Rows[0].Card.Click();
            Assert.That(enhance.DetailName.text, Is.EqualTo(option.Card.Name));
            Assert.That(enhance.Gauge.activeSelf, Is.False);
            enhance.Confirm.onClick.Invoke();
            yield return SceneTests.WaitForTask(enhance.Work);
            Assert.That(view.ToastMessage, Is.EqualTo(WorkshopEnhanceView.AwakenedText(option)));
            var progress = UniqueSkillSession.ProgressOf(option.CharacterId, option.Index, null);
            Assert.That(progress.Has(option.Stage), Is.True);
            Assert.That(progress.Set, Is.EqualTo(option.Stage));
            Assert.That(
                enhance.Awakenings.Any(entry =>
                    entry.CharacterId == option.CharacterId
                    && entry.Index == option.Index
                    && entry.Stage == option.Stage
                ),
                Is.False
            );
            Assert.That(enhance.Awakenings.Count, Is.GreaterThanOrEqualTo(before - 1));
            // 欠片を使いきると、ほかの段階は覚醒できない。
            enhance.Rows[0].Card.Click();
            Assert.That(enhance.Confirm.interactable, Is.False);

            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(enhance.gameObject.activeInHierarchy, Is.False);
        }

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
