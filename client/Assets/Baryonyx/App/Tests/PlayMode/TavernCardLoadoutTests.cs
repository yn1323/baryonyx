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

        // 冒険者のタブの「スキル」は、ステータスのタブで見ていた人の6枚を左に効果まで出し、1つ目のカスタムスキルの
        // 枠を選んで開く。行を押すとその枠を選び、右のカードを押すとその場で入れ替える。固有スキルの行を押すと、
        // 右は進化ツリーになる。「もどる」は冒険者の一覧へ戻る。
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
            SceneTests.AssertBelow(panel.transform.Find("Layout/Person"), view.Back.transform);
            SceneTests.AssertBelow(panel.transform.Find("Layout/Cards"), view.Back.transform);
            yield return null;

            // 開いたときは、ステータスのタブで見ていた人の、1つ目のカスタムスキルの枠。
            var data = panel.Party;
            var people = CardLoadoutPresenter.People(PartySession.Formation(data));
            var store = new CardLoadoutSessionStore(data, panel.Training);
            int uniques = CardLoadoutPresenter.UniqueCount;
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo(people[0].Id));
            Assert.That(panel.Sheet.Tabs[2].Chosen, Is.True);
            Assert.That(panel.UniqueRows.Length, Is.EqualTo(uniques));
            Assert.That(panel.SlotRows.Length, Is.EqualTo(CardLoadoutRules.Size));
            Assert.That(ChosenRow(panel), Is.EqualTo(uniques));
            foreach (var entry in panel.UniqueRows.Concat(panel.SlotRows))
                SceneTests.AssertTouchSize(entry.Card.Button.transform, 0.7f);
            // 左の行は、固有スキルもカスタムスキルも名前・種類・効果の文まで出す。
            var cards = PartySession.CardsOf(data, people[0].Id);
            Assert.That(
                panel.UniqueRows[0].Card.Name.text,
                Is.EqualTo(store.UniqueCardOf(people[0].Id, 0).Name)
            );
            Assert.That(panel.UniqueRows[0].Card.Kind.text, Is.Not.Empty);
            Assert.That(panel.UniqueRows[0].Card.Description.text, Is.Not.Empty);
            Assert.That(panel.SlotRows[0].Card.Description.text, Is.Not.Empty);
            AssertShows(panel, data, people[0]);

            // 右の上にスキル・アイテムのタブを置く。
            Assert.That(
                panel.Tabs.Select(tab => tab.Label.text),
                Is.EqualTo(new[] { "スキル", "アイテム" })
            );
            Assert.That(panel.Tabs[0].Chosen, Is.True);
            Assert.That(panel.Tabs[1].Chosen, Is.False);
            Assert.That(panel.Tabs.All(tab => tab.Button.interactable), Is.True);

            // 固有スキルの行を押すと、右はタブと一覧の代わりに進化ツリーになる。取得した段階を押すと、
            // その場でセットし、左の行も変わる。取得していない段階は影だけで押せない。
            panel.UniqueRows[0].Card.Button.onClick.Invoke();
            Assert.That(ChosenRow(panel), Is.EqualTo(0));
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
            Assert.That(panel.UniqueRows[0].Card.Name.text, Is.EqualTo(set.Name));
            Assert.That(panel.Tree.Stages[unset].Mark.text, Is.EqualTo(CardLoadoutView.SetMark));
            Assert.That(panel.LastNotice, Is.Empty);

            // カスタムスキルの行を押すと、その枠を選び、右はタブと一覧に戻る。
            panel.SlotRows[1].Card.Button.onClick.Invoke();
            Assert.That(panel.Tree.Root.activeSelf, Is.False);
            Assert.That(panel.List.viewport.gameObject.activeSelf, Is.True);
            Assert.That(ChosenRow(panel), Is.EqualTo(uniques + 1));

            // 右へのフリックで、パーティにいない最後の仲間へ回る。枠は2つ目のまま。左の列も光る。
            var other = people[people.Count - 1];
            panel.Swipe.Flick(-1);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo(other.Id));
            Assert.That(
                panel.Sheet.People.Single(person => person.Id == other.Id).Selected.activeSelf,
                Is.True
            );
            Assert.That(ChosenRow(panel), Is.EqualTo(uniques + 1));
            AssertShows(panel, data, other);

            // アイテムのタブは、アイテムをまだ持てないので一覧を空にして、持っていないことだけを出す。
            panel.Tabs[1].Button.onClick.Invoke();
            Assert.That(panel.Tabs[1].Chosen, Is.True);
            Assert.That(panel.Tabs[0].Chosen, Is.False);
            Assert.That(Rows(panel), Is.Empty);
            Assert.That(panel.Status.text, Is.EqualTo(CardLoadoutView.NoItemText));
            Assert.That(panel.UsableLine.activeSelf, Is.False);
            Assert.That(ChosenRow(panel), Is.EqualTo(uniques + 1));
            panel.Tabs[0].Button.onClick.Invoke();
            Assert.That(panel.Tabs[0].Chosen, Is.True);
            AssertShows(panel, data, other);

            // 2つ目の枠に、まだ付けていないカードを入れる。左の行もすぐ変わる。
            var before = PartySession.CardsOf(data, other.Id).ToArray();
            var row = Rows(panel).First(entry => !before.Contains(entry.Id));
            row.Button.onClick.Invoke();
            var after = PartySession.CardsOf(data, other.Id);
            Assert.That(after[1], Is.EqualTo(row.Id));
            // 変わったことは行を見ればわかるので、通知の帯は出さない。
            Assert.That(panel.LastNotice, Is.Empty);
            Assert.That(panel.SlotRows[1].Card.Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
            Assert.That(row.Mark.gameObject.activeSelf, Is.True);
            Assert.That(row.Mark.text, Is.EqualTo("2枚目"));

            // その人のほかの枠にあるカードを押すと、2つの枠の中身を入れ替える。
            Row(panel, after[0]).Button.onClick.Invoke();
            Assert.That(PartySession.CardsOf(data, other.Id)[1], Is.EqualTo(after[0]));
            Assert.That(PartySession.CardsOf(data, other.Id)[0], Is.EqualTo(row.Id));
            Assert.That(panel.LastNotice, Is.Empty);

            // 「もどる」で冒険者の一覧へ戻る。
            view.Back.onClick.Invoke();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(view.PanelFor(ItemOf(view, PartySession.GuideItemKey)).activeSelf, Is.True);
        }

        // 左の6行（固有スキル3つ、続けてカスタムスキル3つ）のうち、金の枠を付けている行。どれもなければ-1。
        private static int ChosenRow(CardLoadoutView panel) =>
            System.Array.FindIndex(
                panel.UniqueRows.Concat(panel.SlotRows).ToArray(),
                row => row.Card.Selected.activeSelf
            );

        // 6枚の一覧のカスタムスキルと右の行を、その人のカードと付けられるカードで出し、文字は枠からはみ出さない。
        private static void AssertShows(
            CardLoadoutView panel,
            PartyMockData data,
            PartyMember member
        )
        {
            var cards = PartySession.CardsOf(data, member.Id);
            for (int i = 0; i < panel.SlotRows.Length; i++)
            {
                var slot = panel.SlotRows[i];
                bool filled = i < cards.Count && cards[i] != null;
                Assert.That(slot.Content.activeSelf, Is.EqualTo(filled), member.Id);
                Assert.That(slot.Empty.activeSelf, Is.EqualTo(!filled), member.Id);
                if (filled)
                {
                    Assert.That(
                        slot.Card.Name.text,
                        Is.EqualTo(CardSkills.Find(cards[i]).Name),
                        member.Id
                    );
                    Assert.That(slot.Card.Art.texture, Is.Not.Null, member.Id);
                }
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
            // 一覧がサーバーの編成を読んでから、パーティの先頭を選んで帯から冒険者のタブを開く。
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
            var panel = view.PanelFor(ItemOf(view, CardLoadoutSession.GuideItemKey))
                .GetComponent<CardLoadoutView>();
            // ステータスのタブで読んだ編成をすぐ出し、読み直しを待たない（読み込み中…を出さない）。
            Assert.That(panel.LoadTask.IsCompleted, Is.False);
            Assert.That(panel.Status.text, Is.Not.EqualTo(CardLoadoutView.LoadingText));
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("toma"));
            yield return SceneTests.WaitUntil(
                () => panel.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            // サーバーのパーティと仲間だけをフリックで回る。
            var second = panel.SlotRows[1].Card;
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("toma"));
            panel.SlotRows[1].Card.Button.onClick.Invoke();
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Ice").Name));
            panel.Swipe.Flick(1);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("luka"));
            panel.Swipe.Flick(1);
            Assert.That(panel.Presenter.State.PersonId, Is.EqualTo("toma"));

            // 保存を待たずに枠を変え、そのあとで保存する。
            Row(panel, "Embers").Button.onClick.Invoke();
            Assert.That(panel.Presenter.ChooseTask.IsCompleted, Is.False);
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "card toma 1 Embers" }));
            Assert.That(server.CardsOf("toma")[1], Is.EqualTo("Embers"));
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(panel.LastNotice, Is.Empty);

            // 保存できなかったときは、出したカードを戻して通知の帯で知らせる。
            server.Fail = true;
            Row(panel, "Meteor").Button.onClick.Invoke();
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Meteor").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(second.Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(view.ToastMessage, Is.EqualTo(CardLoadoutPresenter.SaveFailedMessage));
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

        private static CardLoadoutRowWidget[] Rows(CardLoadoutView panel) =>
            panel
                .Rows.Where(row => row.Button.gameObject.activeSelf)
                .OrderBy(row => row.Button.transform.GetSiblingIndex())
                .ToArray();

        private static CardLoadoutRowWidget Row(CardLoadoutView panel, string id) =>
            panel.Rows.Single(row => row.Id == id);
    }
}
