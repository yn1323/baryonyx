using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Equipment;
using Baryonyx.Party;
using Baryonyx.Tavern;
using Baryonyx.Training;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TavernEquipmentTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            EquipmentSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            EquipmentSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernEquipmentTests));
        }

        // 冒険者のタブの「装備」は、ステータスのタブで見ていた人の武器を選んで開く。左の列とフリックで
        // 枠を選んだまま人を替え、右の装備を押すとその場で付け替え、「外す」で外す。左にはステータスを出す。
        [UnityTest]
        public IEnumerator EquipmentChangesFromTheAdventurersTab()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            var roster = view.PanelFor(item).GetComponent<AdventurerRosterView>();
            roster.Slots[0].Card.Click();
            roster.Chosen.Detail.onClick.Invoke();
            var page = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            yield return SceneTests.WaitForTask(page.LoadTask);

            // 右のタブの「装備」を押す。メニューには出さない画面で、案内人は隠れる。
            Assert.That(page.Sheet.Tabs[0].Chosen, Is.True);
            page.Sheet.Tabs[1].Button.onClick.Invoke();
            int equipment = ItemOf(view, EquipmentSession.GuideItemKey);
            Assert.That(view.Definition.Items[equipment].Hidden, Is.True);
            var panel = view.PanelFor(equipment).GetComponent<EquipmentView>();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(page.gameObject.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            Assert.That(panel.Sheet.Tabs[1].Chosen, Is.True);
            Assert.That(panel.Sheet.Tabs[0].Chosen, Is.False);
            yield return SceneTests.WaitForTask(panel.LoadTask);
            yield return null;
            SceneTests.AssertBelow(panel.transform.Find("Layout/Person"), view.Back.transform);
            SceneTests.AssertBelow(panel.transform.Find("Layout/Items"), view.Back.transform);
            foreach (var slot in panel.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform, 0.7f);
            foreach (var tab in panel.Sheet.Tabs)
                SceneTests.AssertTouchSize(tab.transform);

            // 開いたときは、ステータスのタブで見ていた人の武器。ステータスと3つのアクセサリーも出す。
            // パンくずは人まで（どのタブかはタブでわかる）。
            var (people, _) = PartySession.Formation(panel.Party).TabOrder();
            var person = people[0];
            Assert.That(Chosen(panel.Sheet), Is.EqualTo(person.Id));
            Assert.That(
                view.Breadcrumb.text,
                Is.EqualTo(
                    string.Join(
                        Baryonyx.UI.GuideMenu.GuideMenuView.TrailSeparator,
                        "編成",
                        "冒険者",
                        person.Name
                    )
                )
            );
            Assert.That(panel.Slots[0].Selected.activeSelf, Is.True);
            panel.Slots[1].Button.onClick.Invoke();
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            Assert.That(panel.Slots[0].Selected.activeSelf, Is.False);
            // 右のタブは左の枠と連動する。
            Assert.That(
                panel.Tabs.Select(tab => tab.Label.text),
                Is.EqualTo(new[] { "武器", "防具", "アクセサリー" })
            );
            Assert.That(panel.Tabs[1].Chosen, Is.True);
            Assert.That(panel.Tabs[0].Chosen, Is.False);
            Assert.That(panel.Tabs.All(tab => tab.Button.interactable), Is.True);
            Assert.That(panel.Accessories, Has.Length.EqualTo(3));
            // ステータスは付けている装備で上げた値で、上がった項目だけ緑の上昇分を添える。
            var growth = panel.Training.Find(person.Id);
            var stats = TrainingBond.StatsOf(growth, PartySession.LevelOf(panel.Party, person.Id));
            var raise = EquipmentStats.Bonus(stats, EquipmentLocalSource.Starter(), person.Id);
            for (int i = 0; i < 8; i++)
                if (raise[i] > 0)
                    Assert.That(
                        panel.Stats[i].text,
                        Does.StartWith((stats[i] + raise[i]).ToString())
                            .And.EndWith($" +{raise[i]}</color></size>")
                    );
                else
                    Assert.That(panel.Stats[i].text, Is.EqualTo(stats[i].ToString()));
            // 一覧の行には、付けたときの増減を出す。付けている防具の行には出さない。
            var wornRow = Rows(panel)
                .Single(row => row.Mark.gameObject.activeSelf && row.Mark.text == "装備中");
            Assert.That(wornRow.Change.text, Is.Empty);
            Assert.That(
                Rows(panel).Where(row => row != wornRow).Select(row => row.Change.text),
                Has.Some.Contains("▲").Or.Some.Contains("▼")
            );

            // 左の面のフリックや左の列で人を替えても、防具の枠を選んだまま。
            panel.Swipe.Flick(1);
            Assert.That(Chosen(panel.Sheet), Is.EqualTo(people[1].Id));
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            Person(panel.Sheet, people[2].Id).Button.onClick.Invoke();
            Assert.That(Chosen(panel.Sheet), Is.EqualTo(people[2].Id));
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            Person(panel.Sheet, person.Id).Button.onClick.Invoke();
            Assert.That(Chosen(panel.Sheet), Is.EqualTo(person.Id));

            // 武器の枠を選び、まだ誰も付けていない武器に替える。
            // アクセサリーのタブを押すと1つ目のアクセサリーの枠を選ぶ。1つ目はLv1から開いていて、
            // 持っているアクセサリーを並べる。左のアクセサリーの枠を押してもアクセサリーのタブになる。
            panel.Tabs[2].Button.onClick.Invoke();
            Assert.That(panel.Tabs[2].Chosen, Is.True);
            Assert.That(panel.Accessories[0].Selected.activeSelf, Is.True);
            Assert.That(panel.Slots.Any(slot => slot.Selected.activeSelf), Is.False);
            Assert.That(
                Rows(panel),
                Has.Length.EqualTo(
                    EquipmentCatalog.All.Count(entry => entry.Slot == EquipmentSlot.Accessory)
                )
            );
            Assert.That(panel.Remove.gameObject.activeSelf, Is.True);
            Assert.That(panel.Accessories[0].Name.text, Is.EqualTo("なし"));
            Assert.That(panel.Status.text, Is.Empty);
            panel.Accessories[1].Button.onClick.Invoke();
            Assert.That(panel.Accessories[1].Selected.activeSelf, Is.True);
            Assert.That(panel.Accessories[0].Selected.activeSelf, Is.False);
            Assert.That(panel.Tabs[2].Chosen, Is.True);

            // タブを押しても、左の枠が連動して替わる。
            panel.Tabs[0].Button.onClick.Invoke();
            Assert.That(panel.Slots[0].Selected.activeSelf, Is.True);
            panel.Tabs[1].Button.onClick.Invoke();
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            Assert.That(panel.Slots[0].Selected.activeSelf, Is.False);
            panel.Slots[0].Button.onClick.Invoke();
            // 樫の杖は、トーマとミナの2本を持っている。
            var weapons =
                EquipmentCatalog.All.Count(entry => entry.Slot == EquipmentSlot.Weapon) + 1;
            Assert.That(Rows(panel), Has.Length.EqualTo(weapons));
            Assert.That(panel.Tabs[0].Chosen, Is.True);
            Assert.That(panel.Tabs[1].Chosen, Is.False);
            foreach (var row in Rows(panel))
            {
                SceneTests.AssertTouchSize(row.Button.transform);
                AssertInside(row.Name.transform, row.Button.transform);
                AssertInside(row.Mark.transform, row.Button.transform);
            }
            // 「外す」はリストの最後。
            Assert.That(
                panel.Remove.transform.GetSiblingIndex(),
                Is.EqualTo(panel.Remove.transform.parent.childCount - 1)
            );
            var dagger = Row(panel, "炎のダガー");
            Assert.That(dagger.Mark.gameObject.activeSelf, Is.False);
            dagger.Button.onClick.Invoke();
            yield return SceneTests.WaitForTask(panel.Presenter.ChangeTask);
            // 変わったことは枠と一覧を見ればわかるので、通知の帯は出さない。
            Assert.That(panel.LastNotice, Is.Empty);
            Assert.That(panel.Slots[0].Name.text, Is.EqualTo("炎のダガー"));
            Assert.That(Row(panel, "炎のダガー").Mark.text, Is.EqualTo("装備中"));

            // 防具を外すと、外すボタンは押せなくなる。
            panel.Slots[1].Button.onClick.Invoke();
            panel.Remove.onClick.Invoke();
            yield return SceneTests.WaitForTask(panel.Presenter.ChangeTask);
            Assert.That(panel.LastNotice, Is.Empty);
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo("なし"));
            Assert.That(panel.Remove.interactable, Is.False);

            // 「ステータス」のタブへ戻ると、同じ人のまま。「もどる」は冒険者の一覧へ戻る。
            panel.Sheet.Tabs[0].Button.onClick.Invoke();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(page.gameObject.activeSelf, Is.True);
            yield return SceneTests.WaitForTask(page.LoadTask);
            Assert.That(page.Name.text, Is.EqualTo(person.Name));
            view.Back.onClick.Invoke();
            Assert.That(roster.gameObject.activeSelf, Is.True);
        }

        // 左の列で光っている人のID。
        private static string Chosen(PartySheet sheet) =>
            sheet.People.Single(person => person.Selected.activeSelf).Id;

        private static PartySheetPersonWidget Person(PartySheet sheet, string id) =>
            sheet.People.Single(person => person.Id == id);

        private static int ItemOf(GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);

        private static EquipmentRow[] Rows(EquipmentView panel) =>
            panel
                .Rows.Where(row => row.gameObject.activeSelf)
                .OrderBy(row => row.transform.GetSiblingIndex())
                .ToArray();

        private static EquipmentRow Row(EquipmentView panel, string name) =>
            Rows(panel).Single(row => row.Name.text == name);

        private static void AssertInside(Transform inner, Transform outer)
        {
            var box = SceneTests.ScreenRect(outer);
            var rect = SceneTests.ScreenRect(inner);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(box.xMin - 0.5f), inner.name);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(box.xMax + 0.5f), inner.name);
        }
    }
}
