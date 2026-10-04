using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.CardLoadout;
using Baryonyx.Combat;
using Baryonyx.Party;
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

        // 酒場の「カードスキル」は一覧の代わりにカードスキルを開く。上のタブで人を選び（パーティのあとに
        // ほかの仲間も並ぶ）、左で枠を押してから右のカードを押すとその場で入れ替え、共通の通知の帯で知らせる。
        [UnityTest]
        public IEnumerator CardsChangeForThePartyAndTheOtherCompanions()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == CardLoadoutSession.GuideItemKey
            );
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            view.MenuItems[item].onClick.Invoke();
            var panel = view.PanelFor(item).GetComponent<CardLoadoutView>();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            // 左右の区画をまとめた大きな枠は「もどる」と重ならず、案内人は隠れる。
            Assert.That(view.GuideArt.activeSelf, Is.False);
            AssertBelow(panel.transform.Find("Panel"), view.Back.transform);
            yield return null;

            // タブはパーティの枠の順、続けてほかの仲間を持っている順。開いたときは先頭の人。
            var data = panel.Party;
            var (people, partyCount) = CardLoadoutPresenter.People(PartySession.Formation(data));
            Assert.That(Tabs(panel), Is.EqualTo(people.Select(member => member.Id)));
            Assert.That(partyCount, Is.LessThan(people.Count));
            Assert.That(panel.PeopleDivider.activeSelf, Is.True);
            Assert.That(
                panel.PeopleDivider.transform.GetSiblingIndex(),
                Is.EqualTo(Tab(panel, people[partyCount].Id).Button.transform.GetSiblingIndex() - 1)
            );
            Assert.That(Tab(panel, people[0].Id).Selected.activeSelf, Is.True);
            foreach (var tab in panel.People)
                SceneTests.AssertTouchSize(tab.Button.transform);
            foreach (var slot in panel.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform);

            // 左はその人の4枚、右はその人が付けられるカードをコストの低い順に。
            AssertShows(panel, data, people[0]);

            // パーティにいない仲間を選ぶと、そのタブが見える位置まで横に送り、その人のカードを出す。
            var other = people[people.Count - 1];
            Tab(panel, other.Id).Button.onClick.Invoke();
            yield return null;
            Assert.That(Tab(panel, other.Id).Selected.activeSelf, Is.True);
            Assert.That(Tab(panel, people[0].Id).Selected.activeSelf, Is.False);
            var strip = ScreenRect(panel.PeopleScroll.viewport);
            var shown = ScreenRect(Tab(panel, other.Id).Button.transform);
            Assert.That(shown.xMin, Is.GreaterThanOrEqualTo(strip.xMin - 0.5f));
            Assert.That(shown.xMax, Is.LessThanOrEqualTo(strip.xMax + 0.5f));
            AssertShows(panel, data, other);

            // 3つ目の枠に、まだ付けていないカードを入れる。
            panel.Slots[2].Button.onClick.Invoke();
            Assert.That(panel.Slots[2].Selected.activeSelf, Is.True);
            var before = PartySession.CardsOf(data, other.Id).ToArray();
            var row = Rows(panel).First(entry => !before.Contains(entry.Id));
            row.Button.onClick.Invoke();
            var after = PartySession.CardsOf(data, other.Id);
            Assert.That(after[2], Is.EqualTo(row.Id));
            Assert.That(
                panel.LastNotice,
                Is.EqualTo(
                    $"{other.Name}の「{CardSkills.Find(before[2]).Name}」を「{CardSkills.Find(row.Id).Name}」に替えました"
                )
            );
            Assert.That(view.ToastMessage, Is.EqualTo(panel.LastNotice));
            Assert.That(panel.Slots[2].Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
            Assert.That(row.Mark.gameObject.activeSelf, Is.True);
            Assert.That(row.Mark.text, Is.EqualTo("3枚目"));

            // その人のほかの枠にあるカードを押すと、2つの枠の中身を入れ替える。
            Row(panel, after[0]).Button.onClick.Invoke();
            Assert.That(PartySession.CardsOf(data, other.Id)[2], Is.EqualTo(after[0]));
            Assert.That(PartySession.CardsOf(data, other.Id)[0], Is.EqualTo(row.Id));
            Assert.That(panel.LastNotice, Does.EndWith("を入れ替えました"));

            // 「もどる」でメニューへ戻る。開き直すと、最後に見ていた人と付け替えたカードを出す。
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            view.MenuItems[item].onClick.Invoke();
            yield return null;
            Assert.That(Tab(panel, other.Id).Selected.activeSelf, Is.True);
            Assert.That(panel.Slots[0].Name.text, Is.EqualTo(CardSkills.Find(row.Id).Name));
        }

        // 左の4枚と右の行を、その人のカードと付けられるカードで出し、文字は枠からはみ出さない。
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
            var frame = ScreenRect(box);
            Assert.That(
                ScreenRect(kind.transform).xMax,
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

        private static string[] Tabs(CardLoadoutView panel) =>
            panel
                .People.Where(tab => tab.Button.gameObject.activeSelf)
                .OrderBy(tab => tab.Button.transform.GetSiblingIndex())
                .Select(tab => tab.Id)
                .ToArray();

        private static CardLoadoutPersonWidget Tab(CardLoadoutView panel, string id) =>
            panel.People.Single(tab => tab.Id == id);

        private static CardLoadoutRowWidget[] Rows(CardLoadoutView panel) =>
            panel
                .Rows.Where(row => row.Button.gameObject.activeSelf)
                .OrderBy(row => row.Button.transform.GetSiblingIndex())
                .ToArray();

        private static CardLoadoutRowWidget Row(CardLoadoutView panel, string id) =>
            panel.Rows.Single(row => row.Id == id);

        private static Rect ScreenRect(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertBelow(Transform lower, Transform upper) =>
            Assert.That(
                ScreenRect(lower).yMax,
                Is.LessThanOrEqualTo(ScreenRect(upper).yMin + 0.5f)
            );
    }
}
