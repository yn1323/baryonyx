using System;
using System.Linq;
using Baryonyx.App;
using Baryonyx.App.Editor;
using Baryonyx.Combat.Editor;
using Baryonyx.Combat.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    // VFXの質感デバッグルームのつまみの表。つまみ・出発点・シェーダーの値がずれないことを確かめる。
    public sealed class VfxLookTests
    {
        [Test]
        public void EverySliderHasARangeAndEveryPresetAValueInsideIt()
        {
            Assert.That(
                VfxLook.Specs,
                Has.Length.EqualTo(Enum.GetValues(typeof(VfxLookParam)).Length)
            );
            Assert.That(
                VfxLook.FormNames,
                Has.Length.EqualTo(Enum.GetValues(typeof(VfxLookForm)).Length)
            );
            foreach (var spec in VfxLook.Specs)
                Assert.That(spec.Max, Is.GreaterThan(spec.Min), spec.Group + spec.Label);
            foreach (var (name, values) in VfxLook.Presets)
            {
                Assert.That(values, Has.Length.EqualTo(VfxLook.Count), name);
                for (int i = 0; i < values.Length; i++)
                    Assert.That(
                        VfxLook.Clamp((VfxLookParam)i, values[i]),
                        Is.EqualTo(values[i]),
                        $"{name} {(VfxLookParam)i}"
                    );
            }
            Assert.That(VfxLook.DefaultPreset, Is.InRange(0, VfxLook.Presets.Length - 1));
        }

        // 表のプロパティ名を間違えると、つまみを動かしても見本が変わらない。
        [Test]
        public void TheShaderHasEveryPropertyTheSlidersSet()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(VfxLookLabAssets.ShaderPath);
            Assert.That(shader, Is.Not.Null);
            foreach (var spec in VfxLook.Specs.Where(spec => spec.Property != null))
                Assert.That(
                    shader.FindPropertyIndex(spec.Property),
                    Is.Not.Negative,
                    spec.Property
                );
            Assert.That(shader.FindPropertyIndex("_Form"), Is.Not.Negative);
        }

        [Test]
        public void ALookIsReadBackAsItWasWrittenAndAnOlderLineIsRefused()
        {
            var look = VfxLook.Preset(0);
            look[(int)VfxLookParam.EdgeSoft] = 0.123f;
            look[(int)VfxLookParam.Steps] = 4f;

            var read = VfxLook.Parse(VfxLook.Join(look));

            Assert.That(read, Is.EqualTo(look).Within(0.0005f));
            Assert.That(VfxLook.Parse(""), Is.Null);
            Assert.That(VfxLook.Parse("0.1,0.2"), Is.Null);
            Assert.That(VfxLook.Parse(VfxLook.Join(look).Replace("0.123", "x")), Is.Null);
        }

        [Test]
        public void AValueIsKeptInsideItsRangeAndWholeWhereTheSliderIs()
        {
            Assert.That(VfxLook.Clamp(VfxLookParam.EdgeSoft, 3f), Is.EqualTo(1f));
            Assert.That(VfxLook.Clamp(VfxLookParam.Intensity, 0f), Is.EqualTo(0.2f));
            Assert.That(VfxLook.Clamp(VfxLookParam.Steps, 2.6f), Is.EqualTo(3f));
            var read = VfxLook.Parse(string.Join(",", Enumerable.Repeat("99", VfxLook.Count)));
            for (int i = 0; i < VfxLook.Count; i++)
                Assert.That(read[i], Is.EqualTo(VfxLook.Specs[i].Max));
        }

        [Test]
        public void TheCopiedTextNamesTheFormTheTintAndEveryValue()
        {
            var text = VfxLook.Describe(VfxLookForm.Ring, 1, VfxLook.Preset(0));

            Assert.That(text, Does.StartWith("形 輪　色 氷"));
            Assert.That(text.Split('\n'), Has.Length.EqualTo(1 + GroupCount()));
            Assert.That(text, Does.Contain("輪郭　やわらかさ 0.00　崩し 0.15"));
            Assert.That(text, Does.Contain("階調の段 3"));
        }

        private static int GroupCount() =>
            VfxLook.Specs.Select(spec => spec.Group).Distinct().Count();

        // 画面はシーンのつまみを表の順に持つ。展示室とホームから開くため、ビルド対象にも入れる。
        [Test]
        public void TheSceneHasASliderForEveryValueAndTheSampleItsMaterial()
        {
            Assert.That(
                EditorBuildSettings.scenes.Any(scene =>
                    scene.path == VfxLookLabSceneSetup.ScenePath && scene.enabled
                ),
                Is.True
            );
            var opened = EditorSceneManager.OpenScene(
                VfxLookLabSceneSetup.ScenePath,
                OpenSceneMode.Additive
            );
            try
            {
                var lab = opened
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VfxLookLab>(true))
                    .Single();
                Assert.That(lab.Board, Is.Not.Null);
                Assert.That(lab.LookMaterial, Is.Not.Null);
                Assert.That(lab.Board.material, Is.EqualTo(lab.LookMaterial));
                Assert.That(
                    AssetDatabase.GetAssetPath(lab.LookMaterial.shader),
                    Is.EqualTo(VfxLookLabAssets.ShaderPath)
                );
                Assert.That(lab.Backdrop, Is.Not.Null);
                Assert.That(lab.Actor, Is.Not.Null);
                Assert.That(lab.FloorLight, Is.Not.Null);
                Assert.That(lab.Wake, Is.Not.Null);
                Assert.That(lab.ActorLightMaterial, Is.Not.Null);
                Assert.That(lab.Sliders, Has.Length.EqualTo(VfxLook.Count));
                Assert.That(lab.Sliders, Has.None.Null);
                Assert.That(lab.ValueTexts, Has.Length.EqualTo(VfxLook.Count));
                Assert.That(lab.ValueTexts, Has.None.Null);
                Assert.That(lab.FormButtons, Has.Length.EqualTo(VfxLook.FormNames.Length));
                Assert.That(lab.TintButtons, Has.Length.EqualTo(VfxLook.Tints.Length));
                Assert.That(lab.PresetButtons, Has.Length.EqualTo(VfxLook.Presets.Length));
                Assert.That(lab.ProgressSlider, Is.Not.Null);
                Assert.That(lab.HomeSceneName, Is.EqualTo(SceneNames.Home));
                Assert.That(lab.Transition, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(opened, true);
            }
        }
    }
}
