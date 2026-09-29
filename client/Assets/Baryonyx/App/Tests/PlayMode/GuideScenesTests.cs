using System.Collections;
using Baryonyx.App;
using Baryonyx.Home;
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
        public void UseTestServices() => services = TestGameServices.Use();

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
