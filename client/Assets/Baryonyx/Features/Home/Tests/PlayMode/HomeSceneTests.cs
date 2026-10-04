using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using Baryonyx.Adventure;
using Baryonyx.App;
using Baryonyx.Health;
using Baryonyx.Home;
using Baryonyx.UI;
using Baryonyx.Vfx.Hd2d;
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
                Has.All.Contains("DotGothic16")
            );
        }

        [UnityTest]
        public IEnumerator ThePartyAndTheCampfireStandInTheSunlitGlade()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            yield return null;
            yield return null;
            var view = bootstrap.View;

            var stageCamera = Hd2dStageCamera.Active;
            Assert.That(stageCamera, Is.Not.Null, "Home stands in the 3D forest glade.");
            Assert.That(stageCamera.Camera.orthographic, Is.False);
            Assert.That(stageCamera.KeyLight, Is.Not.Null);
            Assert.That(
                stageCamera.KeyLight.type,
                Is.EqualTo(LightType.Directional),
                "The sun casts the members' shadows."
            );
            Assert.That(
                stageCamera.KeyLight.cookie,
                Is.Not.Null,
                "The leaves dapple the sunlight."
            );
            Assert.That(stageCamera.SwayRadius, Is.GreaterThan(0f), "The camera drifts idly.");
            Assert.That(
                view.transform.Find("Background").gameObject.activeInHierarchy,
                Is.False,
                "The 3D glade takes the painted background's place."
            );

            var boards = view.GetComponentsInChildren<Hd2dUiBillboard>(true);
            Assert.That(
                boards.Select(board => board.name),
                Is.EquivalentTo(new[] { "Toma", "Luka", "Aria", "Mina", "Campfire" })
            );
            foreach (var board in boards)
            {
                Assert.That(board.Staged, Is.True, board.name);
                Assert.That(board.Picture.canvasRenderer.cull, Is.True, board.name);
                Assert.That(board.VisualRenderer.enabled, Is.True, board.name);
                Assert.That(board.FootShadow.canvasRenderer.cull, Is.True, board.name);
            }
            // The logs stand in their drawn colours; the drawn flame gives way to a computed one
            // burning in them, with its own embers.
            var fire = boards.Single(board => board.name == "Campfire");
            Assert.That(fire.Lit, Is.False, "The logs keep their drawn colours.");
            Assert.That(fire.CastShadow, Is.False);
            var drawnFlame = view.transform.Find("World/CampfireFlame").GetComponent<Graphic>();
            Assert.That(fire.HideWhenStaged, Does.Contain(drawnFlame));
            Assert.That(drawnFlame.canvasRenderer.cull, Is.True);
            Assert.That(
                view.transform.Find("World/CampEmbers").gameObject.activeInHierarchy,
                Is.False,
                "The 2D embers belong to the drawn flame."
            );

            var campfire = bootstrap
                .gameObject.scene.GetRootGameObjects()
                .Single(root => root.name == "Campfire")
                .transform;
            var flame = campfire.Find("CampfireFlame").GetComponent<MeshRenderer>();
            Assert.That(flame.sharedMaterial.shader.name, Is.EqualTo("Baryonyx/HD2D/Flame"));
            var logs = fire.ContactRenderer.bounds.center;
            var flameRoot = flame.transform.position;
            Assert.That(
                Vector2.Distance(
                    new Vector2(flameRoot.x, flameRoot.z),
                    new Vector2(logs.x, logs.z)
                ),
                Is.LessThan(0.2f),
                "The flame burns in the logs."
            );
            Assert.That(
                flame.sortingOrder,
                Is.LessThan(fire.VisualRenderer.sortingOrder),
                "The logs hide the base of the flame."
            );
            Assert.That(
                campfire.Find("CampfireEmbers").GetComponent<ParticleSystem>().isPlaying,
                Is.True
            );
            Assert.That(campfire.Find("CampfireLight").GetComponent<Light>(), Is.Not.Null);
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
            Assert.That(
                ((RectTransform)view.StepButton.transform).rect.height,
                Is.GreaterThanOrEqualTo(SceneTests.MinimumTouchSize)
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
            yield return null;
            Assert.That(
                view.RuneEffectLayer.GetComponentsInChildren<Image>().Length,
                Is.GreaterThan(0),
                "Runes fly toward the balance."
            );
            // 着いたルーンは所持ルーンのアイコンでキラキラを弾く。
            var sparkles = view.RuneSparkles.transform;
            bool sparkled = false;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (view.RuneGainPlaying && Time.realtimeSinceStartup < deadline)
            {
                sparkled |= sparkles.GetComponentsInChildren<Image>().Any(image => image.enabled);
                yield return null;
            }
            Assert.That(view.RuneGainPlaying, Is.False);
            Assert.That(sparkled, Is.True, "Sparkles pop as runes land.");
            Assert.That(view.RunesLabel.text, Is.EqualTo(HomeViewState.Runes(balance)));
            // キラキラとモヤは寿命で消えるため、飛ぶルーンと光が片付いたことだけを確かめる。
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
        public IEnumerator PressingDarkensIconsAndLabelsOverDarkBackdrops()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            var view = bootstrap.View;
            Assert.That(EventSystem.current, Is.Not.Null);

            var buttons = new[]
            {
                view.TavernButton,
                view.WorkshopButton,
                view.TempleButton,
                view.SettingsButton,
                view.StepButton,
            };
            foreach (var button in buttons)
            {
                var tint = button as TintGroupButton;
                Assert.That(tint, Is.Not.Null, button.name);
                // ACTパネルは案内の文字だけで、アイコンを持たない。
                if (button != view.StepButton)
                    Assert.That(
                        tint.TintGraphics.OfType<Image>().Any(image => image.sprite != null),
                        Is.True,
                        button.name + " has no icon to darken"
                    );
            }

            // ボタンは押された状態をそれぞれ持つため、まとめて押して色の変化を待つ。
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            float fade = buttons[0].colors.fadeDuration + 0.1f;
            foreach (var button in buttons)
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(fade);
            foreach (var button in buttons)
            foreach (var graphic in ((TintGroupButton)button).TintGraphics)
                AssertColor(graphic.canvasRenderer.GetColor(), button.colors.pressedColor, graphic);

            foreach (var button in buttons)
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(fade);
            foreach (var button in buttons)
            foreach (var graphic in ((TintGroupButton)button).TintGraphics)
                AssertColor(graphic.canvasRenderer.GetColor(), button.colors.normalColor, graphic);

            // The resume card already darkens its bright art and keeps that behaviour.
            Assert.That(view.ResumeButton, Is.Not.InstanceOf<TintGroupButton>());
        }

        // 設定は準備中を知らせ、シーンを移らない。
        // 編成・商会・神殿・旅の案内所は案内人の画面を開く（GuideScenesTestsで検査する）。
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

        // 冒険していないとき、右下のカードは旅の案内所を開く。
        [UnityTest]
        public IEnumerator CardOpensTheTravelOfficeWithNoAdventure()
        {
            var bootstrap = default(HomeBootstrap);
            yield return SceneTests.LoadHome(value => bootstrap = value);
            yield return SceneTests.WaitForTask(bootstrap.AdventureTask);
            var view = bootstrap.View;
            Assert.That(view.DestinationNameLabel.text, Is.EqualTo(HomeViewState.TravelTitle));
            Assert.That(view.DestinationFloorLabel.text, Is.EqualTo(HomeViewState.TravelName));
            Assert.That(view.ResumeLabel.text, Is.EqualTo("出発"));
            Assert.That(view.DestinationArt.texture, Is.SameAs(view.TravelArt));

            // 続けて押しても、シーンの読み込みは1回だけ始める。
            view.ResumeButton.onClick.Invoke();
            view.ResumeButton.onClick.Invoke();
            Assert.That(bootstrap.Presenter.AdventureStarted, Is.True);
            Assert.That(bootstrap.Transition.IsPlaying, Is.True);

            yield return SceneTests.WaitUntil(
                () => SceneManager.GetActiveScene().name == SceneNames.TravelOffice,
                message: "The travel office did not open."
            );
            Assert.That(Object.FindAnyObjectByType<HomeBootstrap>(), Is.Null);
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
            Assert.That(view.DestinationFloorLabel.text, Is.EqualTo("B2F"));
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

        private static void AssertColor(Color actual, Color expected, Graphic graphic)
        {
            string name = graphic.transform.parent.name + "/" + graphic.name;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), name);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), name);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), name);
        }
    }
}
