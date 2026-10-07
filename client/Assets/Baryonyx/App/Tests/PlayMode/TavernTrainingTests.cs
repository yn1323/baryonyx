using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
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
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            TrainingSession.Reset();
            CardLoadoutSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernTrainingTests));
        }

        // 冒険者の一覧の「装備・スキル・育成」は、その人の個別の画面を開く。左にイラストと右下のドット絵、
        // 右にステータスとパッシブ、デッキの4枚（固有スキル2・カスタムスキル2）、装備5枠。左右のフリックで
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
            // 個別の画面はメニューに出さない。
            int item = ItemOf(view, TrainingSession.GuideItemKey);
            Assert.That(view.Definition.Items[item].Hidden, Is.True);
            Assert.That(view.MenuItems[item], Is.Null);

            // 左のイラストと右の枠は「もどる」より下で、重ならない。ドット絵はイラストの右下に立つ。
            var portrait = training.transform.Find("Layout/Portrait");
            var detail = training.transform.Find("Layout/Detail");
            SceneTests.AssertBelow(portrait, view.Back.transform);
            SceneTests.AssertBelow(detail, view.Back.transform);
            SceneTests.AssertBelow(detail, view.transform.Find("SafeArea/Title"));
            Assert.That(
                SceneTests.ScreenRect(portrait).xMax,
                Is.LessThanOrEqualTo(SceneTests.ScreenRect(detail).xMin)
            );
            var picture = SceneTests.ScreenRect(portrait);
            var figure = SceneTests.ScreenRect(training.Figure.transform);
            Assert.That(figure.center.x, Is.GreaterThan(picture.center.x));
            Assert.That(figure.center.y, Is.LessThan(picture.center.y));
            Assert.That(training.Illustration.enabled, Is.True);
            Assert.That(training.Illustration.texture, Is.Not.Null);
            // 横に長いボタンは、メニューの行と同じく高さを7割まで許す。
            SceneTests.AssertTouchSize(training.LevelUp.transform, 0.7f);
            foreach (var gear in training.Gear)
                SceneTests.AssertTouchSize(gear.Button.transform);
            foreach (var card in training.CardSlots)
                SceneTests.AssertTouchSize(card.Button.transform);

            // 最初はパーティの1人目。ステータス・スキル・装備・カードは今のレベルの値。
            var party = PartySession.Formation(training.Party);
            var first = party.Find(party.Member(0));
            var growth = training.Data.Find(first.Id);
            Assert.That(training.Name.text, Is.EqualTo(first.Name));
            Assert.That(training.Level.text, Is.EqualTo(first.Level.ToString()));
            Assert.That(training.Illustration.texture, Is.EqualTo(first.Illustration));
            Assert.That(
                training.Stats.Select(label => label.text),
                Is.EqualTo(Values(growth.StatsAt(first.Level)))
            );
            Assert.That(training.Passives[0].Name.text, Is.EqualTo(growth.Passives[0].Name));
            Assert.That(training.Passives[0].Description.text, Is.Not.Empty);
            Assert.That(training.Passives[1].Lock.activeSelf, Is.True);
            Assert.That(
                training.Passives[1].When.text,
                Is.EqualTo($"Lv {growth.Passives[1].UnlockLevel}で解放")
            );
            // 固有スキルは押せないカードで、未解放のものは暗くして解放するレベルを重ねる。
            Assert.That(training.Uniques[0].Name.text, Is.EqualTo(growth.Uniques[0].Name));
            Assert.That(training.Uniques[0].Description.text, Is.Not.Empty);
            Assert.That(training.Uniques[0].Lock.activeSelf, Is.False);
            Assert.That(training.Uniques[1].Lock.activeSelf, Is.True);
            Assert.That(
                training.Uniques[1].When.text,
                Is.EqualTo($"Lv {growth.Uniques[1].UnlockLevel}で解放")
            );
            Assert.That(training.Uniques.All(card => card.Button == null), Is.True);
            // カスタムスキルは2枚で、枠は属性の枠、説明には数字が入る。
            Assert.That(training.CardSlots.Length, Is.EqualTo(TrainingPresenter.CustomSlots));
            Assert.That(training.CardSlots.All(card => card.Name.text != ""), Is.True);
            Assert.That(training.CardSlots.All(card => card.Art.enabled), Is.True);
            Assert.That(training.CardSlots.All(card => card.Frame.texture != null), Is.True);
            Assert.That(training.CardSlots[0].Description.text, Does.Match(@"\d"));
            Assert.That(training.Gear[0].Slot.text, Is.EqualTo("武器"));
            Assert.That(training.Gear[0].Name.text, Is.Not.Empty);
            Assert.That(training.Gear[0].Detail.text, Is.Not.Empty);
            Assert.That(training.Gear[0].Icon.enabled, Is.True);
            // アクセサリーの3枠は準備中で押せない。
            Assert.That(training.Gear[4].Slot.text, Is.EqualTo("アクセサリー 3"));
            Assert.That(training.Gear.Skip(2).All(gear => gear.Lock.activeSelf), Is.True);
            Assert.That(training.Gear.Skip(2).All(gear => !gear.Button.interactable), Is.True);

            // 左へのフリックで次の人、右へのフリックで前の人。枠の上から動かしてもフリックとして届く。
            Assert.That(
                ExecuteEvents.GetEventHandler<IEndDragHandler>(
                    training.CardSlots[0].Button.gameObject
                ),
                Is.SameAs(training.Swipe.gameObject)
            );
            Flick(training, -1);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(party.Member(1))));
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
                Is.EqualTo(growth.StatsAt(target).Hp.ToString())
            );
            long cost = TrainingRules.CostBetween(first.Level, target, training.Data.CostPerLevel);
            Assert.That(
                training.Balance.text,
                Is.EqualTo(
                    $"所持 8,450 → のこり {(8450 - cost).ToString("#,0", CultureInfo.InvariantCulture)}"
                )
            );
            training.Confirm.onClick.Invoke();
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(PartySession.LevelOf(training.Party, first.Id), Is.EqualTo(target));
            Assert.That(training.Level.text, Is.EqualTo(target.ToString()));
            Assert.That(view.ToastMessage, Does.StartWith($"{first.Name}が Lv {target} になり"));

            // カスタムスキルの枠は、その人とその枠を選んだスキルの付け替えの画面へ移り、戻ると個別の画面に帰る。
            Flick(training, -1);
            string second = party.Member(1);
            training.CardSlots[1].Button.onClick.Invoke();
            var cards = view.PanelFor(ItemOf(view, CardLoadoutSession.GuideItemKey));
            Assert.That(cards.activeInHierarchy, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            Assert.That(CardLoadoutSession.Selected, Is.EqualTo(second));
            Assert.That(
                cards.GetComponent<CardLoadoutView>().Slots[1].Selected.activeSelf,
                Is.True
            );
            view.Back.onClick.Invoke();
            Assert.That(cards.activeSelf, Is.False);
            Assert.That(training.gameObject.activeSelf, Is.True);
            Assert.That(training.Name.text, Is.EqualTo(party.Name(second)));

            // 「もどる」で冒険者の一覧へ、同じ人を選んだまま戻る。アプリを動かしている間はレベルが残る。
            view.Back.onClick.Invoke();
            var roster = view.PanelFor(ItemOf(view, PartySession.GuideItemKey))
                .GetComponent<AdventurerRosterView>();
            Assert.That(roster.gameObject.activeSelf, Is.True);
            Assert.That(training.gameObject.activeSelf, Is.False);
            Assert.That(roster.Name.text, Is.EqualTo(party.Name(second)));
            roster.Slots[0].Button.onClick.Invoke();
            roster.Open.onClick.Invoke();
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
            roster.Open.onClick.Invoke();
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

            // Lv 12 → 14 は 1,200 + 1,300 = 2,500ルーン。サーバーが上げるまで重ねた画面を開いたまま待つ。
            training.LevelUp.onClick.Invoke();
            training.More.onClick.Invoke();
            training.Confirm.onClick.Invoke();
            Assert.That(training.Presenter.Saving, Is.True);
            Assert.That(training.Confirm.interactable, Is.False);
            yield return SceneTests.WaitUntil(() => training.Presenter.ConfirmTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "level toma 12 14" }));
            Assert.That(server.LevelOf("toma"), Is.EqualTo(14));
            Assert.That(training.Dialog.activeSelf, Is.False);
            Assert.That(training.Level.text, Is.EqualTo("14"));
            training.LevelUp.onClick.Invoke();
            Assert.That(training.Balance.text, Does.StartWith("所持 2,500"));
            training.Cancel.onClick.Invoke();
            Assert.That(view.ToastMessage, Does.StartWith($"{data.Find("toma").Name}が Lv 14"));

            // 別の端末でLv 16まで上げていると断られ、読み直したLvを出す。
            server.SetLevel("toma", 16);
            training.LevelUp.onClick.Invoke();
            training.Confirm.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => training.Presenter.ConfirmTask.IsCompleted);
            Assert.That(server.LevelOf("toma"), Is.EqualTo(16));
            Assert.That(server.Runes, Is.EqualTo(2500));
            Assert.That(training.Level.text, Is.EqualTo("16"));
            Assert.That(training.LastNotice, Is.EqualTo(TrainingPresenter.LevelUpFailedMessage));
        }

        private static int ItemOf(Baryonyx.UI.GuideMenu.GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        // メニューの「冒険者」から一覧を開き、選んでいる人の個別の画面を開く。
        private static TrainingView OpenPage(Baryonyx.UI.GuideMenu.GuideMenuView view)
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            view.PanelFor(item).GetComponent<AdventurerRosterView>().Open.onClick.Invoke();
            return view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
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
