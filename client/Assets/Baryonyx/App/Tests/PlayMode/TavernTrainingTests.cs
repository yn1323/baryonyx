using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Tavern;
using Baryonyx.Training;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TavernTrainingTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernTrainingTests));
        }

        // 冒険者の一覧の帯の「装備・スキル」は、その人の冒険者のタブ（ステータス）を開く。左にイラストと右下の
        // ドット絵、右にステータスとパッシブとレベルアップ、両端に人の列とタブ。左右のフリックか人の列で
        // 人を替え、「レベルアップ」で重ねて開いた画面で上げる数を選んでから上げる。戻る操作は重ねた画面を
        // 先に閉じ、次に一覧へ戻る。
        [UnityTest]
        public IEnumerator TheAdventurersPageShowsThemAndRaisesTheLevel()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var training = OpenPage(view);
            Assert.That(training.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(training.Dialog.activeSelf, Is.False);
            // 冒険者のタブはメニューに出さない。
            int item = ItemOf(view, TrainingSession.GuideItemKey);
            Assert.That(view.Definition.Items[item].Hidden, Is.True);
            Assert.That(view.MenuItems[item], Is.Null);

            // 左のイラストと右の枠は「もどる」より下で、重ならない。
            var portrait = training.transform.Find("Layout/Portrait");
            var detail = training.transform.Find("Layout/Detail");
            SceneTests.AssertBelow(portrait, view.Back.transform);
            SceneTests.AssertBelow(detail, view.Back.transform);
            SceneTests.AssertBelow(detail, view.Breadcrumb.transform);
            Assert.That(training.Illustration.enabled, Is.True);
            Assert.That(training.Illustration.texture, Is.Not.Null);
            // 横に長いボタンは、メニューの行と同じく高さを7割まで許す。
            SceneTests.AssertTouchSize(training.LevelUp.transform, 0.7f);
            // 左の人の列と右のタブも、指で押せる大きさで「もどる」より下。
            foreach (var tab in training.Sheet.Tabs)
            {
                SceneTests.AssertTouchSize(tab.transform);
                SceneTests.AssertBelow(tab.transform, view.Back.transform);
            }
            SceneTests.AssertTouchSize(training.Sheet.People[0].Button.transform);
            Assert.That(
                training.Sheet.Tabs.Select(tab => tab.Label.text),
                Is.EqualTo(new[] { "ステータス", "装備", "スキル", "突破" })
            );
            Assert.That(training.Sheet.Tabs[0].Chosen, Is.True);

            // 最初はパーティの1人目。ステータスとパッシブは今のレベルの値。
            var party = PartySession.Formation(training.Party);
            var first = party.Find(party.Member(0));
            var growth = training.Data.Find(first.Id);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(first.Level.ToString()));
            // 上の段のパンくずは「編成 › 冒険者 › 名前」、所持ルーンはこの画面で読んだ残高。
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "冒険者", first.Name)));
            Assert.That(
                view.Runes.text,
                Is.EqualTo(Baryonyx.Economy.RuneBalance.Text(training.Presenter.State.Runes))
            );
            Assert.That(training.Illustration.texture, Is.EqualTo(first.Illustration));
            Assert.That(
                training.Stats.Select(label => label.text),
                Is.EqualTo(Values(Worn(TrainingBond.StatsOf(growth, first.Level), first.Id)))
            );
            Assert.That(training.Passives[0].Name.text, Is.EqualTo(growth.Passives[0].Name));
            Assert.That(training.Passives[0].Description.text, Is.Not.Empty);
            // 2つ目のパッシブは、今のレベルで解放していなければ鍵と解放するレベルを出す。
            bool locked = first.Level < growth.Passives[1].UnlockLevel;
            Assert.That(training.Passives[1].Lock.activeSelf, Is.EqualTo(locked));
            Assert.That(
                training.Passives[1].When.text,
                Is.EqualTo(locked ? $"Lv {growth.Passives[1].UnlockLevel}で解放" : "")
            );
            Assert.That(training.Passives[0].Icon.texture, Is.Not.Null);
            // 左へのフリックで次の人、右へのフリックで前の人。枠の上から動かしてもフリックとして届く。
            Assert.That(
                ExecuteEvents.GetEventHandler<IEndDragHandler>(training.LevelUp.gameObject),
                Is.SameAs(training.Swipe.gameObject)
            );
            Flick(training, -1);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(party.Member(1))));
            Assert.That(
                view.Breadcrumb.text,
                Is.EqualTo(Trail("編成", "冒険者", party.Name(party.Member(1))))
            );
            Flick(training, 1);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));

            // レベルアップを重ねて開く。暗幕が背面の「もどる」を押せなくする。
            training.LevelUp.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.True);
            // 開いたばかりの暗幕は、1度描かれるまでタップの判定に入らない。
            yield return null;
            foreach (
                var button in new Component[]
                {
                    training.Less,
                    training.More,
                    training.Max,
                    training.Cancel,
                    training.Confirm,
                }
            )
                SceneTests.AssertTouchSize(button.transform, 0.7f);
            Assert.That(TopHit(view.Back.transform).name, Is.EqualTo("Dim"));
            // 端末の戻るキーと同じ経路（「もどる」）は、重ねた画面だけを閉じる。
            view.Back.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.gameObject.activeSelf, Is.True);

            training.LevelUp.onClick.Invoke();
            training.More.onClick.Invoke();
            training.More.onClick.Invoke();
            int target = first.Level + 3;
            Assert.That(training.To.text, Is.EqualTo(target.ToString()));
            Assert.That(
                training.Diffs[0].After.text,
                Is.EqualTo(
                    Worn(growth.StatsAt(target, TrainingBond.StageOf(growth)), first.Id)
                        .Hp.ToString()
                )
            );
            long cost = TrainingRules.CostBetween(first.Level, target, training.Data.CostPerLevel);
            Assert.That(
                training.Cost.text,
                Is.EqualTo(cost.ToString("#,0", CultureInfo.InvariantCulture))
            );
            training.Confirm.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(PartySession.LevelOf(training.Party, first.Id), Is.EqualTo(target));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            Assert.That(view.ToastMessage, Does.StartWith($"{first.Name}が Lv {target} になり"));

            // 右のタブの「スキル」は、同じ人の1つ目のカスタムスキルの枠を選んで開く。
            Flick(training, -1);
            string second = party.Member(1);
            training.Sheet.Tabs[2].Button.onClick.Invoke();
            var cards = view.PanelFor(ItemOf(view, CardLoadoutSession.GuideItemKey));
            var loadout = cards.GetComponent<CardLoadoutView>();
            Assert.That(cards.activeInHierarchy, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            Assert.That(loadout.Presenter.State.PersonId, Is.EqualTo(second));
            Assert.That(loadout.Sheet.Tabs[2].Chosen, Is.True);
            Assert.That(loadout.SlotRows[0].Card.Selected.activeSelf, Is.True);

            // 固有スキルの行を押すと、右にその進化ツリーを出す。
            loadout.UniqueRows[1].Card.Button.onClick.Invoke();
            Assert.That(loadout.UniqueRows[1].Card.Selected.activeSelf, Is.True);
            Assert.That(loadout.SlotRows.Any(row => row.Card.Selected.activeSelf), Is.False);
            Assert.That(loadout.Tree.Root.activeSelf, Is.True);

            // 「ステータス」のタブへ戻ると同じ人。左の列で人を替えられる。
            loadout.Sheet.Tabs[0].Button.onClick.Invoke();
            Assert.That(cards.activeSelf, Is.False);
            Assert.That(training.gameObject.activeSelf, Is.True);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(second)));
            training.Sheet.People.Single(person => person.Id == first.Id).Button.onClick.Invoke();
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(
                training.Sheet.People.Single(person => person.Id == first.Id).Selected.activeSelf,
                Is.True
            );

            // 「もどる」で冒険者の一覧へ戻る。パーティの先頭を選んで開き直すと、アプリを動かしている間はレベルが残る。
            view.Back.onClick.Invoke();
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            Assert.That(roster.gameObject.activeSelf, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            Open(roster);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            yield return null;
        }

        // サーバーがあるときは、サーバーのLvと所持ルーンを読み、サーバーでレベルを上げてルーンを使う。
        // 別の端末で上げていて断られたときは、読み直してから失敗を知らせる。
        [UnityTest]
        public IEnumerator TrainingRaisesTheLevelOnTheServer()
        {
            var server = new FakePartySource(5000, "toma", "luka")
                .With("toma", 12, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder");
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            view.MenuItems[ItemOf(view, PartySession.GuideItemKey)].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            Open(roster);
            var training = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(training.Name.text, Is.EqualTo(TrainingView.LoadingText));
            Assert.That(training.LevelUp.interactable, Is.False);
            yield return SceneTests.WaitUntil(
                () => training.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            var data = training.Party;
            Assert.That(training.Name.text, Is.EqualTo(data.Find("toma").Name));
            Assert.That(training.Level.text, Is.EqualTo("12"));
            Assert.That(training.LevelUpCostLabel.text, Is.EqualTo("1,200"));

            // Lv 12 → 14 は 1,200 + 1,300 = 2,500ルーン。保存を待たずに重ねた画面を閉じ、上げたLvを出す。
            training.LevelUp.onClick.Invoke();
            training.More.onClick.Invoke();
            training.Confirm.onClick.Invoke();
            Assert.That(training.Presenter.ConfirmTask.IsCompleted, Is.False);
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.Level.text, Is.EqualTo("14"));
            yield return SceneTests.WaitUntil(() => training.Presenter.ConfirmTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "level toma 12 14" }));
            Assert.That(server.LevelOf("toma"), Is.EqualTo(14));
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.Level.text, Is.EqualTo("14"));
            training.LevelUp.onClick.Invoke();
            Assert.That(training.Presenter.State.Runes, Is.EqualTo(2500));
            training.Cancel.onClick.Invoke();
            Assert.That(view.ToastMessage, Does.StartWith($"{data.Find("toma").Name}が Lv 14"));

            // 別の端末でLv 16まで上げていると断られ、通知の帯で知らせて、読み直したLvを出す。
            server.SetLevel("toma", 16);
            training.LevelUp.onClick.Invoke();
            training.Confirm.onClick.Invoke();
            Assert.That(training.Level.text, Is.EqualTo("15"));
            yield return SceneTests.WaitUntil(() => training.Presenter.ConfirmTask.IsCompleted);
            Assert.That(server.LevelOf("toma"), Is.EqualTo(16));
            Assert.That(server.Runes, Is.EqualTo(2500));
            Assert.That(training.Level.text, Is.EqualTo("16"));
            Assert.That(view.ToastMessage, Is.EqualTo(TrainingPresenter.LevelUpFailedMessage));
        }

        // 右のタブの「突破」は、6段を上から並べ、突破した段に「突破済み」、次の段に「限界突破」を出す。
        // 「限界突破」でしるしを使ってその場で1段上げ、灯った段の演出を出す。戻る操作は演出を先に閉じる。
        [UnityTest]
        public IEnumerator TheBreakthroughTabRaisesTheStageAndLightsItUp()
        {
            TrainingBond.Reset();
            try
            {
                var guide = default(GuideSceneBootstrap);
                yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
                var view = guide.View;
                var page = OpenPage(view);
                var party = PartySession.Formation(page.Party);
                var first = party.Find(party.Member(0));
                var growth = page.Data.Find(first.Id);
                int stage = TrainingBond.StageOf(growth);
                Assert.That(stage, Is.LessThan(TrainingBond.Max - 1));
                Assert.That(TrainingBond.TokensOf(growth), Is.EqualTo(1));

                page.Sheet.Tabs[3].Button.onClick.Invoke();
                var training = view.PanelFor(ItemOf(view, TrainingSession.BreakthroughItemKey))
                    .GetComponent<TrainingView>();
                Assert.That(training.gameObject.activeInHierarchy, Is.True);
                Assert.That(page.gameObject.activeSelf, Is.False);
                Assert.That(training.Sheet.Tabs[3].Chosen, Is.True);
                Assert.That(training.Sheet.Tabs[3].Label.text, Is.EqualTo("突破"));
                Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "冒険者", first.Name)));
                yield return null;

                // 突破した段は石を灯して「突破済み」、次の段は金の縁と「限界突破」、まだの段は薄い。
                Assert.That(training.Stages, Has.Length.EqualTo(TrainingBond.Max));
                for (int i = 0; i < training.Stages.Length; i++)
                {
                    var row = training.Stages[i];
                    Assert.That(row.Effect.text, Is.EqualTo(TrainingBond.EffectOf(i + 1)));
                    Assert.That(row.Done.activeSelf, Is.EqualTo(i < stage));
                    Assert.That(row.BreakThrough.gameObject.activeSelf, Is.EqualTo(i == stage));
                    Assert.That(row.Card.Selected, Is.EqualTo(i == stage));
                    Assert.That(
                        row.Stone.sprite,
                        Is.EqualTo(i < stage ? training.BondLit : training.BondDark)
                    );
                }
                var next = training.Stages[stage].BreakThrough;
                Assert.That(next.interactable, Is.True);
                SceneTests.AssertTouchSize(next.transform, 0.7f);

                next.onClick.Invoke();
                Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
                Assert.That(TrainingBond.StageOf(growth), Is.EqualTo(stage + 1));
                Assert.That(training.Stages[stage].Done.activeSelf, Is.True);
                var celebration = training.Celebration;
                Assert.That(celebration.Name.text, Is.EqualTo(first.Name));
                Assert.That(celebration.Stage.text, Is.EqualTo((stage + 1).ToString()));
                Assert.That(
                    celebration.Effect.text,
                    Is.EqualTo($"全ステータス +{TrainingBond.PercentAt(stage + 1)}%")
                );
                yield return SceneTests.WaitUntil(
                    () => celebration.Card.alpha >= 1f,
                    message: "The breakthrough's moment did not finish."
                );
                Assert.That(celebration.Stones[stage].sprite, Is.EqualTo(training.BondLit));
                Assert.That(celebration.Stones[stage + 1].sprite, Is.EqualTo(training.BondDark));

                // 戻る操作は演出だけを閉じる。しるしを使い切ると、次の段の「限界突破」は押せない。
                view.Back.onClick.Invoke();
                Assert.That(celebration.gameObject.activeSelf, Is.False);
                Assert.That(training.gameObject.activeSelf, Is.True);
                Assert.That(training.Stages[stage + 1].BreakThrough.interactable, Is.False);

                // ステータスのタブへ戻ると、突破で上がったステータスを出す。
                training.Sheet.Tabs[0].Button.onClick.Invoke();
                Assert.That(
                    page.Stats.Select(label => label.text),
                    Is.EqualTo(Values(Worn(growth.StatsAt(first.Level, stage + 1), first.Id)))
                );
            }
            finally
            {
                TrainingBond.Reset();
            }
        }

        // サーバーがあるときは、サーバーの段としるしを読み、保存を待たずに演出を出してサーバーで上げる。
        // 保存できなければ、演出を閉じて元の段に戻し、通知の帯で知らせる。
        [UnityTest]
        public IEnumerator TheBreakthroughIsSavedOnTheServer()
        {
            var server = new FakePartySource(5000, "toma", "luka")
                .With("toma", 12, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder")
                .WithBond("toma", 2, 2);
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            view.MenuItems[ItemOf(view, PartySession.GuideItemKey)].onClick.Invoke();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            Open(roster);
            var page = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            yield return SceneTests.WaitUntil(
                () => page.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            page.Sheet.Tabs[3].Button.onClick.Invoke();
            var training = view.PanelFor(ItemOf(view, TrainingSession.BreakthroughItemKey))
                .GetComponent<TrainingView>();
            yield return SceneTests.WaitUntil(
                () => training.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            Assert.That(training.Presenter.State.Bond, Is.EqualTo(2));

            training.Stages[2].BreakThrough.onClick.Invoke();
            Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
            Assert.That(training.Celebration.Stage.text, Is.EqualTo("3"));
            Assert.That(training.Presenter.DeepenTask.IsCompleted, Is.False);
            yield return SceneTests.WaitUntil(() => training.Presenter.DeepenTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "bond toma 3" }));
            Assert.That(server.BondOf("toma"), Is.EqualTo((3, 1)));
            Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
            Assert.That(training.Presenter.State.Bond, Is.EqualTo(3));
            view.Back.onClick.Invoke();

            // 保存できなければ、演出を閉じて段としるしを戻す。
            server.Fail = true;
            training.Stages[3].BreakThrough.onClick.Invoke();
            Assert.That(training.Celebration.gameObject.activeSelf, Is.True);
            yield return SceneTests.WaitUntil(() => training.Presenter.DeepenTask.IsCompleted);
            Assert.That(training.Celebration.gameObject.activeSelf, Is.False);
            Assert.That(training.Presenter.State.Bond, Is.EqualTo(3));
            Assert.That(view.ToastMessage, Is.EqualTo(TrainingPresenter.DeepenFailedMessage));
        }

        private static int ItemOf(Baryonyx.UI.GuideMenu.GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        // メニューの「冒険者」から一覧を開き、選んでいる人の冒険者のタブを開く。
        private static string Trail(params string[] parts) =>
            string.Join(Baryonyx.UI.GuideMenu.GuideMenuView.TrailSeparator, parts);

        private static TrainingView OpenPage(Baryonyx.UI.GuideMenu.GuideMenuView view)
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            Open(view.PanelFor(item).GetComponent<AdventurerRosterView>());
            return view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
        }

        // 冒険者の一覧でパーティの先頭を選び、下の帯の「装備・スキル」でその人のタブを開く。
        private static void Open(AdventurerRosterView roster)
        {
            roster.Slots[0].Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
        }

        // 指を横へ400（設計座標）動かして離したことにする。-1は左へ（次の人）、1は右へ（前の人）。
        private static void Flick(TrainingView training, int direction)
        {
            var canvas = training.Swipe.GetComponentInParent<Canvas>().rootCanvas;
            var start = new Vector2(Screen.width / 2f, Screen.height / 2f);
            training.Swipe.OnEndDrag(
                new PointerEventData(EventSystem.current)
                {
                    pressPosition = start,
                    position = start + new Vector2(direction * 400f * canvas.scaleFactor, 0f),
                }
            );
        }

        // 付けている装備で上げたステータス（サーバーがないので、アプリ内の初めの装備）。
        private static CharacterStats Worn(CharacterStats stats, string id) =>
            EquipmentStats.With(stats, EquipmentLocalSource.Starter(), id);

        private static IEnumerable<string> Values(CharacterStats stats) =>
            Enumerable.Range(0, CharacterStats.Count).Select(i => stats[i].ToString());

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
    }
}
