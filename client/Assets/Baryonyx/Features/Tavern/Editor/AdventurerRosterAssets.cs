using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Party;
using Baryonyx.Party.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The formation's adventurers. The formation's generator calls <see cref="BuildPanel"/>
    /// for its adventurers item, so the party's four cards on the left and a card for every mock
    /// character on the right are baked into the formation prefab and read in the editor without
    /// Play Mode. The panel fills the screen inside the guide screen's frame
    /// (<see cref="GuideFramedPanel"/>): a dark pane for the party, a pane the scene shows through
    /// for the list, and the gold double line between them. Positions are design pixels from the
    /// top left of a 1920x1080 layer (<see cref="GuidePanelLayout"/>), matching the mockup the
    /// player chose.
    /// </summary>
    public static class AdventurerRosterAssets
    {
        private const float Dot = Guide.DotScale;

        // 左（パーティ）と右（一覧）の境目と、見出し・カードの位置。設計座標、左上から。
        private const float Split = 600f;
        private const float Gap = 24f;
        private const float HeadingHeight = 76f;
        private const float CardGap = 16f;
        private const float PartyLeft = Frame.Margin;
        private const float PartyWidth = Split - Gap - Frame.Margin;
        private const float ListLeft = Split + Gap;
        private const float ListWidth = 1920f - Frame.Margin - ListLeft;
        private const float CardTop = Frame.HeaderHeight + HeadingHeight + CardGap;

        // カード1枚の高さ。パーティの4枚が下端まで並ぶ高さにし、一覧も同じ高さで2列に並べる。
        private static readonly float CardHeight = (Layout.Design.y - CardTop - CardGap * 4f) / 4f;
        private static readonly float ListCardWidth = (ListWidth - CardGap) / 2f;

        // カードの中：立ち姿（余白を切って4倍）の中心、文字の左端。
        private const float FigureCenter = 120f;
        private const float TextLeft = 262f;

        /// <summary>
        /// Builds the adventurers over the guide screen's safe area. Called while the guide
        /// screen's prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            GlassCardAssets.EnsureArt();

            var root = Rect("Adventurers", safe);
            Stretch(root);
            root.gameObject.AddComponent<GuideFramedPanel>();
            var view = root.gameObject.AddComponent<AdventurerRosterView>();
            view.Party = party;
            view.Guide = guide;

            var layer = Layout.Layer(root, "Layout");
            Frame.Pane(
                layer,
                "PartyPane",
                0f,
                Split,
                Frame.HeaderHeight,
                Frame.PaneDark,
                true,
                false
            );
            Frame.Pane(
                layer,
                "ListPane",
                Split,
                1920f,
                Frame.HeaderHeight,
                Frame.PaneLight,
                false,
                true
            );
            Frame.Divider(layer, Split, Frame.HeaderHeight);
            Frame.Heading(
                layer,
                "PartyTitle",
                "パーティ",
                PartyLeft,
                Frame.HeaderHeight,
                300f,
                HeadingHeight
            );
            Frame.Heading(
                layer,
                "ListTitle",
                "一覧",
                ListLeft,
                Frame.HeaderHeight,
                300f,
                HeadingHeight
            );

            BuildParty(layer, view, party, shadow);
            BuildList(layer, view, party, shadow);

            // 仮データの編成で描き、Prefabを開くと停止中でも一覧が読めるようにする。
            new PartyRosterPresenter(view, PartyFormation.From(party)).Dispose();
            return root.gameObject;
        }

        // --- Left: the party's four cards ----------------------------------------------------

        private static void BuildParty(
            RectTransform layer,
            AdventurerRosterView view,
            PartyMockData party,
            Sprite shadow
        )
        {
            view.Slots = Enumerable
                .Range(0, PartyFormation.Size)
                .Select(i =>
                {
                    var rect = Layout.Box(
                        layer,
                        "Slot" + i,
                        PartyLeft,
                        CardTop + i * (CardHeight + CardGap),
                        PartyWidth,
                        CardHeight
                    );
                    var (card, content) = GlassCardAssets.Build(rect);
                    var widget = new AdventurerSlotWidget { Card = card };
                    (widget.Figure, widget.Shadow) = BuildFigure(content, party.Members[0], shadow);
                    (widget.Name, widget.LevelLine, widget.Level, widget.Elements) = BuildNameLines(
                        content,
                        PartyWidth
                    );
                    widget.Empty = Label(
                        content,
                        "Empty",
                        "空き",
                        40,
                        UiPalette.TextSub,
                        TextAlignmentOptions.Center
                    );
                    Stretch((RectTransform)widget.Empty.transform);
                    widget.Empty.gameObject.SetActive(false);
                    return widget;
                })
                .ToArray();
        }

        // --- Right: the characters outside the party, then "remove from the party" ----------

        private static void BuildList(
            RectTransform layer,
            AdventurerRosterView view,
            PartyMockData party,
            Sprite shadow
        )
        {
            // カードの影と選んだときの光が切れないよう、表示範囲はカードより広く取る。
            float top = Frame.HeaderHeight + HeadingHeight;
            var list = Layout.Box(layer, "List", Split, top, 1920f - Split, Layout.Design.y - top);
            var viewport = Rect("Viewport", list);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);
            var content = Rect("Cards", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(ListCardWidth, CardHeight);
            grid.spacing = Vector2.one * CardGap;
            grid.padding = new RectOffset(
                (int)Gap,
                (int)Frame.Margin,
                (int)CardGap,
                (int)(CardGap + Frame.Margin)
            );
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            var formation = PartyFormation.From(party);
            view.Tiles = party
                .Members.Select(member => BuildTile(content, member, formation, shadow))
                .ToArray();
            view.Remove = BuildRemove(content);

            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            view.List = scroll;

            view.Status = Layout.Text(
                layer,
                "Status",
                "",
                36,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                ListLeft,
                CardTop,
                ListWidth,
                CardHeight
            );
        }

        private static AdventurerTileWidget BuildTile(
            RectTransform content,
            PartyMember member,
            PartyFormation formation,
            Sprite shadow
        )
        {
            var rect = Rect("Member_" + member.Id, content);
            var (card, inside) = GlassCardAssets.Build(rect);
            var widget = new AdventurerTileWidget { Id = member.Id, Card = card };
            var (figure, _) = BuildFigure(inside, member, shadow);
            PartyArt.Paint(figure, member, trim: true);
            var (name, _, level, elements) = BuildNameLines(inside, ListCardWidth);
            name.text = member.Name;
            level.text = member.Level.ToString();
            widget.Level = level;
            widget.Elements = elements;
            rect.gameObject.SetActive(formation.SlotOf(member.Id) < 0);
            return widget;
        }

        // 一覧の最後の「パーティから外す」。点線のカードに×と文字。
        private static GlassCard BuildRemove(RectTransform content)
        {
            var rect = Rect("Remove", content);
            var (card, inside) = GlassCardAssets.Build(rect, dashed: true);
            var icon = Layout.Box(
                inside,
                "Icon",
                FigureCenter - Guide.IconSize / 2f,
                (CardHeight - Guide.IconSize) / 2f,
                Guide.IconSize,
                Guide.IconSize
            );
            SpriteImage(icon, Guide.IconClosePath, Color.white);
            Layout.Text(
                inside,
                "Label",
                PartyRosterPresenter.RemoveLabel,
                40,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                TextLeft,
                0f,
                ListCardWidth - TextLeft - 24f,
                CardHeight
            );
            return card;
        }

        // 立ち姿（余白を切って4倍）と足元の影。足をカードの下端の少し上にそろえる。
        private static (RawImage Figure, GameObject Shadow) BuildFigure(
            RectTransform card,
            PartyMember member,
            Sprite shadow
        )
        {
            var size = new Vector2(PartyArt.Figure.width, PartyArt.Figure.height) * Dot;
            float foot = CardHeight - 12f;
            var feet = Picture(card, "Shadow", shadow, Vector2.zero, new Vector2(150, 24), 0.55f);
            Layout.At(feet, FigureCenter - 75f, foot - 14f, 150, 24);
            var figure = Layout.Box(
                card,
                "Figure",
                FigureCenter - size.x / 2f,
                foot - size.y,
                size.x,
                size.y
            );
            var image = figure.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            PartyArt.Paint(image, member, trim: true);
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = Dot;
            return (image, feet.gameObject);
        }

        // 名前と、その下の「Lv」・Lv・属性のアイコン（12×12を3倍）。
        private static (
            TMP_Text Name,
            GameObject Line,
            TMP_Text Level,
            Image[] Elements
        ) BuildNameLines(RectTransform card, float width)
        {
            float middle = CardHeight / 2f;
            var name = Layout.Text(
                card,
                "Name",
                "",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft,
                TextLeft,
                middle - 66f,
                width - TextLeft - 24f,
                60f
            );
            Guide.Shrink(name, 28);
            var line = Layout.Row(
                card,
                "LevelLine",
                TextLeft,
                middle + 4f,
                width - TextLeft - 24f,
                56f,
                10f
            );
            Layout.Fit(
                Label(line, "LevelTag", "Lv", 28, UiPalette.Gold, TextAlignmentOptions.Bottom),
                0
            );
            var level = Label(line, "Level", "", 40, UiPalette.Gold, TextAlignmentOptions.Bottom);
            Layout.Fit(level, 0);
            var elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(line, "Element" + i, 12f * 3f))
                .ToArray();
            return (name, line.gameObject, level, elements);
        }
    }
}
