using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Home;
using Baryonyx.StepBonus;
using Baryonyx.UI.GuideMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class GuideScenesTests
    {
        private TestGameServices services;

        [SetUp]
        public void UseTestServices()
        {
            services = TestGameServices.Use();
            StepBonusSession.Reset();
        }

        [UnityTearDown]
        public IEnumerator RestoreServices()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(GuideScenesTests));
        }

        // Homeの4つのボタンから、それぞれの案内人の画面へ移り、「もどる」でHomeへ戻る。
        [UnityTest]
        public IEnumerator EveryHomeButtonOpensItsGuideSceneAndBackReturnsHome()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            foreach (
                var (action, sceneName) in new[]
                {
                    (HomeAction.Tavern, SceneNames.Pub),
                    (HomeAction.Workshop, SceneNames.Shop),
                    (HomeAction.Temple, SceneNames.Temple),
                    (HomeAction.TravelOffice, SceneNames.TravelOffice),
                }
            )
            {
                Assert.That(SceneNames.GuideFor(action), Is.EqualTo(sceneName));
                // 続けて押しても、画面は1回だけ開く。
                ButtonFor(home.View, action).onClick.Invoke();
                ButtonFor(home.View, action).onClick.Invoke();
                Assert.That(home.Presenter.ScreenOpened, Is.True);

                yield return SceneTests.WaitUntil(
                    () => SceneManager.GetActiveScene().name == sceneName,
                    message: sceneName + " did not open."
                );
                var guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
                Assert.That(guide, Is.Not.Null);
                yield return SceneTests.WaitUntil(() => SceneTests.GuideReady(guide));
                AssertGuideFitsTheScreen(guide.View);
                SceneTests.AssertTouchSize(guide.View.Back.transform, 0.7f);

                guide.Presenter.Back();
                yield return SceneTests.WaitUntil(
                    () => SceneManager.GetActiveScene().name == SceneNames.Home,
                    message: "Home did not open again."
                );
                home = Object.FindAnyObjectByType<HomeBootstrap>();
                yield return SceneTests.WaitUntil(() => SceneTests.HomeReady(home));
            }
        }

        [UnityTest]
        public IEnumerator ListScreensOpenAFullListAndConfirmTheChoice()
        {
            foreach (var sceneName in new[] { SceneNames.Pub, SceneNames.Shop, SceneNames.Temple })
            {
                var guide = default(GuideSceneBootstrap);
                yield return SceneTests.LoadGuide(sceneName, value => guide = value);
                var view = guide.View;
                var definition = view.Definition;

                Assert.That(view.MenuPanel.activeSelf, Is.True);
                Assert.That(view.ListPanel.activeSelf, Is.False);
                Assert.That(view.MenuItems.Length, Is.EqualTo(definition.Items.Length));
                for (int i = 0; i < definition.Items.Length; i++)
                {
                    SceneTests.AssertTouchSize(view.MenuItems[i].transform, 0.7f);
                    // 酒場と工房のメニューは、項目名の前にドット絵のアイコンを置く。
                    var icon = view.MenuItems[i].transform.Find("Icon");
                    Assert.That(icon != null, Is.EqualTo(definition.Items[i].Icon != null));
                }

                for (int i = 0; i < definition.Items.Length; i++)
                {
                    // 酒場のボーナスは一覧の代わりに専用のパネルを開く（下の別のテストで確かめる）。
                    if (view.PanelFor(i) != null)
                        continue;
                    view.MenuItems[i].onClick.Invoke();
                    Assert.That(view.MenuPanel.activeSelf, Is.False);
                    Assert.That(view.ListPanel.activeSelf, Is.True);
                    var entries = definition.Items[i].Entries;
                    Assert.That(view.Entries.Count, Is.EqualTo(entries.Length));
                    // 行の文字はPrefabに焼き込んであり、定義と一致する。
                    for (int j = 0; j < entries.Length; j++)
                        Assert.That(
                            view.Entries[j]
                                .transform.Find("Name")
                                .GetComponent<TMPro.TMP_Text>()
                                .text,
                            Is.EqualTo(entries[j].Name)
                        );
                    Assert.That(view.Confirm.interactable, Is.False);

                    view.Entries[0].onClick.Invoke();
                    Assert.That(view.Confirm.interactable, Is.True);
                    Assert.That(Selected(view.Entries[0]), Is.True);
                    view.Confirm.onClick.Invoke();
                    Assert.That(
                        view.ToastMessage,
                        Is.EqualTo(GuideMenuPresenter.ComingSoon(definition.Items[i].ConfirmLabel))
                    );

                    view.Back.onClick.Invoke();
                    Assert.That(view.MenuPanel.activeSelf, Is.True);
                    Assert.That(guide.Presenter.Left, Is.False);
                }
            }
        }

        // UPTパネルの右下のボタンは、酒場をボーナス設定のまま開き、「もどる」でHomeへ直接戻る。
        [UnityTest]
        public IEnumerator HomeBonusButtonOpensTheTavernOnTheBonusSettings()
        {
            var home = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => home = value);
            SceneTests.AssertTouchSize(home.View.BonusButton.transform);
            Assert.That(SceneNames.GuideFor(HomeAction.Bonus), Is.EqualTo(SceneNames.Pub));

            home.View.BonusButton.onClick.Invoke();
            // ボーナスのボタンは歩数の同期をしない。
            Assert.That(home.Presenter.StepSyncing, Is.False);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Pub,
                message: "The tavern did not open."
            );
            var guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
            yield return SceneTests.WaitUntil(() => SceneTests.GuideReady(guide));
            var settings = BonusSettings(guide.View);
            Assert.That(settings.gameObject.activeInHierarchy, Is.True);
            Assert.That(guide.View.MenuPanel.activeSelf, Is.False);
            Assert.That(guide.View.ListPanel.activeSelf, Is.False);

            guide.Presenter.Back();
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Home,
                message: "Back did not return to Home."
            );
        }

        // 枠を選んでからボーナスを選び、空いているものはセットし、ほかの枠のものは入れ替える。
        [UnityTest]
        public IEnumerator TavernBonusSettingsSetAndSwapBonuses()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.Pub, value => guide = value);
            var view = guide.View;
            int item = System.Array.FindIndex(
                view.Definition.Items,
                entry => entry.Key == StepBonusSession.GuideItemKey
            );
            Assert.That(item, Is.GreaterThanOrEqualTo(0));
            Assert.That(view.MenuItems[item].transform.Find("Icon"), Is.Not.Null);
            view.MenuItems[item].onClick.Invoke();
            var settings = BonusSettings(view);
            Assert.That(settings.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.ListPanel.activeSelf, Is.False);

            var loadout = StepBonusSession.Loadout(settings.Data);
            Assert.That(settings.Slots.Length, Is.EqualTo(loadout.SlotCount));
            Assert.That(settings.Rows.Length, Is.EqualTo(loadout.Owned.Count));
            foreach (var slot in settings.Slots)
                SceneTests.AssertTouchSize(slot.Button.transform);
            SceneTests.AssertTouchSize(settings.Confirm.transform, 0.7f);
            Assert.That(settings.Confirm.interactable, Is.False);

            // 空いている「守り」を5,000の枠へ。
            string free = loadout.Owned.First(roll => loadout.SlotOf(roll.Id) < 0).Id;
            settings.Slots[3].Button.onClick.Invoke();
            Row(settings, free).Button.onClick.Invoke();
            Assert.That(settings.ConfirmLabel.text, Is.EqualTo("セットする"));
            settings.Confirm.onClick.Invoke();
            Assert.That(loadout.Bonus(3), Is.EqualTo(free));
            Assert.That(settings.LastToast, Does.EndWith("をセットしました"));
            Assert.That(view.ToastMessage, Is.EqualTo(settings.LastToast));
            Assert.That(settings.Slots[3].Icon.sprite, Is.EqualTo(loadout.Definition(free).Icon));

            // 2,000の枠の「幸運」を3,000の枠へ選ぶと、2つの枠を入れ替える。
            string second = loadout.Bonus(1);
            string third = loadout.Bonus(2);
            settings.Slots[2].Button.onClick.Invoke();
            Row(settings, second).Button.onClick.Invoke();
            Assert.That(settings.ConfirmLabel.text, Is.EqualTo("入れ替える"));
            Assert.That(settings.Slots[1].Partner.activeSelf, Is.True);
            settings.Confirm.onClick.Invoke();
            Assert.That(loadout.Bonus(2), Is.EqualTo(second));
            Assert.That(loadout.Bonus(1), Is.EqualTo(third));

            // タブで1つのカテゴリだけを並べる。
            settings.Tabs[2].onClick.Invoke();
            foreach (var row in settings.Rows)
                Assert.That(
                    row.Button.gameObject.activeSelf,
                    Is.EqualTo(loadout.Definition(row.Id).Category == StepBonusCategory.Drop),
                    row.Id
                );

            view.Back.onClick.Invoke();
            Assert.That(view.MenuPanel.activeSelf, Is.True);
            Assert.That(settings.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator WorldMapChoosesDestinationsOnTheMap()
        {
            var guide = default(GuideSceneBootstrap);
            yield return SceneTests.LoadGuide(SceneNames.TravelOffice, value => guide = value);
            var view = guide.View;
            var points = view.Definition.MapPoints;

            Assert.That(view.MapPanel.activeSelf, Is.True);
            // マップの画面には、メニューとリストを作らない。
            Assert.That(view.MenuPanel, Is.Null);
            Assert.That(view.Pins.Length, Is.EqualTo(points.Length));
            Assert.That(view.Depart.interactable, Is.False);

            int locked = System.Array.FindIndex(points, point => point.Locked);
            view.Pins[locked].onClick.Invoke();
            Assert.That(view.Depart.interactable, Is.False);

            int open = System.Array.FindIndex(points, point => !point.Locked);
            view.Pins[open].onClick.Invoke();
            Assert.That(view.Depart.interactable, Is.True);
            Assert.That(view.MapDetail.text, Does.StartWith(points[open].Name));
            view.Depart.onClick.Invoke();
            Assert.That(view.ToastMessage, Is.EqualTo("出発（準備中）"));
        }

        private static Button ButtonFor(HomeView view, HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => view.TavernButton,
                HomeAction.Workshop => view.WorkshopButton,
                HomeAction.Temple => view.TempleButton,
                _ => view.TravelOfficeButton,
            };

        private static StepBonusSettingsView BonusSettings(GuideMenuView view) =>
            view
                .ItemPanels.Where(panel => panel != null)
                .Select(panel => panel.GetComponent<StepBonusSettingsView>())
                .Single(settings => settings != null);

        private static StepBonusRowWidget Row(StepBonusSettingsView settings, string id) =>
            settings.Rows.Single(row => row.Id == id);

        private static bool Selected(Button button) =>
            button.transform.Find("Selected").gameObject.activeSelf;

        // 案内人の上・左・右が画面の外へはみ出さない（下端の腰だけは画面の下端に接してよい）。
        private static void AssertGuideFitsTheScreen(GuideMenuView view)
        {
            Canvas.ForceUpdateCanvases();
            var guide = (RectTransform)view.transform.Find("SafeArea/Guide");
            var screen = (RectTransform)view.transform;
            var corners = new Vector3[4];
            guide.GetWorldCorners(corners);
            var min = screen.InverseTransformPoint(corners[0]);
            var max = screen.InverseTransformPoint(corners[2]);
            var rect = screen.rect;
            Assert.That(min.x, Is.GreaterThanOrEqualTo(rect.xMin), "left");
            Assert.That(max.x, Is.LessThanOrEqualTo(rect.xMax), "right");
            Assert.That(max.y, Is.LessThanOrEqualTo(rect.yMax), "top");
        }
    }
}
