using System;
using System.Collections.Generic;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Vfx.Hd2d.Editor
{
    /// <summary>
    /// Creates the small, reusable UI assets used by the HD-2D lighting layer: the materials of the
    /// computed ambient shaders (no image is used), the prefabs and the 2D post-process profile.
    /// The materials and the prefabs' settings are rewritten from here each time, so this code is
    /// their source. Placing them in the Top scene is App's job
    /// (<c>Baryonyx.App.Editor.Hd2dTopSetup</c>).
    /// </summary>
    public static class Hd2dAssets
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

        private const string MaterialDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Materials";

        /// <summary>The fog banks (<c>Baryonyx/HD2D/UI Fog</c>).</summary>
        public const string FogMaterialPath = MaterialDirectory + "/Hd2dUiFog.mat";

        /// <summary>A white-hot core with a little halo: a flame's or a rune's light.</summary>
        public const string GlowCoreMaterialPath = MaterialDirectory + "/Hd2dUiGlowCore.mat";

        /// <summary>A wide coloured halo whose air shimmers: the light thrown around.</summary>
        public const string GlowHaloMaterialPath = MaterialDirectory + "/Hd2dUiGlowHalo.mat";

        /// <summary>Light fallen on the floor, its rim broken by the floor.</summary>
        public const string LightPoolMaterialPath = MaterialDirectory + "/Hd2dUiLightPool.mat";

        /// <summary>A shaft of light through dusty air, with sparkling specks of dust.</summary>
        public const string LightShaftMaterialPath = MaterialDirectory + "/Hd2dUiLightShaft.mat";

        /// <summary>A thin ray fanning out from a small light.</summary>
        public const string LightRayMaterialPath = MaterialDirectory + "/Hd2dUiLightRay.mat";

        /// <summary>A faint speck of dust.</summary>
        public const string DustMaterialPath = MaterialDirectory + "/Hd2dUiDust.mat";

        /// <summary>A four-pointed twinkle.</summary>
        public const string SparkleMaterialPath = MaterialDirectory + "/Hd2dUiSparkle.mat";

        /// <summary>An ember: a round white-hot head and a thin tail that cools.</summary>
        public const string EmberMaterialPath = MaterialDirectory + "/Hd2dUiEmber.mat";

        /// <summary>A puff of soft light whose rim drifts like haze.</summary>
        public const string PuffMaterialPath = MaterialDirectory + "/Hd2dUiPuff.mat";

        private const string PrefabDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Prefabs";
        private const string ProfileDirectory = "Assets/Baryonyx/Shared/VFX/HD2D/Profiles";
        private const string TiltShiftShaderPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Shaders/Hd2dTiltShift.shader";
        private const string StageFocusShaderPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Shaders/Hd2dStageFocus.shader";
        private const string RendererDataDirectory = "Assets/Settings";
        private const string FogShaderPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Shaders/Hd2dUiFog.shader";
        private const string AmbientShaderPath =
            "Assets/Baryonyx/Shared/VFX/HD2D/Shaders/Hd2dUiAmbient.shader";

        // The shapes of the ambient shader, in the order of its _Shape keyword enum.
        private enum Shape
        {
            Glow,
            Pool,
            Beam,
            Mote,
            Star,
            Ember,
            Puff,
        }

        [MenuItem("Baryonyx/VFX/Create HD-2D Lighting VFX Assets")]
        public static void CreateAssets()
        {
            EnsureAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void EnsureAssets()
        {
            AssetFolders.Ensure(PrefabDirectory);
            AssetFolders.Ensure(MaterialDirectory);
            AssetFolders.Ensure(ProfileDirectory);

            EnsureMaterials();
            EnsureLightPointPrefab();
            EnsureParticleFieldPrefab();
            EnsureLightShaftPrefab();
            EnsureFogPrefab();
            EnsureFlickerLightPrefab();
            EnsureEmberEmitterPrefab();
            EnsurePostProcessProfile();
            EnsureTiltShiftInProfile();
            EnsureRendererFeature<Hd2dTiltShiftRendererFeature>(
                "Hd2dTiltShift",
                TiltShiftShaderPath,
                (feature, shader) => feature.Shader = shader,
                feature => feature.Shader
            );
            EnsureRendererFeature<Hd2dStageFocusRendererFeature>(
                "Hd2dStageFocus",
                StageFocusShaderPath,
                (feature, shader) => feature.Shader = shader,
                feature => feature.Shader
            );
            EnsurePrefab();
        }

        // --- Materials ----------------------------------------------------------------------------
        // The look of each effect lives in its material; the components only place, size, tint
        // and time the boards. Values are the result of comparing captures of Top, Home and the
        // battle screen (doc/art/direction.md, Effects).

        private static void EnsureMaterials()
        {
            EnsureMaterial(
                FogMaterialPath,
                FogShaderPath,
                null,
                material =>
                {
                    material.SetColor("_ShadeColor", new Color(0.68f, 0.74f, 0.88f, 1f));
                    material.SetColor("_LitColor", new Color(0.85f, 0.9f, 1f, 1f));
                    material.SetFloat("_Rim", 0.35f);
                    material.SetFloat("_Folds", 0.35f);
                    material.SetFloat("_Veil", 0.4f);
                    material.SetVector("_Cover", new Vector4(0.25f, 0.7f, 0f, 0f));
                    material.SetFloat("_Warp", 2.2f);
                }
            );
            EnsureAmbient(
                GlowCoreMaterialPath,
                Shape.Glow,
                gain: 1f,
                coreWhite: 0.6f,
                core: new Vector4(0.15f, 1.3f),
                halo: new Vector4(0.42f, 0.3f, 0.15f)
            );
            EnsureAmbient(
                GlowHaloMaterialPath,
                Shape.Glow,
                gain: 1f,
                coreWhite: 0.2f,
                core: new Vector4(0.12f, 0.1f),
                halo: new Vector4(0.36f, 0.75f, 0.25f)
            );
            EnsureAmbient(
                LightPoolMaterialPath,
                Shape.Pool,
                gain: 1f,
                coreWhite: 0f,
                core: new Vector4(0.16f, 0.45f),
                halo: new Vector4(0.34f, 0.95f)
            );
            EnsureAmbient(
                LightShaftMaterialPath,
                Shape.Beam,
                gain: 1f,
                coreWhite: 0f,
                core: Vector4.zero,
                halo: Vector4.zero,
                beam: new Vector4(0.42f, 0.9f, 1.3f, 0.45f),
                motes: new Vector4(28f, 0.3f, 1.2f, 10f)
            );
            EnsureAmbient(
                LightRayMaterialPath,
                Shape.Beam,
                gain: 1f,
                coreWhite: 0f,
                core: Vector4.zero,
                halo: Vector4.zero,
                beam: new Vector4(0.12f, 0.85f, 0.7f, 0.35f),
                motes: new Vector4(28f, 0f, 1f, 0f)
            );
            EnsureAmbient(
                DustMaterialPath,
                Shape.Mote,
                gain: 1f,
                coreWhite: 0.4f,
                core: new Vector4(0.14f, 1f),
                halo: new Vector4(0.45f, 0.4f)
            );
            EnsureAmbient(
                SparkleMaterialPath,
                Shape.Star,
                gain: 1.3f,
                coreWhite: 0.7f,
                core: new Vector4(0.08f, 1.8f),
                halo: new Vector4(0.28f, 0.35f)
            );
            EnsureAmbient(
                EmberMaterialPath,
                Shape.Ember,
                gain: 1.2f,
                coreWhite: 0.6f,
                core: new Vector4(0.12f, 1.8f),
                halo: new Vector4(0f, 0.5f, 1.4f)
            );
            EnsureAmbient(
                PuffMaterialPath,
                Shape.Puff,
                gain: 1f,
                coreWhite: 0f,
                core: new Vector4(0.12f, 0.25f),
                halo: new Vector4(0.38f, 1f)
            );
        }

        private static void EnsureAmbient(
            string path,
            Shape shape,
            float gain,
            float coreWhite,
            Vector4 core,
            Vector4 halo,
            Vector4? beam = null,
            Vector4? motes = null
        )
        {
            EnsureMaterial(
                path,
                AmbientShaderPath,
                "_SHAPE_" + shape.ToString().ToUpperInvariant(),
                material =>
                {
                    material.SetFloat("_Shape", (int)shape);
                    material.SetFloat("_Gain", gain);
                    material.SetFloat("_CoreWhite", coreWhite);
                    material.SetVector("_Core", core);
                    material.SetVector("_Halo", halo);
                    if (beam.HasValue)
                        material.SetVector("_Beam", beam.Value);
                    if (motes.HasValue)
                        material.SetVector("_Motes", motes.Value);
                }
            );
        }

        private static void EnsureMaterial(
            string path,
            string shaderPath,
            string keyword,
            Action<Material> configure
        )
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
                throw new InvalidOperationException($"Shader not found: {shaderPath}");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            foreach (var enabled in material.shaderKeywords)
                material.DisableKeyword(enabled);
            if (keyword != null)
                material.EnableKeyword(keyword);
            configure(material);
            EditorUtility.SetDirty(material);
        }

        private static Material LoadMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new InvalidOperationException($"Material not found: {path}");
            return material;
        }

        // --- Prefabs ------------------------------------------------------------------------------
        // A prefab is created once and then updated in place, so the scenes' overrides of its
        // children keep pointing at them.

        private static void EnsurePrefabAsset(
            string path,
            string rootName,
            Action<GameObject> create,
            Action<GameObject> configure
        )
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var created = new GameObject(rootName, typeof(RectTransform));
                try
                {
                    create(created);
                    configure(created);
                    PrefabUtility.SaveAsPrefabAsset(created, path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureEmberEmitterPrefab()
        {
            EnsurePrefabAsset(
                EmberEmitterPrefabPath,
                "Hd2dEmberEmitter",
                root =>
                {
                    UiBuild.Stretch(root.GetComponent<RectTransform>());
                    var emitter = root.AddComponent<Hd2dEmberEmitter>();
                    emitter.ParticleLayer = CreateLayer("ParticleLayer", root.transform);
                    emitter.PlayOnEnable = true;
                    emitter.Animate = true;
                    emitter.UseUnscaledTime = true;
                    emitter.RandomSeed = 7070;
                    emitter.Intensity = 1f;
                    // A single ember stream as a sample; each screen places its own sources.
                    emitter.Sources = new List<Hd2dEmberSource>
                    {
                        new Hd2dEmberSource { Name = "Embers", Anchor = new Vector2(0.5f, 0.4f) },
                    };
                },
                root =>
                {
                    var emitter = root.GetComponent<Hd2dEmberEmitter>();
                    emitter.EmberMaterial = LoadMaterial(EmberMaterialPath);
                    emitter.GlintMaterial = LoadMaterial(SparkleMaterialPath);
                    emitter.GlintDots = 5;
                    emitter.GlintSpin = 20f;
                }
            );
        }

        private static void EnsurePostProcessProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath) != null)
                return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessProfilePath);

            // Flames, additive glows and light shafts bloom, and the low threshold (tuned in
            // the Editor) lets the lit mid-tones glow with them. Raise it to about 0.8 to
            // limit the bloom to the brightest pixels. The 3D stages have looks of their own
            // (Baryonyx.Stages.Editor.StageSetAssets.BuildLooks).
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.4f;
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

        private static void EnsureTiltShiftInProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
            if (profile == null || profile.Has<Hd2dTiltShift>())
                return;

            // Keep the middle band (title and entrance) sharp; soften the ceiling and near floor.
            var tiltShift = profile.Add<Hd2dTiltShift>(true);
            tiltShift.intensity.value = 1f;
            tiltShift.focusCenter.value = 0.5f;
            tiltShift.focusHalfHeight.value = 0.3f;
            tiltShift.falloff.value = 0.3f;
            tiltShift.maxRadius.value = 8f;
            tiltShift.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(tiltShift, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // Adds a renderer feature to every URP renderer of the project, as the renderer's Inspector
        // does, and gives it its shader so the shader is built into the player.
        private static void EnsureRendererFeature<T>(
            string name,
            string shaderPath,
            Action<T, Shader> assign,
            Func<T, Shader> assigned
        )
            where T : ScriptableRendererFeature
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null)
                throw new InvalidOperationException($"Shader not found: {shaderPath}");

            foreach (
                var guid in AssetDatabase.FindAssets(
                    "t:UniversalRendererData",
                    new[] { RendererDataDirectory }
                )
            )
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null)
                    continue;
                var existing = data.rendererFeatures.Find(feature => feature is T) as T;
                if (existing != null)
                {
                    if (assigned(existing) != shader)
                    {
                        assign(existing, shader);
                        EditorUtility.SetDirty(existing);
                    }
                    continue;
                }

                // Mirror the renderer Inspector: the feature is a sub-asset listed with its file ID.
                var feature = ScriptableObject.CreateInstance<T>();
                feature.name = name;
                assign(feature, shader);
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serialized = new SerializedObject(data);
                var features = serialized.FindProperty("m_RendererFeatures");
                var map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue =
                    feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedProperties();
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFlickerLightPrefab()
        {
            EnsurePrefabAsset(
                FlickerLightPrefabPath,
                "Hd2dFlickerLight",
                root =>
                {
                    UiBuild.Stretch(root.GetComponent<RectTransform>());
                    var light = root.AddComponent<Hd2dFlickerLight>();
                    light.LightLayer = CreateLayer("LightLayer", root.transform);
                    light.PlayOnEnable = true;
                    light.Animate = true;
                    light.UseUnscaledTime = true;
                    light.RandomSeed = 4242;
                    light.Intensity = 1f;
                    // A single torch-like sample; each screen places its own light sources.
                    light.Sources = new List<Hd2dFlickerLightSource>
                    {
                        new Hd2dFlickerLightSource
                        {
                            Name = "Torch",
                            Anchor = new Vector2(0.5f, 0.6f),
                            ReflectionAnchor = new Vector2(0.5f, 0.3f),
                        },
                    };
                },
                root =>
                {
                    var light = root.GetComponent<Hd2dFlickerLight>();
                    light.CoreMaterial = LoadMaterial(GlowCoreMaterialPath);
                    light.HaloMaterial = LoadMaterial(GlowHaloMaterialPath);
                    light.ReflectionMaterial = LoadMaterial(LightPoolMaterialPath);
                }
            );
        }

        private static void EnsureFogPrefab()
        {
            EnsurePrefabAsset(
                FogPrefabPath,
                "Hd2dFog",
                root =>
                {
                    UiBuild.Stretch(root.GetComponent<RectTransform>());
                    var fog = root.AddComponent<Hd2dFog>();
                    fog.FogLayer = CreateLayer("FogLayer", root.transform);
                    fog.PlayOnEnable = true;
                    fog.Animate = true;
                    fog.UseUnscaledTime = true;
                    fog.RandomSeed = 1234;
                    fog.Intensity = 1f;
                },
                root =>
                {
                    var fog = root.GetComponent<Hd2dFog>();
                    fog.FogMaterial = LoadMaterial(FogMaterialPath);
                    // Generic ground fog: a slow far band and a faster near band read as depth.
                    // Both settle on the floor and tear into wisps at the top.
                    fog.Layers = new List<Hd2dFogLayer>
                    {
                        new Hd2dFogLayer
                        {
                            Name = "FloorMistFar",
                            // Reaches down under the near band, so no gap shows between them.
                            AnchorMin = new Vector2(-0.05f, 0.08f),
                            AnchorMax = new Vector2(1.05f, 0.4f),
                            Color = new Color(0.55f, 0.64f, 0.8f, 0.24f),
                            Softness = new Vector2Int(240, 70),
                            TileSize = new Vector2(640f, 150f),
                            ScrollSpeed = new Vector2(8f, 0f),
                            DetailOpacity = 0.5f,
                            Settle = 0.6f,
                            Churn = 0.2f,
                            BreathAmount = 0.15f,
                            BreathSpeed = 0.18f,
                        },
                        new Hd2dFogLayer
                        {
                            Name = "FloorMistNear",
                            AnchorMin = new Vector2(-0.05f, -0.06f),
                            AnchorMax = new Vector2(1.05f, 0.24f),
                            Color = new Color(0.6f, 0.68f, 0.82f, 0.28f),
                            Softness = new Vector2Int(240, 80),
                            TileSize = new Vector2(900f, 220f),
                            ScrollSpeed = new Vector2(-16f, 0f),
                            DetailOpacity = 0.6f,
                            Settle = 0.7f,
                            Churn = 0.3f,
                            BreathAmount = 0.15f,
                            BreathSpeed = 0.22f,
                        },
                    };
                }
            );
        }

        private static void EnsureLightShaftPrefab()
        {
            EnsurePrefabAsset(
                LightShaftPrefabPath,
                "Hd2dLightShaft",
                root =>
                {
                    UiBuild.Stretch(root.GetComponent<RectTransform>());
                    var shaft = root.AddComponent<Hd2dLightShaft>();
                    shaft.ShaftLayer = CreateLayer("ShaftLayer", root.transform);
                    shaft.PlayOnEnable = true;
                    shaft.Animate = true;
                    shaft.UseUnscaledTime = true;
                },
                root =>
                {
                    var shaft = root.GetComponent<Hd2dLightShaft>();
                    shaft.BeamMaterial = LoadMaterial(LightShaftMaterialPath);
                    shaft.FloorPoolMaterial = LoadMaterial(LightPoolMaterialPath);
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
                    shaft.FloorPoolAnchor = new Vector2(0.49f, 0.25f);
                    shaft.FloorPoolSize = new Vector2(640f, 170f);
                    shaft.FloorPoolAlpha = 0.4f;
                    shaft.MotesPerShaft = 18;
                    shaft.MoteColor = new Color(0.85f, 0.92f, 1f, 0.95f);
                    shaft.MoteSizeRange = new Vector2(1f, 1.6f);
                    shaft.MoteSpeedRange = new Vector2(6f, 16f);
                    shaft.MoteSway = 3f;
                    shaft.FlickerAmount = 0.08f;
                    shaft.FlickerSpeed = 0.35f;
                    shaft.MotionAmplitude = 6f;
                    shaft.MotionSpeed = 0.1f;
                }
            );
        }

        private static void EnsurePrefab()
        {
            EnsurePrefabAsset(
                PrefabPath,
                "Hd2dLightingVfx",
                root =>
                {
                    UiBuild.Stretch(root.GetComponent<RectTransform>());
                    var lighting = root.AddComponent<Hd2dLightingVfx>();
                    lighting.ParticleLayer = CreateLayer("ParticleLayer", root.transform);
                    ConfigureParticleDefaults(lighting);
                },
                root =>
                {
                    ConfigureParticleMaterials(root.GetComponent<Hd2dLightingVfx>());
                    // Older versions also carry small lights; they draw computed too.
                    foreach (var point in root.GetComponentsInChildren<Hd2dVfxLightPoint>(true))
                        ConfigureLightPoint(point);
                }
            );
        }

        private static void EnsureLightPointPrefab()
        {
            EnsurePrefabAsset(
                LightPointPrefabPath,
                "Hd2dVfxLightPoint",
                root =>
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

                    var glow = CreateImage("Glow", root.transform);
                    var ray = CreateImage("Ray", root.transform);
                    glow.rectTransform.anchorMin = glow.rectTransform.anchorMax =
                        Vector2.one * 0.5f;
                    glow.rectTransform.pivot = Vector2.one * 0.5f;
                    ray.rectTransform.anchorMin = ray.rectTransform.anchorMax =
                        Vector2.one * 0.5f;
                    ray.rectTransform.pivot = new Vector2(0f, 0.5f);
                    point.Glow = glow;
                    point.Ray = ray;
                },
                root =>
                {
                    var point = root.GetComponent<Hd2dVfxLightPoint>();
                    ConfigureLightPoint(point);
                    point.ApplyVisual(0f);
                }
            );
        }

        private static void ConfigureLightPoint(Hd2dVfxLightPoint point)
        {
            ConfigureComputedImage(point.Glow, GlowHaloMaterialPath);
            ConfigureComputedImage(point.Ray, LightRayMaterialPath);
        }

        // An image drawn by a computed material, without a picture.
        private static void ConfigureComputedImage(UnityEngine.UI.Image image, string materialPath)
        {
            if (image == null)
                return;
            // Image.sprite = null is skipped when the old picture is already missing (it compares
            // equal to null), so the reference is cleared through the serialized property.
            var serialized = new SerializedObject(image);
            serialized.FindProperty("m_Sprite").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            image.material = LoadMaterial(materialPath);
            image.preserveAspect = false;
        }

        private static void EnsureParticleFieldPrefab()
        {
            EnsurePrefabAsset(
                ParticleFieldPrefabPath,
                "Hd2dParticleField",
                root =>
                {
                    UiBuild.Stretch(root.GetComponent<RectTransform>());
                    var lighting = root.AddComponent<Hd2dLightingVfx>();
                    lighting.ParticleLayer = CreateLayer("ParticleLayer", root.transform);
                    ConfigureParticleDefaults(lighting);
                },
                root => ConfigureParticleMaterials(root.GetComponent<Hd2dLightingVfx>())
            );
        }

        private static void ConfigureParticleMaterials(Hd2dLightingVfx lighting)
        {
            lighting.DustMaterial = LoadMaterial(DustMaterialPath);
            lighting.SparkleMaterial = LoadMaterial(SparkleMaterialPath);
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
            UiBuild.Stretch(rect);
            return rect;
        }

        private static UnityEngine.UI.Image CreateImage(string name, Transform parent)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }
    }
}
