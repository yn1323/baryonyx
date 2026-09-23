using System;
using System.IO;
using Baryonyx.Vfx.Hd2d;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Baryonyx.App.Editor
{
    /// <summary>
    /// Creates the small, reusable UI assets used by the HD-2D lighting layer.
    /// </summary>
    public static class Hd2dLightingVfxAssetSetup
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dLightingVfx.prefab";
        public const string LightPointPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dVfxLightPoint.prefab";
        public const string ParticleFieldPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dParticleField.prefab";
        public const string LightShaftPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dLightShaft.prefab";
        public const string FogPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dFog.prefab";
        public const string FlickerLightPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dFlickerLight.prefab";
        public const string EmberEmitterPrefabPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs/Hd2dEmberEmitter.prefab";
        public const string PostProcessProfilePath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Profiles/Hd2dPostProcess.asset";
        public const string AdditiveMaterialPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Materials/Hd2dUiAdditive.mat";

        private const string TextureDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Textures";
        private const string PrefabDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs";
        private const string MaterialDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Materials";
        private const string ProfileDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Profiles";
        private const string AdditiveShaderPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Shaders/Hd2dUiAdditive.shader";
        private const string TopScenePath = TopHomeSceneSetup.TopScenePath;

        private const string GlowTexturePath = TextureDirectory + "/Hd2dGlowSoft.png";
        private const string RayTexturePath = TextureDirectory + "/Hd2dLightRay.png";
        private const string DustTexturePath = TextureDirectory + "/Hd2dDust.png";
        private const string SparkleTexturePath = TextureDirectory + "/Hd2dSparkle.png";
        private const string LightShaftTexturePath = TextureDirectory + "/Hd2dLightShaft.png";
        private const string FogNoiseTexturePath = TextureDirectory + "/Hd2dFogNoise.png";
        private const int FogNoiseSize = 256;
        private const int FogNoiseSeed = 1234;

        [MenuItem("Baryonyx/VFX/Create HD-2D Lighting VFX Assets")]
        public static void CreateAssets()
        {
            EnsureAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Lighting VFX Assets and Apply to Top")]
        public static void CreateAssetsAndIntegrateTop()
        {
            EnsureAssets();
            IntegrateTopScene();
            Baryonyx.Showcase.Editor.ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Light Shaft and Apply to Top")]
        public static void CreateLightShaftAndIntegrateTop()
        {
            EnsureAssets();
            IntegrateLightShaftOnly();
            Baryonyx.Showcase.Editor.ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Fog and Apply to Top")]
        public static void CreateFogAndIntegrateTop()
        {
            EnsureAssets();
            IntegrateFogOnly();
            Baryonyx.Showcase.Editor.ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Flicker Light and Apply to Top")]
        public static void CreateFlickerLightAndIntegrateTop()
        {
            EnsureAssets();
            IntegrateFlickerLightOnly();
            Baryonyx.Showcase.Editor.ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Post Process and Apply to Top")]
        public static void CreatePostProcessAndIntegrateTop()
        {
            EnsureAssets();
            if (File.Exists(ToAbsolutePath(TopScenePath)))
            {
                var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
                if (TopHomeSceneSetup.EnsureTopPostProcess(scene))
                    EditorSceneManager.SaveScene(scene, TopScenePath);
            }
            Baryonyx.Showcase.Editor.ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Ember Emitter and Apply to Top")]
        public static void CreateEmberEmitterAndIntegrateTop()
        {
            EnsureAssets();
            IntegrateEmberEmitterOnly();
            Baryonyx.Showcase.Editor.ShowcaseCatalogBuilder.RefreshCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void EnsureAssets()
        {
            EnsureFolder("Assets/Baryonyx/Shared");
            EnsureFolder("Assets/Baryonyx/Shared/VFX");
            EnsureFolder("Assets/Baryonyx/Shared/VFX/HD2D");
            EnsureFolder(TextureDirectory);
            EnsureFolder(PrefabDirectory);
            EnsureFolder(MaterialDirectory);
            EnsureFolder(ProfileDirectory);

            EnsureTexture(GlowTexturePath, 64, 64, CreateGlowPixels, FilterMode.Bilinear);
            EnsureTexture(RayTexturePath, 128, 64, CreateRayPixels, FilterMode.Bilinear);
            EnsureTexture(DustTexturePath, 16, 16, CreateDustPixels, FilterMode.Bilinear);
            EnsureTexture(SparkleTexturePath, 32, 32, CreateSparklePixels, FilterMode.Bilinear);
            EnsureTexture(
                LightShaftTexturePath,
                256,
                128,
                CreateLightShaftPixels,
                FilterMode.Bilinear
            );
            // The fog scrolls its UVs, so the noise must tile instead of clamping at the edges.
            EnsureTexture(
                FogNoiseTexturePath,
                FogNoiseSize,
                FogNoiseSize,
                CreateFogNoisePixels,
                FilterMode.Bilinear,
                TextureWrapMode.Repeat
            );

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureLightPointPrefab();
            EnsureParticleFieldPrefab();
            EnsureLightShaftPrefab();
            EnsureFogPrefab();
            EnsureAdditiveMaterial();
            EnsureFlickerLightPrefab();
            EnsureEmberEmitterPrefab();
            EnsurePostProcessProfile();
            EnsurePrefab();
        }

        private static void IntegrateTopScene()
        {
            if (!File.Exists(ToAbsolutePath(TopScenePath)))
                return;

            var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException(
                    $"Top scene could not be opened: {TopScenePath}"
                );

            var changed = TopHomeSceneSetup.EnsureTopLightingVfx(scene);
            changed |= TopHomeSceneSetup.EnsureTopLightShaft(scene);
            changed |= TopHomeSceneSetup.EnsureTopFog(scene);
            changed |= TopHomeSceneSetup.EnsureTopFlickerLight(scene);
            changed |= TopHomeSceneSetup.EnsureTopEmberEmitter(scene);
            changed |= TopHomeSceneSetup.EnsureTopPostProcess(scene);
            if (changed)
                EditorSceneManager.SaveScene(scene, TopScenePath);
        }

        private static void IntegrateLightShaftOnly()
        {
            if (!File.Exists(ToAbsolutePath(TopScenePath)))
                return;

            var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException(
                    $"Top scene could not be opened: {TopScenePath}"
                );

            if (TopHomeSceneSetup.EnsureTopLightShaft(scene))
                EditorSceneManager.SaveScene(scene, TopScenePath);
        }

        private static void IntegrateFogOnly()
        {
            if (!File.Exists(ToAbsolutePath(TopScenePath)))
                return;

            var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException(
                    $"Top scene could not be opened: {TopScenePath}"
                );

            if (TopHomeSceneSetup.EnsureTopFog(scene))
                EditorSceneManager.SaveScene(scene, TopScenePath);
        }

        private static void IntegrateFlickerLightOnly()
        {
            if (!File.Exists(ToAbsolutePath(TopScenePath)))
                return;

            var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException(
                    $"Top scene could not be opened: {TopScenePath}"
                );

            if (TopHomeSceneSetup.EnsureTopFlickerLight(scene))
                EditorSceneManager.SaveScene(scene, TopScenePath);
        }

        private static void IntegrateEmberEmitterOnly()
        {
            if (!File.Exists(ToAbsolutePath(TopScenePath)))
                return;

            var scene = EditorSceneManager.OpenScene(TopScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new InvalidOperationException(
                    $"Top scene could not be opened: {TopScenePath}"
                );

            if (TopHomeSceneSetup.EnsureTopEmberEmitter(scene))
                EditorSceneManager.SaveScene(scene, TopScenePath);
        }

        private static void EnsureEmberEmitterPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EmberEmitterPrefabPath) != null)
                return;

            var root = new GameObject("Hd2dEmberEmitter", typeof(RectTransform));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var emitter = root.AddComponent<Hd2dEmberEmitter>();
                emitter.ParticleLayer = CreateLayer("ParticleLayer", root.transform);
                emitter.AdditiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    AdditiveMaterialPath
                );
                emitter.PlayOnEnable = true;
                emitter.Animate = true;
                emitter.UseUnscaledTime = true;
                emitter.RandomSeed = 7070;
                emitter.Intensity = 1f;
                // A single ember stream as a sample; each screen places its own sources.
                emitter.Sources = new System.Collections.Generic.List<Hd2dEmberSource>
                {
                    new Hd2dEmberSource { Name = "Embers", Anchor = new Vector2(0.5f, 0.4f) },
                };
                PrefabUtility.SaveAsPrefabAsset(root, EmberEmitterPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsurePostProcessProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath) != null)
                return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessProfilePath);

            // Only bright pixels (flames, additive glows, light shafts) bloom; the pixel art
            // itself stays sharp because the threshold sits above the painted mid-tones.
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.8f;
            bloom.intensity.value = 1.6f;
            bloom.scatter.value = 0.65f;
            bloom.tint.value = Color.white;
            bloom.clamp.value = 65472f;
            bloom.highQualityFiltering.value = false;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 6;

            var vignette = profile.Add<Vignette>(true);
            vignette.color.value = Color.black;
            vignette.center.value = new Vector2(0.5f, 0.5f);
            vignette.intensity.value = 0.28f;
            vignette.smoothness.value = 0.45f;
            vignette.rounded.value = false;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.contrast.value = 8f;
            colorAdjustments.colorFilter.value = Color.white;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.saturation.value = 6f;

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureAdditiveMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath) != null)
                return;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AdditiveShaderPath);
            if (shader == null)
                throw new InvalidOperationException($"Shader not found: {AdditiveShaderPath}");
            AssetDatabase.CreateAsset(new Material(shader), AdditiveMaterialPath);
        }

        private static void EnsureFlickerLightPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FlickerLightPrefabPath) != null)
                return;

            var root = new GameObject("Hd2dFlickerLight", typeof(RectTransform));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var light = root.AddComponent<Hd2dFlickerLight>();
                light.LightLayer = CreateLayer("LightLayer", root.transform);
                light.GlowSprite = LoadSprite(GlowTexturePath);
                light.AdditiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    AdditiveMaterialPath
                );
                light.PlayOnEnable = true;
                light.Animate = true;
                light.UseUnscaledTime = true;
                light.RandomSeed = 4242;
                light.Intensity = 1f;
                // A single torch-like sample; each screen places its own light sources.
                light.Sources = new System.Collections.Generic.List<Hd2dFlickerLightSource>
                {
                    new Hd2dFlickerLightSource
                    {
                        Name = "Torch",
                        Anchor = new Vector2(0.5f, 0.6f),
                        ReflectionAnchor = new Vector2(0.5f, 0.3f),
                    },
                };
                PrefabUtility.SaveAsPrefabAsset(root, FlickerLightPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFogPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FogPrefabPath) != null)
                return;

            var root = new GameObject("Hd2dFog", typeof(RectTransform));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var fog = root.AddComponent<Hd2dFog>();
                fog.FogLayer = CreateLayer("FogLayer", root.transform);
                fog.NoiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FogNoiseTexturePath);
                if (fog.NoiseTexture == null)
                    throw new InvalidOperationException(
                        $"Generated texture could not be loaded: {FogNoiseTexturePath}"
                    );
                fog.PlayOnEnable = true;
                fog.Animate = true;
                fog.UseUnscaledTime = true;
                fog.RandomSeed = 1234;
                fog.Intensity = 1f;
                // Generic ground fog: a slow far band and a faster near band read as depth.
                fog.Layers = new System.Collections.Generic.List<Hd2dFogLayer>
                {
                    new Hd2dFogLayer
                    {
                        Name = "FloorMistFar",
                        AnchorMin = new Vector2(-0.05f, 0.18f),
                        AnchorMax = new Vector2(1.05f, 0.4f),
                        Color = new Color(0.55f, 0.64f, 0.8f, 0.16f),
                        Softness = new Vector2Int(240, 70),
                        TileSize = new Vector2(640f, 150f),
                        ScrollSpeed = new Vector2(8f, 0f),
                        DetailOpacity = 0.5f,
                        BreathAmount = 0.15f,
                        BreathSpeed = 0.18f,
                    },
                    new Hd2dFogLayer
                    {
                        Name = "FloorMistNear",
                        AnchorMin = new Vector2(-0.05f, -0.06f),
                        AnchorMax = new Vector2(1.05f, 0.2f),
                        Color = new Color(0.6f, 0.68f, 0.82f, 0.2f),
                        Softness = new Vector2Int(240, 80),
                        TileSize = new Vector2(900f, 220f),
                        ScrollSpeed = new Vector2(-16f, 0f),
                        DetailOpacity = 0.5f,
                        BreathAmount = 0.15f,
                        BreathSpeed = 0.22f,
                    },
                };
                PrefabUtility.SaveAsPrefabAsset(root, FogPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureLightShaftPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LightShaftPrefabPath) != null)
                return;

            var root = new GameObject("Hd2dLightShaft", typeof(RectTransform));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var shaft = root.AddComponent<Hd2dLightShaft>();
                shaft.ShaftLayer = CreateLayer("ShaftLayer", root.transform);
                shaft.ShaftSprite = LoadSprite(LightShaftTexturePath);
                shaft.PlayOnEnable = true;
                shaft.Animate = true;
                shaft.UseUnscaledTime = true;
                shaft.RandomSeed = 518;
                shaft.SourceAnchor = new Vector2(0.64f, 1.18f);
                shaft.SourceJitter = new Vector2(0.01f, 0.005f);
                shaft.SourceSpread = 0.08f;
                shaft.ShaftCount = 4;
                shaft.ShaftColor = new Color(0.59f, 0.75f, 1f, 0.34f);
                shaft.LengthRange = new Vector2(1300f, 1500f);
                shaft.WidthRange = new Vector2(150f, 250f);
                shaft.RotationRange = new Vector2(-110f, -106f);
                shaft.WidthScaleRange = new Vector2(0.55f, 1.35f);
                shaft.OpacityRange = new Vector2(0.5f, 1f);
                shaft.FloorPoolSprite = LoadSprite(GlowTexturePath);
                shaft.FloorPoolAnchor = new Vector2(0.49f, 0.25f);
                shaft.FloorPoolSize = new Vector2(640f, 170f);
                shaft.FloorPoolAlpha = 0.4f;
                shaft.MotesPerShaft = 18;
                shaft.MoteColor = new Color(0.85f, 0.92f, 1f, 0.95f);
                shaft.MoteSizeRange = new Vector2(3f, 5f);
                shaft.MoteSpeedRange = new Vector2(6f, 16f);
                shaft.MoteSway = 10f;
                shaft.FlickerAmount = 0.08f;
                shaft.FlickerSpeed = 0.35f;
                shaft.MotionAmplitude = 6f;
                shaft.MotionSpeed = 0.1f;
                PrefabUtility.SaveAsPrefabAsset(root, LightShaftPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsurePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
                return;

            var dustSprite = LoadSprite(DustTexturePath);
            var sparkleSprite = LoadSprite(SparkleTexturePath);

            var root = new GameObject("Hd2dLightingVfx", typeof(RectTransform));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);
                var lighting = root.AddComponent<Hd2dLightingVfx>();

                var particleLayer = CreateLayer("ParticleLayer", root.transform);
                lighting.ParticleLayer = particleLayer;
                lighting.DustSprite = dustSprite;
                lighting.SparkleSprite = sparkleSprite;

                ConfigureParticleDefaults(lighting);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureLightPointPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LightPointPrefabPath) != null)
                return;

            var root = new GameObject("Hd2dVfxLightPoint", typeof(RectTransform));
            try
            {
                var point = root.AddComponent<Hd2dVfxLightPoint>();
                point.LightColor = new Color(1f, 0.52f, 0.18f, 1f);
                point.GlowAlpha = 0.24f;
                point.RayAlpha = 0.1f;
                point.GlowSize = new Vector2(260f, 260f);
                point.RaySize = new Vector2(420f, 170f);
                point.RayRotation = 18f;
                point.Animate = true;
                point.FlickerAmount = 0.12f;
                point.FlickerSpeed = 1.2f;
                point.FlickerSeed = 0.1f;

                var glow = CreateImage("Glow", root.transform, LoadSprite(GlowTexturePath), false);
                var ray = CreateImage("Ray", root.transform, LoadSprite(RayTexturePath), true);
                glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = Vector2.one * 0.5f;
                glow.rectTransform.pivot = Vector2.one * 0.5f;
                ray.rectTransform.anchorMin = ray.rectTransform.anchorMax = Vector2.one * 0.5f;
                ray.rectTransform.pivot = new Vector2(0f, 0.5f);
                point.Glow = glow;
                point.Ray = ray;
                point.ApplyVisual(0f);
                PrefabUtility.SaveAsPrefabAsset(root, LightPointPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureParticleFieldPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ParticleFieldPrefabPath) != null)
                return;

            var root = new GameObject("Hd2dParticleField", typeof(RectTransform));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var lighting = root.AddComponent<Hd2dLightingVfx>();
                lighting.ParticleLayer = CreateLayer("ParticleLayer", root.transform);
                lighting.DustSprite = LoadSprite(DustTexturePath);
                lighting.SparkleSprite = LoadSprite(SparkleTexturePath);
                ConfigureParticleDefaults(lighting);
                PrefabUtility.SaveAsPrefabAsset(root, ParticleFieldPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureParticleDefaults(Hd2dLightingVfx lighting)
        {
            lighting.PlayOnEnable = true;
            lighting.AnimateParticles = true;
            lighting.UseUnscaledTime = true;
            lighting.RandomSeed = 2048;
            lighting.ReferenceAreaSize = new Vector2(1920f, 1080f);
            lighting.ParticleAreaPadding = new Vector2(80f, 100f);
            lighting.DustCount = 34;
            lighting.DustColor = new Color(0.76f, 0.84f, 0.84f, 0.2f);
            lighting.DustSizeRange = new Vector2(6f, 14f);
            lighting.DustLifetimeRange = new Vector2(5f, 10f);
            lighting.DustVerticalSpeedRange = new Vector2(2f, 8f);
            lighting.DustHorizontalDrift = 9f;
            lighting.SparkleCount = 8;
            lighting.SparkleColor = new Color(1f, 0.82f, 0.48f, 0.58f);
            lighting.SparkleSizeRange = new Vector2(10f, 22f);
            lighting.SparkleLifetimeRange = new Vector2(2f, 4.8f);
            lighting.SparkleVerticalSpeedRange = new Vector2(1f, 5f);
            lighting.SparkleHorizontalDrift = 5f;
            lighting.ParticleAlphaMultiplier = 1f;
        }

        private static RectTransform CreateLayer(string name, Transform parent)
        {
            var layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            var rect = layer.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        private static UnityEngine.UI.Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            bool preserveAspect
        )
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException(
                    $"Generated sprite could not be loaded: {path}"
                );
            return sprite;
        }

        private static void EnsureTexture(
            string assetPath,
            int width,
            int height,
            Func<int, int, Color> pixelFactory,
            FilterMode filterMode,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp
        )
        {
            var absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                        texture.SetPixel(x, y, pixelFactory(x, y));
                    texture.Apply();
                    File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Texture importer not found: {assetPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = filterMode;
            importer.wrapMode = wrapMode;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static Color CreateGlowPixels(int x, int y)
        {
            const int size = 64;
            var distance =
                Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f))
                / (size * 0.5f);
            var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color CreateRayPixels(int x, int y)
        {
            var length = x / 127f;
            var width = Mathf.Abs(y / 63f - 0.5f) * 2f;
            var alpha =
                Mathf.Pow(Mathf.Clamp01(1f - length), 0.7f)
                * Mathf.Pow(Mathf.Clamp01(1f - width), 2.4f)
                * 0.95f;
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color CreateDustPixels(int x, int y)
        {
            const int size = 16;
            var distance =
                Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * (size * 0.5f))
                / (size * 0.5f);
            var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.5f);
            return new Color(1f, 1f, 1f, alpha);
        }

        private static Color CreateSparklePixels(int x, int y)
        {
            const int size = 32;
            var center = (size - 1) * 0.5f;
            var dx = Mathf.Abs(x - center) / center;
            var dy = Mathf.Abs(y - center) / center;
            var cross = Mathf.Max(
                Mathf.Pow(Mathf.Clamp01(1f - dx), 3.2f) * Mathf.Clamp01(1f - dy * 3f),
                Mathf.Pow(Mathf.Clamp01(1f - dy), 3.2f) * Mathf.Clamp01(1f - dx * 3f)
            );
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            var core = Mathf.Pow(Mathf.Clamp01(1f - distance * 1.8f), 4f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(cross, core)));
        }

        private static Color CreateLightShaftPixels(int x, int y)
        {
            const int width = 256;
            const int height = 128;
            // Bright near the source (left), widening and fading toward the far end (right).
            var along = x / (width - 1f);
            var across = y / (height - 1f) * 2f - 1f;
            return new Color(1f, 1f, 1f, Hd2dLightShaft.EvaluateBeamAlpha(along, across));
        }

        private static Color CreateFogNoisePixels(int x, int y)
        {
            var u = x / (float)FogNoiseSize;
            var v = y / (float)FogNoiseSize;
            return new Color(1f, 1f, 1f, Hd2dFog.EvaluateNoiseAlpha(u, v, FogNoiseSeed));
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(parent))
                return;
            var folder = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath)
                .Replace("/", Path.DirectorySeparatorChar.ToString());
        }
    }
}
