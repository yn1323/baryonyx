using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
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
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            CardLoadoutSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernCardLoadoutTests));
        }

        // 個別の画面でカスタムスキルの枠を押すと、その人とその枠を選んだスキルの付け替えの画面を開く。◀▶で枠を
        // 選んだまま人を替え（パーティのあとにほかの仲間も回る）、右のカードを押すとその場で入れ替え、
        // 共通の通知の帯で知らせる。左にはステータスを出す。
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
            SceneTests.AssertTouchSize(panel.Person.Prev.transform);
            SceneTests.AssertTouchSize(panel.Person.Next.transform);
            foreach (var slot in panel.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform, 0.7f);

            // 開いたときは、個別の画面で見ていた人の、押した2つ目の枠。左はカスタムスキルの2枚。
            var data = panel.Party;
            var people = CardLoadoutPresenter.People(PartySession.Formation(data));
            Assert.That(panel.Person.Name.text, Is.EqualTo(people[0].Name));
            Assert.That(panel.Slots.Length, Is.EqualTo(CardLoadoutRules.Size));
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            Assert.That(panel.Stats.All(stat => stat.text != ""), Is.True);
            AssertShows(panel, data, people[0]);

            // ◀でパーティにいない最後の仲間へ回る。枠は2つ目のまま。
            var other = people[people.Count - 1];
            panel.Person.Prev.onClick.Invoke();
            Assert.That(panel.Person.Name.text, Is.EqualTo(other.Name));
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            AssertShows(panel, data, other);

            // 2つ目の枠に、まだ付けていないカードを入れる。
            var before = PartySession.CardsOf(data, other.Id).ToArray();
            var row = Rows(panel).First(entry => !before.Contains(entry.Id));
            row.Button.onClick.Invoke();
            var after = PartySession.CardsOf(data, other.Id);
            Assert.That(after[1], Is.EqualTo(row.Id));
            Assert.That(
                panel.LastNotice,
                Is.EqualTo(
                    $"{other.Name}の「{CardSkills.Find(before[1]).Name}」を「{CardSkills.Find(row.Id).Name}」に替えました"
                )
            );
            Assert.That(view.ToastMessage, Is.EqualTo(panel.LastNotice));
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
            Assert.That(row.Mark.gameObject.activeSelf, Is.True);
            Assert.That(row.Mark.text, Is.EqualTo("2枚目"));

            // その人のほかの枠にあるカードを押すと、2つの枠の中身を入れ替える。
            Row(panel, after[0]).Button.onClick.Invoke();
            Assert.That(PartySession.CardsOf(data, other.Id)[1], Is.EqualTo(after[0]));
            Assert.That(PartySession.CardsOf(data, other.Id)[0], Is.EqualTo(row.Id));
            Assert.That(panel.LastNotice, Does.EndWith("を入れ替えました"));

            // 「もどる」で個別の画面へ、付け替えの画面で最後に見ていた人のまま戻り、付け替えたカードを出す。
            view.Back.onClick.Invoke();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(page.gameObject.activeSelf, Is.True);
            Assert.That(page.Name.text, Is.EqualTo(other.Name));
            Assert.That(page.CardSlots[0].Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
        }

        // 左の2枚と右の行を、その人のカードと付けられるカードで出し、文字は枠からはみ出さない。
        private static void AssertShows(
            CardLoadoutView panel,
            PartyMockData data,
            PartyMember member
        )
        {
            var cards = PartySession.CardsOf(data, member.Id);
            for (int i = 0; i < panel.Slots.Length; i++)
            {
                var slot = panel.Slots[i];
                Assert.That(slot.Name.text, Is.EqualTo(CardSkills.Find(cards[i]).Name), member.Id);
                Assert.That(slot.Art.texture, Is.Not.Null, member.Id);
                AssertFits(slot.Button.transform, slot.Name, slot.Kind, slot.Description);
            }
            var usable = CardLoadoutRules.Usable(member);
            var expected = CardLoadoutRules.Choices(usable).Select(card => card.Id).ToArray();
            Assert.That(Rows(panel).Select(row => row.Id), Is.EqualTo(expected), member.Id);
            Assert.That(panel.Count.text, Is.EqualTo($"{expected.Length}枚"));
            foreach (var row in Rows(panel))
            {
                SceneTests.AssertTouchSize(row.Button.transform);
                bool held = cards.Contains(row.Id);
                Assert.That(row.Mark.gameObject.activeSelf, Is.EqualTo(held), row.Id);
                var transform = row.Button.transform;
                AssertFits(
                    transform,
                    transform.Find("NameLine/Name").GetComponent<TMP_Text>(),
                    transform.Find("NameLine/Kind").GetComponent<TMP_Text>(),
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
            text.ForceMeshUpdate();
            Assert.That(
                text.preferredHeight,
                Is.LessThanOrEqualTo(((RectTransform)text.transform).rect.height + 1f),
                name.text
            );
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
            // 一覧がサーバーの編成を読んでから、選んでいる人の個別の画面を開く。
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            yield return SceneTests.WaitUntil(
                () => roster.LoadTask.IsCompleted,
                message: "The party did not load."
            );
            roster.Open.onClick.Invoke();
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
            Assert.That(panel.Count.text, Is.EqualTo(CardLoadoutView.LoadingText));
            Assert.That(panel.Person.Name.text, Is.EqualTo(CardLoadoutView.LoadingText));
            Assert.That(Rows(panel), Is.Empty);
            yield return SceneTests.WaitUntil(
                () => panel.LoadTask.IsCompleted,
                message: "The party did not load."
            );

            // サーバーのパーティと仲間だけを◀▶で回る。
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("toma").Name));
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo(CardSkills.Find("Ice").Name));
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            panel.Person.Next.onClick.Invoke();
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("luka").Name));
            panel.Person.Next.onClick.Invoke();
            Assert.That(panel.Person.Name.text, Is.EqualTo(panel.Party.Find("toma").Name));

            Row(panel, "Embers").Button.onClick.Invoke();
            Assert.That(panel.Presenter.Saving, Is.True);
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo(CardSkills.Find("Ice").Name));
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(server.Calls, Is.EqualTo(new[] { "card toma 1 Embers" }));
            Assert.That(server.CardsOf("toma")[1], Is.EqualTo("Embers"));
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(panel.LastNotice, Does.EndWith("に替えました"));

            // 保存できなかったときはカードを変えずに知らせる。
            server.Fail = true;
            Row(panel, "Meteor").Button.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => panel.Presenter.ChooseTask.IsCompleted);
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo(CardSkills.Find("Embers").Name));
            Assert.That(panel.LastNotice, Is.EqualTo(CardLoadoutPresenter.SaveFailedMessage));
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

        private static CardLoadoutRowWidget[] Rows(CardLoadoutView panel) =>
            panel
                .Rows.Where(row => row.Button.gameObject.activeSelf)
                .OrderBy(row => row.Button.transform.GetSiblingIndex())
                .ToArray();

        private static CardLoadoutRowWidget Row(CardLoadoutView panel, string id) =>
            panel.Rows.Single(row => row.Id == id);
    }
}
