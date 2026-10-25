using System.Linq;
using Baryonyx.App.Editor;
using Baryonyx.TempLab;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Baryonyx.Tests.EditMode
{
    public sealed class TempLabSceneTests
    {
        // 試作は TempLabRoom から舞台のカメラ・舞台・Canvasをたどる。展示室から開くため、ビルド対象にも入れる。
        [Test]
        public void TheRoomReachesTheStageAndTheCanvas()
        {
            Assert.That(
                EditorBuildSettings.scenes.Any(scene =>
                    scene.path == TempLabSceneSetup.ScenePath && scene.enabled
                ),
                Is.True
            );
            var opened = EditorSceneManager.OpenScene(
                TempLabSceneSetup.ScenePath,
                OpenSceneMode.Additive
            );
            try
            {
                var room = opened
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TempLabRoom>(true))
                    .Single();
                Assert.That(room.StageCamera, Is.Not.Null);
                Assert.That(room.StageCamera.gameObject.scene, Is.EqualTo(opened));
                Assert.That(room.Stage, Is.Not.Null);
                Assert.That(room.Stage.gameObject.scene, Is.EqualTo(opened));
                Assert.That(room.Sandbox, Is.Not.Null);
                Assert.That(room.Sandbox.IsChildOf(room.transform), Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(opened, true);
            }
        }
    }
}
