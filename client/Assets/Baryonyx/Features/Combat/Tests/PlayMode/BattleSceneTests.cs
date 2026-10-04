using System.Collections;
using System.Linq;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baryonyx.Tests.PlayMode
{
    public sealed class BattleSceneTests
    {
        private const string ScenePath = "Assets/Baryonyx/App/Scenes/Battle.unity";
        private Scene loadedScene;

        [UnityTest]
        public IEnumerator TheBattleCanBeMovedToAnotherBackgroundWhilePlaying()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            loadedScene = SceneManager.GetSceneByPath(ScenePath);
            Assert.That(loadedScene.isLoaded, Is.True);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(loadedScene);
            try
            {
                yield return null;
                var selector = loadedScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BattleStageSelector>(true))
                    .Single();
                var view = loadedScene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BattleInspectView>(true))
                    .Single();
                Assert.That(view.IdleActors, Has.Length.EqualTo(7), "The battle is set up.");
                Assert.That(selector.Shown.Root.activeInHierarchy, Is.True);

                selector.Stage = BattleStage.SnowField;
                selector.Apply();
                yield return null;
                var snow = selector.Find(BattleStage.SnowField);
                Assert.That(snow.Root.activeInHierarchy, Is.True);
                Assert.That(
                    selector.Options.Count(option => option.Root.activeInHierarchy),
                    Is.EqualTo(1)
                );
                Assert.That(RenderSettings.fogColor, Is.EqualTo(snow.Fog), "The snow's air.");
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(snow.AmbientSky));
                Assert.That(selector.StageCamera.KeyLight, Is.SameAs(snow.KeyLight));
                Assert.That(
                    snow.Root.GetComponentsInChildren<ParticleSystem>().Any(p => p.isPlaying),
                    Is.True,
                    "The snow falls once its stage is on."
                );
            }
            finally
            {
                if (previous.IsValid())
                    SceneManager.SetActiveScene(previous);
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(loadedScene);
        }
    }
}
