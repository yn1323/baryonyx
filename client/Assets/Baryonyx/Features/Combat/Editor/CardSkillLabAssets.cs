using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.Vfx.Hd2d.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Combat.Editor
{
    /// <summary>
    /// The card skill lab's screen (CardSkillLabScreen.prefab), a debugging room for the card
    /// skills' effects: the battle's stage with the party and the enemies where they stand in
    /// battle and the effects' layers among them, as on BattleInspectScreen, and over it only the
    /// lab's controls (<see cref="CardSkillLab"/>): the skill playing at the top, a row of buttons
    /// at the bottom and the list of every skill by user, opened from the row.
    /// </summary>
    public static class CardSkillLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/CardSkillLabScreen.prefab";

        private static readonly Color TextMain = new(0.953f, 0.914f, 0.824f);
        private static readonly Color TextSub = new(0.788f, 0.749f, 0.659f);
        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);
        private static readonly Color ButtonColor = new(0.06f, 0.07f, 0.12f, 0.88f);
        private static readonly Color SkillColor = new(0.12f, 0.13f, 0.2f, 0.95f);

        // The row's buttons and the list's close button are as tall as a finger needs
        // (doc/rules/ui-design.md). The list's rows are lower, so all 50 fit without scrolling:
        // the lab is a tool for the Editor's mouse first.
        private const float ControlHeight = 128f;

        // The enemies' names in the lab's controls, by their art's name.
        private static readonly Dictionary<string, string> EnemyNames = new()
        {
            ["MossSlime"] = "苔スライム",
            ["MossWolf"] = "苔むした狼",
            ["ForestGuardian"] = "森の守り手",
        };

        [MenuItem("Baryonyx/Combat/Create Card Skill Lab Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode first.");

            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            using (UiBuild.Begin(font, shadowText))
            {
                var root = Rect("CardSkillLabScreen", null);
                Stretch(root);
                var lab = root.gameObject.AddComponent<CardSkillLab>();
                // The battlefield on the scene's camera, so the effects' light goes through
                // Bloom, and the controls on an overlay canvas over it, as in the battle.
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
                var back = BattleSkillVfxAssets.Layer("VfxBack", stage);
                var world = BattleSkillVfxAssets.Layer("World", stage);
                BuildActors(world, lab);
                var front = BattleSkillVfxAssets.Layer("VfxFront", stage);
                Hd2dStageKit.SortLayer(world, Hd2dStageKit.BoardOrder + 10);
                Hd2dStageKit.SortLayer(front, Hd2dStageKit.BoardOrder + 11);
                lab.Vfx = BattleSkillVfxAssets.Attach(screen, stage, dim, back, front);

                var safe = SafeArea(screen);
                BuildCaption(safe, lab);
                BuildControls(safe, lab);
                BuildPicker(screen, lab);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The painted battle background (shown only where there is no 3D stage, as in the
        /// showcase), and the battle's shades along the top and bottom, which keep the controls
        /// legible and the effects lit as in battle.
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
            Shade(stage, "ShadeTop", top: true, 260f, 0.7f);
            Shade(stage, "ShadeBottom", top: false, 360f, 0.85f);
        }

        /// <summary>
        /// The party and the enemies where they stand in battle (BattleInspectAssets' specs), each
        /// standing on the 3D stage, the back ones first so the front ones draw over them. The
        /// effects aim at the drawn part of each, as in battle.
        /// </summary>
        private static void BuildActors(RectTransform world, CardSkillLab lab)
        {
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var material = BattleInspectAssets.EnsurePixelArtMaterial();
            int partyCount = Enum.GetValues(typeof(CardUser)).Length;
            var party = new RectTransform[partyCount];
            var partyNames = new string[partyCount];
            var partySprites = new RawImage[partyCount];
            foreach (var ally in BattleInspectAssets.Allies)
            {
                var texture = BattleInspectAssets.Art("Battle" + ally.Name);
                var unit = Rect("Ally" + ally.Name, world);
                Place(unit, ally.Feet, Vector2.zero);
                var footShadow = Picture(
                    unit,
                    "Shadow",
                    shadow,
                    new Vector2(0, 2),
                    new Vector2(150, 24),
                    0.5f
                );
                var sprite = PixelActor(
                    unit,
                    "Sprite",
                    texture,
                    Vector2.zero,
                    BattleInspectAssets.DotSize
                );
                sprite.material = material;
                Hd2dStageKit.Stand(unit.gameObject, sprite, footShadow);
                int user = (int)ally.User;
                party[user] = BattleInspectAssets.TargetArea(
                    unit,
                    texture,
                    BattleInspectAssets.DotSize
                );
                partyNames[user] = ally.Label;
                partySprites[user] = sprite;
            }

            var enemies = new List<RectTransform>();
            var enemyNames = new List<string>();
            var enemySprites = new List<RawImage>();
            foreach (var spec in BattleInspectAssets.Enemies)
            {
                var texture = BattleInspectAssets.Art(spec.Name);
                var body = Rect("Enemy" + spec.Name, world);
                Place(body, spec.Feet, Vector2.zero);
                var footShadow = Picture(
                    body,
                    "Shadow",
                    shadow,
                    new Vector2(0, 4),
                    new Vector2(spec.Size * BattleInspectAssets.EnemyDotSize * 0.8f, 32),
                    0.5f
                );
                var sprite = PixelActor(
                    body,
                    "Sprite",
                    texture,
                    Vector2.zero,
                    BattleInspectAssets.EnemyDotSize
                );
                sprite.material = material;
                Hd2dStageKit.Stand(body.gameObject, sprite, footShadow);
                enemies.Add(
                    BattleInspectAssets.TargetArea(body, texture, BattleInspectAssets.EnemyDotSize)
                );
                enemyNames.Add(
                    EnemyNames.TryGetValue(spec.Name, out var label) ? label : spec.Name
                );
                enemySprites.Add(sprite);
            }
            // Screen order left to right, as in battle: the slime is nearest the party.
            enemies.Reverse();
            enemyNames.Reverse();
            enemySprites.Reverse();

            lab.Party = party;
            lab.PartyNames = partyNames;
            lab.Enemies = enemies.ToArray();
            lab.EnemyNames = enemyNames.ToArray();
            lab.Actors = partySprites.Concat(enemySprites).ToArray();
        }

        /// <summary>The skill playing, at the top middle, and under it its user, cost, kind and reach.</summary>
        private static void BuildCaption(RectTransform safe, CardSkillLab lab)
        {
            var caption = Label(safe, "Caption", "", 52, Color.white, TextAlignmentOptions.Center);
            Top(caption.rectTransform, -20f, new Vector2(1400f, 70f));
            caption.outlineWidth = 0.2f;
            caption.outlineColor = new Color(0.05f, 0.04f, 0.1f);
            lab.Caption = caption;
            var detail = Label(safe, "Detail", "", 32, TextSub, TextAlignmentOptions.Center);
            Top(detail.rectTransform, -92f, new Vector2(1400f, 44f));
            lab.Detail = detail;
        }

        private static void Top(RectTransform rect, float y, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = size;
        }

        /// <summary>
        /// The row along the bottom: the previous and next skill around the list, play again, and
        /// how the blows land, which enemy a card on one enemy aims at, and whether it repeats.
        /// </summary>
        private static void BuildControls(RectTransform safe, CardSkillLab lab)
        {
            var row = Rect("Controls", safe);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 20f);
            row.sizeDelta = new Vector2(1840f, ControlHeight);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // DotGothic16 has no arrows (◀▶), so the neighbours are named in words.
            lab.PreviousButton = ControlButton(row, "Previous", "前へ", 130f).button;
            lab.ListButton = ControlButton(row, "List", "スキル一覧", 230f).button;
            lab.NextButton = ControlButton(row, "Next", "次へ", 130f).button;
            lab.ReplayButton = ControlButton(row, "Replay", "もう一度", 200f).button;
            (lab.WeightButton, lab.WeightLabel) = ControlButton(
                row,
                "Weight",
                "当たり：通常",
                270f
            );
            (lab.TargetButton, lab.TargetLabel) = ControlButton(
                row,
                "Target",
                "ねらう敵：苔むした狼",
                400f
            );
            (lab.RepeatButton, lab.RepeatLabel) = ControlButton(
                row,
                "Repeat",
                "くり返し：入",
                250f
            );
        }

        private static (Button button, TMP_Text label) ControlButton(
            RectTransform parent,
            string name,
            string text,
            float width
        )
        {
            var rect = Rect(name, parent);
            rect.sizeDelta = new Vector2(width, ControlHeight);
            var image = Sliced(rect, UiArt.RoundedRectPath, ButtonColor);
            var button = AddButton(rect, image);
            var label = Label(rect, "Label", text, 34, TextMain, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return (button, label);
        }

        /// <summary>
        /// The list of every skill over the whole screen: a column for each user (in
        /// <see cref="CardUser"/>'s order) under the user's name, filled with a button per skill
        /// as the lab starts, so a skill added to the catalog shows without building this again.
        /// </summary>
        private static void BuildPicker(RectTransform screen, CardSkillLab lab)
        {
            var picker = Rect("Picker", screen);
            Stretch(picker);
            // Takes the taps, so nothing behind it is pressed while choosing.
            AddImage(picker, new Color(0.02f, 0.03f, 0.06f, 0.9f), true);
            var safe = SafeArea(picker);

            var title = Label(safe, "Title", "スキルを選ぶ", 44, Gold, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(60f, -40f);
            title.rectTransform.sizeDelta = new Vector2(800f, 64f);

            var close = Rect("Close", safe);
            Corner(close, Vector2.one, new Vector2(-60f, -12f), new Vector2(220f, ControlHeight));
            var closeImage = Sliced(close, UiArt.RoundedRectPath, ButtonColor);
            lab.CloseButton = AddButton(close, closeImage);
            Stretch(
                Label(
                    close,
                    "Label",
                    "とじる",
                    34,
                    TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );

            var columns = Rect("Columns", safe);
            Stretch(columns);
            columns.offsetMin = new Vector2(60f, 32f);
            columns.offsetMax = new Vector2(-60f, -150f);
            var row = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 24f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;

            var colors = BattleInspectAssets.Allies.ToDictionary(
                ally => ally.User,
                ally => ally.Color
            );
            var labels = BattleInspectAssets.Allies.ToDictionary(
                ally => ally.User,
                ally => ally.Label
            );
            var users = (CardUser[])Enum.GetValues(typeof(CardUser));
            var columnRects = new RectTransform[users.Length];
            foreach (var user in users)
            {
                var column = Rect("Column" + user, columns);
                var list = column.gameObject.AddComponent<VerticalLayoutGroup>();
                list.spacing = 6f;
                list.childAlignment = TextAnchor.UpperCenter;
                list.childControlWidth = true;
                list.childForceExpandWidth = true;
                list.childControlHeight = false;
                list.childForceExpandHeight = false;
                var header = Label(
                    column,
                    "User",
                    labels.TryGetValue(user, out var name) ? name : user.ToString(),
                    36,
                    colors.TryGetValue(user, out var color) ? color : TextMain,
                    TextAlignmentOptions.Left
                );
                header.rectTransform.sizeDelta = new Vector2(0f, 52f);
                columnRects[(int)user] = column;
            }
            lab.Columns = columnRects;
            lab.SkillTemplate = SkillButton(safe);
            lab.Picker = picker.gameObject;
        }

        /// <summary>The model of a skill's button: its number and name, and its cost on the right.</summary>
        private static Button SkillButton(RectTransform parent)
        {
            var rect = Rect("SkillTemplate", parent);
            rect.sizeDelta = new Vector2(400f, 56f);
            var image = Sliced(rect, UiArt.RoundedRectPath, SkillColor);
            var button = AddButton(rect, image);
            // Small enough that the longest name (チェインライトニング) stays clear of the cost.
            var name = Label(rect, "Name", "1　斬り払い", 28, TextMain, TextAlignmentOptions.Left);
            Stretch(name.rectTransform);
            name.rectTransform.offsetMin = new Vector2(18f, 0f);
            name.rectTransform.offsetMax = new Vector2(-64f, 0f);
            var cost = Label(rect, "Cost", "1", 28, Gold, TextAlignmentOptions.Right);
            cost.rectTransform.anchorMin = new Vector2(1f, 0f);
            cost.rectTransform.anchorMax = Vector2.one;
            cost.rectTransform.offsetMin = new Vector2(-64f, 0f);
            cost.rectTransform.offsetMax = new Vector2(-18f, 0f);
            rect.gameObject.SetActive(false);
            return button;
        }
    }
}
