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
                    Assert.That(option.KeyLight, Is.Not.Null, option.Stage.ToString());
                    Assert.That(option.Look, Is.Not.Null, option.Stage.ToString());
                    Assert.That(
                        option.Root.activeSelf,
                        Is.EqualTo(option.Stage == selector.Stage),
                        "Only the chosen background is on."
                    );
                }
                Assert.That(selector.StageCamera, Is.Not.Null);
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
        public void EveryBuiltBattleStageCanBeChosen()
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
            }
        }
    }
}
