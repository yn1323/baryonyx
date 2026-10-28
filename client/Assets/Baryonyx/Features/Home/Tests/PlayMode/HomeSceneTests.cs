using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Health;
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
        private const string HomeScenePath = SceneTests.HomePath;
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
            var todayKey = Baryonyx
                .Health.HealthDays.Today()
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var today = days.FirstOrDefault(day => day.day == todayKey && day.hasValue);

            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            yield return WaitForSteps(bootstrap);

            Assert.That(bootstrap.Data, Is.Not.Null);
            Assert.That(bootstrap.AdventureSceneName, Is.EqualTo(SceneNames.Exploration));
            yield return SceneTests.WaitForTask(bootstrap.AdventureTask);

            var snapshot = bootstrap.Data.ToSnapshot(Baryonyx.Health.HealthDays.Today());
            snapshot.StepLink = HomeStepLink.Linked;
            snapshot.Steps = today != null ? (int)today.steps : 0;
            // 所持ルーンは仮データではなくサーバーの残高を表示する。まだ変換していないので0。
            snapshot.Runes = 0;
            var expected = HomeViewState.From(snapshot);
            Assert.That(view.StepsLabel.text, Is.EqualTo(expected.ActText));
            Assert.That(view.ClaimLabel.text, Is.EqualTo("タップでルーン獲得"));
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
                Has.All.Contains("KiwiMaru")
            );
        }

        [UnityTest]
        public IEnumerator EveryControlIsLargeEnoughToTap()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            var view = bootstrap.View;

            foreach (
                var button in new[] { view.TavernButton, view.WorkshopButton, view.TempleButton }
            )
                SceneTests.AssertTouchSize(button.transform);
            SceneTests.AssertTouchSize(view.ResumeButton.transform);
            SceneTests.AssertTouchSize(view.SettingsButton.transform.Find("HitArea"));
            SceneTests.AssertTouchSize(view.DebugRoomsButton.transform.Find("HitArea"));
            // 設定とデバッグルームの丸いボタンは、広げた押せる範囲が重ならない。
            var settingsArea = SceneTests.ScreenRect(view.SettingsButton.transform.Find("HitArea"));
            var roomsArea = SceneTests.ScreenRect(view.DebugRoomsButton.transform.Find("HitArea"));
            Assert.That(settingsArea.Overlaps(roomsArea), Is.False);
            Assert.That(
                ((RectTransform)view.StepButton.transform).rect.height,
                Is.GreaterThanOrEqualTo(SceneTests.MinimumTouchSize)
            );

            // Controls follow the Safe Area.
            Assert.That(
                view.transform.Find("SafeArea").GetComponent<SafeAreaFollower>(),
                Is.Not.Null
            );
            Assert.That(
                view.ResumeButton.transform.IsChildOf(view.transform.Find("SafeArea")),
                Is.True
            );
        }

        [UnityTest]
        public IEnumerator StepPanelTurnsStepsIntoRunesAndShowsTheResult()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            yield return WaitForSteps(bootstrap);
            Assert.That(services.Server.Saves, Is.Zero);

            Assert.That(view.RunesLabel.text, Is.EqualTo("0"));

            // 1回目は保存した歩数がルーンになり、所持数が0から付与後の残高まで増える。
            view.StepButton.onClick.Invoke();
            yield return WaitForSteps(bootstrap);
            Assert.That(services.Server.Saves, Is.EqualTo(1));
            long balance = services
                .Server.ReadRunesAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.That(balance, Is.GreaterThan(0));
            Assert.That(view.RuneGainPlaying, Is.True);
            Assert.That(view.RunesLabel.text, Is.EqualTo("0"));
            yield return SceneTests.WaitUntil(() => !view.RuneGainPlaying);
            Assert.That(view.RuneGainPlaying, Is.False);
            Assert.That(view.RunesLabel.text, Is.EqualTo(HomeViewState.Runes(balance)));
            // キラキラとモヤは寿命で消えるため、飛ぶルーンと光が片付いたことだけを確かめる。
            var sparkles = view.RuneSparkles.transform;
            var haze = view.RuneHaze.transform;
            Assert.That(
                view.RuneEffectLayer.GetComponentsInChildren<Image>()
                    .Where(image =>
                        !image.transform.IsChildOf(sparkles) && !image.transform.IsChildOf(haze)
                    ),
                Is.Empty
            );
            Assert.That(view.CurrentToast, Is.Empty);

            // 同じ歩数のままでは付与されず、画面中央で知らせる。
            view.StepButton.onClick.Invoke();
            yield return WaitForSteps(bootstrap);
            Assert.That(view.RuneGainPlaying, Is.False);
            Assert.That(view.CurrentNotice, Is.EqualTo(HomePresenter.NoRunesMessage));
            Assert.That(view.RunesLabel.text, Is.EqualTo(HomeViewState.Runes(balance)));

            services.Server.Fail = true;
            view.StepButton.onClick.Invoke();
            yield return WaitForSteps(bootstrap);
            Assert.That(view.CurrentToast, Is.EqualTo("歩数を取得できませんでした"));
            Assert.That(view.ClaimLabel.text, Is.EqualTo("タップでルーン獲得"));
            Assert.That(view.RunesLabel.text, Is.EqualTo(HomeViewState.Runes(balance)));
        }

        [UnityTest]
        public IEnumerator UnlinkedStepPanelRequestsPermissionBeforeSyncing()
        {
            services.Provider.Permission = HealthPermission.NotGranted;
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
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
        public IEnumerator MockButtonsShowFeedbackAndStayOnHome()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            int loads = 0;
            void Count(Scene scene, LoadSceneMode _) => loads++;

            SceneManager.sceneLoaded += Count;
            try
            {
                view.SettingsButton.onClick.Invoke();
                Assert.That(view.CurrentToast, Is.EqualTo("設定（準備中）"));
                Assert.That(bootstrap.Presenter.AdventureStarted, Is.False);
                Assert.That(bootstrap.Presenter.ScreenOpened, Is.False);
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

        // 冒険していないとき、右下のカードは冒険の世界地図を開く。
        [UnityTest]
        public IEnumerator CardOpensTheWorldMapWithNoAdventure()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            yield return SceneTests.WaitForTask(bootstrap.AdventureTask);
            var view = bootstrap.View;
            Assert.That(view.DestinationNameLabel.text, Is.Empty);
            Assert.That(view.DestinationFloorLabel.text, Is.EqualTo(HomeViewState.TravelName));
            Assert.That(view.ResumeLabel.text, Is.EqualTo("出発"));
            Assert.That(view.DestinationArt.texture, Is.SameAs(view.TravelArt));

            // 続けて押しても、シーンの読み込みは1回だけ始める。
            view.ResumeButton.onClick.Invoke();
            view.ResumeButton.onClick.Invoke();
            Assert.That(bootstrap.Presenter.AdventureStarted, Is.True);
            Assert.That(bootstrap.Transition.IsPlaying, Is.True);

            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.World,
                message: "The world map did not open."
            );
            Assert.That(Object.FindAnyObjectByType<HomeBootstrap>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<WorldBootstrap>(), Is.Not.Null);
        }

        // 冒険の途中は、右下のカードに行き先と階を出し、探索を再開する。
        [UnityTest]
        public IEnumerator CardResumesTheAdventureInProgress()
        {
            var started = services
                .Adventure.StartAsync(AdventureLocalSource.ForestRuins, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            services
                .Adventure.MoveAsync(started.Run.Exits[0], CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            yield return SceneTests.WaitForTask(bootstrap.AdventureTask);
            var view = bootstrap.View;
            Assert.That(view.DestinationNameLabel.text, Is.EqualTo("ミストラ遺跡"));
            Assert.That(view.DestinationFloorLabel.text, Is.EqualTo("第2層"));
            Assert.That(view.ResumeLabel.text, Is.EqualTo("再開"));
            Assert.That(view.DestinationArt.texture, Is.SameAs(view.ResumeArt));

            view.ResumeButton.onClick.Invoke();
            Assert.That(bootstrap.Presenter.AdventureStarted, Is.True);
            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.Exploration,
                message: "The exploration did not open."
            );
            Assert.That(Object.FindAnyObjectByType<HomeBootstrap>(), Is.Null);
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(HomeSceneTests));
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
    }
}
