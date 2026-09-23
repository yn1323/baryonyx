using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Home;
using Baryonyx.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class HomeSceneTests
    {
        private const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private const string MainScenePath = "Assets/Baryonyx/App/Scenes/Main.unity";
        private const float MinimumTouchSize = 128f;

        [UnityTest]
        public IEnumerator HomeShowsMockDataWithoutReadingHealthData()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;

            Assert.That(bootstrap.Data, Is.Not.Null);
            Assert.That(bootstrap.AdventureSceneName, Is.EqualTo("Main"));
            Assert.That(
                bootstrap.Transition.EnterSettings.Type,
                Is.EqualTo(SceneTransitionType.Shutter)
            );
            Assert.That(
                Object.FindObjectsByType<WireframeBootstrap>(FindObjectsSortMode.None),
                Is.Empty
            );
            Assert.That(
                Object.FindObjectsByType<HealthScreenBootstrap>(FindObjectsSortMode.None),
                Is.Empty
            );

            var expected = HomeViewState.From(bootstrap.Data.ToSnapshot(System.DateTime.Today));
            Assert.That(view.StepsLabel.text, Is.EqualTo(expected.StepsText));
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
            view.StepButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("ルーンを取得しました（モック）"));
            view.SettingsButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("設定（準備中）"));
            view.WorldMapButton.onClick.Invoke();
            Assert.That(view.CurrentToast, Is.EqualTo("ワールドマップ（準備中）"));
            Assert.That(bootstrap.Presenter.AdventureStarted, Is.False);
        }

        [UnityTest]
        public IEnumerator RepeatedResumeTapsLoadTheWireframeOnce()
        {
            var bootstrap = default(HomeBootstrap);
            yield return LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            int mainLoads = 0;
            void Count(Scene scene, LoadSceneMode _)
            {
                if (scene.path == MainScenePath)
                    mainLoads++;
            }

            SceneManager.sceneLoaded += Count;
            try
            {
                view.ResumeButton.onClick.Invoke();
                view.ResumeButton.onClick.Invoke();
                view.WorldMapButton.onClick.Invoke();
                Assert.That(bootstrap.Presenter.AdventureStarted, Is.True);

                float deadline =
                    Time.realtimeSinceStartup
                    + bootstrap.Transition.DefaultSettings.CoverDuration
                    + 3f;
                while (
                    !SceneManager.GetSceneByPath(MainScenePath).isLoaded
                    && Time.realtimeSinceStartup < deadline
                )
                    yield return null;
                yield return null;
            }
            finally
            {
                SceneManager.sceneLoaded -= Count;
            }

            Assert.That(SceneManager.GetSceneByPath(MainScenePath).isLoaded, Is.True);
            Assert.That(mainLoads, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene(nameof(HomeSceneTests)));
            foreach (var path in new[] { HomeScenePath, MainScenePath })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
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

        private static void AssertTouchSize(Transform target)
        {
            Assert.That(target, Is.Not.Null);
            var rect = ((RectTransform)target).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(MinimumTouchSize), target.name);
            Assert.That(rect.height, Is.GreaterThanOrEqualTo(MinimumTouchSize), target.name);
        }
    }
}
