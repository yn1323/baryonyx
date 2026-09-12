using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Health;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class StartupSceneTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator StartupSceneLoadsWithAnActiveCamera()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            yield return null;

            var cameras = loadedScene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>());
            Assert.That(
                cameras.Any(camera => camera.isActiveAndEnabled),
                Is.True,
                "The startup scene needs an active camera to render the startup scene."
            );
            var roots = loadedScene.GetRootGameObjects();
            var bootstrap = roots
                .SelectMany(root => root.GetComponentsInChildren<HealthScreenBootstrap>())
                .Single();
            Assert.That(bootstrap.Screen, Is.Not.Null);
            Assert.That(bootstrap.Settings, Is.Not.Null);
            Assert.That(bootstrap.Screen.DayButtons.Length, Is.EqualTo(7));
            Assert.That(
                roots.SelectMany(root => root.GetComponentsInChildren<HealthClient>()),
                Is.Empty,
                "The local preview must not initialize server synchronization."
            );
            Assert.That(
                roots
                    .SelectMany(root =>
                        root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()
                    )
                    .Count(),
                Is.EqualTo(1)
            );
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
