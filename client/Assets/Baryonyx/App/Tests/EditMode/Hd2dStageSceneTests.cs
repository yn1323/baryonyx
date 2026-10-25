using System.Linq;
using Baryonyx.Stages.Editor;
using Baryonyx.Vfx.Hd2d;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Baryonyx.Tests.EditMode
{
    public sealed class Hd2dStageSceneTests
    {
        private const string HomeScenePath = "Assets/Baryonyx/App/Scenes/Home.unity";

        [Test]
        public void HomeShowsThePartyInTheGladeWhileTheSceneIsEdited()
        {
            var scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
            try
            {
                var stageCamera = Hd2dStageCamera.Active;
                Assert.That(stageCamera, Is.Not.Null, "The stage camera runs outside Play Mode.");
                Assert.That(stageCamera.gameObject.scene, Is.EqualTo(scene));

                var roots = scene.GetRootGameObjects();
                var boards = roots
                    .SelectMany(root => root.GetComponentsInChildren<Hd2dUiBillboard>(true))
                    .ToArray();
                Assert.That(boards, Is.Not.Empty);
                foreach (var board in boards)
                {
                    Assert.That(board.Staged, Is.True, board.name);
                    Assert.That(board.Picture.canvasRenderer.cull, Is.True, board.name);
                    Assert.That(
                        board.VisualRenderer.gameObject.hideFlags & HideFlags.DontSave,
                        Is.EqualTo(HideFlags.DontSave),
                        "The boards made outside Play Mode are never saved."
                    );
                }

                foreach (
                    var part in roots.SelectMany(root =>
                        root.GetComponentsInChildren<Hd2dFlatOnly>(true)
                    )
                )
                {
                    Assert.That(
                        part.gameObject.activeSelf,
                        Is.True,
                        "Outside Play Mode the 2D part keeps its saved state."
                    );
                    // The mist, lights and embers build their pictures later, some under a mask;
                    // a canvas group that is never saved fades them all out.
                    var group = part.GetComponents<CanvasGroup>()
                        .SingleOrDefault(candidate =>
                            (candidate.hideFlags & HideFlags.DontSaveInEditor) != 0
                        );
                    Assert.That(group, Is.Not.Null, part.name);
                    Assert.That(group.alpha, Is.EqualTo(0f), part.name);
                }
                var screen = roots.Single(root => root.name == "HomeScreen");
                Assert.That(
                    PrefabUtility.GetAddedComponents(screen),
                    Is.Empty,
                    "Nothing is added to the screen's prefab."
                );

                // The embers and motes run while the scene is edited, as they do in Play Mode.
                var particles = roots
                    .SelectMany(root => root.GetComponentsInChildren<ParticleSystem>(true))
                    .ToArray();
                Assert.That(particles, Is.Not.Empty);
                Assert.That(particles.Sum(system => system.particleCount), Is.GreaterThan(0));
                Assert.That(scene.isDirty, Is.False, "Showing the stage changes nothing to save.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheBattlefieldIsDrawnByTheSceneCameraWhileTheSceneIsEdited()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Baryonyx/App/Scenes/Debug/BattleLab.unity",
                OpenSceneMode.Additive
            );
            try
            {
                var stage = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .Single(canvas => canvas.name == "StageCanvas");
                Assert.That(stage.worldCamera, Is.Not.Null, "Set in the scene, not on Play.");
                Assert.That(stage.worldCamera.gameObject.scene, Is.EqualTo(scene));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // The canvas height of the front row's feet: Home's party stands at 24-28% of the
        // screen's height, the battle's front row at 30%.
        [TestCase(StageSetAssets.ForestGladeLookPath, "Home", 260f)]
        [TestCase(StageSetAssets.DuskHighlandLookPath, "Battle", 324f)]
        public void TheFrontRowsFeetAreInFocus(string path, string viewName, float feetHeight)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile.TryGet<Hd2dStageFocus>(out var focus), Is.True);
            var view = viewName == "Home" ? StageSetAssets.HomeView : StageSetAssets.BattleView;

            float RadiusAt(float canvasHeight)
            {
                var ground = Baryonyx.App.Editor.Hd2dStageSceneSetup.GroundUnder(
                    view,
                    new Vector2(960f, canvasHeight)
                );
                float depth = Vector3.Dot(
                    ground - view.Position,
                    Quaternion.Euler(view.Euler) * Vector3.forward
                );
                return Hd2dStageFocus.EvaluateBlurRadius(
                    depth,
                    focus.focusDistance.value,
                    focus.focusRange.value,
                    focus.nearFalloff.value,
                    focus.farFalloff.value,
                    focus.nearMaxRadius.value,
                    focus.maxRadius.value,
                    focus.intensity.value
                );
            }

            Assert.That(RadiusAt(feetHeight), Is.EqualTo(0f), "The front row's feet are sharp.");
        }

        // The battle's backgrounds are checked in BattleStageSetTests.
        [Test]
        public void TheDressingKeepsClearOfTheCharactersFeet()
        {
            var scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var feet = roots
                    .SelectMany(root => root.GetComponentsInChildren<Hd2dUiBillboard>(true))
                    .Where(board => board.Staged)
                    .Select(board => board.ContactRenderer.bounds.center)
                    .Select(foot => new Vector2(foot.x, foot.z))
                    .ToArray();
                Assert.That(feet, Is.Not.Empty);
                var dressing = roots
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "Dressing");
                foreach (var renderer in dressing.GetComponentsInChildren<MeshRenderer>())
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    // What lies flat (the ring of the fire, litter, shade) may lie under them.
                    if (mesh.normals[0].y > 0.9f)
                        continue;
                    var vertices = mesh.vertices;
                    for (int quad = 0; quad + 3 < vertices.Length; quad += 4)
                    {
                        var centre = renderer.transform.TransformPoint(
                            (vertices[quad] + vertices[quad + 2]) * 0.5f
                        );
                        foreach (var foot in feet)
                            Assert.That(
                                Vector2.Distance(foot, new Vector2(centre.x, centre.z)),
                                Is.GreaterThan(0.8f),
                                $"{renderer.name} stands at the feet of a character."
                            );
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
