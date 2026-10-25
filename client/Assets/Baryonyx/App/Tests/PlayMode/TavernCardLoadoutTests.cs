using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
using Baryonyx.SkillEvolution;
using Baryonyx.Tavern;
using Baryonyx.Training;
using NUnit.Framework;
using TMPro;
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

        // 個別の画面でカスタムスキルの枠を押すと、その人とその枠を選んだスキルの付け替えの画面を、選択モードで開く。
        // 左は人とステータスの下に6枚の札と、選んだ枠の今のカード。右のカードを押すとその場で入れ替える。
        // 「戻る」で6枚の一覧に戻り、一覧を押しても何も変わらない。「今のデッキ」で6枚を全画面で出す。
        [UnityTest]
        public IEnumerator CardsChangeFromTheAdventurersPage()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var page = OpenPage(view);
            page.CardSlots[1].Button.onClick.Invoke();
            int item = ItemOf(view, CardLoadoutSession.GuideItemKey);
            Assert.That(view.Definition.Items[item].Hidden, Is.True);
            var panel = view.PanelFor(item).GetComponent<CardLoadoutView>();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            SceneTests.AssertBelow(panel.transform.Find("Layout/Person"), view.Back.transform);
            SceneTests.AssertBelow(panel.transform.Find("Layout/Cards"), view.Back.transform);
            yield return null;

            // 開いたときは、個別の画面で見ていた人の、押した2つ目の枠の選択モード。
            var data = panel.Party;
            var people = CardLoadoutPresenter.People(PartySession.Formation(data));
            var store = new CardLoadoutSessionStore(data, panel.Training);
            int uniques = CardLoadoutPresenter.UniqueCount;
            Assert.That(panel.Person.Name.text, Is.EqualTo(people[0].Name));
            Assert.That(panel.Selection.activeSelf, Is.True);
            Assert.That(panel.Overview.activeSelf, Is.False);
            Assert.That(panel.Tiles.Length, Is.EqualTo(uniques + CardLoadoutRules.Size));
            Assert.That(SelectedTile(panel), Is.EqualTo(uniques + 1));
            var cards = PartySession.CardsOf(data, people[0].Id);
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find(cards[1]).Name));
            Assert.That(panel.Chosen.Description.text, Is.Not.Empty);
            Assert.That(panel.Stats.All(stat => stat.text != ""), Is.True);
            foreach (var tile in panel.Tiles)
                SceneTests.AssertTouchSize(tile.Button.transform, 0.7f);

            // 右の上にスキル・アイテムのタブを置く。
            Assert.That(
                panel.Tabs.Select(tab => tab.Label.text),
                Is.EqualTo(new[] { "スキル", "アイテム" })
            );
            Assert.That(panel.Tabs[0].Chosen, Is.True);
            Assert.That(panel.Tabs[1].Chosen, Is.False);
            Assert.That(panel.Tabs.All(tab => tab.Button.interactable), Is.True);

            // 「戻る」で6枚の一覧に戻る。固有スキル3枚とカスタムスキル3枚。右の一覧は読めるが、押しても変わらない。
            panel.Back.onClick.Invoke();
            Assert.That(panel.Overview.activeSelf, Is.True);
            Assert.That(panel.Selection.activeSelf, Is.False);
            Assert.That(panel.UniqueCards.Length, Is.EqualTo(uniques));
            Assert.That(panel.SlotCards.Length, Is.EqualTo(CardLoadoutRules.Size));
            foreach (var card in panel.UniqueCards.Concat(panel.SlotCards))
                SceneTests.AssertTouchSize(card.Button.transform, 0.7f);
            Assert.That(
                panel.UniqueCards[0].Name.text,
                Is.EqualTo(store.UniqueCardOf(people[0].Id, 0).Name)
            );
            Assert.That(panel.UniqueCards[0].Kind.text, Is.Not.Empty);
            AssertShows(panel, data, people[0]);
            Rows(panel).First(entry => !cards.Contains(entry.Id)).Button.onClick.Invoke();
            Assert.That(PartySession.CardsOf(data, people[0].Id), Is.EqualTo(cards));

            // 固有スキルを押すと、その固有スキルの選択モードになり、右はタブと一覧の代わりに進化ツリーになる。
            // 取得した段階を押すと、その場でセットし、左のカードも変わる。取得していない段階は影だけで押せない。
            panel.UniqueCards[0].Button.onClick.Invoke();
            Assert.That(panel.Selection.activeSelf, Is.True);
            Assert.That(SelectedTile(panel), Is.EqualTo(0));
            Assert.That(panel.Tree.Root.activeSelf, Is.True);
            Assert.That(panel.List.viewport.gameObject.activeSelf, Is.False);
            Assert.That(panel.Tabs.All(tab => !tab.gameObject.activeSelf), Is.True);
            var stages = store.UniquesOf(people[0].Id)[0];
            var unset = Enumerable
                .Range(0, UniqueSkillTreeShape.Count)
                .First(stage => stages.Progress.Has(stage) && stage != stages.Progress.SetStage);
            var shadow = Enumerable
                .Range(0, UniqueSkillTreeShape.Count)
                .First(stage => !stages.Progress.Has(stage));
            Assert.That(panel.Tree.Stages[shadow].Shadow.activeSelf, Is.True);
            Assert.That(panel.Tree.Stages[shadow].Card.Button.interactable, Is.False);
            foreach (var stage in panel.Tree.Stages)
                SceneTests.AssertTouchSize(stage.Card.Button.transform, 0.7f);
            panel.Tree.Stages[unset].Card.Button.onClick.Invoke();
            var set = stages.Tree.StageAt(unset).Card;
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(set.Name));
            Assert.That(panel.Tree.Stages[unset].Mark.text, Is.EqualTo(CardLoadoutView.SetMark));
            Assert.That(panel.LastNotice, Is.Empty);

            // カスタムスキルの札を押すと、その枠の選択モードになり、右はタブと一覧に戻る。
            panel.Tiles[uniques + 1].Button.onClick.Invoke();
            Assert.That(panel.Tree.Root.activeSelf, Is.False);
            Assert.That(panel.List.viewport.gameObject.activeSelf, Is.True);
            Assert.That(SelectedTile(panel), Is.EqualTo(uniques + 1));

            // 右へのフリックで、パーティにいない最後の仲間へ回る。枠は2つ目のまま。
            var other = people[people.Count - 1];
            panel.Swipe.Flick(-1);
            Assert.That(panel.Person.Name.text, Is.EqualTo(other.Name));
            Assert.That(SelectedTile(panel), Is.EqualTo(uniques + 1));
            AssertShows(panel, data, other);

            // アイテムのタブは、アイテムをまだ持てないので一覧を空にして、持っていないことだけを出す。
            panel.Tabs[1].Button.onClick.Invoke();
            Assert.That(panel.Tabs[1].Chosen, Is.True);
            Assert.That(panel.Tabs[0].Chosen, Is.False);
            Assert.That(Rows(panel), Is.Empty);
            Assert.That(panel.Status.text, Is.EqualTo(CardLoadoutView.NoItemText));
            Assert.That(panel.UsableLine.activeSelf, Is.False);
            Assert.That(SelectedTile(panel), Is.EqualTo(uniques + 1));
            panel.Tabs[0].Button.onClick.Invoke();
            Assert.That(panel.Tabs[0].Chosen, Is.True);
            AssertShows(panel, data, other);

            // 2つ目の枠に、まだ付けていないカードを入れる。左のカードもすぐ変わる。
            var before = PartySession.CardsOf(data, other.Id).ToArray();
            var row = Rows(panel).First(entry => !before.Contains(entry.Id));
            row.Button.onClick.Invoke();
            var after = PartySession.CardsOf(data, other.Id);
            Assert.That(after[1], Is.EqualTo(row.Id));
            // 変わったことはカードを見ればわかるので、通知の帯は出さない。
            Assert.That(panel.LastNotice, Is.Empty);
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
            Assert.That(row.Mark.gameObject.activeSelf, Is.True);
            Assert.That(row.Mark.text, Is.EqualTo("2枚目"));

            // その人のほかの枠にあるカードを押すと、2つの枠の中身を入れ替える。
            Row(panel, after[0]).Button.onClick.Invoke();
            Assert.That(PartySession.CardsOf(data, other.Id)[1], Is.EqualTo(after[0]));
            Assert.That(PartySession.CardsOf(data, other.Id)[0], Is.EqualTo(row.Id));
            Assert.That(panel.LastNotice, Is.Empty);

            // 「今のデッキ」は、6枚を全画面で出す。上の帯の「もどる」と×は、全画面だけを閉じる。
            panel.Back.onClick.Invoke();
            panel.DeckButton.onClick.Invoke();
            Assert.That(panel.Deck.activeSelf, Is.True);
            Assert.That(panel.DeckTitle.text, Is.EqualTo($"{other.Name}のデッキ"));
            Assert.That(panel.DeckSlots[0].Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
            Assert.That(panel.DeckSlots[0].Description.text, Is.Not.Empty);
            Assert.That(panel.DeckUniques.Length, Is.EqualTo(uniques));
            view.Back.onClick.Invoke();
            Assert.That(panel.Deck.activeSelf, Is.False);
            Assert.That(panel.gameObject.activeSelf, Is.True);
            panel.DeckButton.onClick.Invoke();
            panel.DeckClose.onClick.Invoke();
            Assert.That(panel.Deck.activeSelf, Is.False);

            // 「もどる」で個別の画面へ、付け替えの画面で最後に見ていた人のまま戻り、付け替えたカードを出す。
            view.Back.onClick.Invoke();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(page.gameObject.activeSelf, Is.True);
            Assert.That(page.Name.text, Is.EqualTo(other.Name));
            Assert.That(page.CardSlots[0].Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));

            // 個別の画面の固有スキルも、付け替えの画面でセットした段階を出す。
            page.Swipe.Flick(1);
            Assert.That(page.Name.text, Is.EqualTo(people[0].Name));
            Assert.That(page.Uniques[0].Name.text, Is.EqualTo(set.Name));
        }

        // 選択モードで金の枠を付けている札。どれもなければ-1。
        private static int SelectedTile(CardLoadoutView panel) =>
            System.Array.FindIndex(panel.Tiles, tile => tile.Selected.activeSelf);

        // 6枚の一覧のカスタムスキルと右の行を、その人のカードと付けられるカードで出し、文字は枠からはみ出さない。
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
                string name =
                    i < cards.Count && cards[i] != null
                        ? CardSkills.Find(cards[i]).Name
                        : TrainingPresenter.EmptyName;
                Assert.That(slot.Name.text, Is.EqualTo(name), member.Id);
                if (name != TrainingPresenter.EmptyName)
                    Assert.That(slot.Art.texture, Is.Not.Null, member.Id);
            }
            var usable = CardLoadoutRules.Usable(member);
            var expected = CardLoadoutRules.Choices(usable).Select(card => card.Id).ToArray();
            Assert.That(Rows(panel).Select(row => row.Id), Is.EqualTo(expected), member.Id);
            Assert.That(panel.Status.text, Is.Empty);
            Assert.That(panel.UsableLine.activeSelf, Is.True);
            foreach (var row in Rows(panel))
            {
                SceneTests.AssertTouchSize(row.Button.transform);
                bool held = cards.Contains(row.Id);
                Assert.That(row.Mark.gameObject.activeSelf, Is.EqualTo(held), row.Id);
                var transform = row.Button.transform;
                AssertFits(
                    transform,
                    transform.Find("Body/Content/NameLine/Name").GetComponent<TMP_Text>(),
                    transform.Find("Body/Content/NameLine/Kind").GetComponent<TMP_Text>(),
                    row.Description
                );
            }
        }

        private static void AssertFits(Transform box, TMP_Text name, TMP_Text kind, TMP_Text text)
        {
            var frame = SceneTests.ScreenRect(box);
            Assert.That(
                SceneTests.ScreenRect(kind.transform).xMax,
                Is.LessThanOrEqualTo(frame.xMax),
                name.text
            );
            // 2026-10-25のカードの作り直しで、説明文が欄からはみ出すカードがある。ユーザーの判断で当面はそのままにし、
            // 説明文の高さは確かめない（doc/plans/2026-10-25-fixed-energy-cards.md の未決）。
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
            // 一覧がサーバーの編成を読んでから、パーティの先頭を長押しして個別の画面を開く。
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            roster.Slots[0].Card.Hold();
            var page = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(page.gameObject.activeInHierarchy, Is.True);
            yield return SceneTests.WaitUntil(
                () => page.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            page.CardSlots[1].Button.onClick.Invoke();
            var panel = view.PanelFor(ItemOf(view, CardLoadoutSession.GuideItemKey))
                .GetComponent<CardLoadoutView>();
            // 個別の画面で読んだ編成をすぐ出し、読み直しを待たない（読み込み中…を出さない）。
            Assert.That(panel.LoadTask.IsCompleted, Is.False);
            Assert.That(panel.Status.text, Is.Not.EqualTo(CardLoadoutView.LoadingText));
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("toma").Name));
            yield return SceneTests.WaitUntil(
                () => panel.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            // サーバーのパーティと仲間だけをフリックで回る。
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("toma").Name));
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find("Ice").Name));
            Assert.That(SelectedTile(panel), Is.EqualTo(CardLoadoutPresenter.UniqueCount + 1));
            panel.Swipe.Flick(1);
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("luka").Name));
            panel.Swipe.Flick(1);
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("toma").Name));

            // 保存を待たずに枠を変え、そのあとで保存する。
            Row(panel, "Embers").Button.onClick.Invoke();
            Assert.That(panel.Presenter.ChooseTask.IsCompleted, Is.False);
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "card toma 1 Embers" }));
            Assert.That(server.CardsOf("toma")[1], Is.EqualTo("Embers"));
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(panel.LastNotice, Is.Empty);

            // 保存できなかったときは、出したカードを戻して通知の帯で知らせる。
            server.Fail = true;
            Row(panel, "Meteor").Button.onClick.Invoke();
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find("Meteor").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(panel.Chosen.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(view.ToastMessage, Is.EqualTo(CardLoadoutPresenter.SaveFailedMessage));
        }

        private static int ItemOf(Baryonyx.UI.GuideMenu.GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        // メニューの「冒険者」から一覧を開き、パーティの先頭を長押しして個別の画面を開く。
        private static TrainingView OpenPage(Baryonyx.UI.GuideMenu.GuideMenuView view)
        {
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            view.PanelFor(item).GetComponent<AdventurerRosterView>().Slots[0].Card.Hold();
            return view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
        }

        private static CardLoadoutRowWidget[] Rows(CardLoadoutView panel) =>
            panel
                .Rows.Where(row => row.Button.gameObject.activeSelf)
                .OrderBy(row => row.Button.transform.GetSiblingIndex())
                .ToArray();

        private static CardLoadoutRowWidget Row(CardLoadoutView panel, string id) =>
            panel.Rows.Single(row => row.Id == id);
    }
}
