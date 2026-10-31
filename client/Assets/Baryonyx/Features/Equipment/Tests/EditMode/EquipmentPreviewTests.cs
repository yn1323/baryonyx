using System.Linq;
using Baryonyx.Equipment;
using Baryonyx.Tavern.Editor;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // 装備の付け替えの画面は、停止中も防具とアクセサリーの枠、読み込み中の見た目に切り替えられる。
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
            // 保存した形は武器の枠を選んでいる。右の一覧の上にタブは置かない。
            Assert.That(view.Slots[0].Selected.activeSelf, Is.True);
            Assert.That(view.List.GetComponentsInChildren<GuideTab>(true), Is.Empty);
        }

        [Test]
        public void TheArmourStateChoosesTheArmourSlotAndListsTheArmour()
        {
            view.ShowPreviewState(0);

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

            Assert.That(view.Accessories[0].Selected.activeSelf, Is.True);
            Assert.That(view.Slots.Any(slot => slot.Selected.activeSelf), Is.False);
            // 1つ目のアクセサリーの枠はLv1から開いていて、仮データの持っているアクセサリーを並べる。
            Assert.That(
                view.Rows.Count(row => row.gameObject.activeSelf),
                Is.EqualTo(
                    EquipmentCatalog.All.Count(entry => entry.Slot == EquipmentSlot.Accessory)
                )
            );
            Assert.That(view.Remove.gameObject.activeSelf, Is.True);
            Assert.That(view.Status.text, Is.Empty);
        }

        [Test]
        public void AnAccessorySlotShowsTheWornAccessoryLikeTheWeaponSlot()
        {
            var worn = new EquipmentItemState
            {
                ItemId = "pearl-1",
                EquipmentId = "kraken-pearl",
                Name = "大ダコの黒真珠",
                Stars = "★★★★★",
                Detail = "属攻 +8%｜会心 +4%",
            };
            EquipmentView.ShowAccessory(view.Accessories[0], 0, 1, false, worn);
            Assert.That(view.Accessories[0].Name.text, Is.EqualTo("大ダコの黒真珠"));
            Assert.That(view.Accessories[0].Detail.text, Is.EqualTo("属攻 +8%｜会心 +4%"));
            Assert.That(view.Accessories[0].Stars.text, Does.StartWith("★★★★★"));
            Assert.That(view.Accessories[0].Lock.activeSelf, Is.False);

            // 開いていない枠は、武器・防具と同じ高さのまま、錠前と解放するLvだけを出す。
            EquipmentView.ShowAccessory(view.Accessories[1], 1, 1, false, worn);
            Assert.That(view.Accessories[1].Lock.activeSelf, Is.True);
            Assert.That(view.Accessories[1].Name.text, Is.EqualTo(AccessorySlots.UnlockText(1)));
            Assert.That(view.Accessories[1].Detail.text, Is.Empty);
            Assert.That(view.Accessories[1].Icon.gameObject.activeSelf, Is.False);
            var slot = (RectTransform)view.Slots[0].Button.transform;
            var accessory = (RectTransform)view.Accessories[2].Button.transform;
            Assert.That(accessory.rect.height, Is.EqualTo(slot.rect.height).Within(0.5f));
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
