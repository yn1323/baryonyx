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
            AssertComputed(lighting.DustMaterial, "_SHAPE_MOTE");
            AssertComputed(lighting.SparkleMaterial, "_SHAPE_STAR");

            var images = root.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            Assert.That(images.All(image => !image.raycastTarget), Is.True);
            // Every light is computed: no picture is drawn.
            Assert.That(images.All(image => image.sprite == null), Is.True);
        }

        [Test]
        public void ParticlesArePreviewedOutsidePlayModeWithoutBeingSaved()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompositePrefabPath);
            Assert.That(prefab, Is.Not.Null, CompositePrefabPath);

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
                var images = lighting.ParticleLayer.GetComponentsInChildren<UnityEngine.UI.Image>(
                    true
                );
                Assert.That(
                    images
                        .Where(image => image.name.StartsWith("Dust_"))
                        .All(image => image.material == lighting.DustMaterial),
                    Is.True
                );
                Assert.That(
                    images
                        .Where(image => image.name.StartsWith("Sparkle_"))
                        .All(image => image.material == lighting.SparkleMaterial),
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
            Assert.That(lightPointComponent.Glow.sprite, Is.Null);
            AssertComputed(lightPointComponent.Glow.material, "_SHAPE_GLOW");
            AssertComputed(lightPointComponent.Ray.material, "_SHAPE_BEAM");

            var particleField = AssetDatabase.LoadAssetAtPath<GameObject>(ParticleFieldPrefabPath);
            Assert.That(particleField, Is.Not.Null, ParticleFieldPrefabPath);
            var particleComponent = particleField.GetComponent<Hd2dLightingVfx>();
            Assert.That(particleComponent, Is.Not.Null);
            Assert.That(particleComponent.ParticleLayer, Is.Not.Null);
            AssertComputed(particleComponent.DustMaterial, "_SHAPE_MOTE");
            AssertComputed(particleComponent.SparkleMaterial, "_SHAPE_STAR");
        }

        [Test]
        public void LightShaftIsAnIndividuallyReusableEditablePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightShaftPrefabPath);
            Assert.That(prefab, Is.Not.Null, LightShaftPrefabPath);

            var shaft = prefab.GetComponent<Hd2dLightShaft>();
            Assert.That(shaft, Is.Not.Null);
            Assert.That(shaft.ShaftLayer, Is.Not.Null);
            AssertComputed(shaft.BeamMaterial, "_SHAPE_BEAM");
            AssertComputed(shaft.FloorPoolMaterial, "_SHAPE_POOL");

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

                Assert.That(shafts, Has.Length.EqualTo(shaft.ShaftCount));
                Assert.That(
                    shafts.Select(rect => rect.anchorMin.x).Distinct().Count(),
                    Is.EqualTo(shaft.ShaftCount)
                );
                Assert.That(generated.Count(rect => rect.name == "FloorPool"), Is.EqualTo(1));
                var images = layer.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                Assert.That(images.All(image => !image.raycastTarget), Is.True);
                // Each shaft reads its own unsaved copy of the beam, with its own dust.
                var beams = images
                    .Where(image => image.name.StartsWith("Shaft_"))
                    .Select(image => image.material)
                    .ToArray();
                Assert.That(beams.Distinct().Count(), Is.EqualTo(shaft.ShaftCount));
                Assert.That(
                    beams.All(material =>
                        material.shader == shaft.BeamMaterial.shader
                        && material.hideFlags == HideFlags.DontSave
                    ),
                    Is.True
                );
                Assert.That(
                    beams.Select(material => material.GetVector("_Seed")).Distinct().Count(),
                    Is.EqualTo(shaft.ShaftCount)
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void FogFlowsWithTheWindAndShowsMoreSpecksInLongerShafts()
        {
            // Reading the noise further back moves the pattern forward, in tiles per second.
            var flow = Hd2dFog.EvaluateFlow(new Vector2(16f, -4f), new Vector2(800f, 200f), 10f);
            Assert.That(flow.x, Is.EqualTo(-0.2f).Within(1e-5f));
            Assert.That(flow.y, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(Hd2dFog.EvaluateFlow(Vector2.one, Vector2.one, 0f), Is.EqualTo(Vector2.zero));

            Assert.That(Hd2dLightShaft.EvaluateMoteShare(0, 1300f, 200f), Is.EqualTo(0f));
            Assert.That(
                Hd2dLightShaft.EvaluateMoteShare(18, 1300f, 200f),
                Is.GreaterThan(Hd2dLightShaft.EvaluateMoteShare(18, 2600f, 200f))
            );
            Assert.That(Hd2dLightShaft.EvaluateMoteShare(100000, 10f, 10f), Is.EqualTo(1f));
        }

        [Test]
        public void FogIsAnIndividuallyReusableEditablePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogPrefabPath);
            Assert.That(prefab, Is.Not.Null, FogPrefabPath);

            var fog = prefab.GetComponent<Hd2dFog>();
            Assert.That(fog, Is.Not.Null);
            Assert.That(fog.FogLayer, Is.Not.Null);
            Assert.That(fog.FogMaterial, Is.Not.Null);
            Assert.That(fog.FogMaterial.shader.name, Is.EqualTo("Baryonyx/HD2D/UI Fog"));
            Assert.That(fog.FogMaterial.shader.isSupported, Is.True);

            var instance = Object.Instantiate(prefab);
            try
            {
                var instanceFog = instance.GetComponent<Hd2dFog>();
                instanceFog.RebuildLayers();
                var banks = instanceFog.FogLayer.GetComponentsInChildren<UnityEngine.UI.RawImage>(
                    true
                );
                Assert.That(banks, Has.Length.EqualTo(fog.Layers.Count));
                Assert.That(banks.All(bank => !bank.raycastTarget), Is.True);
                Assert.That(banks.All(bank => bank.texture == null), Is.True);
                Assert.That(
                    banks.All(bank => bank.gameObject.hideFlags == HideFlags.DontSave),
                    Is.True
                );
                // Each bank reads its own part of the noise from an unsaved copy of the material.
                Assert.That(banks.Select(bank => bank.material).Distinct().Count(), Is.EqualTo(banks.Length));
                Assert.That(
                    banks.All(bank =>
                        bank.material.shader == fog.FogMaterial.shader
                        && bank.material.hideFlags == HideFlags.DontSave
                    ),
                    Is.True
                );

                // Later moments move the pattern with the wind.
                var before = banks[0].material.GetVector("_Flow");
                instanceFog.ShowAt(5f);
                Assert.That(banks[0].material.GetVector("_Flow"), Is.Not.EqualTo(before));
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
            AssertComputed(light.CoreMaterial, "_SHAPE_GLOW");
            AssertComputed(light.HaloMaterial, "_SHAPE_GLOW");
            AssertComputed(light.ReflectionMaterial, "_SHAPE_POOL");

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
                // The core and the halo are separate pictures, so each keeps its own spread.
                Assert.That(
                    images.Where(image => image.name.EndsWith("_Core")).All(image => image.material == light.CoreMaterial),
                    Is.True
                );
                Assert.That(
                    images.Where(image => image.name.EndsWith("_Halo")).All(image => image.material == light.HaloMaterial),
                    Is.True
                );
                Assert.That(
                    images
                        .Where(image => image.name.EndsWith("_Reflection"))
                        .All(image => image.material == light.ReflectionMaterial),
                    Is.True
                );
                Assert.That(images.All(image => !image.raycastTarget), Is.True);
                Assert.That(
                    images.All(image => image.gameObject.hideFlags == HideFlags.DontSave),
                    Is.True
                );
                var core = images.Single(image => image.name.EndsWith("_Core"));
                Assert.That(core.rectTransform.anchorMin, Is.EqualTo(light.Sources[0].Anchor));
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
        public void EmberCurlNoiseSwirlsWithoutSpreadingOrGathering()
        {
            const float scale = 56f;
            const float speed = 0.5f;
            // Same spacing as the stream-function stencil, so the mixed differences cancel.
            const float h = scale * 0.01f;
            var total = 0f;
            for (var index = 0; index < 16; index++)
            {
                var point = new Vector2(index * 37.3f - 280f, index * 19.1f - 150f);
                var time = index * 0.7f;
                var dvx =
                    Hd2dEmberEmitter.CurlVelocity(point + Vector2.right * h, time, scale, speed).x
                    - Hd2dEmberEmitter
                        .CurlVelocity(point - Vector2.right * h, time, scale, speed)
                        .x;
                var dvy =
                    Hd2dEmberEmitter.CurlVelocity(point + Vector2.up * h, time, scale, speed).y
                    - Hd2dEmberEmitter.CurlVelocity(point - Vector2.up * h, time, scale, speed).y;
                // Divergence in noise units, where the flow itself is about 1.
                var divergence = (dvx + dvy) / (2f * h) * scale;
                Assert.That(Mathf.Abs(divergence), Is.LessThan(0.05f), $"point {index}");
                total += Hd2dEmberEmitter.CurlVelocity(point, time, scale, speed).magnitude;
            }
            Assert.That(total / 16f, Is.GreaterThan(0.2f));

            var before = Hd2dEmberEmitter.CurlVelocity(new Vector2(40f, 90f), 0f, scale, speed);
            var after = Hd2dEmberEmitter.CurlVelocity(new Vector2(40f, 90f), 3f, scale, speed);
            Assert.That((after - before).magnitude, Is.GreaterThan(0.05f));
            Assert.That(
                Hd2dEmberEmitter.CurlVelocity(new Vector2(40f, 90f), 3f, scale, 0f),
                Is.EqualTo(before)
            );
        }

        [Test]
        public void EmbersMixPointsCrossesAndStreaksByShare()
        {
            Assert.That(
                Hd2dEmberEmitter.ChooseShape(0.05f, 0.3f, 0.15f),
                Is.EqualTo(Hd2dEmberShape.Streak)
            );
            Assert.That(
                Hd2dEmberEmitter.ChooseShape(0.3f, 0.3f, 0.15f),
                Is.EqualTo(Hd2dEmberShape.Cross)
            );
            Assert.That(
                Hd2dEmberEmitter.ChooseShape(0.5f, 0.3f, 0.15f),
                Is.EqualTo(Hd2dEmberShape.Point)
            );
            // Crosses only get what the streaks leave.
            Assert.That(
                Hd2dEmberEmitter.ChooseShape(0.99f, 1f, 0.5f),
                Is.EqualTo(Hd2dEmberShape.Cross)
            );
            Assert.That(
                Hd2dEmberEmitter.ChooseShape(0.99f, 0f, 0f),
                Is.EqualTo(Hd2dEmberShape.Point)
            );

            // The tail is the path of the last moment: longer when faster, within limits.
            Assert.That(
                Hd2dEmberEmitter.EvaluateTailLength(200f, 0.05f, 2f),
                Is.EqualTo(10f).Within(1e-4f)
            );
            Assert.That(Hd2dEmberEmitter.EvaluateTailLength(0f, 0.05f, 2f), Is.EqualTo(2f));
            Assert.That(Hd2dEmberEmitter.EvaluateTailLength(10000f, 0.14f, 2f), Is.EqualTo(24f));
        }

        [Test]
        public void EmberEmitterIsAnIndividuallyReusablePrefabWithBursts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmberEmitterPrefabPath);
            Assert.That(prefab, Is.Not.Null, EmberEmitterPrefabPath);

            var emitter = prefab.GetComponent<Hd2dEmberEmitter>();
            Assert.That(emitter, Is.Not.Null);
            Assert.That(emitter.ParticleLayer, Is.Not.Null);
            AssertComputed(emitter.EmberMaterial, "_SHAPE_EMBER");
            AssertComputed(emitter.GlintMaterial, "_SHAPE_STAR");

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
                Assert.That(images.All(image => image.sprite == null), Is.True);
                Assert.That(
                    images.All(image =>
                        image.material == emitter.EmberMaterial
                        || image.material == emitter.GlintMaterial
                    ),
                    Is.True
                );
                // An ember's board is turned to its travel and is at least as long as it is high,
                // with its tail behind the head.
                var embers = images.Where(image => image.material == emitter.EmberMaterial).ToArray();
                Assert.That(embers, Is.Not.Empty);
                Assert.That(
                    embers.All(image =>
                        image.rectTransform.sizeDelta.x >= image.rectTransform.sizeDelta.y
                        && image.rectTransform.pivot.x > 0.5f
                    ),
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

                // BurstAt centres the burst on a world position instead of the anchor.
                instanceEmitter.RebuildParticles();
                var source = instanceEmitter.Sources[0];
                var layer = instanceEmitter.ParticleLayer;
                var offset = new Vector2(50f, 30f);
                var anchorPoint = layer.rect.min + Vector2.Scale(layer.rect.size, source.Anchor);
                instanceEmitter.BurstAt(0, 1, layer.TransformPoint(anchorPoint + offset));
                var burst = layer
                    .GetComponentsInChildren<UnityEngine.UI.Image>(true)
                    .Single(image => image.enabled);
                var position = burst.rectTransform.anchoredPosition;
                Assert.That(
                    Mathf.Abs(position.x - offset.x),
                    Is.LessThanOrEqualTo(source.SpawnArea.x * 0.5f + 0.01f)
                );
                Assert.That(
                    Mathf.Abs(position.y - offset.y),
                    Is.LessThanOrEqualTo(source.SpawnArea.y * 0.5f + 0.01f)
                );
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // A computed light: the ambient shader, built for this platform, with the given shape.
        private static void AssertComputed(Material material, string shapeKeyword)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Baryonyx/HD2D/UI Ambient"));
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(material.IsKeywordEnabled(shapeKeyword), Is.True, material.name);
        }

        [Test]
        public void TiltShiftKeepsTheCenterBandSharpAndBlursTheEdges()
        {
            float Amount(float height01) =>
                Hd2dTiltShift.EvaluateBlurAmount(height01, 0.5f, 0.26f, 0.3f, 1f);

            Assert.That(Amount(0.5f), Is.EqualTo(0f));
            Assert.That(Amount(0.76f), Is.EqualTo(0f));
            Assert.That(Amount(0.24f), Is.EqualTo(0f));
            Assert.That(Amount(0.9f), Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(Amount(1f), Is.EqualTo(Amount(0f)).Within(1e-5f));
            Assert.That(Amount(0.95f), Is.GreaterThan(Amount(0.85f)));
            Assert.That(
                Hd2dTiltShift.EvaluateBlurAmount(1f, 0.5f, 0.26f, 0.3f, 0f),
                Is.EqualTo(0f)
            );
        }

        [Test]
        public void TiltShiftIsOffByDefault()
        {
            var defaults = ScriptableObject.CreateInstance<Hd2dTiltShift>();
            try
            {
                Assert.That(defaults.IsActive(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }
    }
}
