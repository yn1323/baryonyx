using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class TopHomeSceneTests
    {
        private const string TopScenePath = SceneTests.TopPath;
        private const string HomeScenePath = SceneTests.HomePath;
        private const string TitleText = "てくてくダンジョン（仮）";
        private Scene loadedScene;
        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [UnityTest]
        public IEnumerator FullScreenTopTapLoadsHomeScene()
        {
            yield return SceneManager.LoadSceneAsync(TopScenePath, LoadSceneMode.Single);
            loadedScene = SceneManager.GetSceneByPath(TopScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);

            var roots = loadedScene.GetRootGameObjects();
            var canvas = roots
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(candidate => candidate.name == "TopCanvas");
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(
                roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count(),
                Is.EqualTo(1)
            );

            // 画面のどこを押しても開始できるよう、開始ボタンは全面を覆う。
            var button = canvas.transform.Find("TopScreen").GetComponent<Button>();
            var rect = (RectTransform)button.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
            var controller = button.GetComponent<TopSceneController>();
            Assert.That(controller.NextSceneName, Is.EqualTo("Home"));
            Assert.That(controller.Transition, Is.Not.Null);

            // 背景と装飾は開始操作を遮らない。
            var backdropCanvas = roots
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(candidate => candidate.name == "TopBackdropCanvas");
            Assert.That(backdropCanvas.GetComponent<GraphicRaycaster>(), Is.Null);
            var background = backdropCanvas
                .transform.Find("TopBackground")
                .GetComponent<RawImage>();
            Assert.That(background.texture, Is.Not.Null);
            Assert.That(background.raycastTarget, Is.False);
            var responsiveBackground = background.GetComponent<ResponsiveBackground>();
            Assert.That(responsiveBackground, Is.Not.Null);
            Assert.That(
                responsiveBackground.AspectRatio,
                Is.EqualTo(background.texture.width / (float)background.texture.height)
                    .Within(.001f)
            );

            var safeArea = button.transform.Find("TopSafeArea");
            Assert.That(safeArea, Is.Not.Null);
            Assert.That(safeArea.GetComponent<SafeAreaFollower>(), Is.Not.Null);

            var reusablePanel = button
                .GetComponentsInChildren<TranslucentTextPanel>(true)
                .Single(candidate => candidate.Label.text == TitleText);
            Assert.That(reusablePanel.Panel.raycastTarget, Is.False);
            Assert.That(reusablePanel.Backdrop.raycastTarget, Is.False);
            Assert.That(reusablePanel.Label.raycastTarget, Is.False);

            var tapPanel = button
                .GetComponentsInChildren<TranslucentTextPanel>(true)
                .Single(candidate => candidate.name == "TapToStartPanel");
            Assert.That(tapPanel.Panel.raycastTarget, Is.False);
            Assert.That(tapPanel.LabelGroup, Is.Not.Null);
            Assert.That(tapPanel.LabelGroup.blocksRaycasts, Is.False);
            Assert.That(controller.TapToStartPrompt, Is.SameAs(tapPanel.gameObject));
            yield return WaitForInputReady(controller);
            yield return WaitForPhase(controller, HealthStartupPhase.Ready);
            Assert.That(tapPanel.Label.text, Is.EqualTo(TopSceneController.StartText));
            Assert.That(services.Server.Saves, Is.EqualTo(1));
            button.onClick.Invoke();
            yield return WaitForHome();

            loadedScene = SceneManager.GetSceneByPath(HomeScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            var transition = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SceneTransitionController>(true))
                .Single();
            yield return SceneTests.WaitUntil(() => !transition.IsPlaying);
            Assert.That(transition.IsPlaying, Is.False);
            Assert.That(transition.IsCovered, Is.False);
            Assert.That(
                loadedScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<HomeBootstrap>(true))
                    .Single()
                    .View,
                Is.Not.Null
            );
        }

        [UnityTest]
        public IEnumerator TopIgnoresTapsUntilRevealTransitionCompletes()
        {
            // 開く演出の途中のタップを確かめるため、演出を設定どおりの長さで再生する。
            SceneTransitionController.DurationScale = 1f;
            yield return SceneManager.LoadSceneAsync(TopScenePath, LoadSceneMode.Single);
            loadedScene = SceneManager.GetSceneByPath(TopScenePath);
            var controller = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TopSceneController>(true))
                .Single();
            var transition = controller.Transition;
            // 読み込み直後のフレームでStartが走り、覆った状態と開く演出が始まる。
            yield return null;

            // 起動直後は覆った状態から開き、全面のブロッカーがTopScreenより手前でタップを受ける。
            Assert.That(transition.IsPlaying || transition.IsCovered, Is.True);
            Assert.That(controller.IsInputReady, Is.False);
            Assert.That(controller.ContinueButton.interactable, Is.False);
            Assert.That(controller.TapToStartPrompt.activeSelf, Is.False);
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current)
                {
                    position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                },
                results
            );
            Assert.That(results, Is.Not.Empty);
            Assert.That(results[0].gameObject.name, Is.EqualTo("TransitionBlocker"));

            // 受付前に届いたクリックでは遷移を始めない。
            controller.ContinueButton.onClick.Invoke();
            yield return WaitForInputReady(controller);
            Assert.That(transition.IsPlaying, Is.False);
            Assert.That(transition.IsCovered, Is.False);
            Assert.That(SceneManager.GetSceneByPath(HomeScenePath).isLoaded, Is.False);

            // 開き終わったら開始の案内を表示し、TopScreenがタップを受ける。
            Assert.That(controller.ContinueButton.interactable, Is.True);
            Assert.That(controller.TapToStartPrompt.activeSelf, Is.True);
            results.Clear();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current)
                {
                    position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                },
                results
            );
            Assert.That(
                results.Select(result => result.gameObject.name),
                Has.No.Member("TransitionBlocker")
            );
        }

        [UnityTest]
        public IEnumerator TopShowsLoadingUntilSyncedAndRetriesAfterAFailure()
        {
            services.Server.Fail = true;
            var controller = default(TopSceneController);
            yield return LoadTop(value => controller = value);
            yield return WaitForInputReady(controller);
            yield return WaitForPhase(controller, HealthStartupPhase.Failed);
            Assert.That(controller.TapToStartPrompt.activeSelf, Is.True);
            Assert.That(controller.PromptText, Does.Contain(TopSceneController.RetryText));
            Assert.That(controller.PromptText, Does.Contain("通信に失敗しました"));

            // 失敗中のタップは遷移せず、再試行する。
            services.Server.Fail = false;
            controller.ContinueButton.onClick.Invoke();
            yield return WaitForPhase(controller, HealthStartupPhase.Ready);
            Assert.That(controller.PromptText, Is.EqualTo(TopSceneController.StartText));
            Assert.That(SceneManager.GetSceneByPath(HomeScenePath).isLoaded, Is.False);
            Assert.That(services.Server.Saves, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UnlinkedTopShowsTheLinkModalAndLinksFromIt()
        {
            services.Provider.Permission = HealthPermission.NotGranted;
            var controller = default(TopSceneController);
            yield return LoadTop(value => controller = value);
            yield return WaitForInputReady(controller);
            yield return WaitForPhase(controller, HealthStartupPhase.LinkRequired);

            var modal = controller.LinkModal;
            Assert.That(modal, Is.Not.Null);
            Assert.That(modal.IsShown, Is.True);
            Assert.That(controller.TapToStartPrompt.activeSelf, Is.False);
            Assert.That(modal.ActionLabel.text, Is.EqualTo("Health Connectと連携"));
            Assert.That(modal.LaterLabel.text, Is.EqualTo("あとで"));
            Assert.That(modal.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(
                modal.transform.parent,
                Is.SameAs(controller.transform.parent),
                "The modal sits beside the full-screen start button, not inside it."
            );
            Assert.That(
                modal.transform.GetSiblingIndex(),
                Is.GreaterThan(controller.transform.GetSiblingIndex())
            );

            modal.ActionButton.onClick.Invoke();
            yield return WaitForPhase(controller, HealthStartupPhase.Ready);
            Assert.That(modal.IsShown, Is.False);
            Assert.That(services.Server.Saves, Is.EqualTo(1));
            Assert.That(controller.PromptText, Is.EqualTo(TopSceneController.StartText));
        }

        [UnityTest]
        public IEnumerator LaterSkipsLinkingAndStillStartsTheGame()
        {
            services.Provider.Permission = HealthPermission.NotGranted;
            var controller = default(TopSceneController);
            yield return LoadTop(value => controller = value);
            yield return WaitForInputReady(controller);
            yield return WaitForPhase(controller, HealthStartupPhase.LinkRequired);

            controller.LinkModal.LaterButton.onClick.Invoke();
            Assert.That(controller.Flow.Phase, Is.EqualTo(HealthStartupPhase.Ready));
            Assert.That(controller.LinkModal.IsShown, Is.False);
            Assert.That(services.Server.Saves, Is.Zero);

            // 「あとで」を選んだ起動中は、開始時の再確認でモーダルを出し直さない。
            controller.ContinueButton.onClick.Invoke();
            yield return WaitForHome();
            Assert.That(SceneManager.GetSceneByPath(HomeScenePath).isLoaded, Is.True);
        }

        [UnityTest]
        public IEnumerator StartChecksPermissionAgainAndShowsTheModalWhenRevoked()
        {
            var controller = default(TopSceneController);
            yield return LoadTop(value => controller = value);
            yield return WaitForInputReady(controller);
            yield return WaitForPhase(controller, HealthStartupPhase.Ready);

            services.Provider.Permission = HealthPermission.NotGranted;
            controller.ContinueButton.onClick.Invoke();
            yield return WaitForPhase(controller, HealthStartupPhase.LinkRequired);
            Assert.That(controller.LinkModal.IsShown, Is.True);
            Assert.That(SceneManager.GetSceneByPath(HomeScenePath).isLoaded, Is.False);

            // 連携し直すと、押した開始操作の続きとしてHomeへ進む。
            controller.LinkModal.ActionButton.onClick.Invoke();
            yield return WaitForHome();
            Assert.That(SceneManager.GetSceneByPath(HomeScenePath).isLoaded, Is.True);
        }

        [UnityTest]
        public IEnumerator SettingsButtonSitsTopRightAndDoesNotStartTheGame()
        {
            var controller = default(TopSceneController);
            yield return LoadTop(value => controller = value);
            yield return WaitForInputReady(controller);
            yield return WaitForPhase(controller, HealthStartupPhase.Ready);

            var settings = controller.SettingsButton;
            Assert.That(settings, Is.Not.Null);
            var rect = (RectTransform)settings.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(settings.transform.parent.name, Is.EqualTo("TopSafeArea"));
            var hit = ((RectTransform)settings.transform.Find("HitArea")).rect;
            Assert.That(hit.width, Is.GreaterThanOrEqualTo(128f));
            Assert.That(hit.height, Is.GreaterThanOrEqualTo(128f));

            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            ExecuteEvents.ExecuteHierarchy(
                settings.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler
            );
            yield return null;
            Assert.That(controller.Transition.IsPlaying, Is.False);
            Assert.That(controller.ContinueButton.interactable, Is.True);
        }

        private static IEnumerator WaitForPhase(
            TopSceneController controller,
            HealthStartupPhase phase
        )
        {
            yield return SceneTests.WaitUntil(() => controller.Flow.Phase == phase);
            Assert.That(controller.Flow.Phase, Is.EqualTo(phase));
        }

        // 開く演出が終わり、開始操作を受け付けるまで待つ。
        private static IEnumerator WaitForInputReady(TopSceneController controller) =>
            SceneTests.WaitUntil(() => controller.IsInputReady);

        private static IEnumerator WaitForHome() =>
            SceneTests.WaitUntil(() => SceneManager.GetSceneByPath(HomeScenePath).isLoaded);

        private IEnumerator LoadTop(System.Action<TopSceneController> found)
        {
            yield return SceneManager.LoadSceneAsync(TopScenePath, LoadSceneMode.Single);
            loadedScene = SceneManager.GetSceneByPath(TopScenePath);
            found(
                loadedScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TopSceneController>(true))
                    .Single()
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(TopHomeSceneTests));
        }
    }
}
