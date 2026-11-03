using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Tavern;
using Baryonyx.Training;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TavernCardLoadoutTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            CardLoadoutSession.Reset();
            UniqueSkillSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernCardLoadoutTests));
        }

        // 冒険者のタブの「スキル」は、ステータスのタブで見ていた人の6枚を戦闘のカードで3枚ずつ2段に並べる。
        // カードを押すと、画面を覆う幕の上に選べるカードを大きく出し、今のカードを選んでおく。押したカードを選び、
        // 右下のチェックで入れ替えて幕を閉じる。固有スキルを押すと覚醒ツリーの段階を出す。
        // 幕の余白と戻るキーは何も変えずに閉じる。
        [UnityTest]
        public IEnumerator CardsChangeFromTheAdventurersTab()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var page = OpenPage(view);
            page.Sheet.Tabs[2].Button.onClick.Invoke();
            int item = ItemOf(view, CardLoadoutSession.GuideItemKey);
            Assert.That(view.Definition.Items[item].Hidden, Is.True);
            var panel = view.PanelFor(item).GetComponent<CardLoadoutView>();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            var deck = panel.transform.Find("Layout/Deck");
            SceneTests.AssertBelow(deck, view.Back.transform);
            yield return null;

            var data = panel.Party;
            var people = CardLoadoutPresenter.People(PartySession.Formation(data));
            var store = new CardLoadoutSessionStore(data, panel.Training);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo(people[0].Id));
            Assert.That(panel.Sheet.Tabs[2].Chosen, Is.True);
            Assert.That(panel.Picker.Open, Is.False);
            Assert.That(panel.UniqueCards.Length, Is.EqualTo(CardLoadoutPresenter.UniqueCount));
            Assert.That(panel.SlotCards.Length, Is.EqualTo(CardLoadoutRules.Size));
            // 6枚はスクロールせずに面に収まり、名前・種類・効果の文まで出す。
            var area = SceneTests.ScreenRect(deck);
            foreach (var card in panel.UniqueCards.Concat(panel.SlotCards))
            {
                var rect = SceneTests.ScreenRect(card.transform);
                Assert.That(area.Contains(rect.min) && area.Contains(rect.max), Is.True, card.name);
                SceneTests.AssertTouchSize(card.transform, 0.7f);
            }
            var unique = store.UniqueCardOf(people[0].Id, 0);
            Assert.That(panel.UniqueCards[0].Face.Name.text, Is.EqualTo(unique.Name));
            Assert.That(panel.UniqueCards[0].Face.Kind.text, Is.Not.Empty);
            Assert.That(panel.UniqueCards[0].Face.Description.text, Is.Not.Empty);
            AssertShows(panel, data, people[0]);
            // 左の縦の欄に、ステータス8つの数字をカードの段より左に出す。
            Assert.That(panel.Stats.Length, Is.EqualTo(CharacterStats.Count));
            Assert.That(panel.Stats.All(stat => int.TryParse(stat.text, out _)), Is.True);
            Assert.That(
                SceneTests.ScreenRect(panel.Stats[0].transform).xMax,
                Is.LessThan(SceneTests.ScreenRect(panel.UniqueCards[0].transform).xMin)
            );

            // 固有スキルを押すと、覚醒ツリーの7つの段階を出す。取得していない段階は封印した裏で押せない。
            // 取得した段階を押すと幕を閉じ、6枚の固有スキルが変わる（変わったカードは縦に回る）。
            panel.UniqueCards[0].Glass.Click();
            Assert.That(panel.Picker.Open, Is.True);
            Assert.That(panel.Picker.TreeRoot.activeSelf, Is.True);
            Assert.That(panel.Picker.ChoicesRoot.activeSelf, Is.False);
            var stages = store.UniquesOf(people[0].Id)[0];
            var unset = Enumerable
                .Range(0, UniqueSkillTreeShape.Count)
                .First(stage => stages.Progress.Has(stage) && stage != stages.Progress.SetStage);
            var shadow = Enumerable
                .Range(0, UniqueSkillTreeShape.Count)
                .First(stage => !stages.Progress.Has(stage));
            Assert.That(panel.Picker.Stages[shadow].Face.Back.gameObject.activeSelf, Is.True);
            Assert.That(panel.Picker.Stages[shadow].Seal.activeSelf, Is.True);
            Assert.That(panel.Picker.Stages[shadow].Glass.Interactable, Is.False);
            var setStage = panel.Picker.Stages[stages.Progress.SetStage];
            Assert.That(setStage.Glass.Selected, Is.True);
            Assert.That(setStage.Mark.text, Is.EqualTo(CardLoadoutView.SetMark));
            foreach (var stage in panel.Picker.Stages)
                SceneTests.AssertTouchSize(stage.transform, 0.7f);
            Pick(panel, panel.Picker.Stages[unset]);
            Assert.That(panel.Picker.Open, Is.False);
            var set = stages.Tree.StageAt(unset).Card;
            Assert.That(panel.UniqueCards[0].Face.Name.text, Is.EqualTo(set.Name));
            Assert.That(
                panel.UniqueCards[0].GetComponent<GlassCardTurn>() is { Turning: true },
                Is.True
            );
            Assert.That(panel.LastNotice, Is.Empty);

            // カスタムスキルを押すと、その人が付けられるカードを出す。今の枠のカードは金の縁で囲む。
            // 幕の余白を押すと、何も変えずに閉じる。
            var before = PartySession.CardsOf(data, people[0].Id).ToArray();
            panel.SlotCards[1].Glass.Click();
            Assert.That(panel.Picker.Open, Is.True);
            Assert.That(panel.Picker.ChoicesRoot.activeSelf, Is.True);
            AssertChoices(panel, people[0], before, 1);
            panel.Picker.Closers[0].onClick.Invoke();
            Assert.That(panel.Picker.Open, Is.False);
            Assert.That(PartySession.CardsOf(data, people[0].Id), Is.EqualTo(before));

            // 右へのフリックで、パーティにいない最後の仲間へ回る。左の列も光る。
            var other = people[people.Count - 1];
            panel.Swipe.Flick(-1);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo(other.Id));
            Assert.That(
                panel.Sheet.People.Single(person => person.Id == other.Id).Selected.activeSelf,
                Is.True
            );
            AssertShows(panel, data, other);

            // 2つ目の枠に、まだ付けていないカードを入れる。幕を閉じ、6枚のカードもすぐ変わる。
            before = PartySession.CardsOf(data, other.Id).ToArray();
            panel.SlotCards[1].Glass.Click();
            AssertChoices(panel, other, before, 1);
            var choice = Choices(panel)
                .First(card => card.Glass.Interactable && !before.Contains(card.Id));
            string id = choice.Id;
            // 押すだけでは入れ替えず、選んだカードを金の縁で囲む。
            choice.Glass.Click();
            Assert.That(panel.Picker.Open, Is.True);
            Assert.That(choice.Glass.Selected, Is.True);
            Assert.That(PartySession.CardsOf(data, other.Id), Is.EqualTo(before));
            panel.Picker.Confirm.onClick.Invoke();
            Assert.That(panel.Picker.Open, Is.False);
            var after = PartySession.CardsOf(data, other.Id);
            Assert.That(after[1], Is.EqualTo(id));
            // 変わったことはカードを見ればわかるので、通知の帯は出さない。
            Assert.That(panel.LastNotice, Is.Empty);
            Assert.That(panel.SlotCards[1].Face.Name.text, Is.EqualTo(CardSkills.Find(id).Name));
            Assert.That(
                panel.SlotCards[1].GetComponent<GlassCardTurn>() is { Turning: true },
                Is.True
            );

            // 開き直すと、入れたカードに金の「E」が付く。その人のほかの枠にあるカードを押すと、2つの枠を入れ替える。
            panel.SlotCards[1].Glass.Click();
            var held = Choices(panel).Single(card => card.Id == id);
            Assert.That(held.MarkPlate.activeSelf, Is.True);
            Assert.That(held.Mark.text, Is.EqualTo(CardLoadoutPresenter.EquippedMark));
            Assert.That(held.Mark.color, Is.EqualTo(UiPalette.Gold));
            Pick(panel, Choices(panel).Single(card => card.Id == after[0]));
            Assert.That(PartySession.CardsOf(data, other.Id)[1], Is.EqualTo(after[0]));
            Assert.That(PartySession.CardsOf(data, other.Id)[0], Is.EqualTo(id));
            Assert.That(panel.LastNotice, Is.Empty);

            // 幕を開いているときの「もどる」（端末の戻るキー）は、幕だけを閉じる。もう一度で冒険者の一覧へ戻る。
            panel.SlotCards[0].Glass.Click();
            Assert.That(panel.Picker.Open, Is.True);
            view.Back.onClick.Invoke();
            Assert.That(panel.Picker.Open, Is.False);
            Assert.That(panel.gameObject.activeSelf, Is.True);
            view.Back.onClick.Invoke();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(view.PanelFor(ItemOf(view, PartySession.GuideItemKey)).activeSelf, Is.True);
        }

        // 6枚のカスタムスキルを、その人のカードで出す。空いた枠は点線のカード。
        private static void AssertShows(
            CardLoadoutView panel,
            PartyMockData data,
            PartyMember member
        )
        {
            var cards = PartySession.CardsOf(data, member.Id);
            for (int i = 0; i < panel.SlotCards.Length; i++)
            {
                var slot = panel.SlotCards[i];
                bool filled = i < cards.Count && cards[i] != null;
                Assert.That(slot.Face.Back.gameObject.activeSelf, Is.False, member.Id);
                Assert.That(slot.Face.gameObject.activeSelf, Is.EqualTo(filled), member.Id);
                Assert.That(slot.Empty.activeSelf, Is.EqualTo(!filled), member.Id);
                if (filled)
                {
                    Assert.That(
                        slot.Face.Name.text,
                        Is.EqualTo(CardSkills.Find(cards[i]).Name),
                        member.Id
                    );
                    Assert.That(slot.Face.Art.texture, Is.Not.Null, member.Id);
                }
            }
        }

        // 選ぶ画面の候補：その人が付けられるカードをコストの低い順に並べ、付けているカードに「E」、今の枠のカードを金の縁で囲む。
        // 枠に何か入っていれば、最後に「スキルを外す」を出す。
        private static void AssertChoices(
            CardLoadoutView panel,
            PartyMember member,
            string[] cards,
            int slot
        )
        {
            var usable = CardLoadoutRules.Usable(member);
            var expected = CardLoadoutRules.Listed(usable).Select(card => card.Id).ToArray();
            var shown = Choices(panel);
            Assert.That(shown.Select(card => card.Id), Is.EqualTo(expected), member.Id);
            foreach (var card in shown)
            {
                bool held = cards.Contains(card.Id);
                Assert.That(card.MarkPlate.activeSelf, Is.EqualTo(held), card.Id + " E");
                Assert.That(
                    card.Glass.Selected,
                    Is.EqualTo(card.Id == cards[slot]),
                    card.Id + " selected"
                );
                // 属性が合わないカードは、下の段に暗くして押せない。
                Assert.That(
                    card.Glass.Interactable,
                    Is.EqualTo(CardLoadoutRules.CanUse(usable, CardSkills.Find(card.Id))),
                    card.Id + " interactable"
                );
                SceneTests.AssertTouchSize(card.transform, 0.7f);
            }
            Assert.That(panel.Picker.Remove.gameObject.activeSelf, Is.EqualTo(cards[slot] != null));
            Assert.That(panel.Picker.Confirm.interactable, Is.EqualTo(cards[slot] != null));
        }

        // サーバーがあるときは、開くたびにサーバーのカードを読み、付け替えを保存してから表示を変える。
        [UnityTest]
        public IEnumerator CardsAreReadAndSavedOnTheServer()
        {
            var server = new FakePartySource(0, "toma")
                .With("toma", 12, "Fire", "Ice")
                .With("luka", 11, "VitalThrust", "Thunder");
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var panel = default(CardLoadoutView);
            yield return OpenOnServer(view, value => panel = value);

            // ステータスのタブで読んだ編成をすぐ出し、読み直しを待たない（カードを伏せない）。
            Assert.That(panel.LoadTask.IsCompleted, Is.False);
            Assert.That(panel.Status.text, Is.Not.EqualTo(CardLoadoutView.LoadingText));
            Assert.That(
                panel
                    .UniqueCards.Concat(panel.SlotCards)
                    .All(card => !card.Face.Back.gameObject.activeSelf),
                Is.True
            );
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("toma"));
            yield return SceneTests.WaitUntil(
                () => panel.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            // サーバーのパーティと仲間だけをフリックで回る。
            var second = panel.SlotCards[1].Face;
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Ice").Name));
            panel.Swipe.Flick(1);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("luka"));
            panel.Swipe.Flick(1);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("toma"));

            // 保存を待たずに枠を変え、そのあとで保存する。
            panel.SlotCards[1].Glass.Click();
            Pick(panel, Choice(panel, "Embers"));
            Assert.That(panel.Presenter.ChooseTask.IsCompleted, Is.False);
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "card toma 1 Embers" }));
            Assert.That(server.CardsOf("toma")[1], Is.EqualTo("Embers"));
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(panel.LastNotice, Is.Empty);

            // 保存できなかったときは、出したカードを戻して通知の帯で知らせる。
            server.Fail = true;
            panel.SlotCards[1].Glass.Click();
            Pick(panel, Choice(panel, "Meteor"));
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Meteor").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(view.ToastMessage, Is.EqualTo(CardLoadoutPresenter.SaveFailedMessage));
        }

        // 持っているスキルのうち、属性が合わないスキルは、付けられるスキルの下の段から暗くして並べ、押せない。
        [UnityTest]
        public IEnumerator SkillsOfOtherElementsAreDarkBelow()
        {
            var server = new FakePartySource(0, "toma")
                .With("toma", 12, PartyCardKey.Card("c1"), PartyCardKey.Card("c2"))
                .WithOwned(
                    new PartySkillCard("c1", "Fire", 1f),
                    new PartySkillCard("c2", "Ice", 1f),
                    new PartySkillCard("c3", "Embers", 1f),
                    new PartySkillCard("c4", "Thunder", 1f)
                );
            PartySession.Source = server;
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var panel = default(CardLoadoutView);
            yield return OpenOnServer(guide.View, value => panel = value);
            yield return SceneTests.WaitUntil(
                () => panel.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            panel.SlotCards[2].Glass.Click();
            var thunder = Choice(panel, PartyCardKey.Card("c4"));
            var usable = Choices(panel).Where(card => card != thunder).ToArray();
            Assert.That(usable.Length, Is.EqualTo(3));
            Assert.That(usable.All(card => card.Glass.Interactable), Is.True);
            Assert.That(thunder.Glass.Interactable, Is.False);
            Assert.That(thunder.Face.Shade.enabled, Is.True);
            // 段を改めるので、付けられないカードはどれよりも下にある。
            float top = SceneTests.ScreenRect(thunder.transform).yMax;
            Assert.That(
                usable.All(card => SceneTests.ScreenRect(card.transform).yMin > top),
                Is.True
            );
            // 空いた枠なので、初めは何も選ばずチェックも押せない。暗いカードは押しても選ばない。
            Assert.That(panel.Picker.Confirm.interactable, Is.False);
            thunder.Glass.Click();
            Assert.That(thunder.Glass.Selected, Is.False);
            Assert.That(panel.Picker.Confirm.interactable, Is.False);
            Assert.That(panel.Picker.Open, Is.True);
            Assert.That(server.Calls, Is.Empty);
        }

        // 一覧がサーバーの編成を読んでから、パーティの先頭を選んで帯から冒険者のタブを開き、スキルのタブへ移る。
        private static IEnumerator OpenOnServer(
            Baryonyx.UI.GuideMenu.GuideMenuView view,
            System.Action<CardLoadoutView> opened
        )
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            roster.Slots[0].Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
            var page = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(page.gameObject.activeInHierarchy, Is.True);
            yield return SceneTests.WaitUntil(
                () => page.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            page.Sheet.Tabs[2].Button.onClick.Invoke();
            opened(
                view.PanelFor(ItemOf(view, CardLoadoutSession.GuideItemKey))
                    .GetComponent<CardLoadoutView>()
            );
        }

        private static int ItemOf(Baryonyx.UI.GuideMenu.GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        // メニューの「冒険者」から一覧を開き、パーティの先頭を選んで帯から冒険者のタブ（ステータス）を開く。
        private static TrainingView OpenPage(Baryonyx.UI.GuideMenu.GuideMenuView view)
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            roster.Slots[0].Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
            return view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
        }

        // 選ぶ画面に出ているカードを、上の段から左から順に。
        private static CardLoadoutCard[] Choices(CardLoadoutView panel) =>
            panel
                .Picker.Cards.Where(card => card.gameObject.activeSelf)
                .OrderByDescending(card => ((RectTransform)card.transform).anchoredPosition.y)
                .ThenBy(card => ((RectTransform)card.transform).anchoredPosition.x)
                .ToArray();

        // 選ぶ画面でカードを押して選び、右下のチェックで決める。
        private static void Pick(CardLoadoutView panel, CardLoadoutCard card)
        {
            card.Glass.Click();
            Assert.That(card.Glass.Selected, Is.True, card.name);
            panel.Picker.Confirm.onClick.Invoke();
        }

        private static CardLoadoutCard Choice(CardLoadoutView panel, string id) =>
            Choices(panel).Single(card => card.Id == id);
    }
}
