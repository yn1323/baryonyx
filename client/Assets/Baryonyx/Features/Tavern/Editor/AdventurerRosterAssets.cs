using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment.Editor;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The formation's adventurers. The formation's generator calls <see cref="BuildPanel"/>
    /// for its adventurers item, so the chosen person's sample, the party's slots and a tile for
    /// every mock character are baked into the formation prefab and read in the editor without
    /// Play Mode. Positions are design pixels from the top left of a 1920x1080 layer
    /// (<see cref="GuidePanelLayout"/>), matching the mockup the player chose.
    /// </summary>
    public static class AdventurerRosterAssets
    {
        private const float Dot = Guide.DotScale;

        // 左の見本と右の一覧。設計座標、左上から。
        private static readonly Rect Chosen = new(40, 200, 590, 850);
        private static readonly Rect Roster = new(650, 200, 1230, 850);
        private const float Pad = 30f;

        // 見本の装備とスキルの箱。中に24×24ドットのアイコン（スキルは挿絵の中央）を3倍で置く。
        private const float BoxSize = 96f;
        private const float BoxGap = 10f;
        private const float BoxIcon = 72f;

        // 一覧のタイル。立ち姿は周りの余白を切って3倍で出す。
        private static readonly Vector2 TileSize = new(280, 236);
        private const float TileGap = 18f;
        private const float TileDot = 3f;

        // スキルの挿絵（64×58）の中央24×24ドット。
        private static readonly Rect ArtCrop = new(20f / 64f, 17f / 58f, 24f / 64f, 24f / 58f);

        // デッキに入る4枚（固有スキル2枚とカスタムスキル2枚）。
        private const int DeckCards = 4;
        private static readonly Color Locked = new(0.62f, 0.62f, 0.68f, 0.85f);
        private static readonly Color TagColor = new(0.96f, 0.71f, 0.24f, 1f);

        /// <summary>
        /// Builds the adventurers over the guide screen's safe area. Called while the guide
        /// screen's prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var training = TrainingAssets.CreateData();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);

            var root = Rect("Adventurers", safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<AdventurerRosterView>();
            view.Party = party;
            view.Training = training;
            view.Guide = guide;
            view.WeaponIcon = Guide.Icon(EquipmentAssets.IconWeaponPath);
            view.ArmorIcon = Guide.Icon(EquipmentAssets.IconArmorPath);

            var layer = Layout.Layer(root, "Layout");
            BuildChosen(layer, view);
            BuildRoster(layer, view, party, shadow);

            // 仮データの編成で描き、Prefabを開くと停止中でも見本と一覧が読めるようにする。
            new PartyRosterPresenter(view, PartyFormation.From(party)).Dispose();
            return root.gameObject;
        }

        // --- Left: the chosen person's sample and the two buttons -------------------------

        private static void BuildChosen(RectTransform layer, AdventurerRosterView view)
        {
            var panel = Layout.FrameBox(
                layer,
                "Chosen",
                Chosen.x,
                Chosen.y,
                Chosen.width,
                Chosen.height
            );
            float width = Chosen.width - Pad * 2;

            var header = Layout.Row(panel, "Header", Pad, 16, width, 72, 12);
            view.Name = Label(
                header,
                "Name",
                "",
                50,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(view.Name, 0);
            Layout.Fit(
                Label(
                    header,
                    "LevelTag",
                    "Lv",
                    26,
                    UiPalette.TextSub,
                    TextAlignmentOptions.Midline
                ),
                0
            );
            view.Level = Label(
                header,
                "Level",
                "",
                50,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(view.Level, 0);
            view.Elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(header, "Element" + i, 12f * Dot))
                .ToArray();

            // 立ち姿（余白を切って4倍）と足元の影。右にステータスを2列で置く。
            var figureSize = new Vector2(PartyArt.Figure.width, PartyArt.Figure.height) * Dot;
            var foot = Picture(
                panel,
                "Shadow",
                ArtAssets.LoadSprite(UiArt.ShadowPath),
                Vector2.zero,
                new Vector2(150, 24),
                0.55f
            );
            Layout.At(foot, Pad + (figureSize.x - 150) / 2f, 100 + figureSize.y - 14, 150, 24);
            var figure = Layout.Box(panel, "Figure", Pad, 100, figureSize.x, figureSize.y);
            view.Figure = figure.gameObject.AddComponent<RawImage>();
            view.Figure.raycastTarget = false;
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = Dot;
            view.Stats = TrainingAssets.BuildStats(
                panel,
                Pad + figureSize.x + 28,
                104,
                width - figureSize.x - 28,
                2
            );

            // 装備（武器・防具、準備中のアクセサリー3つ）と、デッキの4枚（固有スキル2・カスタムスキル2）のアイコン。見本なので押せない。
            Layout.Text(
                panel,
                "GearTitle",
                "装備",
                26,
                UiPalette.Gold,
                TextAlignmentOptions.BottomLeft,
                Pad + 2,
                320,
                200,
                36
            );
            view.Gear = Enumerable
                .Range(0, 2)
                .Select(i =>
                {
                    var box = IconBox(
                        panel,
                        "Gear" + i,
                        Pad + i * (BoxSize + BoxGap),
                        360,
                        Color.white
                    );
                    var image = Layout
                        .Box(
                            box,
                            "Icon",
                            (BoxSize - BoxIcon) / 2f,
                            (BoxSize - BoxIcon) / 2f,
                            BoxIcon,
                            BoxIcon
                        )
                        .gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                    return image;
                })
                .ToArray();
            for (int i = 0; i < 3; i++)
            {
                var box = IconBox(
                    panel,
                    "Accessory" + i,
                    Pad + (2 + i) * (BoxSize + BoxGap),
                    360,
                    Locked
                );
                var lockImage = Layout.Box(
                    box,
                    "Lock",
                    (BoxSize - 36) / 2f,
                    (BoxSize - 40) / 2f,
                    36,
                    40
                );
                SpriteImage(lockImage, TrainingAssets.LockPath, Color.white);
            }
            Layout.Text(
                panel,
                "CardsTitle",
                "スキル",
                26,
                UiPalette.Gold,
                TextAlignmentOptions.BottomLeft,
                Pad + 2,
                470,
                200,
                36
            );
            view.Cards = Enumerable
                .Range(0, DeckCards)
                .Select(i =>
                {
                    var box = IconBox(
                        panel,
                        "Card" + i,
                        Pad + i * (BoxSize + BoxGap),
                        510,
                        Color.white
                    );
                    var art = Layout
                        .Box(
                            box,
                            "Art",
                            (BoxSize - BoxIcon) / 2f,
                            (BoxSize - BoxIcon) / 2f,
                            BoxIcon,
                            BoxIcon
                        )
                        .gameObject.AddComponent<RawImage>();
                    art.uvRect = ArtCrop;
                    art.raycastTarget = false;
                    return art;
                })
                .ToArray();

            // 選んだ人の個別の画面（装備・スキル・育成）を開くボタン。
            var open = Layout.Box(panel, "Open", 26, 630, Chosen.width - 52, 96);
            view.Open = Guide.AddButton(
                open,
                Guide.Frame(open, Guide.FrameSelectedPath, Color.white)
            );
            var openRow = Layout.Row(
                open,
                "Line",
                0,
                0,
                Chosen.width - 52,
                96,
                18,
                TextAnchor.MiddleCenter
            );
            var gearIcon = Rect("Icon", openRow);
            Layout.Fit(gearIcon, 48);
            var gearImage = Rect("Image", gearIcon);
            Place(gearImage, Vector2.zero, Vector2.one * 48);
            gearImage.gameObject.AddComponent<Image>().sprite = Guide.Icon(
                EquipmentAssets.IconWeaponPath
            );
            gearImage.GetComponent<Image>().raycastTarget = false;
            Layout.Fit(
                Label(
                    openRow,
                    "Label",
                    "装備・スキル・育成",
                    36,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Midline
                ),
                0
            );
            var arrowCell = Rect("Arrow", openRow);
            Layout.Fit(arrowCell, 5 * 4);
            var arrow = Rect("Image", arrowCell);
            Place(arrow, Vector2.zero, new Vector2(5, 9) * 4f);
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);

            // パーティから外す・入れる・やめる。
            var action = Layout.Box(panel, "Action", 26, 738, Chosen.width - 52, 92);
            view.Action = Guide.AddButton(
                action,
                Guide.Frame(action, Guide.FramePath, Color.white)
            );
            view.ActionLabel = Label(
                action,
                "Label",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)view.ActionLabel.transform);
        }

        private static RectTransform IconBox(
            RectTransform panel,
            string name,
            float x,
            float y,
            Color color
        )
        {
            var box = Layout.Box(panel, name, x, y, BoxSize, BoxSize);
            Guide.Frame(box, Guide.FramePath, color).raycastTarget = false;
            return box;
        }

        // --- Right: the party's four slots, then the bench -----------------------------------

        private static void BuildRoster(
            RectTransform layer,
            AdventurerRosterView view,
            PartyMockData party,
            Sprite shadow
        )
        {
            var panel = Layout.FrameBox(
                layer,
                "Roster",
                Roster.x,
                Roster.y,
                Roster.width,
                Roster.height
            );
            Layout.Text(
                panel,
                "PartyTitle",
                "パーティ",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomLeft,
                34,
                12,
                300,
                36
            );
            view.Owned = Layout.Text(
                panel,
                "Owned",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomRight,
                Roster.width - 34 - 300,
                12,
                300,
                36
            );
            view.Slots = Enumerable
                .Range(0, PartyFormation.Size)
                .Select(i =>
                    BuildSlot(
                        panel,
                        i,
                        28 + i * (TileSize.x + TileGap),
                        54,
                        party.Members[0],
                        shadow
                    )
                )
                .ToArray();

            Layout.Text(
                panel,
                "BenchTitle",
                "控え",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.BottomLeft,
                34,
                300,
                300,
                36
            );
            var bench = Layout.Box(
                panel,
                "Bench",
                20,
                342,
                Roster.width - 40,
                Roster.height - 342 - 18
            );
            var viewport = Rect("Viewport", bench);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);
            var content = Rect("Tiles", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = TileSize;
            grid.spacing = Vector2.one * TileGap;
            grid.padding = new RectOffset(8, 8, 0, 8);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            var formation = PartyFormation.From(party);
            view.Tiles = party
                .Members.Select(member => BuildTile(content, member, formation, shadow))
                .ToArray();

            var scroll = bench.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.List = scroll;
        }

        private static AdventurerSlotWidget BuildSlot(
            RectTransform panel,
            int index,
            float x,
            float y,
            PartyMember art,
            Sprite shadow
        )
        {
            var (button, selected) = Layout.Choice(
                panel,
                "Slot" + index,
                x,
                y,
                TileSize.x,
                TileSize.y
            );
            var tile = (RectTransform)button.transform;
            var widget = new AdventurerSlotWidget { Button = button, Selected = selected };
            (widget.Figure, widget.Shadow) = BuildFigure(tile, art, shadow);
            (widget.Name, widget.Level, widget.Elements) = BuildNameLines(tile);

            widget.Empty = Label(
                tile,
                "Empty",
                "空き",
                40,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)widget.Empty.transform);
            widget.Empty.gameObject.SetActive(false);

            // 控えの人を入れる相手を選んでいる間だけ出す札。
            var swap = Layout.Box(tile, "Swap", TileSize.x - 156, 10, 146, 36);
            AddImage(swap, TagColor, false);
            var swapLabel = Label(
                swap,
                "Label",
                "⇄ 入れ替え",
                22,
                new Color(0.05f, 0.08f, 0.19f),
                TextAlignmentOptions.Center,
                shadow: false
            );
            Stretch((RectTransform)swapLabel.transform);
            swap.gameObject.SetActive(false);
            widget.Swap = swap.gameObject;
            return widget;
        }

        private static AdventurerTileWidget BuildTile(
            RectTransform content,
            PartyMember member,
            PartyFormation formation,
            Sprite shadow
        )
        {
            var rect = Rect("Member_" + member.Id, content);
            var button = Guide.AddButton(
                rect,
                Guide.Frame(rect, Guide.FramePath, new Color(1f, 1f, 1f, 0.9f))
            );
            var selected = Rect("Selected", rect);
            Stretch(selected);
            Guide.Frame(selected, Guide.FrameSelectedPath, Color.white).raycastTarget = false;
            selected.gameObject.SetActive(false);
            var widget = new AdventurerTileWidget
            {
                Id = member.Id,
                Button = button,
                Selected = selected.gameObject,
                Group = rect.gameObject.AddComponent<CanvasGroup>(),
            };
            var (figure, _) = BuildFigure(rect, member, shadow);
            PartyArt.Paint(figure, member, trim: true);
            var (name, level, elements) = BuildNameLines(rect);
            name.text = member.Name;
            level.text = PartyArt.LevelText(member.Level);
            widget.Level = level;
            widget.Elements = elements;
            rect.gameObject.SetActive(formation.SlotOf(member.Id) < 0);
            return widget;
        }

        // 立ち姿（余白を切って3倍）と足元の影を、タイルの上の中央に置く。
        private static (RawImage Figure, GameObject Shadow) BuildFigure(
            RectTransform tile,
            PartyMember member,
            Sprite shadow
        )
        {
            var size = new Vector2(PartyArt.Figure.width, PartyArt.Figure.height) * TileDot;
            float x = (TileSize.x - size.x) / 2f;
            var foot = Picture(tile, "Shadow", shadow, Vector2.zero, new Vector2(120, 20), 0.5f);
            Layout.At(foot, (TileSize.x - 120) / 2f, 6 + size.y - 12, 120, 20);
            var figure = Layout.Box(tile, "Figure", x, 6, size.x, size.y);
            var image = figure.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            PartyArt.Paint(image, member, trim: true);
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = TileDot;
            return (image, foot.gameObject);
        }

        // 名前と、その下のLv・属性のアイコン。
        private static (TMP_Text Name, TMP_Text Level, Image[] Elements) BuildNameLines(
            RectTransform tile
        )
        {
            var name = Layout.Text(
                tile,
                "Name",
                "",
                28,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                8,
                160,
                TileSize.x - 16,
                36
            );
            Guide.Shrink(name, 20);
            var line = Layout.Row(
                tile,
                "LevelLine",
                8,
                196,
                TileSize.x - 16,
                34,
                8,
                TextAnchor.MiddleCenter
            );
            var level = Label(line, "Level", "", 24, UiPalette.Gold, TextAlignmentOptions.Midline);
            Layout.Fit(level, 0);
            var elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(line, "Element" + i, 12f * 2f))
                .ToArray();
            return (name, level, elements);
        }
    }
}
