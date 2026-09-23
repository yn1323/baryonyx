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
        private const string FogPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dFog.prefab";
        private const string FlickerLightPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dFlickerLight.prefab";
        private const string EmberEmitterPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dEmberEmitter.prefab";

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
                var generated = lighting
                    .ParticleLayer.GetComponentsInChildren<RectTransform>(true)
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

        [Test]
        public void FogNoiseTilesSeamlessly()
        {
            for (var step = 0; step <= 8; step++)
            {
                var t = step / 8f;
                Assert.That(
                    Hd2dFog.EvaluateNoiseDensity(0f, t, 1234),
                    Is.EqualTo(Hd2dFog.EvaluateNoiseDensity(1f, t, 1234)).Within(1e-4f)
                );
                Assert.That(
                    Hd2dFog.EvaluateNoiseDensity(t, 0f, 1234),
                    Is.EqualTo(Hd2dFog.EvaluateNoiseDensity(t, 1f, 1234)).Within(1e-4f)
                );
            }
        }

        [Test]
        public void FogIsAnIndividuallyReusableEditablePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogPrefabPath);
            Assert.That(prefab, Is.Not.Null, FogPrefabPath);

            var fog = prefab.GetComponent<Hd2dFog>();
            Assert.That(fog, Is.Not.Null);
            Assert.That(fog.FogLayer, Is.Not.Null);
            Assert.That(fog.NoiseTexture, Is.Not.Null);
            Assert.That(fog.NoiseTexture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(fog.PreviewInEditor, Is.True);
            Assert.That(fog.Layers, Is.Not.Empty);

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceFog = instance.GetComponent<Hd2dFog>();
                instanceFog.RebuildLayers();
                var banks = instanceFog
                    .FogLayer.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true)
                    .ToArray();
                Assert.That(banks, Has.Length.EqualTo(fog.Layers.Count));
                Assert.That(
                    banks.Select(bank => bank.softness),
                    Is.EqualTo(fog.Layers.Select(layer => layer.Softness))
                );

                var sheets = instanceFog.FogLayer.GetComponentsInChildren<UnityEngine.UI.RawImage>(
                    true
                );
                Assert.That(
                    sheets,
                    Has.Length.EqualTo(fog.Layers.Sum(layer => layer.DetailOpacity > 0f ? 2 : 1))
                );
                Assert.That(sheets.All(sheet => !sheet.raycastTarget), Is.True);
                Assert.That(sheets.All(sheet => sheet.texture == fog.NoiseTexture), Is.True);
                Assert.That(
                    banks.All(bank => bank.gameObject.hideFlags == HideFlags.DontSave),
                    Is.True
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void FlickerStaysInRangeAndChangesOverTime()
        {
            var samples = Enumerable
                .Range(0, 200)
                .Select(step => Hd2dFlickerLight.EvaluateFlicker01(12.3f, step * 0.05f, 2.4f))
                .ToArray();

            Assert.That(samples.All(value => value >= 0f && value <= 1f), Is.True);
            Assert.That(samples.Max() - samples.Min(), Is.GreaterThan(0.2f));
            Assert.That(
                Hd2dFlickerLight.EvaluateFlicker01(12.3f, 1f, 2.4f),
                Is.EqualTo(Hd2dFlickerLight.EvaluateFlicker01(12.3f, 1f, 2.4f))
            );
        }

        [Test]
        public void FlickerLightIsAnIndividuallyReusableAdditivePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlickerLightPrefabPath);
            Assert.That(prefab, Is.Not.Null, FlickerLightPrefabPath);

            var light = prefab.GetComponent<Hd2dFlickerLight>();
            Assert.That(light, Is.Not.Null);
            Assert.That(light.LightLayer, Is.Not.Null);
            Assert.That(light.GlowSprite, Is.Not.Null);
            Assert.That(light.AdditiveMaterial, Is.Not.Null);
            Assert.That(
                light.AdditiveMaterial.shader.name,
                Is.EqualTo("Baryonyx/HD2D/UI Additive")
            );
            Assert.That(light.AdditiveMaterial.shader.isSupported, Is.True);
            Assert.That(light.PreviewInEditor, Is.True);
            Assert.That(light.Sources, Is.Not.Empty);

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceLight = instance.GetComponent<Hd2dFlickerLight>();
                instanceLight.RebuildLights();
                var images = instanceLight.LightLayer.GetComponentsInChildren<UnityEngine.UI.Image>(
                    true
                );
                var expected = light.Sources.Sum(source => source.ReflectionAlpha > 0f ? 3 : 2);
                Assert.That(images, Has.Length.EqualTo(expected));
                Assert.That(images.All(image => image.material == light.AdditiveMaterial), Is.True);
                Assert.That(images.All(image => !image.raycastTarget), Is.True);
                Assert.That(
                    images.All(image => image.gameObject.hideFlags == HideFlags.DontSave),
                    Is.True
                );
                var core = images.Single(image => image.name.EndsWith("_Core"));
                Assert.That(core.rectTransform.anchorMin, Is.EqualTo(light.Sources[0].Anchor));
                Assert.That(core.color.a, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void EmbersRiseWithBuoyancyAndFadeOverTheirLife()
        {
            var still = Hd2dEmberEmitter.EvaluateOffset(Vector2.zero, 0f, 0f, 0f, 0f, 2f);
            Assert.That(still, Is.EqualTo(Vector2.zero));

            var rising = Hd2dEmberEmitter.EvaluateOffset(new Vector2(0f, 40f), 10f, 0f, 0f, 0f, 2f);
            Assert.That(rising.y, Is.EqualTo(40f * 2f + 0.5f * 10f * 4f).Within(1e-4f));

            var swayAtBirth = Hd2dEmberEmitter.EvaluateOffset(Vector2.zero, 0f, 8f, 1.6f, 1.2f, 0f);
            Assert.That(swayAtBirth.magnitude, Is.LessThan(1e-4f));

            Assert.That(Hd2dEmberEmitter.EvaluateLifeAlpha(0f, 1f, 0f), Is.EqualTo(0f));
            Assert.That(Hd2dEmberEmitter.EvaluateLifeAlpha(0.2f, 1f, 0f), Is.GreaterThan(0.8f));
            Assert.That(Hd2dEmberEmitter.EvaluateLifeAlpha(1f, 1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void EmberEmitterIsAnIndividuallyReusablePrefabWithBursts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmberEmitterPrefabPath);
            Assert.That(prefab, Is.Not.Null, EmberEmitterPrefabPath);

            var emitter = prefab.GetComponent<Hd2dEmberEmitter>();
            Assert.That(emitter, Is.Not.Null);
            Assert.That(emitter.ParticleLayer, Is.Not.Null);
            Assert.That(emitter.AdditiveMaterial, Is.Not.Null);
            Assert.That(emitter.PreviewInEditor, Is.True);
            Assert.That(emitter.Sources, Is.Not.Empty);

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceEmitter = instance.GetComponent<Hd2dEmberEmitter>();
                instanceEmitter.RebuildParticles();
                var images =
                    instanceEmitter.ParticleLayer.GetComponentsInChildren<UnityEngine.UI.Image>(
                        true
                    );
                Assert.That(
                    images,
                    Has.Length.EqualTo(emitter.Sources.Sum(source => source.Count))
                );
                Assert.That(images.All(image => !image.raycastTarget), Is.True);
                Assert.That(
                    images.All(image => image.gameObject.hideFlags == HideFlags.DontSave),
                    Is.True
                );
                Assert.That(
                    images.All(image => image.rectTransform.sizeDelta.x % 1f == 0f),
                    Is.True
                );

                // A one-shot source stays hidden until a burst emits it.
                instanceEmitter.Sources[0].Loop = false;
                instanceEmitter.RebuildParticles();
                images =
                    instanceEmitter.ParticleLayer.GetComponentsInChildren<UnityEngine.UI.Image>(
                        true
                    );
                Assert.That(images.Count(image => image.enabled), Is.EqualTo(0));
                instanceEmitter.Burst(0, 3);
                Assert.That(images.Count(image => image.enabled), Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
