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
            Assert.That(shaft.SourceAnchor.x, Is.InRange(0.95f, 1.05f));
            Assert.That(shaft.SourceAnchor.y, Is.GreaterThan(1f));
            Assert.That(shaft.WidthScaleRange.x, Is.LessThan(1f));
            Assert.That(shaft.WidthScaleRange.y, Is.GreaterThan(1f));

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceShaft = instance.GetComponent<Hd2dLightShaft>();
                instanceShaft.RebuildShafts();
                var widths = instanceShaft.ShaftLayer
                    .GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect.name.StartsWith("Shaft_"))
                    .Select(rect => rect.sizeDelta.y)
                    .ToArray();

                Assert.That(widths, Has.Length.EqualTo(2));
                Assert.That(widths[0], Is.LessThan(widths[1]));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
