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

        // 個別の画面で武器か防具の枠を押すと、その人とその枠を選んだ装備の付け替えの画面を開く。左上の◀▶で
        // 枠を選んだまま人を替え、右の装備を押すとその場で付け替え、「外す」で外す。左にはステータスを出す。
        [UnityTest]
        public IEnumerator EquipmentChangesFromTheAdventurersPage()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[item].onClick.Invoke();
            view.PanelFor(item).GetComponent<AdventurerRosterView>().Open.onClick.Invoke();
            var page = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            yield return SceneTests.WaitForTask(page.LoadTask);

            // 防具の枠を押す。メニューには出さない画面で、案内人は隠れる。
            page.Gear[1].Button.onClick.Invoke();
            int equipment = ItemOf(view, EquipmentSession.GuideItemKey);
            Assert.That(view.Definition.Items[equipment].Hidden, Is.True);
            var panel = view.PanelFor(equipment).GetComponent<EquipmentView>();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(page.gameObject.activeSelf, Is.False);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            yield return SceneTests.WaitForTask(panel.LoadTask);
            yield return null;
            SceneTests.AssertBelow(panel.transform.Find("Layout/Person"), view.Back.transform);
            SceneTests.AssertBelow(panel.transform.Find("Layout/Items"), view.Back.transform);
            SceneTests.AssertTouchSize(panel.Person.Prev.transform);
            SceneTests.AssertTouchSize(panel.Person.Next.transform);
            foreach (var slot in panel.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform, 0.7f);

            // 開いたときは、個別の画面で見ていた人の防具。ステータスと3つの準備中のアクセサリーも出す。
            var (people, _) = PartySession.Formation(panel.Party).TabOrder();
            var person = people[0];
            Assert.That(panel.Person.Name.text, Is.EqualTo(person.Name));
            Assert.That(panel.Person.Figure.enabled, Is.True);
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            Assert.That(panel.Slots[0].Selected.activeSelf, Is.False);
            Assert.That(panel.Title.text, Is.EqualTo("防具"));
            Assert.That(panel.Accessories, Has.Length.EqualTo(3));
            var growth = panel.Training.Find(person.Id);
            Assert.That(
                panel.Stats.Select(stat => stat.text),
                Is.EqualTo(
                    Enumerable
                        .Range(0, 8)
                        .Select(i =>
                            growth
                                .StatsAt(PartySession.LevelOf(panel.Party, person.Id))[i]
                                .ToString()
                        )
                )
            );

            // ◀▶で人を替えても、防具の枠を選んだまま。
            panel.Person.Next.onClick.Invoke();
            Assert.That(panel.Person.Name.text, Is.EqualTo(people[1].Name));
            Assert.That(panel.Slots[1].Selected.activeSelf, Is.True);
            panel.Person.Prev.onClick.Invoke();
            Assert.That(panel.Person.Name.text, Is.EqualTo(person.Name));

            // 武器の枠を選び、まだ誰も付けていない武器に替える。
            panel.Slots[0].Button.onClick.Invoke();
            var weapons = EquipmentCatalog.All.Count(entry => entry.Slot == EquipmentSlot.Weapon);
            Assert.That(Rows(panel), Has.Length.EqualTo(weapons));
            Assert.That(panel.Title.text, Is.EqualTo("武器"));
            Assert.That(panel.Count.text, Is.EqualTo($"所持 {weapons}"));
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
            string before = panel.Slots[0].Name.text;
            var dagger = Row(panel, "炎のダガー");
            Assert.That(dagger.Mark.gameObject.activeSelf, Is.False);
            dagger.Button.onClick.Invoke();
            yield return SceneTests.WaitForTask(panel.Presenter.ChangeTask);
            Assert.That(
                panel.LastNotice,
                Is.EqualTo($"{person.Name}の「{before}」を「炎のダガー」に替えました")
            );
            Assert.That(view.ToastMessage, Is.EqualTo(panel.LastNotice));
            Assert.That(panel.Slots[0].Name.text, Is.EqualTo("炎のダガー"));
            Assert.That(Row(panel, "炎のダガー").Mark.text, Is.EqualTo("装備中"));

            // 防具を外すと、外すボタンは押せなくなる。
            panel.Slots[1].Button.onClick.Invoke();
            string armour = panel.Slots[1].Name.text;
            panel.Remove.onClick.Invoke();
            yield return SceneTests.WaitForTask(panel.Presenter.ChangeTask);
            Assert.That(panel.LastNotice, Is.EqualTo($"{person.Name}の「{armour}」を外しました"));
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo("なし"));
            Assert.That(panel.Remove.interactable, Is.False);

            // 「もどる」で個別の画面へ戻り、付け替えた装備を出す。
            view.Back.onClick.Invoke();
            Assert.That(panel.gameObject.activeSelf, Is.False);
            Assert.That(page.gameObject.activeSelf, Is.True);
            yield return SceneTests.WaitForTask(page.LoadTask);
            Assert.That(page.Name.text, Is.EqualTo(person.Name));
            Assert.That(page.Gear[0].Name.text, Is.EqualTo("炎のダガー"));
            Assert.That(page.Gear[1].Name.text, Is.EqualTo("なし"));
        }

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
