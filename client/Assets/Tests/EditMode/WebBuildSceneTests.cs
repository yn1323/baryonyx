using Baryonyx.Editor.CI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Baryonyx.Tests.EditMode
{
    public sealed class WebBuildSceneTests
    {
        private const string SampleScene = "Assets/Scenes/SampleScene.unity";

        [Test]
        public void DisabledScenesAreExcluded()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Missing.unity", false),
                new EditorBuildSettingsScene(SampleScene, true),
            };
            Assert.That(WebBuild.GetEnabledScenePaths(scenes), Is.EqualTo(new[] { SampleScene }));
        }

        [Test]
        public void NoEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                WebBuild.GetEnabledScenePaths(
                    new[] { new EditorBuildSettingsScene(SampleScene, false) }
                )
            );

        [Test]
        public void MissingEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                WebBuild.GetEnabledScenePaths(
                    new[] { new EditorBuildSettingsScene("Assets/Missing.unity", true) }
                )
            );

        [Test]
        public void DuplicateEnabledSceneIsRejected() =>
            Assert.Throws<BuildFailedException>(() =>
                WebBuild.GetEnabledScenePaths(
                    new[]
                    {
                        new EditorBuildSettingsScene(SampleScene, true),
                        new EditorBuildSettingsScene(SampleScene, true),
                    }
                )
            );

        [Test]
        public void ProjectHasValidEnabledScenes() =>
            Assert.That(WebBuild.GetEnabledScenePaths(EditorBuildSettings.scenes), Is.Not.Empty);
    }
}
