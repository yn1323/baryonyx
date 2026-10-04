using System;
using System.Linq;
using Baryonyx.App.Editor;
using Baryonyx.Combat.Presentation;
using Baryonyx.Stages.Editor;
using Baryonyx.Vfx.Hd2d;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Tests.EditMode
{
    public sealed class BattleStageSetTests
    {
        private static readonly BattleStage[] Stages = (BattleStage[])
            Enum.GetValues(typeof(BattleStage));

        [Test]
        public void TheBattleSceneOffersEveryBackground()
        {
            var scene = EditorSceneManager.OpenScene(
                BattleSceneSetup.ScenePath,
                OpenSceneMode.Additive
            );
            try
            {
                var selector = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BattleStageSelector>(true))
                    .Single();
                Assert.That(selector.Options.Select(option => option.Stage), Is.EqualTo(Stages));
                foreach (var option in selector.Options)
                {
                    Assert.That(option.Root, Is.Not.Null, option.Stage.ToString());
                    Assert.That(option.Root.scene, Is.EqualTo(scene));
                    Assert.That(
                        PrefabUtility.IsPartOfPrefabInstance(option.Root),
                        Is.True,
                        "The stage is its prefab, built from code."
                    );
                    Assert.That(option.KeyLight.type, Is.EqualTo(LightType.Directional));
                    Assert.That(option.KeyLight.shadows, Is.Not.EqualTo(LightShadows.None));
                    Assert.That(option.Look, Is.Not.Null);
                    Assert.That(option.Look.TryGet<Hd2dStageFocus>(out var focus), Is.True);
                    Assert.That(focus.IsActive(), Is.True);
                    Assert.That(option.FogEnd, Is.GreaterThan(option.FogStart));
                    Assert.That(
                        option.Root.activeSelf,
                        Is.EqualTo(option.Stage == selector.Stage),
                        "Only the chosen background is on."
                    );
                }
                Assert.That(selector.StageCamera, Is.Not.Null);
                Assert.That(selector.StageCamera.KeyLight, Is.EqualTo(selector.Shown.KeyLight));
                Assert.That(selector.LookVolume.sharedProfile, Is.EqualTo(selector.Shown.Look));
                Assert.That(scene.isDirty, Is.False, "Opening the scene changes nothing to save.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ChoosingABackgroundShowsItWithItsLookAndLight()
        {
            var scene = EditorSceneManager.OpenScene(
                BattleSceneSetup.ScenePath,
                OpenSceneMode.Additive
            );
            try
            {
                var selector = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<BattleStageSelector>(true))
                    .Single();
                foreach (var stage in Stages)
                {
                    selector.Stage = stage;
                    selector.Apply();
                    var chosen = selector.Find(stage);
                    Assert.That(selector.Shown, Is.SameAs(chosen));
                    Assert.That(
                        selector.Options.Where(option => option.Root.activeSelf),
                        Is.EquivalentTo(new[] { chosen }),
                        stage.ToString()
                    );
                    Assert.That(selector.LookVolume.sharedProfile, Is.SameAs(chosen.Look));
                    Assert.That(selector.StageCamera.KeyLight, Is.SameAs(chosen.KeyLight));
                    Assert.That(
                        selector.StageCamera.Camera.backgroundColor,
                        Is.EqualTo(chosen.Fog)
                    );
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EveryBackgroundKeepsClearOfTheCharactersFeet()
        {
            var scene = EditorSceneManager.OpenScene(
                BattleSceneSetup.ScenePath,
                OpenSceneMode.Additive
            );
            try
            {
                var roots = scene.GetRootGameObjects();
                var feet = roots
                    .SelectMany(root => root.GetComponentsInChildren<Hd2dUiBillboard>(true))
                    .Where(board => board.Staged)
                    .Select(board => board.ContactRenderer.bounds.center)
                    .Select(foot => new Vector2(foot.x, foot.z))
                    .ToArray();
                Assert.That(feet, Has.Length.EqualTo(7), "4 allies and 3 enemies stand.");
                var selector = roots
                    .SelectMany(root => root.GetComponentsInChildren<BattleStageSelector>(true))
                    .Single();
                foreach (var option in selector.Options)
                {
                    var dressing = option.Root.transform.Find("Dressing");
                    Assert.That(dressing, Is.Not.Null, option.Stage.ToString());
                    foreach (var renderer in dressing.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        // What lies flat (litter, shade) may lie under them.
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
                                    $"{option.Stage}: {renderer.name} stands at a character's feet."
                                );
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EachBattleStageIsDressedWithThingsThatCatchTheLight()
        {
            Assert.That(
                BattleStageSets.All.Count,
                Is.EqualTo(Stages.Length - 1),
                "Every background but the dusk highland is built here."
            );
            foreach (var set in BattleStageSets.All)
            {
                Assert.That(
                    Enum.TryParse<BattleStage>(set.Name, out _),
                    Is.True,
                    "Every stage can be chosen in the battle: " + set.Name
                );
                var stage = AssetDatabase.LoadAssetAtPath<GameObject>(set.PrefabPath);
                Assert.That(stage, Is.Not.Null, set.PrefabPath);
                var dressing = stage.transform.Find("Dressing");
                Assert.That(dressing.Find("Shades"), Is.Not.Null, "Soft shade under what stands.");
                Assert.That(dressing.Find("Litter"), Is.Not.Null, "Litter over the tiles.");
                Assert.That(
                    dressing.GetComponentsInChildren<MeshRenderer>().Length,
                    Is.GreaterThan(15),
                    set.Name + ": scenery, props, litter and shades."
                );
                var sky = stage.transform.Find("Backdrop1").GetComponent<Renderer>().sharedMaterial;
                Assert.That(sky.shader.name, Is.EqualTo("Baryonyx/HD2D/Painted Distance"));
                foreach (var renderer in stage.GetComponentsInChildren<MeshRenderer>())
                {
                    var material = renderer.sharedMaterial;
                    if (material.shader.name == "Baryonyx/HD2D/Stage Foliage")
                    {
                        Assert.That(material.GetFloat("_Sway"), Is.GreaterThan(0f), material.name);
                        continue;
                    }
                    if (material.shader.name != "Universal Render Pipeline/Simple Lit")
                        continue;
                    // Every lit surface has bumps the lights pick out.
                    Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null, material.name);
                    Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True, material.name);
                    Assert.That(
                        material.GetFloat("_SpecularHighlights"),
                        Is.EqualTo(1f),
                        "No shine: " + material.name
                    );
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.tangents, Has.Length.EqualTo(mesh.vertexCount), mesh.name);
                }

                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(set.LookPath);
                Assert.That(profile.TryGet<Hd2dStageFocus>(out _), Is.True, set.LookPath);
                Assert.That(profile.TryGet<Vignette>(out var vignette) && vignette.active, Is.True);
                Assert.That(profile.TryGet<SplitToning>(out var toning) && toning.active, Is.True);
            }
        }
    }
}
