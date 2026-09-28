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
        private const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private const float MinimumTouchSize = 128f;
        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [TearDown]
        public void RestoreServices() => services?.Dispose();

        private static readonly HomeAction[] ScreenButtons =
        {
            HomeAction.Tavern,
            HomeAction.Workshop,
            HomeAction.Temple,
            HomeAction.TravelOffice,
        };

        [UnityTest]
        public IEnumerator HomeButtonOpensTheGuideSceneAndBackReturnsHome(
            [ValueSource(nameof(ScreenButtons))] HomeAction action
        )
        {
            var sceneName = HomeBootstrap.ScreenSceneFor(action);
            Assert.That(sceneName, Is.EqualTo(ExpectedScene(action)));
            yield return SceneManager.LoadSceneAsync(HomeScenePath, LoadSceneMode.Single);
            var home = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return WaitUntil(() => home.Presenter != null && !home.Transition.IsPlaying);

            ButtonFor(home.View, action).onClick.Invoke();
            ButtonFor(home.View, action).onClick.Invoke();
            Assert.That(home.Presenter.ScreenOpened, Is.True);

            yield return WaitUntil(() => SceneManager.GetActiveScene().name == sceneName, 5f);
            var guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
            Assert.That(guide, Is.Not.Null);
            yield return WaitUntil(() => guide.Presenter != null && !guide.Transition.IsPlaying);

            var view = guide.View;
            AssertGuideFitsTheScreen(view);
            AssertTouchSize(view.Back);

            guide.Presenter.Back();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Home", 5f);
            // 次のテストへ演出の途中の状態を持ち越さないよう、Homeが開き終わるまで待つ。
            var back = Object.FindAnyObjectByType<HomeBootstrap>();
            yield return WaitUntil(() => back.Presenter != null && !back.Transition.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ListScreensOpenAFullListAndConfirmTheChoice()
        {
            foreach (var sceneName in new[] { "Pub", "Shop", "Temple" })
            {
                var guide = default(GuideSceneBootstrap);
                yield return LoadGuide(sceneName, value => guide = value);
                var view = guide.View;
                var definition = view.Definition;

                Assert.That(view.MenuPanel.activeSelf, Is.True);
                Assert.That(view.ListPanel.activeSelf, Is.False);
                Assert.That(view.MenuItems.Length, Is.EqualTo(definition.Items.Length));
                for (int i = 0; i < definition.Items.Length; i++)
                {
                    AssertTouchSize(view.MenuItems[i]);
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
            yield return LoadGuide("TravelOffice", value => guide = value);
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

        private static string ExpectedScene(HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => "Pub",
                HomeAction.Workshop => "Shop",
                HomeAction.Temple => "Temple",
                _ => "TravelOffice",
            };

        private static Button ButtonFor(HomeView view, HomeAction action) =>
            action switch
            {
                HomeAction.Tavern => view.TavernButton,
                HomeAction.Workshop => view.WorkshopButton,
                HomeAction.Temple => view.TempleButton,
                _ => view.TravelOfficeButton,
            };

        private static IEnumerator LoadGuide(
            string sceneName,
            System.Action<GuideSceneBootstrap> found
        )
        {
            yield return SceneManager.LoadSceneAsync(
                $"Assets/Baryonyx/App/Scenes/Guide/{sceneName}.unity",
                LoadSceneMode.Single
            );
            var guide = Object.FindAnyObjectByType<GuideSceneBootstrap>();
            Assert.That(guide, Is.Not.Null);
            yield return WaitUntil(() => guide.Presenter != null && !guide.Transition.IsPlaying);
            Assert.That(guide.Presenter, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            found(guide);
        }

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

        private static void AssertTouchSize(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var size = ((RectTransform)button.transform).rect.size;
            Assert.That(size.x, Is.GreaterThanOrEqualTo(MinimumTouchSize), button.name);
            Assert.That(size.y, Is.GreaterThanOrEqualTo(MinimumTouchSize * 0.7f), button.name);
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds = 3f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for the scene.");
        }
    }
}
