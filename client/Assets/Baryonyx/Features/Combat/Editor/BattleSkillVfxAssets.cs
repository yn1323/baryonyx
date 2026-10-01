using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.UI;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// The skill effects' parts on a battle screen: the darkening veil and the effect layers on
    /// the stage, the full-screen flash and the cut-in over it, and their pictures. Also makes
    /// the showcase's preview (BattleSkillVfxPreview.prefab), which plays every skill in turn.
    /// The pictures are made outside Unity (doc/features/screens.md, the skill effects).
    /// </summary>
    public static class BattleSkillVfxAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Combat/Vfx";
        public const string TextureFolder = Folder + "/Textures";
        public const string PreviewPrefabPath = Folder + "/BattleSkillVfxPreview.prefab";
        public const string MaterialFolder = Folder + "/Materials";
        public const string ShaderPath = Folder + "/Shaders/BattleVfx.shader";

        /// <summary>The shock ring worked out by its shader with no picture, on trial against VfxShockwave.png.</summary>
        public const string RingShaderPath = Folder + "/Shaders/BattleVfxRing.shader";

        /// <summary>The showcase's side-by-side of the picture's ring and the shader's ring.</summary>
        public const string RingComparisonPrefabPath = Folder + "/BattleRingComparison.prefab";

        /// <summary>The battlefield's post-processing: Bloom on the effects' light, and a vignette.</summary>
        public const string PostProcessProfilePath = Folder + "/BattlePostProcess.asset";

        // The streaks of the cut-in scroll, so their picture repeats.
        private const string CutInStreaksName = "VfxCutInStreaks";

        // The cut-in's band across the middle of the screen, a little above the centre.
        private const float CutInHeight = 280f;
        private const float CutInY = 90f;

        // The user is drawn at 8 px per dot in the cut-in, its head and chest inside the band.
        private const float CutInDot = 8f;

        /// <summary>Imports the effect pictures (smooth light, not pixel art) and loads them.</summary>
        public static BattleSkillVfxTextures EnsureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
                Import(AssetDatabase.GUIDToAssetPath(guid));
            return new BattleSkillVfxTextures
            {
                SlashArc = Load("VfxSlashArc"),
                SlashCross = Load("VfxSlashCross"),
                Fireball = Load("VfxFireball"),
                IceSpear = Load("VfxIceSpear"),
                Lightning = Load("VfxLightning"),
                HealPillar = Load("VfxHealPillar"),
                ShieldDome = Load("VfxShieldDome"),
                MagicCircle = Load("VfxMagicCircle"),
                ImpactStar = Load("VfxImpactStar"),
                Shockwave = Load("VfxShockwave"),
                Streak = Load("VfxStreak"),
                Smoke = Load("VfxSmoke"),
                Shard = Load("VfxShard"),
                CutInStreaks = Load(CutInStreaksName),
                Glow = ArtAssets.LoadTexture(Hd2dAssets.GlowTexturePath),
                Sparkle = ArtAssets.LoadTexture(Hd2dAssets.SparkleTexturePath),
                // Realistic flame and smoke, two shapes side by side in each.
                Flames = Load("VfxFlames"),
                FlameCells = new Vector2Int(2, 1),
                FlameTongue = Load("VfxFlameTongue"),
                Smokes = Load("VfxSmokes"),
                SmokeCells = new Vector2Int(2, 1),
                Dust = Load("VfxDust"),
                Scorch = Load("VfxScorch"),
                // Realistic ice: three spikes side by side, broken pieces, cold mist, frost.
                IceSpikes = Load("VfxIceSpikes"),
                IceSpikeCells = new Vector2Int(3, 1),
                IceShards = Load("VfxIceShards"),
                FrostMist = Load("VfxFrostMist"),
                Frost = Load("VfxFrost"),
            };
        }

        /// <summary>
        /// Smooth light: bilinear and compressed, no mipmaps (they are shown near their size).
        /// Set only on a change, so a rebuild does not reimport every picture.
        /// </summary>
        private static void Import(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;
            var wrap = path.Contains(CutInStreaksName)
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;
            if (
                importer.textureType == TextureImporterType.Default
                && importer.filterMode == FilterMode.Bilinear
                && !importer.mipmapEnabled
                && importer.alphaIsTransparency
                && importer.wrapMode == wrap
                && importer.maxTextureSize == 512
                && importer.textureCompression == TextureImporterCompression.Compressed
            )
                return;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        /// <summary>The effects' materials, made from the battle effect shader when missing.</summary>
        public static (
            Material glow,
            Material erode,
            Material core,
            Material smoke
        ) EnsureMaterials()
        {
            AssetFolders.Ensure(MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + ShaderPath);
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(Hd2dAssets.FogNoiseTexturePath);
            return (
                EnsureMaterial(
                    "VfxGlow",
                    shader,
                    noise,
                    erode: false,
                    intensity: 1f,
                    additive: true
                ),
                EnsureMaterial(
                    "VfxErode",
                    shader,
                    noise,
                    erode: true,
                    intensity: 1.2f,
                    additive: true
                ),
                // Brighter than white, so the camera's Bloom spreads only these cores.
                EnsureMaterial(
                    "VfxCore",
                    shader,
                    noise,
                    erode: true,
                    intensity: 1.8f,
                    additive: true
                ),
                EnsureMaterial(
                    "VfxSmoke",
                    shader,
                    noise,
                    erode: true,
                    intensity: 1f,
                    additive: false
                )
            );
        }

        private static Material EnsureMaterial(
            string name,
            Shader shader,
            Texture2D noise,
            bool erode,
            float intensity,
            bool additive
        )
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Erode", erode ? 1f : 0f);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Softness", additive ? 0.12f : 0.2f);
            material.SetFloat("_EdgeGlow", additive ? 0.6f : 0f);
            material.SetTexture("_NoiseTex", noise);
            material.SetFloat("_NoiseScale", 2f);
            material.SetFloat("_NoiseAmount", additive ? 0.3f : 0.45f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha)
            );
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The material of the shader's shock ring, with the shader's own defaults: they are the
        /// ring's settings, so a rebuild brings the material back to them.
        /// </summary>
        public static Material EnsureRingMaterial()
        {
            AssetFolders.Ensure(MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(RingShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + RingShaderPath);
            const string path = MaterialFolder + "/VfxRingProcedural.mat";
            var defaults = new Material(shader) { name = "VfxRingProcedural" };
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                AssetDatabase.CreateAsset(defaults, path);
                return defaults;
            }
            material.shader = shader;
            material.CopyPropertiesFromMaterial(defaults);
            UnityEngine.Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The battlefield's post-processing, made with the defaults when missing and then left to
        /// be tuned. Bloom starts at 1, above any picture's own colour, so only the effects' light
        /// (bright cores and stacked additive light, past white on the HDR camera) spreads; the
        /// pixel art and the background stay crisp. A light vignette draws the eye to the middle.
        /// </summary>
        public static VolumeProfile EnsurePostProcessProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
            if (existing != null)
                return existing;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessProfilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1f;
            bloom.intensity.value = 1.4f;
            bloom.scatter.value = 0.7f;
            bloom.tint.value = Color.white;
            bloom.clamp.value = 65472f;
            bloom.highQualityFiltering.value = false;
            bloom.downscale.value = BloomDownscaleMode.Half;
            bloom.maxIterations.value = 6;
            var vignette = profile.Add<Vignette>(true);
            vignette.color.value = Color.black;
            vignette.center.value = new Vector2(0.5f, 0.5f);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.45f;
            vignette.rounded.value = false;
            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// Turns a canvas into the battlefield's canvas: drawn by the scene's camera through
        /// <see cref="PostProcessProfilePath"/>, with no raycasts (the controls take the taps).
        /// </summary>
        public static void MakeStageCanvas(RectTransform canvasRect)
        {
            var canvas = canvasRect.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 10f;
            var raycaster = canvasRect.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                UnityEngine.Object.DestroyImmediate(raycaster);
            canvasRect.gameObject.AddComponent<BattleStageCamera>();
            var volume = Rect("PostProcess", canvasRect).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsurePostProcessProfile();
        }

        private static Texture2D Load(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{name}.png");
            if (texture == null)
                throw new InvalidOperationException($"Missing effect picture: {name}");
            return texture;
        }

        /// <summary>
        /// A layer for effects in the world's space: the 1920x1080 design area in the middle,
        /// shrunk with the world on narrow screens.
        /// </summary>
        public static RectTransform Layer(string name, RectTransform parent)
        {
            var layer = Rect(name, parent);
            layer.anchorMin = layer.anchorMax = layer.pivot = Vector2.one * 0.5f;
            layer.sizeDelta = new Vector2(1920, 1080);
            layer.gameObject.AddComponent<WorldLayerFit>();
            return layer;
        }

        /// <summary>
        /// A rect a little larger than the screen, so the background still covers it while the
        /// stage shakes.
        /// </summary>
        public static RectTransform Overscan(string name, RectTransform parent)
        {
            var rect = Rect(name, parent);
            Stretch(rect);
            rect.offsetMin = -Vector2.one * 48f;
            rect.offsetMax = Vector2.one * 48f;
            return rect;
        }

        /// <summary>The veil that darkens the background (not the actors) while a skill plays.</summary>
        public static Graphic Dim(RectTransform stage)
        {
            var dim = Overscan("Dim", stage);
            // A night blue, not black, so the background keeps its air while it darkens.
            var image = AddImage(dim, new Color(0.02f, 0.04f, 0.11f, 0f), false);
            image.enabled = false;
            return image;
        }

        /// <summary>
        /// Puts the effects on a screen: the flash and the cut-in go over the stage (under what is
        /// added later, such as the controls), and the player is set up with its parts.
        /// </summary>
        public static BattleSkillVfx Attach(
            RectTransform root,
            RectTransform stage,
            Graphic dim,
            RectTransform back,
            RectTransform front
        )
        {
            var (glow, erode, core, smoke) = EnsureMaterials();
            var textures = EnsureTextures();

            var flash = Rect("Flash", root);
            Stretch(flash);
            var flashImage = flash.gameObject.AddComponent<RawImage>();
            flashImage.material = glow;
            flashImage.raycastTarget = false;
            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.enabled = false;

            var vfx = root.gameObject.AddComponent<BattleSkillVfx>();
            vfx.Stage = stage;
            vfx.Dim = dim;
            vfx.BackLayer = back;
            vfx.FrontLayer = front;
            vfx.Flash = flashImage;
            vfx.GlowMaterial = glow;
            vfx.ErodeMaterial = erode;
            vfx.CoreMaterial = core;
            vfx.SmokeMaterial = smoke;
            vfx.Textures = textures;
            BuildCutIn(root, vfx, textures, glow);
            return vfx;
        }

        /// <summary>
        /// The cut-in: a dark band, tilted a little, with streaks racing across it, the user drawn
        /// large on the left (cut off by the band) and the skill's name on the right, between
        /// thin gold edges.
        /// </summary>
        private static void BuildCutIn(
            RectTransform root,
            BattleSkillVfx vfx,
            BattleSkillVfxTextures textures,
            Material additive
        )
        {
            var cutIn = Rect("CutIn", root);
            Place(cutIn, new Vector2(0, CutInY), new Vector2(2600, CutInHeight));
            cutIn.localEulerAngles = new Vector3(0, 0, 4f);

            var band = Rect("Band", cutIn);
            Stretch(band);
            var bandImage = band.gameObject.AddComponent<RawImage>();
            bandImage.raycastTarget = false;
            bandImage.color = new Color(0.04f, 0.05f, 0.1f, 0.9f);
            band.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var streaks = Rect("Streaks", band);
            Stretch(streaks);
            var streakImage = streaks.gameObject.AddComponent<RawImage>();
            streakImage.texture = textures.CutInStreaks;
            streakImage.material = additive;
            streakImage.uvRect = new UnityEngine.Rect(0, 0, 5f, 1f);
            streakImage.raycastTarget = false;

            var actor = Rect("Actor", band);
            actor.anchorMin = actor.anchorMax = new Vector2(0.5f, 0.5f);
            actor.pivot = new Vector2(0.5f, 0f);
            actor.sizeDelta = Vector2.one * 64f * CutInDot;
            // Feet below the band, so the band shows the head and chest.
            actor.anchoredPosition = new Vector2(-560f, -CutInHeight * 0.5f - 20f * CutInDot);
            var actorImage = actor.gameObject.AddComponent<RawImage>();
            actorImage.raycastTarget = false;

            foreach (float y in new[] { 0.5f, -0.5f })
            {
                var edge = Rect(y > 0 ? "EdgeTop" : "EdgeBottom", cutIn);
                edge.anchorMin = new Vector2(0, 0.5f + y);
                edge.anchorMax = new Vector2(1, 0.5f + y);
                edge.pivot = new Vector2(0.5f, 0.5f);
                edge.sizeDelta = new Vector2(0, 6);
                edge.anchoredPosition = Vector2.zero;
                AddImage(edge, new Color(0.98f, 0.8f, 0.36f), false);
            }

            var name = Label(
                cutIn,
                "SkillName",
                "サンダー",
                96,
                Color.white,
                TextAlignmentOptions.Left
            );
            Place(name.rectTransform, new Vector2(220, 0), new Vector2(900, 140));
            name.outlineWidth = 0.18f;
            name.outlineColor = new Color(0.05f, 0.04f, 0.1f);

            vfx.CutIn = cutIn;
            vfx.CutInBand = bandImage;
            vfx.CutInStreaks = streakImage;
            vfx.CutInActor = actorImage;
            vfx.CutInName = name;
            cutIn.gameObject.SetActive(false);
        }

        /// <summary>
        /// The showcase's preview: the battle background with the party on the left and the
        /// enemies on the right, and <see cref="BattleSkillVfxDemo"/> playing every skill in turn
        /// with the card's user and targets.
        /// </summary>
        public static void BuildPreview(Texture2D background, Func<string, Texture2D> art)
        {
            var root = CanvasRoot("BattleSkillVfxPreview");
            var stage = Rect("Stage", root);
            Stretch(stage);
            var backdrop = Overscan("Backdrop", stage);
            var image = Rect("Background", backdrop).gameObject.AddComponent<RawImage>();
            image.texture = background;
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio =
                background.width / (float)background.height;
            var dim = Dim(stage);
            var back = Layer("VfxBack", stage);
            var world = Layer("World", stage);
            var front = Layer("VfxFront", stage);

            RectTransform Actor(string name, Vector2 feet, float dot)
            {
                var texture = art(name);
                var sprite = PixelActor(world, name, texture, feet, dot);
                return sprite.rectTransform;
            }
            var aria = Actor("BattleAria", new Vector2(-420, -220), 4f);
            var toma = Actor("BattleToma", new Vector2(-420, 40), 4f);
            var luka = Actor("BattleLuka", new Vector2(-690, -100), 4f);
            var mina = Actor("BattleMina", new Vector2(-690, 170), 4f);
            var slime = Actor("MossSlime", new Vector2(400, -170), 3f);
            var wolf = Actor("MossWolf", new Vector2(400, 100), 3f);
            var guardian = Actor("ForestGuardian", new Vector2(790, -40), 3f);

            var vfx = Attach(root, stage, dim, back, front);
            var demo = root.gameObject.AddComponent<BattleSkillVfxDemo>();
            demo.Vfx = vfx;
            demo.Actors = new[] { aria, toma, luka, mina, slime, wolf, guardian }
                .Select(actor => actor.GetComponent<RawImage>())
                .ToArray();
            Texture Art(RectTransform actor) => actor.GetComponent<RawImage>().texture;
            demo.Casts = new[]
            {
                Cast(BattleSkillVfxKind.Slash, "斬り払い", aria, Art(aria), false, wolf),
                Cast(BattleSkillVfxKind.Fire, "ファイア", toma, Art(toma), false, guardian),
                Cast(BattleSkillVfxKind.Ice, "アイスランス", toma, Art(toma), true, wolf),
                Cast(
                    BattleSkillVfxKind.Thunder,
                    "サンダー",
                    luka,
                    Art(luka),
                    true,
                    slime,
                    wolf,
                    guardian
                ),
                Cast(BattleSkillVfxKind.Heal, "ヒール", mina, Art(mina), false, aria),
                Cast(
                    BattleSkillVfxKind.Guard,
                    "ガード",
                    aria,
                    Art(aria),
                    false,
                    toma,
                    mina,
                    aria,
                    luka
                ),
            };
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PreviewPrefabPath);
        }

        /// <summary>
        /// The showcase's comparison of the shock ring: the picture's ring on the left and the
        /// shader's ring on the right, on the battle background, each standing up (to see its
        /// shape) and lying on the floor under an enemy (as a blow on a weakness shows it).
        /// <see cref="BattleRingComparison"/> plays them all together.
        /// </summary>
        public static void BuildRingComparison(Texture2D background, Func<string, Texture2D> art)
        {
            var (glow, _, _, _) = EnsureMaterials();
            var textures = EnsureTextures();
            var procedural = EnsureRingMaterial();

            var root = CanvasRoot("BattleRingComparison");
            var backdrop = Rect("Backdrop", root);
            Stretch(backdrop);
            var image = Rect("Background", backdrop).gameObject.AddComponent<RawImage>();
            image.texture = background;
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio =
                background.width / (float)background.height;
            var world = Layer("World", root);

            var divider = Rect("Divider", world);
            Place(divider, Vector2.zero, new Vector2(4, 1000));
            AddImage(divider, new Color(1f, 1f, 1f, 0.3f), false);

            RawImage RingImage(
                string name,
                Vector2 at,
                float size,
                Texture texture,
                Material material
            )
            {
                var rect = Rect(name, world);
                Place(rect, at, new Vector2(size, size));
                var ring = rect.gameObject.AddComponent<RawImage>();
                ring.texture = texture;
                ring.material = material;
                ring.raycastTarget = false;
                return ring;
            }

            var rings = new List<BattleRingComparison.Ring>();
            void Side(float x, string title, Texture texture, Material material)
            {
                var label = Label(
                    world,
                    "Title",
                    title,
                    44,
                    Color.white,
                    TextAlignmentOptions.Center
                );
                Place(label.rectTransform, new Vector2(x, 470), new Vector2(900, 70));
                rings.Add(
                    new BattleRingComparison.Ring
                    {
                        Image = RingImage(
                            "StandingRing",
                            new Vector2(x, 170),
                            460f,
                            texture,
                            material
                        ),
                        Floor = false,
                    }
                );
                // The floor ring goes behind the enemy standing on it, as on the battlefield.
                var feet = new Vector2(x, -300);
                rings.Add(
                    new BattleRingComparison.Ring
                    {
                        Image = RingImage("FloorRing", feet, 640f, texture, material),
                        Floor = true,
                    }
                );
                PixelActor(world, "MossWolf", art("MossWolf"), feet, 3f);
            }
            Side(-480f, "画像（VfxShockwave.png）", textures.Shockwave, glow);
            Side(480f, "計算（シェーダー・画像なし）", null, procedural);

            var comparison = root.gameObject.AddComponent<BattleRingComparison>();
            comparison.Rings = rings.ToArray();
            comparison.Show(0f);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, RingComparisonPrefabPath);
        }

        private static BattleSkillVfxDemo.Cast Cast(
            BattleSkillVfxKind kind,
            string name,
            RectTransform caster,
            Texture art,
            bool cutIn,
            params RectTransform[] targets
        ) =>
            new()
            {
                Kind = kind,
                Name = name,
                Caster = caster,
                CasterArt = art,
                CutIn = cutIn,
                Targets = targets,
            };
    }
}
