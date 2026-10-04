using System;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.UI;
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
    /// the stage, the full-screen flash and the cut-in over it, and the materials their shapes are
    /// worked out with (no pictures: doc/art/direction.md, how effects are drawn). Also makes the
    /// showcase's preview (BattleSkillVfxPreview.prefab), which plays every skill in turn.
    /// </summary>
    public static class BattleSkillVfxAssets
    {
        public const string Folder = "Assets/Baryonyx/Features/Combat/Vfx";
        public const string PreviewPrefabPath = Folder + "/BattleSkillVfxPreview.prefab";
        public const string MaterialFolder = Folder + "/Materials";

        /// <summary>A character's picture drawn as additive light (the light on the actors, the flash).</summary>
        public const string ShaderPath = Folder + "/Shaders/BattleVfx.shader";

        /// <summary>Every effect's shape, worked out with no picture.</summary>
        public const string ShapeShaderPath = Folder + "/Shaders/BattleVfxShape.shader";

        /// <summary>The light over a weakness icon as it is revealed, in the icon's own shape.</summary>
        public const string GlintShaderPath = Folder + "/Shaders/BattleWeaknessGlint.shader";

        /// <summary>A beaten enemy's picture, eaten away dot by dot.</summary>
        public const string DefeatShaderPath = Folder + "/Shaders/BattleDefeat.shader";

        /// <summary>The showcase's preview of the weaknesses' glint as they are revealed.</summary>
        public const string WeaknessRevealPreviewPath =
            Folder + "/BattleWeaknessRevealPreview.prefab";

        /// <summary>The battlefield's post-processing: Bloom on the effects' light, and a vignette.</summary>
        public const string PostProcessProfilePath = Folder + "/BattlePostProcess.asset";

        // The cut-in's band across the middle of the screen, a little above the centre.
        private const float CutInHeight = 280f;
        private const float CutInY = 90f;

        // The user is drawn at 8 px per dot in the cut-in, its head and chest inside the band.
        private const float CutInDot = 8f;

        /// <summary>
        /// The additive light drawn in the shape of a character's picture (the light the effects
        /// throw on the actors) and over the whole screen (the flash).
        /// </summary>
        public static Material EnsureGlowMaterial()
        {
            AssetFolders.Ensure(MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + ShaderPath);
            const string path = MaterialFolder + "/VfxGlow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "VfxGlow" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Intensity", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// One material per shape (in <see cref="BattleVfxShape"/>'s order), made from the shape
        /// shader when missing: the shape's keyword on, solid shapes blended normally and the rest
        /// added as light. The ring is drawn a little brighter, as it was tuned.
        /// </summary>
        public static Material[] EnsureShapeMaterials()
        {
            AssetFolders.Ensure(MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShapeShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + ShapeShaderPath);
            var shapes = (BattleVfxShape[])Enum.GetValues(typeof(BattleVfxShape));
            var materials = new Material[shapes.Length];
            foreach (var shape in shapes)
            {
                var name = "VfxShape" + shape;
                var path = $"{MaterialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                foreach (var other in shapes)
                    material.DisableKeyword(KeywordOf(other));
                material.EnableKeyword(KeywordOf(shape));
                material.SetFloat("_Shape", (int)shape);
                material.SetFloat("_Intensity", shape == BattleVfxShape.Ring ? 1.3f : 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat(
                    "_DstBlend",
                    (float)(Solid(shape) ? BlendMode.OneMinusSrcAlpha : BlendMode.One)
                );
                EditorUtility.SetDirty(material);
                materials[(int)shape] = material;
            }
            return materials;
        }

        /// <summary>The light that glints over a weakness icon as it is revealed, added to it.</summary>
        public static Material EnsureGlintMaterial()
        {
            AssetFolders.Ensure(MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GlintShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + GlintShaderPath);
            const string path = MaterialFolder + "/VfxWeaknessGlint.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "VfxWeaknessGlint" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The picture of a beaten enemy as it crumbles away, dot by dot.</summary>
        public static Material EnsureDefeatMaterial()
        {
            AssetFolders.Ensure(MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DefeatShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + DefeatShaderPath);
            const string path = MaterialFolder + "/VfxDefeat.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "VfxDefeat" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The shader keyword that picks a shape (the shader's _Shape keyword enum).</summary>
        public static string KeywordOf(BattleVfxShape shape) =>
            "_SHAPE_" + shape.ToString().ToUpperInvariant();

        /// <summary>Smoke, scorch, frost, ice and chips cover what is behind them; the rest is light.</summary>
        public static bool Solid(BattleVfxShape shape) =>
            shape
                is BattleVfxShape.Smoke
                    or BattleVfxShape.Scorch
                    or BattleVfxShape.Frost
                    or BattleVfxShape.IceSpike
                    or BattleVfxShape.Chip;

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
            // Close to the camera, so a 3D stage behind it never hides the UI (UI tests depth).
            canvas.planeDistance = 1f;
            var raycaster = canvasRect.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                UnityEngine.Object.DestroyImmediate(raycaster);
            // The effects' shapes take their seed and moment from the second UV.
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            canvasRect.gameObject.AddComponent<BattleStageCamera>();
            var volume = Rect("PostProcess", canvasRect).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsurePostProcessProfile();
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
            var glow = EnsureGlowMaterial();
            var shapes = EnsureShapeMaterials();

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
            vfx.Shapes = shapes;
            vfx.DefeatMaterial = EnsureDefeatMaterial();
            BuildCutIn(root, vfx, shapes[(int)BattleVfxShape.CutInStreaks]);
            return vfx;
        }

        /// <summary>
        /// The cut-in: a dark band, tilted a little, with streaks racing across it, the user drawn
        /// large on the left (cut off by the band) and the skill's name on the right, between
        /// thin gold edges.
        /// </summary>
        private static void BuildCutIn(RectTransform root, BattleSkillVfx vfx, Material streakShape)
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
            // The streaks are worked out from the UV, so scrolling the uvRect moves them on.
            var streakImage = streaks.gameObject.AddComponent<RawImage>();
            streakImage.material = streakShape;
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
        /// enemies on the right, and <see cref="BattleSkillVfxDemo"/> playing every card skill's
        /// effect in turn with its user and targets, its name at the top.
        /// </summary>
        public static void BuildPreview(Texture2D background, Func<string, Texture2D> art)
        {
            var root = CanvasRoot("BattleSkillVfxPreview");
            root.GetComponent<Canvas>().additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1;
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
            // The party in the cards' users' order: Aria, Toma, Luka, Mina.
            demo.Party = new[] { aria, toma, luka, mina };
            demo.Enemies = new[] { slime, wolf, guardian };
            demo.Actors = new[] { aria, toma, luka, mina, slime, wolf, guardian }
                .Select(actor => actor.GetComponent<RawImage>())
                .ToArray();
            // Which card plays now, at the top, over the effects.
            var caption = Label(root, "Caption", "", 44, Color.white, TextAlignmentOptions.Center);
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(
                0.5f,
                1f
            );
            caption.rectTransform.pivot = new Vector2(0.5f, 1f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            caption.rectTransform.sizeDelta = new Vector2(1200f, 70f);
            caption.outlineWidth = 0.2f;
            caption.outlineColor = new Color(0.05f, 0.04f, 0.1f);
            demo.Caption = caption;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PreviewPrefabPath);
        }
    }
}
