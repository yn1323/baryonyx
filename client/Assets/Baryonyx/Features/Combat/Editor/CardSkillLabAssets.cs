using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.SkillEvolution;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
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
    /// lab's controls (<see cref="CardSkillLab"/>): the skill playing at the top, the way back to
    /// home at the top left, whether a unique skill evolves first at the top right, a row of
    /// buttons at the bottom, where the stage to evolve into is picked, and the list of every
    /// skill as its card on two tabs and of the wind-ups on a third, opened from the row.
    /// </summary>
    public static class CardSkillLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Combat/UI/CardSkillLabScreen.prefab";

        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);

        // The row's buttons and the list's close button are as tall as a finger needs
        // (doc/rules/ui-design.md). The list's cards are larger still.
        private const float ControlHeight = 128f;

        // The list's cards: the battle's card (70x98 dots at 3 px) at 0.8, in cells with room
        // for the gold glow of the one picked. The card skills go 8 to a row; the unique skills
        // 7, so each row is one evolution tree (its first stage, then the branches' steps).
        private const float CardScale = 0.8f;
        private static readonly Vector2 Cell = new(
            70 * 3 * CardScale + 12f,
            98 * 3 * CardScale + 12f
        );
        private const float CellGap = 18f;

        // The wind-ups' buttons, 3 to a row, as tall as the list's other buttons.
        private static readonly Vector2 WindUpCell = new(540f, 160f);

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
            EditorGuard.RequireEditMode();

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
                BuildHome(safe, lab);
                BuildEvolve(safe, lab);
                BuildControls(safe, lab);
                // Over the controls (hidden while choosing) and under the list.
                lab.Choice = BattleEvolutionChoiceAssets.Build(
                    screen,
                    lab.Vfx.MaterialOf(BattleVfxShape.Beam)
                );
                BuildPicker(screen, lab);
                lab.HiddenWhilePicking = new[]
                {
                    lab.Caption.gameObject,
                    lab.Detail.gameObject,
                    lab.ListButton.transform.parent.gameObject,
                    lab.EvolveButton.transform.parent.gameObject,
                };

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
            // Narrow enough to clear the buttons in the top corners.
            var caption = Label(safe, "Caption", "", 52, Color.white, TextAlignmentOptions.Center);
            Top(caption.rectTransform, -20f, new Vector2(1200f, 70f));
            caption.outlineWidth = 0.2f;
            caption.outlineColor = new Color(0.05f, 0.04f, 0.1f);
            lab.Caption = caption;
            var detail = Label(
                safe,
                "Detail",
                "",
                32,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Top(detail.rectTransform, -92f, new Vector2(1200f, 44f));
            lab.Detail = detail;
        }

        /// <summary>
        /// The button back to home at the top left, beside the caption. The list covers it while
        /// open; the scene sets where it goes, and without that it stays hidden.
        /// </summary>
        private static void BuildHome(RectTransform safe, CardSkillLab lab)
        {
            var home = Rect("Home", safe);
            Corner(
                home,
                new Vector2(0f, 1f),
                new Vector2(60f, -12f),
                new Vector2(200f, ControlHeight)
            );
            var (button, _, card) = LabCard(home);
            Stretch(
                Label(
                    card,
                    "Label",
                    "ホームへ",
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );
            lab.HomeButton = button;
        }

        /// <summary>
        /// Whether a unique skill's stage that can evolve evolves first, at the top right. The
        /// list covers it while open.
        /// </summary>
        private static void BuildEvolve(RectTransform safe, CardSkillLab lab)
        {
            var evolve = Rect("Evolve", safe);
            Corner(evolve, Vector2.one, new Vector2(-60f, -12f), new Vector2(240f, ControlHeight));
            var (button, _, card) = LabCard(evolve);
            var label = Label(
                card,
                "Label",
                "覚醒：切",
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            lab.EvolveButton = button;
            lab.EvolveLabel = label;
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

            // Kiwi Maru has no arrows (◀▶), so the neighbours are named in words.
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

        // 舞台の上に直に置く、模様のないすりガラスのカードのボタン（案内人の画面のカードと同じ形）。
        private static (Button Button, GameObject Selected, RectTransform Content) LabCard(
            RectTransform rect
        ) => GlassCardAssets.Choice(rect, pattern: false, fill: GlassCardAssets.SceneFillColor);

        private static (Button button, TMP_Text label) ControlButton(
            RectTransform parent,
            string name,
            string text,
            float width
        )
        {
            var rect = Rect(name, parent);
            rect.sizeDelta = new Vector2(width, ControlHeight);
            var (button, _, card) = LabCard(rect);
            var label = Label(
                card,
                "Label",
                text,
                34,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            return (button, label);
        }

        /// <summary>
        /// The list of every skill over the whole screen, as the battle's cards: a tab for the
        /// card skills and one for the unique skills' stages, each a grid scrolled up and down
        /// with a swipe, and a tab of the wind-ups the skills open with, played on their own. The cards are filled in as the lab starts, so a skill added to the catalog
        /// shows without building this again; their art is baked here, in the skills' order.
        /// </summary>
        private static void BuildPicker(RectTransform screen, CardSkillLab lab)
        {
            var picker = Rect("Picker", screen);
            Stretch(picker);
            // Takes the taps, so nothing behind it is pressed while choosing.
            AddImage(picker, new Color(0.02f, 0.03f, 0.06f, 0.96f), true);
            var safe = SafeArea(picker);

            var title = Label(safe, "Title", "スキルを選ぶ", 44, Gold, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(60f, -40f);
            title.rectTransform.sizeDelta = new Vector2(800f, 64f);

            var close = Rect("Close", safe);
            Corner(close, Vector2.one, new Vector2(-60f, -12f), new Vector2(220f, ControlHeight));
            var (closeButton, _, closeCard) = LabCard(close);
            lab.CloseButton = closeButton;
            Stretch(
                Label(
                    closeCard,
                    "Label",
                    "とじる",
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );

            int uniques = UniqueSkillTrees.Stages().Count();
            lab.Tabs = GuideFrameAssets.Tabs(
                safe,
                "Tabs",
                new[]
                {
                    $"カードスキル　{CardSkills.All.Count}",
                    $"固有スキル　{uniques}",
                    $"覚醒版　{CardSkillLab.Skills.Count - CardSkills.All.Count - uniques}",
                    $"溜め　{CardSkillLab.WindUps.Count}",
                },
                60f,
                112f,
                400f,
                76f,
                1680f
            );

            // Swiping anywhere over the list scrolls it, between the cards too (the viewport
            // takes the taps).
            var viewport = GuidePanelLayout.Viewport(
                safe,
                new Vector2(60f, 24f),
                new Vector2(-60f, -204f)
            );
            var padding = new RectOffset(0, 0, 12, 32);
            var cards = GuidePanelLayout.Grid(viewport, "CardSkills", Cell, CellGap, padding, 8);
            var trees = GuidePanelLayout.Grid(viewport, "UniqueSkills", Cell, CellGap, padding, 7);
            var awakened = GuidePanelLayout.Grid(
                viewport,
                "AwakenedSkills",
                Cell,
                CellGap,
                padding,
                8
            );
            var windUps = GuidePanelLayout.Grid(
                viewport,
                "WindUps",
                WindUpCell,
                CellGap,
                padding,
                3
            );
            foreach (var content in new[] { cards, trees, awakened, windUps })
                content.GetComponent<GridLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            trees.gameObject.SetActive(false);
            awakened.gameObject.SetActive(false);
            windUps.gameObject.SetActive(false);
            lab.TabContents = new[] { cards, trees, awakened, windUps };
            lab.Scroll = GuidePanelLayout.Scroll(viewport, viewport, cards);
            lab.Tabs[0].Chosen = true;

            lab.CardTemplate = CardButton(safe);
            lab.WindUpTemplate = WindUpButton(safe);
            BakeCardArt(lab);
            lab.Picker = picker.gameObject;
        }

        /// <summary>
        /// The model of a skill's card in the list: a button the size of a cell, a gold glow
        /// behind (shown while it is picked), and the battle's card over it, without the hand's
        /// drag (so a swipe over it scrolls the list).
        /// </summary>
        private static Button CardButton(RectTransform parent)
        {
            var rect = Rect("CardTemplate", parent);
            rect.sizeDelta = Cell;
            var hit = AddImage(rect, Color.clear, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var glow = Rect(CardSkillLab.SelectedName, rect);
            Stretch(glow);
            AddImage(glow, new Color(Gold.r, Gold.g, Gold.b, 0.55f), false);
            glow.gameObject.SetActive(false);

            var instance = InstantiatePrefab(BattleInspectAssets.CardPrefabPath, "Card", rect);
            var card = (RectTransform)instance.transform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.localScale = Vector3.one * CardScale;
            var input = instance.GetComponent<BattleInspectCardInput>();
            if (input != null)
                UnityEngine.Object.DestroyImmediate(input);
            rect.gameObject.SetActive(false);
            return button;
        }

        /// <summary>
        /// The model of a wind-up's button in the list: a button the size of a cell, a gold glow
        /// behind (shown while it is picked), and a glass card over it with the wind-up's name.
        /// </summary>
        private static Button WindUpButton(RectTransform parent)
        {
            var rect = Rect("WindUpTemplate", parent);
            rect.sizeDelta = WindUpCell;
            var hit = AddImage(rect, Color.clear, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var glow = Rect(CardSkillLab.SelectedName, rect);
            Stretch(glow);
            AddImage(glow, new Color(Gold.r, Gold.g, Gold.b, 0.55f), false);
            glow.gameObject.SetActive(false);

            var face = Rect("Card", rect);
            Stretch(face);
            face.offsetMin = Vector2.one * 6f;
            face.offsetMax = -Vector2.one * 6f;
            var content = GlassCardAssets.Plaque(
                face,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            Stretch(
                Label(
                    content,
                    "Label",
                    "魔法の溜め",
                    34,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                ).rectTransform
            );
            rect.gameObject.SetActive(false);
            return button;
        }

        /// <summary>
        /// The art the list's cards show, which the lab cannot load in Play: each skill's art in
        /// <see cref="CardSkillLab.Skills"/>' order (a unique skill's stage has art of its own),
        /// and the frame and element icon of every element.
        /// </summary>
        private static void BakeCardArt(CardSkillLab lab)
        {
            lab.CardArts = CardSkillLab
                .Skills.Select(skill =>
                    (Texture)
                        BattleInspectAssets.Art(
                            UniqueSkillTrees.FindStage(skill.Id)?.Art ?? skill.Art
                        )
                )
                .ToArray();
            var elements = (CardElement[])Enum.GetValues(typeof(CardElement));
            lab.CardFrames = elements
                .Select(element => (Texture)BattleInspectAssets.Art("CardFrame" + element))
                .ToArray();
            lab.ElementIcons = elements
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : (Texture)BattleInspectAssets.Art("Element" + element)
                )
                .ToArray();
        }
    }
}
