using System.Linq;
using Baryonyx.Equipment;
using Baryonyx.Tavern.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 装備の付け替えの画面は、停止中も防具とアクセサリーのタブ、読み込み中の見た目に切り替えられる。
    public sealed class EquipmentPreviewTests
    {
        private GameObject screen;
        private EquipmentView view;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TavernScreenAssets.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            screen = Object.Instantiate(prefab);
            view = screen.GetComponentInChildren<EquipmentView>(true);
            Assert.That(view, Is.Not.Null);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(screen);

        [Test]
        public void TheScreenListsItsStates()
        {
            Assert.That(
                view.PreviewStates,
                Is.EqualTo(new[] { "防具", "アクセサリー", "読み込み中", "取得できない" })
            );
            // 保存した形は武器のタブ。
            Assert.That(view.Tabs[0].Chosen, Is.True);
            Assert.That(view.Slots[0].Selected.activeSelf, Is.True);
        }

        [Test]
        public void TheArmourStateChoosesTheArmourSlotAndListsTheArmour()
        {
            view.ShowPreviewState(0);

            Assert.That(view.Tabs[1].Chosen, Is.True);
            Assert.That(view.Tabs[0].Chosen, Is.False);
            Assert.That(view.Slots[1].Selected.activeSelf, Is.True);
            Assert.That(view.Slots[0].Selected.activeSelf, Is.False);
            var shown = view.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            int armour = EquipmentCatalog.All.Count(entry => entry.Slot == EquipmentSlot.Armor);
            Assert.That(shown, Has.Length.EqualTo(armour));
            Assert.That(
                shown.Select(row => row.Name.text),
                Is.SubsetOf(
                    EquipmentCatalog
                        .All.Where(entry => entry.Slot == EquipmentSlot.Armor)
                        .Select(entry => entry.Name)
                )
            );
            Assert.That(view.Remove.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void TheAccessoryStateChoosesTheFirstAccessorySlot()
        {
            view.ShowPreviewState(1);

            Assert.That(view.Tabs[2].Chosen, Is.True);
            Assert.That(view.Accessories[0].Selected.activeSelf, Is.True);
            Assert.That(view.Slots.Any(slot => slot.Selected.activeSelf), Is.False);
            Assert.That(view.Rows.Any(row => row.gameObject.activeSelf), Is.False);
            Assert.That(view.Remove.gameObject.activeSelf, Is.False);
            // 1つ目のアクセサリーの枠はLv1から開いているが、アクセサリーはまだ持っていない。
            Assert.That(view.Status.text, Is.EqualTo(EquipmentView.NoAccessoryText));
        }

        [Test]
        public void TheLoadingStatesHideTheMockEquipment()
        {
            view.ShowPreviewState(2);
            Assert.That(view.Status.text, Is.EqualTo(EquipmentView.LoadingText));
            Assert.That(view.Rows.Any(row => row.gameObject.activeSelf), Is.False);

            view.ShowPreviewState(3);
            Assert.That(view.Status.text, Is.EqualTo(EquipmentView.LoadFailedText));
        }
    }
}
