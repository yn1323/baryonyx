using System.Collections;
using System.Linq;
using Baryonyx.Showcase;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class ShowcaseSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Showcase.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator ShowcaseSceneLoadsWithCameraAndBuildsPreviewUi()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;

            var roots = loadedScene.GetRootGameObjects();
            var bootstrap = roots.SelectMany(root => root.GetComponentsInChildren<ShowcaseBootstrap>())
                .Single();
            Assert.That(bootstrap.Catalog, Is.Not.Null);
            Assert.That(
                roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Any(camera => camera.isActiveAndEnabled),
                Is.True,
                "The showcase scene needs an active camera to render the Game view."
            );

            yield return null;

            var canvas = Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .SingleOrDefault(candidate =>
                    candidate.name == "ShowcaseCanvas" && candidate.gameObject.scene == loadedScene
                );
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(canvas.GetComponentsInChildren<Button>(true), Is.Not.Empty);
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
