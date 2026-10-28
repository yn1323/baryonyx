using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baryonyx.Tests.PlayMode
{
    // 実際のシーンを読み込むPlayModeテストの共通処理。
    internal static class SceneTests
    {
        public const string ScenesFolder = "Assets/Baryonyx/App/Scenes";
        public const string TopPath = ScenesFolder + "/Top.unity";
        public const string HomePath = ScenesFolder + "/Home.unity";
        public const string ExplorationPath = ScenesFolder + "/Exploration.unity";
        public const string BattlePath = ScenesFolder + "/Battle.unity";
        public const string WorldMapPath = ScenesFolder + "/WorldMap.unity";
        public const string WorldPath = ScenesFolder + "/World.unity";

        // UI設計ルールで決めた、指で押せる大きさの下限（1920x1080の設計の単位）。
        public const float MinimumTouchSize = 128f;

        public static string GuidePath(string scene) => $"{ScenesFolder}/Guide/{scene}.unity";

        // 条件を満たすまでフレームを進める。期限を過ぎたら失敗にする。
        public static IEnumerator WaitUntil(
            Func<bool> condition,
            float seconds = 3f,
            string message = "Timed out."
        )
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, message);
        }

        // 非同期の処理（読み込み・保存）が終わるまでフレームを進める。失敗したら例外を出す。
        public static IEnumerator WaitForTask(
            System.Threading.Tasks.Task task,
            float seconds = 5f,
            string message = "The task did not finish."
        )
        {
            yield return WaitUntil(() => task == null || task.IsCompleted, seconds, message);
            if (task != null && task.IsFaulted)
                throw task.Exception.InnerException ?? task.Exception;
        }

        // シーンをSingleで読み込み、起動処理と開く演出が終わるまで待つ。
        public static IEnumerator Load<T>(string path, Func<T, bool> ready, Action<T> found)
            where T : Object
        {
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            var bootstrap = Object.FindAnyObjectByType<T>();
            Assert.That(bootstrap, Is.Not.Null, typeof(T).Name);
            yield return WaitUntil(() => ready(bootstrap), message: path + " did not open.");
            Canvas.ForceUpdateCanvases();
            found(bootstrap);
        }

        public static IEnumerator LoadHome(Action<HomeBootstrap> found) =>
            Load<HomeBootstrap>(HomePath, HomeReady, found);

        public static IEnumerator LoadGuide(string scene, Action<GuideSceneBootstrap> found) =>
            Load<GuideSceneBootstrap>(GuidePath(scene), GuideReady, found);

        public static IEnumerator LoadWorldMap(Action<WorldMapBootstrap> found) =>
            Load<WorldMapBootstrap>(WorldMapPath, WorldMapReady, found);

        public static bool WorldMapReady(WorldMapBootstrap map) =>
            map.View != null
            && map.View.Loaded
            && !map.Transition.IsPlaying
            && !map.Transition.IsCovered;

        public static IEnumerator LoadWorld(Action<WorldBootstrap> found) =>
            Load<WorldBootstrap>(WorldPath, WorldReady, found);

        public static bool WorldReady(WorldBootstrap world) =>
            world.Navigator.Current != null
            && !world.Navigator.Switching
            && !world.Transition.IsPlaying
            && !world.Transition.IsCovered;

        public static bool HomeReady(HomeBootstrap home) =>
            home.Presenter != null && !home.Transition.IsPlaying;

        public static bool GuideReady(GuideSceneBootstrap guide) =>
            guide.Presenter != null && !guide.Transition.IsPlaying;

        // Singleで読み込んだ最後のシーンは直接閉じられないため、空のシーンへ切り替えてから閉じる。
        // EventSystemなどを後続のテストへ残さない。
        public static IEnumerator UnloadAll(string emptySceneName)
        {
            var empty = SceneManager.CreateScene(emptySceneName);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        public static void AssertTouchSize(Transform target, float heightRatio = 1f)
        {
            Assert.That(target, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var rect = ((RectTransform)target).rect;
            Assert.That(rect.width, Is.GreaterThanOrEqualTo(MinimumTouchSize), target.name);
            Assert.That(
                rect.height,
                Is.GreaterThanOrEqualTo(MinimumTouchSize * heightRatio),
                target.name
            );
        }

        /// <summary>Nothing lies over the button's middle to take its tap.</summary>
        public static void AssertTappable(Scene scene, Button button)
        {
            var events = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .Single();
            var data = new PointerEventData(events)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    ((RectTransform)button.transform).TransformPoint(
                        ((RectTransform)button.transform).rect.center
                    )
                ),
            };
            var hits = new List<RaycastResult>();
            events.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
        }

        /// <summary>The screen rectangle of a UI element, after the canvases are laid out.</summary>
        public static Rect ScreenRect(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // lower が upper より下にあり、重ならない（0.5pxまでの誤差は許す）。
        public static void AssertBelow(Transform lower, Transform upper) =>
            Assert.That(
                ScreenRect(lower).yMax,
                Is.LessThanOrEqualTo(ScreenRect(upper).yMin + 0.5f),
                lower.name
            );
    }
}
