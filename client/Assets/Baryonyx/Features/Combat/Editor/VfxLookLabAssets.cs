using System;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Inventory.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// The look lab's screen (VfxLookLabScreen.prefab), a debugging room for the look the effects
    /// are built on (<see cref="VfxLookLab"/>): the battle's stage with one enemy and the sample
    /// over it, drawn through the battle's bloom, and over it the lab's controls. The way back to
    /// home, the forms, and play and the enemy go down the left; the tints along the top; the
    /// looks to start from and the bar of the sample's moment along the bottom; and on the right
    /// a panel with a slider for every value of the look (<see cref="VfxLook.Specs"/>), with the
    /// button that copies them under it.
    /// </summary>
    public static class VfxLookLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/VfxLookLabScreen.prefab";

        /// <summary>The sample's shader, worked out with no picture from the look's values.</summary>
        public const string ShaderPath = BattleSkillVfxAssets.Folder + "/Shaders/VfxLookLab.shader";

        public const string MaterialPath = BattleSkillVfxAssets.MaterialFolder + "/VfxLookLab.mat";

        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);

        // The buttons are as large as a finger needs (doc/rules/ui-design.md).
        private const float ButtonHeight = UiSpace.Hit;
        private const float ButtonGap = 10f;
        private const float Margin = 12f;
        private const float ColumnX = 60f;
        private const float ColumnWidth = 200f;

        // The stage left free between the column on the left and the panel on the right.
        private const float PanelX = 1300f;
        private const float PanelWidth = 560f;
        private const float FreeLeft = ColumnX + ColumnWidth + 36f;
        private const float FreeWidth = PanelX - 36f - FreeLeft;

        // The sample, in the middle of the free part of the stage, over the enemy's body.
        private static readonly Vector2 SampleCenter = new(FreeLeft + FreeWidth * 0.5f - 960f, 0f);
        private const float SampleSize = 680f;
        private static readonly Vector2 EnemyFeet = new(SampleCenter.x, -80f);
        private const string EnemyArt = "MossWolf";

        private const float RowHeight = 64f;
        private const float GroupHeight = 46f;
        private const float PanelHeight = 1080f - Margin - 20f - ButtonHeight - Margin;

        [MenuItem("Baryonyx/Combat/Create Vfx Look Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();

            UiArt.EnsureAll();
            var material = EnsureMaterial();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            using (UiBuild.Begin(font, shadowText))
            {
                var root = Rect("VfxLookLabScreen", null);
                Stretch(root);
                var lab = root.gameObject.AddComponent<VfxLookLab>();
                lab.LookMaterial = material;
                // The stage on the scene's camera, so the sample's light goes through Bloom, and
                // the controls on an overlay canvas over it, as in the battle.
                var stageCanvas = CanvasRoot("StageCanvas");
                stageCanvas.SetParent(root, false);
                BattleSkillVfxAssets.MakeStageCanvas(stageCanvas);
                var screen = CanvasRoot("ScreenCanvas");
                screen.SetParent(root, false);
                screen.GetComponent<Canvas>().sortingOrder = 1;

                var stage = Rect("Stage", stageCanvas);
                Stretch(stage);
                BuildBackground(stage);
                var dim = BattleSkillVfxAssets.Dim(stage);
                dim.enabled = true;
                lab.Backdrop = dim;
                lab.FloorLight = BuildFloorLight(BattleSkillVfxAssets.Layer("VfxBack", stage));
                lab.ActorLightMaterial = BattleSkillVfxAssets.EnsureGlowMaterial();
                var world = BattleSkillVfxAssets.Layer("World", stage);
                lab.Actor = BuildEnemy(world);
                var front = BattleSkillVfxAssets.Layer("VfxFront", stage);
                Hd2dStageKit.SortLayer(world, Hd2dStageKit.BoardOrder + 10);
                Hd2dStageKit.SortLayer(front, Hd2dStageKit.BoardOrder + 11);
                lab.Wake = BuildWake(front, material);
                lab.Board = BuildSample(front, material);
                lab.FloorHeight = (EnemyFeet.y - SampleCenter.y) / (SampleSize * 0.5f);

                var frame = Layout.Layer(SafeArea(screen), "Frame");
                BuildColumn(frame, lab);
                BuildTints(frame, lab);
                BuildBottom(frame, lab);
                BuildPanel(frame, lab);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The sample's material, made from the look shader when missing and set to the look the
        /// lab opens with, so the stopped scene and the showcase show that look too.
        /// </summary>
        public static Material EnsureMaterial()
        {
            AssetFolders.Ensure(BattleSkillVfxAssets.MaterialFolder);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException("Missing shader: " + ShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "VfxLookLab" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            var look = VfxLook.Preset(VfxLook.DefaultPreset);
            for (int i = 0; i < VfxLook.Count; i++)
                if (VfxLook.Specs[i].Property != null)
                    material.SetFloat(VfxLook.Specs[i].Property, look[i]);
            material.SetFloat("_Form", (int)VfxLookForm.Orb);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The painted battle background, shown only where there is no 3D stage (as in the
        /// showcase).
        /// </summary>
        private static void BuildBackground(RectTransform stage)
        {
            var background = BattleInspectAssets.Art("BattleBackground");
            var backdrop = BattleSkillVfxAssets.Overscan("Backdrop", stage);
            var image = Rect("Background", backdrop).gameObject.AddComponent<RawImage>();
            image.texture = background;
            image.material = BattleInspectAssets.EnsurePixelArtMaterial();
            image.raycastTarget = false;
            image.gameObject.AddComponent<ResponsiveBackground>().AspectRatio =
                background.width / (float)background.height;
            Hd2dStageKit.FlatOnly(image.gameObject);
        }

        /// <summary>The enemy the sample is laid over, standing on the 3D stage as in battle.</summary>
        private static GameObject BuildEnemy(RectTransform world)
        {
            var sprite = PixelActor(
                world,
                "Enemy" + EnemyArt,
                BattleInspectAssets.Art(EnemyArt),
                EnemyFeet,
                BattleInspectAssets.EnemyDotSize
            );
            sprite.material = BattleInspectAssets.EnsurePixelArtMaterial();
            Hd2dStageKit.Stand(sprite.gameObject, sprite, null);
            return sprite.gameObject;
        }

        /// <summary>
        /// The pool of light the sample throws on the floor: the battle's soft round light, laid
        /// flat under the enemy's feet and drawn behind the enemy.
        /// </summary>
        private static BattleVfxImage BuildFloorLight(RectTransform back)
        {
            var rect = Rect("FloorLight", back);
            Place(rect, EnemyFeet + new Vector2(0f, 10f), new Vector2(1040f, 300f));
            var pool = rect.gameObject.AddComponent<BattleVfxImage>();
            pool.material = BattleSkillVfxAssets.EnsureShapeMaterials()[(int)BattleVfxShape.Glow];
            pool.color = Color.clear;
            pool.raycastTarget = false;
            pool.enabled = false;
            return pool;
        }

        /// <summary>
        /// The board the wake along the floor is drawn on, under the sample; the lab moves it to
        /// where the form meets the floor.
        /// </summary>
        private static BattleVfxImage BuildWake(RectTransform front, Material material)
        {
            var rect = Rect("Wake", front);
            Place(rect, EnemyFeet, new Vector2(900f, 200f));
            var wake = rect.gameObject.AddComponent<BattleVfxImage>();
            wake.material = material;
            wake.color = VfxLook.Tints[0].Color;
            wake.raycastTarget = false;
            wake.enabled = false;
            return wake;
        }

        private static BattleVfxImage BuildSample(RectTransform front, Material material)
        {
            var rect = Rect("Sample", front);
            Place(rect, SampleCenter, Vector2.one * SampleSize);
            var board = rect.gameObject.AddComponent<BattleVfxImage>();
            board.material = material;
            board.color = VfxLook.Tints[0].Color;
            board.raycastTarget = false;
            return board;
        }

        /// <summary>
        /// Down the left: the way back to home, then the forms (the one picked rimmed in gold),
        /// and whether the sample plays and whether the enemy stands under it.
        /// </summary>
        private static void BuildColumn(RectTransform frame, VfxLookLab lab)
        {
            float y = Margin;
            lab.HomeButton = ColumnButton(frame, "Home", "ホームへ", ref y).Button;

            y += 10f;
            int forms = VfxLook.FormNames.Length;
            lab.FormButtons = new Button[forms];
            lab.FormSelected = new GameObject[forms];
            for (int i = 0; i < forms; i++)
            {
                var form = ColumnButton(
                    frame,
                    "Form" + (VfxLookForm)i,
                    VfxLook.FormNames[i],
                    ref y
                );
                lab.FormButtons[i] = form.Button;
                lab.FormSelected[i] = form.Selected;
            }

            y += 10f;
            var play = ColumnButton(frame, "Play", VfxLookLab.PlayingText, ref y);
            lab.PlayButton = play.Button;
            lab.PlayLabel = play.Label;
            var actor = ColumnButton(frame, "Actor", VfxLookLab.ActorShownText, ref y);
            lab.ActorButton = actor.Button;
            lab.ActorLabel = actor.Label;
        }

        private static (Button Button, GameObject Selected, TMP_Text Label) ColumnButton(
            RectTransform frame,
            string name,
            string text,
            ref float y
        )
        {
            var made = TextButton(frame, name, text, ColumnX, y, ColumnWidth, ButtonHeight);
            y += ButtonHeight + ButtonGap;
            return made;
        }

        /// <summary>Along the top of the free stage: the tints, the one picked rimmed in gold.</summary>
        private static void BuildTints(RectTransform frame, VfxLookLab lab)
        {
            int tints = VfxLook.Tints.Length;
            const float gap = 12f;
            float left = FreeLeft + (FreeWidth - (ButtonHeight * tints + gap * (tints - 1))) * 0.5f;
            lab.TintButtons = new Button[tints];
            lab.TintSelected = new GameObject[tints];
            for (int i = 0; i < tints; i++)
            {
                var chip = Layout.Box(
                    frame,
                    "Tint" + i,
                    left + i * (ButtonHeight + gap),
                    Margin,
                    ButtonHeight,
                    ButtonHeight
                );
                var (button, selected, content) = LabCard(chip);
                var swatch = Rect("Swatch", content);
                Stretch(swatch);
                swatch.offsetMin = Vector2.one * 30f;
                swatch.offsetMax = -Vector2.one * 30f;
                AddImage(swatch, VfxLook.Tints[i].Color, false);
                lab.TintButtons[i] = button;
                lab.TintSelected[i] = selected;
            }
        }

        /// <summary>
        /// Along the bottom of the free stage: the bar of the sample's moment, and under it the
        /// looks to start from.
        /// </summary>
        private static void BuildBottom(RectTransform frame, VfxLookLab lab)
        {
            int presets = VfxLook.Presets.Length;
            const float gap = 8f;
            float width = (FreeWidth - gap * (presets - 1)) / presets;
            float y = Layout.Design.y - 20f - ButtonHeight;
            lab.PresetButtons = new Button[presets];
            for (int i = 0; i < presets; i++)
                lab.PresetButtons[i] = TextButton(
                    frame,
                    "Preset" + i,
                    VfxLook.Presets[i].Name,
                    FreeLeft + i * (width + gap),
                    y,
                    width,
                    ButtonHeight,
                    24f
                ).Button;

            lab.ProgressSlider = QualityLabAssets.BuildSlider(
                frame,
                "Progress",
                FreeLeft,
                y - 16f - 56f,
                FreeWidth,
                56f
            );
            lab.ProgressSlider.minValue = 0f;
            lab.ProgressSlider.maxValue = 1f;
            lab.ProgressSlider.value = 0f;
        }

        /// <summary>
        /// The panel on the right: the sliders, a group under each heading, scrolled up and down
        /// by dragging beside them; and under the panel, the button that copies the values.
        /// </summary>
        private static void BuildPanel(RectTransform frame, VfxLookLab lab)
        {
            var panel = Layout.Box(frame, "Panel", PanelX, Margin, PanelWidth, PanelHeight);
            GlassCardAssets.Plaque(panel, pattern: false, fill: GlassCardAssets.SceneFillColor);
            var copy = TextButton(
                frame,
                "Copy",
                VfxLookLab.CopyText,
                PanelX,
                Margin + PanelHeight + Margin,
                PanelWidth,
                ButtonHeight
            );
            lab.CopyButton = copy.Button;
            lab.CopyLabel = copy.Label;

            var viewport = Layout.Viewport(panel, new Vector2(16f, 16f), new Vector2(-16f, -16f));
            var rows = Layout.Column(viewport, "Rows", 0f, new RectOffset(0, 0, 0, 16));
            Layout.Scroll(viewport, viewport, rows);
            float width = PanelWidth - 32f;

            lab.Sliders = new Slider[VfxLook.Count];
            lab.ValueTexts = new TMP_Text[VfxLook.Count];
            string group = null;
            for (int i = 0; i < VfxLook.Count; i++)
            {
                var spec = VfxLook.Specs[i];
                if (spec.Group != group)
                {
                    group = spec.Group;
                    var heading = Layout.ListItem(rows, "Group" + group, GroupHeight);
                    var title = Label(heading, "Label", group, 26, Gold, TextAlignmentOptions.Left);
                    Layout.At(title, 8f, 10f, width - 16f, GroupHeight - 10f);
                }

                var row = Layout.ListItem(rows, ((VfxLookParam)i).ToString(), RowHeight);
                var name = Label(
                    row,
                    "Label",
                    spec.Label,
                    26,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Left
                );
                Layout.At(name, 20f, 0f, 166f, RowHeight);
                var slider = QualityLabAssets.BuildSlider(row, "Slider", 190f, 4f, 250f, 56f);
                slider.minValue = spec.Min;
                slider.maxValue = spec.Max;
                slider.wholeNumbers = spec.Whole;
                slider.value = VfxLook.Presets[VfxLook.DefaultPreset].Values[i];
                var value = Label(
                    row,
                    "Value",
                    VfxLook.Format(spec, slider.value),
                    24,
                    UiPalette.TextSub,
                    TextAlignmentOptions.Right
                );
                Layout.At(value, 446f, 0f, 70f, RowHeight);
                lab.Sliders[i] = slider;
                lab.ValueTexts[i] = value;
            }
        }

        // 案内人の画面のカードと同じ、模様のないすりガラスのカードのボタン。
        private static (Button Button, GameObject Selected, RectTransform Content) LabCard(
            RectTransform rect
        ) => GlassCardAssets.Choice(rect, pattern: false, fill: GlassCardAssets.SceneFillColor);

        private static (Button Button, GameObject Selected, TMP_Text Label) TextButton(
            RectTransform parent,
            string name,
            string text,
            float x,
            float y,
            float width,
            float height,
            float fontSize = 32f
        )
        {
            var rect = Layout.Box(parent, name, x, y, width, height);
            var (button, selected, card) = LabCard(rect);
            var label = Label(
                card,
                "Label",
                text,
                fontSize,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            return (button, selected, label);
        }
    }
}
