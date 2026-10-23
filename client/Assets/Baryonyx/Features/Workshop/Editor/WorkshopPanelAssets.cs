using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// The parts the trading house's full-screen panels share (buy, sell, craft and enhance): the
    /// two panes split by the gold double line, the tabs over the left pane, the scrolling grid or
    /// list under them, the cards and rows, the big art on the right with a soft light behind it,
    /// and a price with the rune icon. Positions are design pixels from the top left of the
    /// 1920x1080 layer, as in the mockups.
    /// </summary>
    public static class WorkshopPanelAssets
    {
        // 左の面の上のタブと、その下の一覧の始まり。
        public const float TabTop = GuideFrameAssets.HeaderHeight + 12f;
        public const float TabHeight = 72f;
        public const float BodyTop = TabTop + TabHeight + 8f;

        // 面の端と中身の間。左右の面とも、画面の端の側は枠と同じ48、区切りの線の側は24空ける。
        public const float Edge = GuideFrameAssets.Margin;
        public const float Inner = ListGap;

        // 下端の決めるボタンの上端。右の面の中身はこれより上に置く。
        public const float ConfirmTop = 1080f - ConfirmBottom - ConfirmHeight;

        // 一覧の行・カードの間。
        public const float Gap = 22f;

        private static readonly Color Glow = new(1f, 0.82f, 0.5f, 0.14f);

        /// <summary>A full-screen panel with the view <typeparamref name="T"/> and its two panes split at <paramref name="split"/>.</summary>
        public static (T View, RectTransform Layer) Panel<T>(
            RectTransform safe,
            string name,
            float split
        )
            where T : Component
        {
            var (view, layer) = GuideFrameAssets.FramedPanel<T>(safe, name);
            GuideFrameAssets.SplitPanes(layer, split, "LeftPane", "RightPane");
            return (view, layer);
        }

        /// <summary>The tabs over the left pane, from its left edge to the line at <paramref name="split"/>.</summary>
        public static GuideTab[] Tabs(RectTransform layer, string[] labels, float split)
        {
            float width = split - Edge - Inner;
            var tabs = GuideFrameAssets.Tabs(
                layer,
                "Tabs",
                labels,
                Edge,
                TabTop,
                width / labels.Length,
                TabHeight,
                width
            );
            if (tabs.Length > 0)
                tabs[0].Chosen = true;
            return tabs;
        }

        /// <summary>
        /// A scrolling grid of <paramref name="columns"/> cards between <paramref name="left"/> and
        /// <paramref name="right"/>, the cards as wide as the room inside the padding allows.
        /// </summary>
        public static (RectTransform Content, ScrollRect Scroll, Vector2 Cell) Grid(
            RectTransform layer,
            string name,
            float left,
            float right,
            float top,
            float bottom,
            int columns,
            float cellHeight,
            float padLeft = Edge,
            float padRight = Inner
        )
        {
            var viewport = Layout.Box(
                layer,
                name + "Viewport",
                left,
                top,
                right - left,
                bottom - top
            );
            Layout.Mask(viewport);
            float width = right - left - padLeft - padRight;
            var cell = new Vector2((width - Gap * (columns - 1)) / columns, cellHeight);
            var content = Layout.Grid(
                viewport,
                name,
                cell,
                Gap,
                new RectOffset((int)padLeft, (int)padRight, 14, (int)(Gap * 2f)),
                columns
            );
            return (content, Layout.Scroll(viewport, viewport, content), cell);
        }

        /// <summary>A scrolling column of rows between <paramref name="left"/> and <paramref name="right"/>.</summary>
        public static (RectTransform Content, ScrollRect Scroll) Column(
            RectTransform layer,
            string name,
            float left,
            float right,
            float top,
            float bottom,
            int padLeft,
            int padRight
        )
        {
            var viewport = Layout.Box(
                layer,
                name + "Viewport",
                left,
                top,
                right - left,
                bottom - top
            );
            Layout.Mask(viewport);
            var content = Layout.Column(
                viewport,
                name,
                Gap * 0.6f,
                new RectOffset(padLeft, padRight, 14, (int)(Gap * 2f))
            );
            return (content, Layout.Scroll(viewport, viewport, content));
        }

        /// <summary>
        /// A good's card for the buy grid: the art in the middle of the top, the name, the stars,
        /// and the price with the rune icon.
        /// </summary>
        public static WorkshopGoodRow GoodCard(
            RectTransform content,
            string name,
            Vector2 cell,
            float artBox
        )
        {
            var rect = Rect(name, content);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
            var row = rect.gameObject.AddComponent<WorkshopGoodRow>();
            row.Card = card;
            row.Art = ArtBox(inside, (cell.x - artBox) / 2f, 18f, artBox);
            float y = 18f + artBox + 6f;
            row.Name = Layout.Text(
                inside,
                "Name",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                8f,
                y,
                cell.x - 16f,
                40f
            );
            Shrink(row.Name, 20);
            row.Detail = Layout.Text(
                inside,
                "Stars",
                "",
                24,
                UiPalette.Gold,
                TextAlignmentOptions.Center,
                8f,
                y + 38f,
                cell.x - 16f,
                28f
            );
            row.Price = RunePrice(
                inside,
                "Price",
                0f,
                y + 66f,
                cell.x,
                40f,
                32f,
                TextAnchor.MiddleCenter
            );
            return row;
        }

        /// <summary>
        /// A held thing's card (sell and the craft's materials): the art in the middle, the
        /// quality's letter at the top left, the count at the bottom right, and an "E" tag at the
        /// top right while someone uses it.
        /// </summary>
        public static WorkshopGoodRow OwnedCard(
            RectTransform content,
            string name,
            Vector2 cell,
            float artBox
        )
        {
            var rect = Rect(name, content);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
            var row = rect.gameObject.AddComponent<WorkshopGoodRow>();
            row.Card = card;
            row.Art = ArtBox(inside, (cell.x - artBox) / 2f, (cell.y - artBox) / 2f, artBox);
            row.Quality = Layout.Text(
                inside,
                "Quality",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft,
                16f,
                10f,
                60f,
                44f
            );
            row.Count = Layout.Text(
                inside,
                "Count",
                "",
                26,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomRight,
                cell.x - 96f,
                cell.y - 46f,
                82f,
                36f
            );

            var tag = Layout.Box(inside, "InUse", cell.x - 46f, 10f, 36f, 36f);
            AddImage(tag, new Color(0.03f, 0.04f, 0.1f, 0.85f), false);
            var letter = Label(
                tag,
                "Label",
                "E",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)letter.transform);
            tag.gameObject.SetActive(false);
            row.InUse = tag.gameObject;
            return row;
        }

        /// <summary>
        /// A row in a list (the craft's recipes, the enhance targets, the things to sell): the art
        /// on the left, the name, a line under it, and a text <paramref name="valueWidth"/> wide at
        /// the right end, which the name and the line stop short of.
        /// </summary>
        public static WorkshopGoodRow ListRow(
            RectTransform content,
            string name,
            float height,
            float artBox,
            float nameSize = 40f,
            float valueWidth = 0f
        )
        {
            var rect = Layout.ListItem(content, name, height);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
            var row = rect.gameObject.AddComponent<WorkshopGoodRow>();
            row.Card = card;
            var box = Rect("ArtBox", inside);
            Corner(box, new Vector2(0, 0.5f), new Vector2(20f, 0), Vector2.one * artBox);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * artBox);
            row.Art = art.gameObject.AddComponent<RawImage>();
            row.Art.raycastTarget = false;

            float textLeft = 20f + artBox + 20f;
            float half = height / 2f;
            row.Name = Label(
                inside,
                "Name",
                "",
                nameSize,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            float textRight = valueWidth > 0f ? valueWidth + 40f : 24f;
            Band((RectTransform)row.Name.transform, top: true, 4f, half - 6f, textLeft, textRight);
            Shrink(row.Name, 24);
            row.Detail = Label(
                inside,
                "Detail",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            row.Detail.richText = true;
            Band(
                (RectTransform)row.Detail.transform,
                top: false,
                6f,
                half - 10f,
                textLeft + 2f,
                textRight
            );
            Shrink(row.Detail, 18);
            row.Price = Label(
                inside,
                "Value",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            row.Price.richText = true;
            var value = (RectTransform)row.Price.transform;
            value.anchorMin = new Vector2(1f, 0);
            value.anchorMax = Vector2.one;
            value.offsetMin = new Vector2(-24f - valueWidth, 0);
            value.offsetMax = new Vector2(-24f, 0);
            Shrink(row.Price, 22);
            return row;
        }

        /// <summary>
        /// The chosen thing's big art on the right, with a soft warm light behind it, hidden with
        /// its light until something is chosen.
        /// </summary>
        public static RawImage ArtStage(
            RectTransform layer,
            string name,
            float x,
            float y,
            float box
        )
        {
            var stage = Layout.Box(layer, name, x, y, box, box);
            var glow = Rect("Glow", stage);
            Place(glow, Vector2.zero, Vector2.one * box * 1.5f);
            SpriteImage(glow, SoftSpotPath, Glow);
            var art = Rect("Art", stage);
            Place(art, Vector2.zero, Vector2.one * box);
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            // 選ぶまでは、光ごと隠しておく。
            stage.gameObject.SetActive(false);
            return image;
        }

        /// <summary>A price: the rune icon and the number after it, aligned by <paramref name="alignment"/>.</summary>
        public static TMP_Text RunePrice(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            float size,
            TextAnchor alignment
        )
        {
            var row = Layout.Row(parent, name, x, y, width, height, size * 0.25f, alignment);
            var cell = Rect("Icon", row);
            Layout.Fit(cell, size);
            var icon = Rect("Image", cell);
            Place(icon, Vector2.zero, Vector2.one * size);
            SpriteImage(icon, IconRunePath, Color.white).preserveAspect = true;
            var label = Label(
                row,
                "Value",
                "",
                size,
                UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(label, 0);
            return label;
        }

        /// <summary>The text shown in a list's place while it reads, fails or is empty.</summary>
        public static TMP_Text Status(RectTransform layer, float left, float right, float top) =>
            Layout.Text(
                layer,
                "Status",
                "",
                34,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                left,
                top + 24f,
                right - left,
                EntryHeight
            );

        private static RawImage ArtBox(RectTransform parent, float x, float y, float size)
        {
            var box = Layout.Box(parent, "ArtBox", x, y, size, size);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * size);
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }
    }
}
