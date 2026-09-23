using System.Linq;
using Baryonyx.Vfx.Hd2d;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baryonyx.Tests.EditMode
{
    public sealed class Hd2dLightingVfxTests
    {
        private const string CompositePrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dLightingVfx.prefab";
        private const string LightPointPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dVfxLightPoint.prefab";
        private const string ParticleFieldPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dParticleField.prefab";
        private const string LightShaftPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dLightShaft.prefab";

        [Test]
        public void LightingPrefabContainsEditableParticleLayer()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(CompositePrefabPath);
            Assert.That(root, Is.Not.Null, CompositePrefabPath);

            var lighting = root.GetComponent<Hd2dLightingVfx>();
            Assert.That(lighting, Is.Not.Null);
            Assert.That(lighting.ParticleLayer, Is.Not.Null);
            Assert.That(lighting.DustSprite, Is.Not.Null);
            Assert.That(lighting.SparkleSprite, Is.Not.Null);
            Assert.That(lighting.DustCount, Is.GreaterThan(0));
            Assert.That(lighting.SparkleCount, Is.GreaterThan(0));

            Assert.That(
                root.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                    .All(image => !image.raycastTarget),
                Is.True
            );
        }

        [Test]
        public void ParticlesArePreviewedOutsidePlayModeWithoutBeingSaved()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompositePrefabPath);
            Assert.That(prefab, Is.Not.Null, CompositePrefabPath);
            Assert.That(prefab.GetComponent<Hd2dLightingVfx>().PreviewInEditor, Is.True);

            var instance = Object.Instantiate(prefab);
            try
            {
                var lighting = instance.GetComponent<Hd2dLightingVfx>();
                lighting.RebuildParticles();
                var generated = lighting.ParticleLayer
                    .GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect != lighting.ParticleLayer)
                    .ToArray();

                Assert.That(
                    generated.Count(rect => rect.name.StartsWith("Dust_")),
                    Is.EqualTo(lighting.DustCount)
                );
                Assert.That(
                    generated.Count(rect => rect.name.StartsWith("Sparkle_")),
                    Is.EqualTo(lighting.SparkleCount)
                );
                Assert.That(
                    generated.All(rect => rect.gameObject.hideFlags == HideFlags.DontSave),
                    Is.True
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LightPointAndParticleFieldAreIndividuallyReusablePrefabs()
        {
            var lightPoint = AssetDatabase.LoadAssetAtPath<GameObject>(LightPointPrefabPath);
            Assert.That(lightPoint, Is.Not.Null, LightPointPrefabPath);
            var lightPointComponent = lightPoint.GetComponent<Hd2dVfxLightPoint>();
            Assert.That(lightPointComponent, Is.Not.Null);
            Assert.That(lightPointComponent.Glow, Is.Not.Null);
            Assert.That(lightPointComponent.Ray, Is.Not.Null);

            var particleField = AssetDatabase.LoadAssetAtPath<GameObject>(ParticleFieldPrefabPath);
            Assert.That(particleField, Is.Not.Null, ParticleFieldPrefabPath);
            var particleComponent = particleField.GetComponent<Hd2dLightingVfx>();
            Assert.That(particleComponent, Is.Not.Null);
            Assert.That(particleComponent.ParticleLayer, Is.Not.Null);
            Assert.That(particleComponent.DustSprite, Is.Not.Null);
            Assert.That(particleComponent.SparkleSprite, Is.Not.Null);
        }

        [Test]
        public void LightShaftIsAnIndividuallyReusableEditablePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightShaftPrefabPath);
            Assert.That(prefab, Is.Not.Null, LightShaftPrefabPath);

            var shaft = prefab.GetComponent<Hd2dLightShaft>();
            Assert.That(shaft, Is.Not.Null);
            Assert.That(shaft.ShaftLayer, Is.Not.Null);
            Assert.That(shaft.ShaftSprite, Is.Not.Null);
            Assert.That(shaft.ShaftCount, Is.GreaterThan(0));
            Assert.That(shaft.LengthRange.y, Is.GreaterThan(shaft.LengthRange.x));
            Assert.That(shaft.RotationRange.y, Is.LessThan(0f));
            Assert.That(shaft.SourceAnchor.x, Is.InRange(0f, 1f));
            Assert.That(shaft.SourceAnchor.y, Is.GreaterThan(1f));
            Assert.That(shaft.SourceSpread, Is.GreaterThan(0f));
            Assert.That(shaft.WidthScaleRange.x, Is.LessThan(1f));
            Assert.That(shaft.WidthScaleRange.y, Is.GreaterThan(1f));
            Assert.That(shaft.FloorPoolSprite, Is.Not.Null);
            Assert.That(shaft.FloorPoolAlpha, Is.GreaterThan(0f));
            Assert.That(shaft.MotesPerShaft, Is.GreaterThan(0));

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceShaft = instance.GetComponent<Hd2dLightShaft>();
                instanceShaft.RebuildShafts();
                var layer = instanceShaft.ShaftLayer;
                var generated = layer.GetComponentsInChildren<RectTransform>(true);
                var shafts = generated
                    .Where(rect => rect.name.StartsWith("Shaft_"))
                    .OrderBy(rect => rect.name)
                    .ToArray();
                var widths = shafts.Select(rect => rect.sizeDelta.y).ToArray();

                Assert.That(widths, Has.Length.EqualTo(shaft.ShaftCount));
                Assert.That(widths[0], Is.LessThan(widths[widths.Length - 1]));
                Assert.That(
                    shafts.Select(rect => rect.anchorMin.x).Distinct().Count(),
                    Is.EqualTo(shaft.ShaftCount)
                );
                Assert.That(
                    generated.Count(rect => rect.name.StartsWith("Mote_")),
                    Is.EqualTo(shaft.ShaftCount * shaft.MotesPerShaft)
                );
                Assert.That(generated.Count(rect => rect.name == "FloorPool"), Is.EqualTo(1));
                var images = layer.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                Assert.That(images.All(image => !image.raycastTarget), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
