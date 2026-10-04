using System.Linq;
using Baryonyx.Stages.Editor;
using Baryonyx.Vfx.Hd2d;
using Baryonyx.Vfx.Hd2d.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
                Assert.That(stageCamera.KeyLight.type, Is.EqualTo(LightType.Directional), "Sun.");

                var roots = scene.GetRootGameObjects();
                var boards = roots
                    .SelectMany(root => root.GetComponentsInChildren<Hd2dUiBillboard>(true))
                    .ToArray();
                Assert.That(boards, Has.Length.EqualTo(5));
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
                "Assets/Baryonyx/App/Scenes/Debug/BattleInspect.unity",
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
                Assert.That(stage.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase(StageSetAssets.ForestGladeLookPath)]
        [TestCase(StageSetAssets.StarlitGateLookPath)]
        [TestCase(StageSetAssets.DuskHighlandLookPath)]
        public void EachStageLooksThroughALensThatKeepsTheCharactersSharp(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile, Is.Not.Null, path);
            Assert.That(profile.TryGet<Hd2dStageFocus>(out var focus), Is.True);
            Assert.That(focus.IsActive(), Is.True);
            Assert.That(
                focus.nearMaxRadius.value,
                Is.GreaterThanOrEqualTo(focus.maxRadius.value),
                "Things near the camera show bigger dots and blur more."
            );
            Assert.That(profile.TryGet<Vignette>(out var vignette) && vignette.active, Is.True);
            Assert.That(profile.TryGet<SplitToning>(out var toning) && toning.active, Is.True);

            // The lens blurs the opaque stage before the transparent queue, where the
            // characters' boards are drawn, so they are never blurred.
            var sprite = AssetDatabase.LoadAssetAtPath<Material>(Hd2dStageKit.SpriteMaterialPath);
            Assert.That(sprite.renderQueue, Is.GreaterThan((int)RenderQueue.GeometryLast));
        }

        [Test]
        public void TheSkiesArePaintedAndTheStarsShine()
        {
            var night = AssetDatabase.LoadAssetAtPath<GameObject>(
                StageSetAssets.StarlitGatePrefabPath
            );
            var dusk = AssetDatabase.LoadAssetAtPath<GameObject>(
                StageSetAssets.DuskHighlandPrefabPath
            );
            Assert.That(night, Is.Not.Null);
            Assert.That(dusk, Is.Not.Null);

            var sky = night.transform.Find("Sky0").GetComponent<Renderer>().sharedMaterial;
            Assert.That(sky.shader.name, Is.EqualTo("Baryonyx/HD2D/Painted Distance"));
            Assert.That(sky.GetFloat("_StarBoost"), Is.GreaterThan(1f), "The stars bloom.");
            Assert.That(sky.GetFloat("_Twinkle"), Is.GreaterThan(0f));
            var land = dusk.transform.Find("Backdrop1").GetComponent<Renderer>().sharedMaterial;
            Assert.That(land.shader.name, Is.EqualTo("Baryonyx/HD2D/Painted Distance"));

            // The night's faces turned to the camera would be black under the moon behind the gate.
            var fill = night.transform.Find("FillLight").GetComponent<Light>();
            Assert.That(fill.shadows, Is.EqualTo(LightShadows.None));
            var moon = night.transform.Find(StageSetAssets.KeyLightName).GetComponent<Light>();
            Assert.That(fill.intensity, Is.LessThan(moon.intensity));
        }

        // The canvas height of the front row's feet: Home's party stands at 24-28% of the
        // screen's height, the battle's front row at 30%.
        [TestCase(StageSetAssets.ForestGladeLookPath, "Home", 260f)]
        [TestCase(StageSetAssets.DuskHighlandLookPath, "Battle", 324f)]
        public void TheForegroundBlursStepByStepDownToTheBottomEdge(
            string path,
            string viewName,
            float feetHeight
        )
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
            Assert.That(
                RadiusAt(feetHeight - 60f),
                Is.GreaterThan(0f),
                "The blur starts right under the front row, not far below it."
            );
            Assert.That(
                RadiusAt(40f),
                Is.LessThan(RadiusAt(0f)),
                "The blur still grows near the bottom edge instead of flattening above it."
            );
            Assert.That(
                RadiusAt(feetHeight * 0.5f),
                Is.InRange(RadiusAt(0f) * 0.25f, RadiusAt(0f) * 0.75f),
                "Half way down from the feet the blur is part way, neither barely begun nor done."
            );
        }

        [Test]
        public void HomeFocusesWhereThePartyStands()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                StageSetAssets.ForestGladeLookPath
            );
            Assert.That(profile.TryGet<Hd2dStageFocus>(out var focus), Is.True);
            Assert.That(
                focus.focusDistance.value,
                Is.EqualTo(StageSetAssets.HomeView.FocusDistance)
            );
            var view = StageSetAssets.HomeView;
            var feet = Baryonyx.App.Editor.Hd2dStageSceneSetup.GroundUnder(
                view,
                new Vector2(960f, 280f)
            );
            float depth = Vector3.Dot(
                feet - view.Position,
                Quaternion.Euler(view.Euler) * Vector3.forward
            );
            Assert.That(
                Hd2dStageFocus.EvaluateBlurRadius(
                    depth,
                    focus.focusDistance.value,
                    focus.focusRange.value,
                    focus.nearFalloff.value,
                    focus.farFalloff.value,
                    focus.nearMaxRadius.value,
                    focus.maxRadius.value,
                    focus.intensity.value
                ),
                Is.EqualTo(0f),
                "The ground at the party's feet is in focus."
            );
        }

        [TestCase(StageSetAssets.ForestGladePrefabPath)]
        [TestCase(StageSetAssets.StarlitGatePrefabPath)]
        [TestCase(StageSetAssets.DuskHighlandPrefabPath)]
        public void EachStageIsDressedWithPropsThatCatchTheLight(string path)
        {
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var dressing = stage.transform.Find("Dressing");
            Assert.That(dressing, Is.Not.Null, path);
            Assert.That(
                dressing.Find("Tufts"),
                Is.Not.Null,
                "Tufts bed the scenery into the ground."
            );
            Assert.That(dressing.Find("Shades"), Is.Not.Null, "Soft shade under what stands.");

            var renderers = stage.GetComponentsInChildren<MeshRenderer>();
            Assert.That(
                dressing.GetComponentsInChildren<MeshRenderer>().Length,
                Is.GreaterThan(10),
                "Props, litter, tufts and shades."
            );
            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                if (material.shader.name == "Baryonyx/HD2D/Stage Foliage")
                {
                    Assert.That(material.GetFloat("_Sway"), Is.GreaterThan(0f), material.name);
                    continue;
                }
                if (material.shader.name != "Universal Render Pipeline/Simple Lit")
                    continue;
                // Every lit surface of the stage has bumps the lights pick out, except the far
                // backdrops that light themselves.
                if (material.IsKeywordEnabled("_EMISSION"))
                    continue;
                Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null, material.name);
                Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True, material.name);
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.tangents, Has.Length.EqualTo(mesh.vertexCount), mesh.name);
            }
        }

        [TestCase(HomeScenePath)]
        [TestCase("Assets/Baryonyx/App/Scenes/Debug/BattleInspect.unity")]
        public void TheDressingKeepsClearOfTheCharactersFeet(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
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
