using System;
using System.Collections;
using Baryonyx.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Baryonyx.Tests.PlayMode
{
    // 実際のシーンを読み込むPlayModeテストの共通処理。
    internal static class SceneTests
    {
        public const string ScenesFolder = "Assets/Baryonyx/App/Scenes";
        public const string TopPath = ScenesFolder + "/Top.unity";
        public const string HomePath = ScenesFolder + "/Home.unity";

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
    }
}
