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
using Card = Baryonyx.Tavern.Editor.AdventurerCardAssets;
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
    /// (<see cref="GuideFramedPanel"/>): a pane for the party and one for the list, both
    /// the scene shows through, and the gold double line between them, over the band at the
    /// bottom with the person chosen and the button to their tabs. Positions are design pixels
    /// from the top left of a 1920x1080 layer (<see cref="GuidePanelLayout"/>), matching the
    /// mockup the player chose.
    /// </summary>
    public static class AdventurerRosterAssets
    {
        // 左（パーティ）と右（一覧）の境目と、見出し・カードの位置。設計座標、左上から。
        private const float Split = 660f;
        private const float Gap = 24f;
        private const float HeadingHeight = 76f;
        private const float CardGap = Card.CardGap;
        private const float PartyLeft = Frame.Margin;
        private const float PartyWidth = Split - Gap - Frame.Margin;
        private const float ListLeft = Split + Gap;
        private const float ListWidth = 1920f - Frame.Margin - ListLeft;
        private const float CardTop = Frame.HeaderHeight + HeadingHeight + CardGap;

        // 下の帯（選んでいる人と「装備・スキル」）の上端と高さ。
        private const float BandHeight = 140f;
        private const float BandTop = 1080f - BandHeight;

        // カード1枚の高さ。パーティの4枚が帯の上まで並ぶ高さにし、一覧も同じ高さで2列に並べる。
        private static readonly float CardHeight = (BandTop - CardTop - CardGap * 4f) / 4f;
        private static readonly float ListCardWidth = (ListWidth - CardGap) / 2f;

        private const float FigureCenter = Card.FigureCenter;

        // 名前とLvの行の左端と右の余白。人選びのカードより立ち姿へ寄せ、
        // 「Lv」・2桁のLv・属性のアイコン3つが縮まずに入るようにする。
        private const float TextLeft = 232f;
        private const float TextGap = 24f;

        // 帯の「装備・スキル」の幅と、その中の金の矢印（5×9ドットを4倍）。
        private const float DetailWidth = 420f;
        private const float Dot = Guide.DotScale;

        /// <summary>
        /// Builds the adventurers over the guide screen's safe area. Called while the guide
        /// screen's prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            GlassCardAssets.EnsureArt();

            var (view, layer) = Frame.FramedPanel<AdventurerRosterView>(safe, "Adventurers");
            view.Party = party;
            view.Guide = guide;

            Frame.SplitPanes(layer, Split, "PartyPane", "ListPane");
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
            BuildBand(layer, view, party);

            // 仮データの編成で描き、Prefabを開くと停止中でも一覧が読めるようにする。
            new PartyRosterPresenter(view, PartyFormation.From(party)).Dispose();
            return view.gameObject;
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
                    (widget.Figure, widget.Shadow) = Card.Figure(
                        content,
                        party.Members[0],
                        shadow,
                        CardHeight
                    );
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
            var (list, viewport, content) = Card.Grid(
                layer,
                Split,
                Frame.HeaderHeight + HeadingHeight,
                new Vector2(ListCardWidth, CardHeight),
                new RectOffset(
                    (int)Gap,
                    (int)Frame.Margin,
                    (int)CardGap,
                    (int)(CardGap + Frame.Margin)
                ),
                2,
                BandTop
            );

            var formation = PartyFormation.From(party);
            view.Tiles = party
                .Members.Select(member => BuildTile(content, member, formation, shadow))
                .ToArray();
            view.Remove = BuildRemove(content);

            view.List = Layout.Scroll(list, viewport, content);

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
            var (figure, _) = Card.Figure(inside, member, shadow, CardHeight);
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

        // --- Bottom: the person chosen and the button to their tabs --------------------

        // 下の帯：左に選んでいる人の立ち姿・名前・Lv・属性、右端に金の縁の「装備・スキル ▶」。
        // 押すと選ぶだけのカードと分け、詳しい画面へは帯のボタン1つから入る（2026-10-26）。
        private static void BuildBand(
            RectTransform layer,
            AdventurerRosterView view,
            PartyMockData party
        )
        {
            // 帯の地は、パーティと一覧の面の間の線が透けないよう濃い紺で不透明に塗り、画面の左右と下の端の外まで敷く。
            var back = Layout.Box(
                layer,
                "BandBack",
                -Frame.Bleed,
                BandTop,
                Layout.Design.x + Frame.Bleed * 2f,
                BandHeight + Frame.Bleed
            );
            var dark = Frame.PaneDark;
            AddImage(back, new Color(dark.r, dark.g, dark.b, 1f), true);
            AddImage(
                Layout.Box(back, "Line", 0, 0, Layout.Design.x + Frame.Bleed * 2f, 2f),
                Frame.LineColor,
                false
            );
            var band = Layout.Box(layer, "Band", 0, BandTop, Layout.Design.x, BandHeight);
            var widget = view.Chosen;

            var size = new Vector2(PartyArt.Figure.width, PartyArt.Figure.height) * 2f;
            var figure = Layout.Box(
                band,
                "Figure",
                Frame.Margin + 20f,
                (BandHeight - size.y) / 2f,
                size.x,
                size.y
            );
            widget.Figure = figure.gameObject.AddComponent<RawImage>();
            widget.Figure.raycastTarget = false;
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = 2f;

            var line = Layout.Row(
                band,
                "Person",
                Frame.Margin + 40f + size.x,
                0,
                900f,
                BandHeight,
                24f
            );
            widget.Name = Label(
                line,
                "Name",
                "",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.Midline
            );
            Layout.Fit(widget.Name, 0);
            var level = Rect("LevelLine", line);
            var group = level.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            widget.LevelLine = level.gameObject;
            widget.Level = Card.Level(level, "");
            widget.Elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(level, "Element" + i, 12f * 3f))
                .ToArray();

            var (button, rim, inside) = GlassCardAssets.Choice(
                Layout.Box(
                    band,
                    "Detail",
                    Layout.Design.x - Frame.Margin - DetailWidth,
                    20f,
                    DetailWidth,
                    BandHeight - 40f
                ),
                pattern: false
            );
            rim.SetActive(true);
            widget.Detail = button;
            widget.DetailGroup = button.GetComponent<GlassCard>().Group;
            var row = Layout.Row(
                inside,
                "Row",
                0,
                0,
                DetailWidth,
                BandHeight - 40f,
                18f,
                TextAnchor.MiddleCenter
            );
            Layout.Fit(
                Label(
                    row,
                    "Label",
                    DetailLabel,
                    36,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Midline
                ),
                0
            );
            var cell = Rect("Arrow", row);
            Layout.Fit(cell, 5 * Dot);
            var arrow = Rect("Image", cell);
            Place(arrow, Vector2.zero, new Vector2(5, 9) * Dot);
            SpriteImage(arrow, Guide.ArrowPath, UiPalette.Gold);

            AdventurerRosterView.ShowChosen(widget, null, 1, null);
        }

        /// <summary>The band's button that opens the person's tabs.</summary>
        public const string DetailLabel = "装備・スキル";

        // 名前と、その下の「Lv」・Lv・属性のアイコン（12×12を3倍）。
        private static (
            TMP_Text Name,
            GameObject Line,
            TMP_Text Level,
            Image[] Elements
        ) BuildNameLines(RectTransform card, float width)
        {
            float middle = CardHeight / 2f;
            float textWidth = width - TextLeft - TextGap;
            var name = Card.Name(card, "", middle - 66f, textWidth, TextLeft);
            var line = Layout.Row(card, "LevelLine", TextLeft, middle + 4f, textWidth, 56f, 10f);
            var level = Card.Level(line, "");
            var elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(line, "Element" + i, 12f * 3f))
                .ToArray();
            return (name, line.gameObject, level, elements);
        }
    }
}
