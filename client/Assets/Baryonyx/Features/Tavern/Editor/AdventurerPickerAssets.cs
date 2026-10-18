using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.Equipment;
using Baryonyx.Equipment.Editor;
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
    /// The formation's equipment item: a card for every mock character, three to a row, baked
    /// into the formation prefab by its generator (<see cref="BuildPanel"/>) so they read in the
    /// editor without Play Mode. The cards are the adventurers' frosted glass
    /// (<see cref="GlassCardAssets"/>) inside the guide screen's frame
    /// (<see cref="GuideFramedPanel"/>), on one pane the scene shows through. Positions are design
    /// pixels from the top left of a 1920x1080 layer (<see cref="GuidePanelLayout"/>).
    /// </summary>
    public static class AdventurerPickerAssets
    {
        public const string Heading = "冒険者を選ぶ";

        private const float Dot = Guide.DotScale;
        private const int Columns = 3;
        private const float HeadingHeight = 76f;
        private const float CardGap = 16f;
        private const float ListTop = Frame.HeaderHeight + HeadingHeight;
        private const float CardWidth =
            (1920f - Frame.Margin * 2f - CardGap * (Columns - 1)) / Columns;

        // 9人が3行でスクロールせずに収まる高さ。
        private const float CardHeight =
            (1080f - ListTop - CardGap - Frame.Margin - CardGap * 2f) / 3f;

        // カードの中：立ち姿（余白を切って4倍）の中心、文字の左端と幅。
        private const float FigureCenter = 120f;
        private const float TextLeft = 262f;
        private const float TextWidth = CardWidth - TextLeft - 24f;

        // 武器・防具のアイコン（24×24を2倍）。
        private const float GearIcon = 24f * 2f;

        /// <summary>
        /// Builds the picker over the guide screen's safe area. Called while the guide screen's
        /// prefab is being built, so the shared frames and font are ready.
        /// </summary>
        public static GameObject BuildPanel(RectTransform safe, GuideMenuView guide)
        {
            var party = PartyAssets.CreateData();
            var shadow = ArtAssets.LoadSprite(UiArt.ShadowPath);
            var weapon = Guide.Icon(EquipmentAssets.IconWeaponPath);
            var armor = Guide.Icon(EquipmentAssets.IconArmorPath);
            GlassCardAssets.EnsureArt();

            var root = Rect("GearPicker", safe);
            Stretch(root);
            root.gameObject.AddComponent<GuideFramedPanel>();
            var view = root.gameObject.AddComponent<AdventurerPickerView>();
            view.Party = party;
            view.Guide = guide;

            var layer = Layout.Layer(root, "Layout");
            Frame.Pane(layer, "Pane", 0f, 1920f, Frame.HeaderHeight, Frame.PaneLight, true, true);
            Frame.Heading(
                layer,
                "Title",
                Heading,
                Frame.Margin,
                Frame.HeaderHeight,
                600f,
                HeadingHeight
            );

            // カードの影と選んだときの光が切れないよう、表示範囲はカードより広く取る。
            var list = Layout.Box(layer, "List", 0f, ListTop, 1920f, Layout.Design.y - ListTop);
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
            grid.cellSize = new Vector2(CardWidth, CardHeight);
            grid.spacing = Vector2.one * CardGap;
            grid.padding = new RectOffset(
                (int)Frame.Margin,
                (int)Frame.Margin,
                (int)CardGap,
                (int)(CardGap + Frame.Margin)
            );
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            view.Cards = party
                .Members.Select(member => BuildCard(content, member, shadow, weapon, armor))
                .ToArray();

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
                Frame.Margin,
                ListTop + CardGap,
                1920f - Frame.Margin * 2f,
                CardHeight
            );

            // 仮データの編成と装備で描き、Prefabを開くと停止中でも読めるようにする。
            view.Render(PartyFormation.From(party), EquipmentLocalSource.Starter());
            return root.gameObject;
        }

        private static AdventurerPickWidget BuildCard(
            RectTransform content,
            PartyMember member,
            Sprite shadow,
            Sprite weapon,
            Sprite armor
        )
        {
            var rect = Rect("Member_" + member.Id, content);
            var (card, inside) = GlassCardAssets.Build(rect);
            var widget = new AdventurerPickWidget { Id = member.Id, Card = card };
            BuildFigure(inside, member, shadow);

            var name = Layout.Text(
                inside,
                "Name",
                member.Name,
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft,
                TextLeft,
                20f,
                TextWidth,
                60f
            );
            Guide.Shrink(name, 28);

            var line = Layout.Row(inside, "LevelLine", TextLeft, 84f, TextWidth, 48f, 16f);
            Layout.Fit(
                Label(line, "LevelTag", "Lv", 28, UiPalette.Gold, TextAlignmentOptions.Bottom),
                0
            );
            widget.Level = Label(
                line,
                "Level",
                member.Level.ToString(),
                40,
                UiPalette.Gold,
                TextAlignmentOptions.Bottom
            );
            Layout.Fit(widget.Level, 0);
            var tag = Label(
                line,
                "PartyTag",
                "パーティ",
                26,
                UiPalette.GoldLight,
                TextAlignmentOptions.Bottom
            );
            Layout.Fit(tag, 0);
            widget.PartyTag = tag.gameObject;

            (widget.WeaponIcon, widget.Weapon) = BuildGear(inside, "Weapon", 148f, weapon);
            (widget.ArmorIcon, widget.Armor) = BuildGear(inside, "Armor", 200f, armor);
            return widget;
        }

        // 種類のアイコンと、付けている装備の名前の1行。
        private static (Image Icon, TMP_Text Name) BuildGear(
            RectTransform card,
            string name,
            float y,
            Sprite sprite
        )
        {
            var row = Layout.Row(card, name, TextLeft, y, TextWidth, GearIcon, 10f);
            var icon = Layout.RowIcon(row, "Icon", GearIcon);
            icon.sprite = sprite;
            var label = Label(
                row,
                "Name",
                AdventurerPickerView.EmptyText,
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(label, TextWidth - GearIcon - 10f);
            Guide.Shrink(label, 22);
            return (icon, label);
        }

        // 立ち姿（余白を切って4倍）と足元の影。足をカードの下端の少し上にそろえる。
        private static void BuildFigure(RectTransform card, PartyMember member, Sprite shadow)
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
        }
    }
}
