using System.Collections;
using System.Linq;
using Baryonyx.App;
using Baryonyx.Wireframe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class StartupSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Main.unity";
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
                .SelectMany(root => root.GetComponentsInChildren<WireframeBootstrap>())
                .Single();
            Assert.That(bootstrap.View, Is.Not.Null);
            Assert.That(bootstrap.Data, Is.Not.Null);
            Assert.That(bootstrap.HealthSettings, Is.Not.Null);
            yield return null;
            Assert.That(bootstrap.View.Session, Is.Not.Null);
            Assert.That(bootstrap.View.Session.Screen, Is.EqualTo(WireScreen.Home));
            Assert.That(bootstrap.View.Session.Popup, Is.EqualTo(WirePopup.None));
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
