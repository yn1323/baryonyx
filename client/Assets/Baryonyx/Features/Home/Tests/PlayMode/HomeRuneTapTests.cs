using System.Collections;
using System.Linq;
using System.Threading;
using Baryonyx.App;
using Baryonyx.Home;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    // ACTパネルを押してルーンを獲得する演出を、サーバーの代役と仮想の入力で確かめる。
    // ルーンは押した位置から弾け、所持ルーンのアイコンへ吸い込まれる。
    // 各テストは新しい代役で始まるため、1回目の押下で保存した7日分のACTがルーンになる。
    public sealed class HomeRuneTapTests : ScenarioInputFixture
    {
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
        public IEnumerator RunesBurstFromTheClaimHintWhenThePointerIsElsewhere()
        {
            var view = default(HomeView);
            yield return LoadHome(value => view = value);
            MoveMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            yield return Tap(view);
            Vector3 hint = view.RuneEffectLayer.InverseTransformPoint(
                view.RuneOrigin.TransformPoint(view.RuneOrigin.rect.center)
            );
            Assert.That(Distance(view.BurstOrigin, hint), Is.LessThan(1f));
        }

        // 同期したACTが1ACT＝1ルーンで付与され、サーバーの残高に保存される。
        // 所持ルーンのアイコンも所持数の文字と一緒に大きくなり、文字に重ならず、終わると戻る。
        [UnityTest]
        public IEnumerator SyncedActBecomesRunesAndGrowsTheIcon()
        {
            var view = default(HomeView);
            yield return LoadHome(value => view = value);
            Assert.That(view.RunesLabel.text, Is.EqualTo("0"));
            Vector2 restPivot = view.RuneTarget.pivot;

            yield return Tap(view);
            long act = services
                .Server.ReadAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Where(day => day.hasValue)
                .Sum(day => day.steps);
            Assert.That(act, Is.GreaterThan(0));
            Assert.That(view.GainLabel.text, Is.EqualTo("+" + HomeViewState.Runes(act)));
            var icon = new Vector3[4];
            var label = new Vector3[4];
            float largest = 1f;
            while (view.RuneGainPlaying)
            {
                largest = Mathf.Max(largest, view.RuneTarget.localScale.x);
                view.RuneTarget.GetWorldCorners(icon);
                view.RunesLabel.rectTransform.GetWorldCorners(label);
                Assert.That(icon[2].x, Is.LessThanOrEqualTo(label[0].x + 2f));
                yield return null;
            }

            Assert.That(largest, Is.GreaterThan(1.35f));
            Assert.That(view.RuneTarget.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.RuneTarget.pivot, Is.EqualTo(restPivot));
            Assert.That(view.RunesLabel.text, Is.EqualTo(HomeViewState.Runes(act)));
            Assert.That(
                services.Server.ReadRunesAsync(CancellationToken.None).GetAwaiter().GetResult(),
                Is.EqualTo(act),
                "The server keeps the granted runes."
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScenes()
        {
            services?.Dispose();
            services = null;
            yield return SceneTests.UnloadAll(nameof(HomeRuneTapTests));
        }

        private IEnumerator LoadHome(System.Action<HomeView> found)
        {
            services = TestGameServices.Use();
            yield return SceneTests.Load<HomeBootstrap>(
                SceneTests.HomePath,
                home => SceneTests.HomeReady(home) && home.Presenter.StepTask.IsCompleted,
                home => found(home.View)
            );
        }

        private static IEnumerator Tap(HomeView view)
        {
            view.StepButton.onClick.Invoke();
            yield return SceneTests.WaitUntil(() => view.RuneGainPlaying);
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
