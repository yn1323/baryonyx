using Baryonyx.Editor.CI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BuildSceneTests
    {
        private const string MainScene = "Assets/Baryonyx/App/Scenes/Main.unity";

        [Test]
        public void DisabledScenesAreExcluded()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Baryonyx/App/Scenes/Missing.unity", false),
                new EditorBuildSettingsScene(MainScene, true),
            };
            Assert.That(BuildScenes.GetEnabledScenePaths(scenes), Is.EqualTo(new[] { MainScene }));
        }

        [Test]
        public void NoEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                BuildScenes.GetEnabledScenePaths(
                    new[] { new EditorBuildSettingsScene(MainScene, false) }
                )
            );

        [Test]
        public void MissingEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                BuildScenes.GetEnabledScenePaths(
                    new[] { new EditorBuildSettingsScene("Assets/Missing.unity", true) }
                )
            );

        [Test]
        public void DuplicateEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                BuildScenes.GetEnabledScenePaths(
                    new[]
                    {
                        new EditorBuildSettingsScene(MainScene, true),
                        new EditorBuildSettingsScene(MainScene, true),
                    }
                )
            );

        [Test]
        public void ProjectHasValidEnabledScenes() =>
            Assert.That(BuildScenes.GetEnabledScenePaths(EditorBuildSettings.scenes), Is.Not.Empty);
    }
}
