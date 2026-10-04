using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Equipment;
using Baryonyx.Party;
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

        // 編成の「装備」は一覧の代わりに装備の画面を開く。上のタブで人を選び、左で武器か防具の枠を選んで
        // 右の装備を押すとその場で付け替え、「外す」で外す。変えたことは共通の通知の帯で知らせる。
        [UnityTest]
        public IEnumerator EquipmentChangesAndTakesOffItems()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            Assert.That(view.Definition.Title, Is.EqualTo("編成"));
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == EquipmentSession.GuideItemKey
            );
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Icon"), Is.Not.Null);
            view.MenuItems[item].onClick.Invoke();
            var panel = view.PanelFor(item).GetComponent<EquipmentView>();
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);
            // 左右の区画をまとめた大きな枠は「もどる」と重ならず、案内人は隠れる。
            Assert.That(view.GuideArt.activeSelf, Is.False);
            AssertBelow(panel.transform.Find("Panel"), view.Back.transform);
            yield return SceneTests.WaitForTask(panel.LoadTask);
            yield return null;

            // タブはパーティの枠の順、続けてほかの仲間を持っている順。開いたときは先頭の人の武器。
            var (people, _) = PartySession.Formation(panel.Party).TabOrder();
            Assert.That(Tabs(panel), Is.EqualTo(people.Select(member => member.Id)));
            Assert.That(panel.People.Find(people[0].Id).Selected.activeSelf, Is.True);
            Assert.That(panel.Slots[0].Selected.activeSelf, Is.True);
            Assert.That(panel.Figure.enabled, Is.True);
            foreach (var tab in panel.People.Tabs)
                SceneTests.AssertTouchSize(tab.Button.transform);
            foreach (var slot in panel.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform);

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
            Assert.That(panel.Remove.interactable, Is.True);
            SceneTests.AssertTouchSize(panel.Remove.transform);

            // まだ誰も付けていない武器に替える。
            var person = people[0];
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

            // 防具の枠では防具を並べ、「外す」で外すと押せなくなる。
            panel.Slots[1].Button.onClick.Invoke();
            Assert.That(panel.Title.text, Is.EqualTo("防具"));
            Assert.That(
                Rows(panel).Select(row => row.Name.text),
                Is.All.Matches<string>(name =>
                    EquipmentCatalog.All.Any(entry =>
                        entry.Name == name && entry.Slot == EquipmentSlot.Armor
                    )
                )
            );
            string armour = panel.Slots[1].Name.text;
            panel.Remove.onClick.Invoke();
            yield return SceneTests.WaitForTask(panel.Presenter.ChangeTask);
            Assert.That(panel.LastNotice, Is.EqualTo($"{person.Name}の「{armour}」を外しました"));
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo("なし"));
            Assert.That(panel.Remove.interactable, Is.False);

            // 「もどる」でメニューへ戻る。開き直すと、付け替えた装備を出す。
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            view.MenuItems[item].onClick.Invoke();
            yield return SceneTests.WaitForTask(panel.LoadTask);
            yield return null;
            Assert.That(panel.Slots[0].Name.text, Is.EqualTo("炎のダガー"));
            Assert.That(panel.Slots[1].Name.text, Is.EqualTo("なし"));
        }

        private static string[] Tabs(EquipmentView panel) =>
            panel
                .People.Tabs.Where(tab => tab.Button.gameObject.activeSelf)
                .OrderBy(tab => tab.Button.transform.GetSiblingIndex())
                .Select(tab => tab.Id)
                .ToArray();

        private static EquipmentRow[] Rows(EquipmentView panel) =>
            panel
                .Rows.Where(row => row.gameObject.activeSelf)
                .OrderBy(row => row.transform.GetSiblingIndex())
                .ToArray();

        private static EquipmentRow Row(EquipmentView panel, string name) =>
            Rows(panel).Single(row => row.Name.text == name);

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

        private static void AssertInside(Transform inner, Transform outer)
        {
            var box = ScreenRect(outer);
            var rect = ScreenRect(inner);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(box.xMin - 0.5f), inner.name);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(box.xMax + 0.5f), inner.name);
        }
    }
}
