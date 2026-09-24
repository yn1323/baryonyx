using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class HomeSceneTests
    {
        private const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private const float MinimumTouchSize = 128f;
        private TestGameServices services;

        [SetUp]
        public void UseTestServices() => services = TestGameServices.Use();

        [UnityTest]
        public IEnumerator HomeShowsMockDataWithStepsSavedOnTheServer()
        {
            var days = services
                .Provider.ReadRecentDaysAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Days;
            services.Server.SaveAsync(days, CancellationToken.None).GetAwaiter().GetResult();
            var todayKey = System.DateTime.Today.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );
            var today = days.FirstOrDefault(day => day.day == todayKey && day.hasValue);

            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            yield return WaitForSteps(bootstrap);

            Assert.That(bootstrap.Data, Is.Not.Null);
            Assert.That(bootstrap.AdventureSceneName, Is.Empty);
            Assert.That(
                bootstrap.Transition.EnterSettings.Type,
                Is.EqualTo(SceneTransitionType.Shutter)
            );

            var snapshot = bootstrap.Data.ToSnapshot(System.DateTime.Today);
            snapshot.StepLink = HomeStepLink.Linked;
            snapshot.Steps = today != null ? (int)today.steps : 0;
            var expected = HomeViewState.From(snapshot);
            Assert.That(view.StepsLabel.text, Is.EqualTo(expected.StepsText));
            Assert.That(view.ClaimLabel.text, Is.EqualTo("タップで歩数を同期"));
            Assert.That(view.RunesLabel.text, Is.EqualTo(expected.RunesText));
            Assert.That(view.DestinationNameLabel.text, Is.EqualTo(expected.DestinationNameText));
            Assert.That(view.DestinationFloorLabel.text, Is.EqualTo(expected.DestinationFloorText));
            Assert.That(view.StepDetails.activeSelf, Is.EqualTo(expected.ShowSteps));
            Assert.That(view.Segments, Has.Length.EqualTo(HomeViewState.GaugeSegments));
            Assert.That(
                view.Segments.Count(segment => segment.color != HomeView.GaugeEmpty),
                Is.EqualTo(expected.FilledSegments)
            );
            Assert.That(
                view.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(label => label.font.name),
                Has.All.Contains("DotGothic16")
            );
        }

        [UnityTest]
        public IEnumerator EveryControlIsLargeEnoughToTap()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;

            foreach (
                var button in new[]
                {
                    view.PartyButton,
                    view.EquipmentButton,
                    view.SummonButton,
                    view.GoalsButton,
                }
            )
                AssertTouchSize(button.transform);
            AssertTouchSize(view.ResumeButton.transform);
            AssertTouchSize(view.SettingsButton.transform.Find("HitArea"));
            Assert.That(
                ((RectTransform)view.WorldMapButton.transform.Find("HitArea")).rect.height,
                Is.GreaterThanOrEqualTo(MinimumTouchSize)
            );
            Assert.That(
                ((RectTransform)view.StepButton.transform).rect.height,
                Is.GreaterThanOrEqualTo(MinimumTouchSize)
            );

            // Party members, the fire and the gate stay in the centred world layer; controls
            // follow the Safe Area.
            Assert.That(view.transform.Find("World/Aria"), Is.Not.Null);
            Assert.That(view.transform.Find("World/Campfire"), Is.Not.Null);
            Assert.That(
                view.transform.Find("SafeArea").GetComponent<SafeAreaFollower>(),
                Is.Not.Null
            );
            Assert.That(
                view.ResumeButton.transform.IsChildOf(view.transform.Find("SafeArea")),
                Is.True
            );
            // The resume card replaces the old label over the gate.
            Assert.That(view.transform.Find("World/Gate"), Is.Null);
        }

        [UnityTest]
        public IEnumerator ButtonsShowMockFeedback()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;

            view.EquipmentButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("装備（準備中）"));
            view.SummonButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("召喚（準備中）"));
            view.PartyWorldButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("パーティ（準備中）"));
            view.SettingsButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("設定（準備中）"));
            view.WorldMapButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("ワールドマップ（準備中）"));
            Assert.That(bootstrap.Presenter.AdventureStarted, Is.False);
        }

        [UnityTest]
        public IEnumerator StepPanelSyncsHealthDataAndShowsTheResult()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            yield return WaitForSteps(bootstrap);
            Assert.That(services.Server.Saves, Is.Zero);

            view.StepButton.onClick.Invoke();
            yield return WaitForSteps(bootstrap);
            Assert.That(services.Server.Saves, Is.EqualTo(1));
            Assert.That(view.CurrentToast, Is.EqualTo("歩数を同期しました"));

            services.Server.Fail = true;
            view.StepButton.onClick.Invoke();
            yield return WaitForSteps(bootstrap);
            Assert.That(view.CurrentToast, Is.EqualTo("歩数を取得できませんでした"));
            Assert.That(view.ClaimLabel.text, Is.EqualTo("タップで歩数を同期"));
        }

        [UnityTest]
        public IEnumerator UnlinkedStepPanelRequestsPermissionBeforeSyncing()
        {
            services.Provider.Permission = HealthPermission.NotGranted;
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            yield return WaitForSteps(bootstrap);
            Assert.That(view.UnlinkedDetails.activeSelf, Is.True);
            Assert.That(view.ClaimLabel.text, Is.EqualTo("タップして歩数を連携"));

            // プレビューでは許可の要求が許可済みとして返る。
            view.StepButton.onClick.Invoke();
            yield return WaitForSteps(bootstrap);
            Assert.That(services.Provider.Permission, Is.EqualTo(HealthPermission.Granted));
            Assert.That(services.Server.Saves, Is.EqualTo(1));
            Assert.That(view.StepDetails.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator PressingDarkensIconsAndLabelsOverDarkBackdrops()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            Assert.That(EventSystem.current, Is.Not.Null);

            foreach (
                var button in new[]
                {
                    view.PartyButton,
                    view.EquipmentButton,
                    view.SummonButton,
                    view.GoalsButton,
                    view.WorldMapButton,
                    view.SettingsButton,
                    view.StepButton,
                }
            )
            {
                var tint = button as TintGroupButton;
                Assert.That(tint, Is.Not.Null, button.name);
                Assert.That(
                    tint.TintGraphics.OfType<UnityEngine.UI.Image>()
                        .Any(image => image.sprite != null),
                    Is.True,
                    button.name + " has no icon to darken"
                );

                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                };
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.2f);
                foreach (var graphic in tint.TintGraphics)
                    AssertColor(
                        graphic.canvasRenderer.GetColor(),
                        button.colors.pressedColor,
                        graphic
                    );

                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                yield return new WaitForSecondsRealtime(0.2f);
                foreach (var graphic in tint.TintGraphics)
                    AssertColor(
                        graphic.canvasRenderer.GetColor(),
                        button.colors.normalColor,
                        graphic
                    );
            }

            // The resume card already darkens its bright art and keeps that behaviour.
            Assert.That(view.ResumeButton, Is.Not.InstanceOf<TintGroupButton>());
        }

        [UnityTest]
        public IEnumerator ResumeWithoutDestinationStaysOnHome()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            int loads = 0;
            void Count(Scene scene, LoadSceneMode _) => loads++;

            SceneManager.sceneLoaded += Count;
            try
            {
                view.ResumeButton.onClick.Invoke();
                Assert.That(view.CurrentToast, Is.EqualTo("再開（準備中）"));
                Assert.That(bootstrap.Presenter.AdventureStarted, Is.False);
                Assert.That(bootstrap.Transition.IsPlaying, Is.False);
                yield return null;
                yield return null;
            }
            finally
            {
                SceneManager.sceneLoaded -= Count;
            }

            Assert.That(loads, Is.Zero);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HomeScenePath));
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            SceneManager.SetActiveScene(SceneManager.CreateScene(nameof(HomeSceneTests)));
            var scene = SceneManager.GetSceneByPath(HomeScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static IEnumerator LoadHome(System.Action<HomeBootstrap> found)
        {
            yield return SceneManager.LoadSceneAsync(HomeScenePath, LoadSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<HomeBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (
                (bootstrap.Presenter == null || bootstrap.Transition.IsPlaying)
                && Time.realtimeSinceStartup < deadline
            )
                yield return null;
            Assert.That(bootstrap.Presenter, Is.Not.Null);
            Assert.That(bootstrap.Transition.IsCovered, Is.False);
            Canvas.ForceUpdateCanvases();
            found(bootstrap);
        }

        private static IEnumerator WaitForSteps(HomeBootstrap bootstrap)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (
                !bootstrap.Presenter.StepTask.IsCompleted && Time.realtimeSinceStartup < deadline
            )
                yield return null;
            Assert.That(bootstrap.Presenter.StepTask.IsCompleted, Is.True);
            Assert.That(bootstrap.Presenter.StepSyncing, Is.False);
        }

        private static void AssertColor(Color actual, Color expected, Graphic graphic)
        {
            string name = graphic.transform.parent.name + "/" + graphic.name;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), name);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), name);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), name);
        }

        private static void AssertTouchSize(Transform target)
        {
            Assert.That(target, Is.Not.Null);
            var rect = ((RectTransform)target).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(MinimumTouchSize), target.name);
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(MinimumTouchSize), target.name);
        }
    }
}
