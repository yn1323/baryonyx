using Baryonyx.Combat.Editor;
using Baryonyx.Editor;
using Baryonyx.Editor.UI;
using Baryonyx.Inventory;
using Baryonyx.Inventory.Editor;
using Baryonyx.TravelOffice.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using Baryonyx.WorldMap.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// The drop editor's screen (DropLabScreen.prefab, <see cref="DropLab"/>): over a dark
    /// backdrop, the way back to home, the dungeon's button, the notice and "コピー" along the
    /// top; the dungeon's drop list in sections by category on the left and its five clears'
    /// rewards on the right; the chosen thing and the buttons that change it along the bottom.
    /// The dungeon list drops from the dungeon's button, and the list of every kind covers the
    /// screen with its categories on the left. The forest ruins' list is baked so the stopped
    /// screen shows it.
    /// </summary>
    public static class DropLabAssets
    {
        public const string PrefabPath =
            "Assets/Baryonyx/Features/Adventure/UI/DropLabScreen.prefab";

        private static readonly Color Backdrop = new(0.03f, 0.04f, 0.08f, 1f);
        private static readonly Color Field = new(0.98f, 0.96f, 0.9f, 0.95f);
        private static readonly Color Hint = new(0.45f, 0.42f, 0.38f, 1f);

        // 16:9（幅1920）の画面にも、Editorの狭いGameビューにも収まる幅。
        private const float FrameWidth = 1700f;
        private const float Left = 20f;
        private const float LineWidth = 1660f;

        // Buttons are as tall as a finger needs (doc/rules/ui-design.md).
        private const float ButtonHeight = 128f;
        private const float Gap = 12f;
        private const float BodyTop = 152f;
        private const float BodyHeight = 700f;

        // 左のドロップの一覧：段の名前の列と、3列の札。
        private const float DropsWidth = 1180f;
        private const float SectionLabelWidth = 170f;
        private const int DropColumns = 3;
        private static readonly Vector2 DropTile = new(328f, ButtonHeight);

        // 右のクリア回数の報酬：「1回目」の列と札。
        private const float RewardsLeft = Left + DropsWidth + 16f;
        private const float RewardNumberWidth = 96f;
        private const float RewardCardWidth = Left + LineWidth - RewardsLeft - RewardNumberWidth;

        // 下の段：選んだ物の名前と説明（左）、変えるボタン（右）。
        private const float BottomTop = BodyTop + BodyHeight + 24f;
        private const float InfoWidth = 820f;
        private const float ActionsLeft = Left + InfoWidth + 20f;
        private const float ActionsWidth = Left + LineWidth - ActionsLeft;

        // 上の段：ダンジョンのボタン、知らせ、コピー。
        private const float DungeonLeft = 236f;
        private const float DungeonWidth = 460f;
        private const float CopyWidth = 260f;

        // 物を選ぶ一覧：左の分類と、4列の札。
        private const float PickerListLeft = Left + 300f + 20f;
        private const int PickerColumns = 4;
        private static readonly Vector2 PickerTile = new(
            (Left + LineWidth - PickerListLeft - Gap * (PickerColumns - 1)) / PickerColumns,
            ButtonHeight
        );

        // 札の左の絵（32×32ドットを3倍）と、最奥だけの王冠（12×12ドットを4倍）。
        private const float IconSize = 96f;
        private const float CrownSize = 48f;
        private const float TextLeft = 16f + IconSize + 16f;

        private static readonly string[] SectionTitles =
        {
            "アイテム",
            "アクセサリー",
            "武器",
            "防具",
            "スキル",
        };

        private static readonly string[] PickerTitles =
        {
            "アイテム",
            "アクセサリー",
            "武器",
            "防具",
            "スキル",
            "強化素材",
        };

        [MenuItem("Baryonyx/Adventure/Create Drop Lab Assets")]
        public static void CreateAssets()
        {
            EditorGuard.RequireEditMode();

            UiArt.EnsureAll();
            var font = GameFontAssets.GetOrCreate();
            var shadowText = UiArt.EnsureTextShadow(font);
            using (UiBuild.Begin(font, shadowText))
            {
                var root = CanvasRoot("DropLabScreen");
                var lab = root.gameObject.AddComponent<DropLab>();
                AddImage(root, Backdrop, false);
                var safe = Frame(SafeArea(root));

                var templates = Rect("Templates", root);
                BuildTemplates(templates, lab);
                BuildHeader(safe, lab);
                BuildDrops(safe, lab);
                BuildRewards(safe, lab);
                BuildBottom(safe, lab);
                BuildDungeons(safe, lab);
                BuildPicker(root, lab);
                templates.gameObject.SetActive(false);

                lab.Art = ItemArtAssets.Load();
                lab.TileSize = DropTile;
                lab.TileGap = Gap;
                lab.Bake(AdventureCatalog.ForestRuins);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                AssetDatabase.SaveAssetIfDirty(font);
            }
            AssetDatabase.SaveAssets();
        }

        // 上を揃えた、幅 FrameWidth の操作の枠。
        private static RectTransform Frame(RectTransform safe)
        {
            var frame = Rect("Frame", safe);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 1f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(FrameWidth, 1080f);
            return frame;
        }

        // 押せる札（すりガラス）と、真ん中の文字。
        private static (Button Button, TMP_Text Label) CardButton(
            RectTransform parent,
            string name,
            string text,
            float x,
            float y,
            float width,
            float height = ButtonHeight
        )
        {
            var box = Layout.Box(parent, name, x, y, width, height);
            var (button, _, card) = GlassCardAssets.Choice(
                box,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var label = Label(
                card,
                "Label",
                text,
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 32f;
            return (button, label);
        }

        // 左上のホームへ、ダンジョンのボタン、知らせ、右上のコピー。
        private static void BuildHeader(RectTransform safe, DropLab lab)
        {
            lab.HomeButton = CardButton(safe, "Home", "ホームへ", Left, 8f, 200f).Button;
            (lab.DungeonButton, lab.DungeonLabel) = CardButton(
                safe,
                "Dungeon",
                "",
                DungeonLeft,
                8f,
                DungeonWidth
            );
            float statusLeft = DungeonLeft + DungeonWidth + 16f;
            float copyLeft = Left + LineWidth - CopyWidth;
            lab.Status = Layout.Text(
                safe,
                "Status",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                statusLeft,
                8f,
                copyLeft - 16f - statusLeft,
                ButtonHeight
            );
            lab.Status.textWrappingMode = TextWrappingModes.Normal;
            // 変えたダンジョンをJSONにしてクリップボードへ写す（AIに貼って表へ反映する）。
            lab.CopyButton = CardButton(safe, "Copy", "コピー", copyLeft, 8f, CopyWidth).Button;
        }

        // 左のドロップの一覧：分類ごとの段に、名前の列と札の格子。札は Play の開始時に並べ直す。
        private static void BuildDrops(RectTransform safe, DropLab lab)
        {
            var panel = Layout.Box(safe, "Drops", Left, BodyTop, DropsWidth, BodyHeight);
            var viewport = Layout.Viewport(panel);
            var content = Layout.Column(viewport, "Sections", 20f, new RectOffset(0, 0, 0, 24));
            lab.DropScroll = Layout.Scroll(panel, viewport, content);
            int count = DropLabDrafts.ListCategories.Length;
            lab.Sections = new RectTransform[count];
            lab.SectionSizes = new LayoutElement[count];
            for (int s = 0; s < count; s++)
            {
                var item = Layout.ListItem(content, "Section" + s, ButtonHeight);
                lab.SectionSizes[s] = item.GetComponent<LayoutElement>();
                var title = Layout.Text(
                    item,
                    "Label",
                    SectionTitles[s],
                    30,
                    UiPalette.TextSub,
                    TextAlignmentOptions.MidlineLeft,
                    0f,
                    0f,
                    SectionLabelWidth - Gap,
                    ButtonHeight
                );
                Shrink(title);
                var grid = Rect("Grid", item);
                Stretch(grid);
                grid.offsetMin = new Vector2(SectionLabelWidth, 0f);
                var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
                layout.cellSize = DropTile;
                layout.spacing = Vector2.one * Gap;
                layout.startAxis = GridLayoutGroup.Axis.Horizontal;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = DropColumns;
                lab.Sections[s] = grid;
            }
        }

        // 右のクリア回数の報酬：「N回目」と、絵・名前と数・★と品質の札を5つ。
        private static void BuildRewards(RectTransform safe, DropLab lab)
        {
            lab.Rewards = new DropLabRewardSlot[AdventureClearRewards.Count];
            for (int i = 0; i < lab.Rewards.Length; i++)
            {
                float y = BodyTop + i * (ButtonHeight + Gap);
                Layout.Text(
                    safe,
                    "Clear" + (i + 1),
                    $"{i + 1}回目",
                    30,
                    UiPalette.TextSub,
                    TextAlignmentOptions.MidlineLeft,
                    RewardsLeft,
                    y,
                    RewardNumberWidth,
                    ButtonHeight
                );
                var box = Layout.Box(
                    safe,
                    "Reward" + (i + 1),
                    RewardsLeft + RewardNumberWidth,
                    y,
                    RewardCardWidth,
                    ButtonHeight
                );
                var (button, _, card) = GlassCardAssets.Choice(
                    box,
                    pattern: false,
                    fill: GlassCardAssets.SceneFillColor
                );
                var icon = Icon(card);
                var name = Label(
                    card,
                    "Name",
                    "",
                    28,
                    UiPalette.TextMain,
                    TextAlignmentOptions.BottomLeft
                );
                Shrink(name);
                Band(name.rectTransform, 0.5f, 1f, 6f);
                var grade = Label(
                    card,
                    "Grade",
                    "",
                    24,
                    UiPalette.Gold,
                    TextAlignmentOptions.TopLeft
                );
                grade.richText = true;
                Band(grade.rectTransform, 0f, 0.5f, 6f);
                lab.Rewards[i] = new DropLabRewardSlot
                {
                    Button = button,
                    Card = box.GetComponent<GlassCard>(),
                    Icon = icon,
                    Name = name,
                    Grade = grade,
                };
            }
        }

        // 下の段：選んだ物の1行と説明、ドロップを変えるボタンと報酬を変えるボタン（選んだ方だけを出す）。
        private static void BuildBottom(RectTransform safe, DropLab lab)
        {
            lab.Info = Layout.Text(
                safe,
                "Info",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft,
                Left,
                BottomTop,
                InfoWidth,
                64f
            );
            lab.Info.richText = true;
            Shrink(lab.Info);
            lab.Detail = Layout.Text(
                safe,
                "Detail",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft,
                Left,
                BottomTop + 68f,
                InfoWidth,
                64f
            );
            lab.Detail.textWrappingMode = TextWrappingModes.Normal;

            var drop = Layout.Row(
                safe,
                "DropActions",
                ActionsLeft,
                BottomTop,
                ActionsWidth,
                ButtonHeight,
                Gap,
                TextAnchor.MiddleRight
            );
            lab.DropActions = HiddenGroup(drop);
            (lab.BossButton, lab.BossLabel) = EnemyLabAssets.ControlButton(
                drop,
                "Boss",
                DropLab.BossText(false),
                320f
            );
            (lab.StarsButton, lab.StarsLabel) = EnemyLabAssets.ControlButton(
                drop,
                "Stars",
                "★1",
                160f
            );
            lab.RemoveButton = EnemyLabAssets.ControlButton(drop, "Remove", "外す", 200f).button;

            var reward = Layout.Row(
                safe,
                "RewardActions",
                ActionsLeft,
                BottomTop,
                ActionsWidth,
                ButtonHeight,
                Gap,
                TextAnchor.MiddleRight
            );
            lab.RewardActions = HiddenGroup(reward);
            lab.ChangeButton = EnemyLabAssets
                .ControlButton(reward, "Change", "物を替える", 280f)
                .button;
            var caption = Label(
                reward,
                "CountCaption",
                "数",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Layout.Fit(caption, 56f);
            lab.CountInput = CountInput(reward);
            (lab.QualityButton, lab.QualityLabel) = EnemyLabAssets.ControlButton(
                reward,
                "Quality",
                DropLab.QualityText(ItemQuality.A),
                220f
            );
        }

        // 数を書く欄（生成りの地に濃い文字、空のときは薄い見本の文字）。
        private static TMP_InputField CountInput(RectTransform row)
        {
            var rect = Rect("Count", row);
            Layout.Fit(rect, 160f);
            var plate = Sliced(rect, UiArt.CapsulePath, Field);
            plate.pixelsPerUnitMultiplier = 2.4f;
            var area = Rect("Text Area", rect);
            Stretch(area);
            area.offsetMin = new Vector2(24f, 6f);
            area.offsetMax = new Vector2(-24f, -6f);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Label(
                area,
                "Placeholder",
                "1",
                34,
                Hint,
                TextAlignmentOptions.Center,
                false
            );
            Stretch(placeholder.rectTransform);
            var text = Label(
                area,
                "Text",
                "",
                34,
                WorldMapEventWindowAssets.Ink,
                TextAlignmentOptions.Center,
                false
            );
            Stretch(text.rectTransform);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = plate;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = text.font;
            input.pointSize = 34;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 2;
            input.caretColor = WorldMapEventWindowAssets.Ink;
            input.customCaretColor = true;
            return input;
        }

        // ダンジョンのボタンの下に開く、ダンジョンの一覧。行は Play の開始時に見本から作る。
        private static void BuildDungeons(RectTransform safe, DropLab lab)
        {
            var panel = Layout.Box(
                safe,
                "Dungeons",
                DungeonLeft,
                BodyTop,
                DungeonWidth,
                1060f - BodyTop
            );
            lab.DungeonGroup = HiddenGroup(panel);
            GlassCardAssets.Plaque(panel, pattern: false, fill: UiPalette.SurfaceDark);
            var viewport = Layout.Viewport(panel, Vector2.one * 12f, -Vector2.one * 12f);
            var content = Layout.Column(viewport, "Rows", 8f);
            Layout.Scroll(panel, viewport, content);
            lab.DungeonContent = content;
            var (row, label) = EnemyLabAssets.ControlButton(content, "Row", "", 0f);
            row.GetComponent<LayoutElement>().preferredHeight = ButtonHeight;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 32f;
            label.rectTransform.offsetMin = new Vector2(28f, 0f);
            row.gameObject.SetActive(false);
            lab.DungeonTemplate = row;
        }

        // 全面を覆う、物を選ぶ一覧：上に題と閉じる、左に分類、右に分類の全種類の札。
        private static void BuildPicker(RectTransform root, DropLab lab)
        {
            var layer = Rect("Picker", root);
            Stretch(layer);
            AddImage(layer, Backdrop, true);
            lab.PickerGroup = HiddenGroup(layer);
            var frame = Frame(SafeArea(layer));
            lab.PickerTitle = Layout.Text(
                frame,
                "Title",
                "",
                38,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft,
                Left,
                8f,
                LineWidth - CopyWidth - 16f,
                ButtonHeight
            );
            lab.PickerClose = CardButton(
                frame,
                "Close",
                "閉じる",
                Left + LineWidth - CopyWidth,
                8f,
                CopyWidth
            ).Button;
            lab.PickerCategories = new Button[PickerTitles.Length];
            for (int i = 0; i < PickerTitles.Length; i++)
                lab.PickerCategories[i] = CardButton(
                    frame,
                    "Category" + i,
                    PickerTitles[i],
                    Left,
                    BodyTop + i * (ButtonHeight + 8f),
                    300f
                ).Button;
            var panel = Layout.Box(
                frame,
                "Kinds",
                PickerListLeft,
                BodyTop,
                Left + LineWidth - PickerListLeft,
                1060f - BodyTop
            );
            var viewport = Layout.Viewport(panel);
            lab.PickerContent = Layout.Grid(
                viewport,
                "Tiles",
                PickerTile,
                Gap,
                new RectOffset(0, 0, 0, 24),
                PickerColumns
            );
            lab.PickerScroll = Layout.Scroll(panel, viewport, lab.PickerContent);
        }

        // 札の見本：絵・名前・★と、上の小さな行（選ぶ一覧で、どのダンジョンの一覧にあるか）、右上の王冠。
        // 「＋」の札は点線の縁にする。
        private static void BuildTemplates(RectTransform templates, DropLab lab)
        {
            var tile = Rect("Tile", templates);
            tile.sizeDelta = DropTile;
            var (button, _, card) = GlassCardAssets.Choice(
                tile,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            Icon(card);
            var detail = Label(
                card,
                DropLab.DetailName,
                "",
                20,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            Band(detail.rectTransform, 0.7f, 1f, 8f);
            var name = Label(
                card,
                DropLab.LabelName,
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Shrink(name);
            Band(name.rectTransform, 0.34f, 0.7f, 0f);
            var stars = Label(
                card,
                DropLab.StarsName,
                "",
                22,
                UiPalette.Gold,
                TextAlignmentOptions.TopLeft
            );
            Band(stars.rectTransform, 0f, 0.34f, 4f);
            var crown = Rect(DropLab.BossName, card);
            Corner(crown, Vector2.one, new Vector2(-6f, -6f), Vector2.one * CrownSize);
            var image = crown.gameObject.AddComponent<Image>();
            image.sprite = GuideMenuAssets.Icon(TravelOfficeScreenAssets.IconBossOnlyPath);
            image.raycastTarget = false;
            crown.gameObject.SetActive(false);
            lab.TileTemplate = button;

            var add = Rect("Add", templates);
            add.sizeDelta = DropTile;
            var (addButton, _, addCard) = GlassCardAssets.Choice(
                add,
                dashed: true,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var plus = Label(
                addCard,
                "Label",
                "＋",
                56,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Stretch(plus.rectTransform);
            lab.AddTemplate = addButton;
        }

        // 札の左の、真ん中に置いた絵。
        private static Image Icon(RectTransform card)
        {
            var rect = Rect(DropLab.IconName, card);
            Corner(
                rect,
                new Vector2(0f, 0.5f),
                new Vector2(16f + IconSize / 2f, 0f),
                Vector2.one * IconSize
            );
            rect.pivot = new Vector2(0.5f, 0.5f);
            var icon = rect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        // 長い名前（ギガントインパクトなど）は、札に収まるまで縮める。
        private static void Shrink(TMP_Text label)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = label.fontSize;
            label.overflowMode = TextOverflowModes.Truncate;
        }

        // 札の絵の右の、上下の割合で区切った帯。右は札の端の手前まで。
        private static void Band(RectTransform rect, float bottom, float top, float gap)
        {
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = new Vector2(1f, top);
            rect.offsetMin = new Vector2(TextLeft, gap);
            rect.offsetMax = new Vector2(-16f, -gap);
        }
    }
}
