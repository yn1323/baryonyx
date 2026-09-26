using System.Collections;
using Baryonyx.App;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // ルーンが弾ける中心を、歩数パネルを押した位置にする。
    public sealed class HomeRuneTapTests : ScenarioInputFixture
    {
        private const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";
        private TestGameServices services;

        [UnityTest]
        public IEnumerator RunesBurstFromWhereThePanelWasTapped()
        {
            var view = default(HomeView);
            yield return LoadHome(value => view = value);
            var panel = (RectTransform)view.StepButton.transform;
            // パネルの右寄り・下寄りを押し、アイコンの位置とはっきり離す。
            Vector2 tap = RectTransformUtility.WorldToScreenPoint(
                null,
                panel.TransformPoint(
                    new Vector3(
                        Mathf.Lerp(panel.rect.xMin, panel.rect.xMax, 0.8f),
                        Mathf.Lerp(panel.rect.yMin, panel.rect.yMax, 0.3f)
                    )
                )
            );
            MoveMouse(tap);
            Assert.That(UnityEngine.InputSystem.Pointer.current, Is.SameAs(Mouse));
            Assert.That(Mouse.position.ReadValue(), Is.EqualTo(tap));

            yield return Tap(view);
            Assert.That(Distance(view.BurstOrigin, LayerPoint(view, tap)), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator RunesBurstFromThePanelIconWhenThePointerIsElsewhere()
        {
            var view = default(HomeView);
            yield return LoadHome(value => view = value);
            MoveMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            yield return Tap(view);
            Vector3 icon = view.RuneEffectLayer.InverseTransformPoint(
                view.RuneOrigin.TransformPoint(view.RuneOrigin.rect.center)
            );
            Assert.That(Distance(view.BurstOrigin, icon), Is.LessThan(1f));
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            SceneManager.SetActiveScene(SceneManager.CreateScene(nameof(HomeRuneTapTests)));
            var scene = SceneManager.GetSceneByPath(HomeScenePath);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        private IEnumerator LoadHome(System.Action<HomeView> found)
        {
            services = TestGameServices.Use();
            // 仮のルーンなら、歩数に関係なく押すたびに獲得できる。
            HomeBootstrap.MockRuneGainOverride = true;
            yield return SceneManager.LoadSceneAsync(HomeScenePath, LoadSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<HomeBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (
                (
                    bootstrap.Presenter == null
                    || bootstrap.Transition.IsPlaying
                    || !bootstrap.Presenter.StepTask.IsCompleted
                )
                && Time.realtimeSinceStartup < deadline
            )
                yield return null;
            Assert.That(bootstrap.Presenter.StepTask.IsCompleted, Is.True);
            Canvas.ForceUpdateCanvases();
            found(bootstrap.View);
        }

        private static IEnumerator Tap(HomeView view)
        {
            view.StepButton.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!view.RuneGainPlaying && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(view.RuneGainPlaying, Is.True);
        }

        // Game viewにフォーカスがないと入力イベントは捨てられるため、状態を直接書き換える。
        private void MoveMouse(Vector2 screen) =>
            UnityEngine.InputSystem.LowLevel.InputState.Change(Mouse.position, screen);

        private static Vector3 LayerPoint(HomeView view, Vector2 screen)
        {
            Assert.That(
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    view.RuneEffectLayer,
                    screen,
                    null,
                    out var local
                ),
                Is.True
            );
            return local;
        }

        private static float Distance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
    }
}
