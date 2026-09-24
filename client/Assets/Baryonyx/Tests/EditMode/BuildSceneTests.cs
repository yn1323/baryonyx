using Baryonyx.Editor.CI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BuildSceneTests
    {
        private const string TopScene = "Assets/Baryonyx/App/Scenes/Top.unity";

        [Test]
        public void DisabledScenesAreExcluded()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Baryonyx/App/Scenes/Missing.unity", false),
                new EditorBuildSettingsScene(TopScene, true),
            };
            Assert.That(BuildScenes.GetEnabledScenePaths(scenes), Is.EqualTo(new[] { TopScene }));
        }

        [Test]
        public void NoEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                BuildScenes.GetEnabledScenePaths(
                    new[] { new EditorBuildSettingsScene(TopScene, false) }
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
                        new EditorBuildSettingsScene(TopScene, true),
                        new EditorBuildSettingsScene(TopScene, true),
                    }
                )
            );

        [Test]
        public void ProjectHasValidEnabledScenes() =>
            Assert.That(BuildScenes.GetEnabledScenePaths(EditorBuildSettings.scenes), Is.Not.Empty);
    }
}
