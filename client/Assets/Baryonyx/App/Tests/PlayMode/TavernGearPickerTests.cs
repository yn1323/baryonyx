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
    public sealed class TavernGearPickerTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            PartySession.Reset();
            EquipmentSession.Reset();
            TrainingSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            PartySession.Reset();
            EquipmentSession.Reset();
            TrainingSession.Reset();
            yield return SceneTests.UnloadAll(nameof(TavernGearPickerTests));
        }

        // 編成のメニューの「装備」は、冒険者の次に並び、全員のカード（パーティが先）を開く。
        // 1人を押すとその人の個別の画面へ進み、個別の画面の「もどる」で人選びへ戻る。
        [UnityTest]
        public IEnumerator GearOpensThePageOfTheChosenAdventurer()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            var items = view.Definition.Items;
            int item = ItemOf(view, AdventurerPickerView.GuideItemKey);
            Assert.That(item, Is.EqualTo(ItemOf(view, PartySession.GuideItemKey) + 1));
            Assert.That(items[item].Label, Is.EqualTo("装備"));
            Assert.That(items[item].Hidden, Is.False);
            Assert.That(view.MenuItems[item], Is.Not.Null);

            view.MenuItems[item].onClick.Invoke();
            var picker = view.PanelFor(item).GetComponent<AdventurerPickerView>();
            Assert.That(picker.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.GuideArt.activeSelf, Is.False);
            yield return SceneTests.WaitForTask(picker.LoadTask);
            yield return null;
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "装備")));

            // パーティを枠の順に、続けて控えを並べる。パーティの人だけ「パーティ」の札を出す。
            var formation = PartySession.Formation(picker.Party);
            var order = AdventurerPickerView.Order(formation);
            Assert.That(order.Count, Is.EqualTo(formation.Roster.Count));
            var shown = picker
                .Cards.Where(card => card.Card.gameObject.activeSelf)
                .OrderBy(card => card.Card.transform.GetSiblingIndex())
                .ToArray();
            Assert.That(shown.Select(card => card.Id), Is.EqualTo(order));
            var equipment = EquipmentLocalSource.Starter();
            foreach (var card in shown)
            {
                SceneTests.AssertTouchSize(card.Card.transform);
                Assert.That(card.PartyTag.activeSelf, Is.EqualTo(formation.SlotOf(card.Id) >= 0));
                Assert.That(
                    card.Level.text,
                    Is.EqualTo(PartySession.LevelOf(picker.Party, card.Id).ToString())
                );
                Assert.That(
                    card.Weapon.text,
                    Is.EqualTo(
                        AdventurerPickerView.GearName(equipment, card.Id, EquipmentSlot.Weapon)
                    )
                );
                Assert.That(
                    card.Armor.text,
                    Is.EqualTo(
                        AdventurerPickerView.GearName(equipment, card.Id, EquipmentSlot.Armor)
                    )
                );
            }
            // 仮データでは、パーティの人は武器を付けていて、控えには付けていない人がいる。
            Assert.That(shown[0].Weapon.text, Is.Not.EqualTo(AdventurerPickerView.EmptyText));
            Assert.That(
                shown.Any(card => card.Weapon.text == AdventurerPickerView.EmptyText),
                Is.True
            );

            // 控えの1人を押すと、その人の個別の画面を開く。パンくずは「編成 › 装備 › 名前」。
            var chosen = shown.Last();
            chosen.Card.Click();
            var page = view.PanelFor(ItemOf(view, TrainingSession.GuideItemKey))
                .GetComponent<TrainingView>();
            Assert.That(page.gameObject.activeInHierarchy, Is.True);
            Assert.That(picker.gameObject.activeSelf, Is.False);
            yield return SceneTests.WaitForTask(page.LoadTask);
            string name = formation.Name(chosen.Id);
            Assert.That(page.Name.text, Is.EqualTo(name));
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "装備", name)));

            // 個別の画面の「もどる」で人選びへ、もう一度でメニューへ戻る。
            view.Back.onClick.Invoke();
            Assert.That(picker.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "装備")));
            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(guide.Presenter.Left, Is.False);

            // 冒険者の一覧から開いた個別の画面は、これまでどおり冒険者の一覧へ戻る。
            int roster = ItemOf(view, PartySession.GuideItemKey);
            view.MenuItems[roster].onClick.Invoke();
            var list = view.PanelFor(roster).GetComponent<AdventurerRosterView>();
            yield return SceneTests.WaitForTask(list.LoadTask);
            list.Slots[0].Card.Hold();
            yield return SceneTests.WaitForTask(page.LoadTask);
            string first = formation.Name(formation.Member(0));
            Assert.That(view.Breadcrumb.text, Is.EqualTo(Trail("編成", "冒険者", first)));
            view.Back.onClick.Invoke();
            Assert.That(list.gameObject.activeInHierarchy, Is.True);
        }

        private static string Trail(params string[] parts) =>
            string.Join(GuideMenuView.TrailSeparator, parts);

        private static int ItemOf(GuideMenuView view, string key) =>
            System.Array.FindIndex(view.Definition.Items, entry => entry.Key == key);
    }
}
