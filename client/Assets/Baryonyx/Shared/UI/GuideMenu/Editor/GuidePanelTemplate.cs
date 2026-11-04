using Baryonyx.Editor.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.UI.GuideMenu.Editor
{
    /// <summary>
    /// The skeletons of the framed panels that list things (doc/rules/ui-design.md の画面の型).
    /// A template lays out the panes, the categories down the left, the heading row with the
    /// count, the scrolling list and the status text, and returns where the screen puts its own
    /// cards, rows and detail (<see cref="ListPanel"/>). Screens of the same type are built from
    /// the same template, so they share the split, the heading, the paddings and the gaps.
    /// Positions are design pixels from the top left of the 1920x1080 layer.
    /// </summary>
    public static class GuidePanelTemplate
    {
        // 一覧と詳細の分け目、一覧の上の「◆名前＋数」の段の高さ、一覧の始まり。
        public const float Split = UiSpace.ListSplit;
        public const float HeadingHeight = GuideMenuAssets.ListHeadingHeight;
        public const float ListTop = GuideFrameAssets.HeaderHeight + HeadingHeight;

        // 札・行の間。
        public const float CardGap = UiSpace.Inset;

        // 右の詳細の面の中身の左右（区切りの線の側は24、画面の端の側は48空ける）。
        public const float DetailLeft = Split + UiSpace.Pad;
        public const float DetailRight = 1920f - UiSpace.Edge;
        public const float DetailWidth = DetailRight - DetailLeft;

        // 見出しの段の右に置く数の欄の幅。
        private const float CountWidth = 300f;

        /// <summary>The places a template returns for the screen to fill.</summary>
        public sealed class ListPanel
        {
            public RectTransform Layer;

            /// <summary>The categories down the left edge; empty when the panel has none.</summary>
            public GuideTab[] Tabs = System.Array.Empty<GuideTab>();

            /// <summary>
            /// The heading over the list ("◆武器") and the count at its right end; null on a
            /// lineless panel, where the chosen category says what the list holds.
            /// </summary>
            public TMP_Text Title;
            public TMP_Text Count;

            /// <summary>Where the list starts, under the heading row (or the header without it).</summary>
            public float Top = ListTop;

            /// <summary>The text in the list's top row while it reads, fails or is empty.</summary>
            public TMP_Text Status;

            /// <summary>The list's content (cards in a grid, or rows in a column) and its scroll.</summary>
            public RectTransform Content;
            public ScrollRect Scroll;

            /// <summary>A card's size in a grid (zero in a column).</summary>
            public Vector2 Cell;

            /// <summary>The list's left and right edges inside its paddings.</summary>
            public float ListLeft;
            public float ListRight;
        }

        /// <summary>
        /// A: cards in a grid and the chosen one's detail (the shop's buy and sell, the library's
        /// kinds). The list pane holds <paramref name="tabs"/> down the left (none when null), the
        /// heading row and a grid of <paramref name="columns"/> cards <paramref name="cellHeight"/>
        /// high; the detail pane right of <see cref="Split"/> is the screen's own, from
        /// <see cref="DetailLeft"/> to <see cref="DetailRight"/>, with the decide button at the
        /// bottom right (<see cref="GuideMenuAssets.ConfirmButton"/>).
        /// </summary>
        public static (T View, ListPanel Panel) GridDetail<T>(
            RectTransform safe,
            string name,
            string title,
            int columns,
            float cellHeight,
            string[] tabs = null,
            params int[] locked
        )
            where T : Component
        {
            var (view, panel) = Begin<T>(safe, name, title, tabs, locked);
            float width = panel.ListRight - panel.ListLeft;
            panel.Cell = new Vector2((width - CardGap * (columns - 1)) / columns, cellHeight);
            var viewport = ListViewport(panel, out var list);
            panel.Content = Layout.Grid(
                viewport,
                "Cards",
                panel.Cell,
                CardGap,
                Padding(panel),
                columns
            );
            panel.Scroll = Layout.Scroll(list, viewport, panel.Content);
            return (view, panel);
        }

        /// <summary>
        /// B: rows in a column and the chosen one's detail (the shop's craft and enhancement): the
        /// same panes, categories and heading row as <see cref="GridDetail{T}"/>, with the rows
        /// <paramref name="gap"/> apart.
        /// </summary>
        public static (T View, ListPanel Panel) RowsDetail<T>(
            RectTransform safe,
            string name,
            string title,
            string[] tabs = null,
            float gap = CardGap * 0.8f,
            params int[] locked
        )
            where T : Component
        {
            var (view, panel) = Begin<T>(safe, name, title, tabs, locked);
            var viewport = ListViewport(panel, out var list);
            panel.Content = Layout.Column(viewport, "Rows", gap, Padding(panel));
            panel.Scroll = Layout.Scroll(list, viewport, panel.Content);
            return (view, panel);
        }

        /// <summary>
        /// C: one list across the whole panel (the skills to awaken, the daily steps): one pane, the
        /// heading row and rows <paramref name="gap"/> apart, from <paramref name="left"/> to
        /// <paramref name="right"/> (the panel's edges unless given). With <paramref name="tabs"/>
        /// the categories go down the left and the list starts right of them.
        /// </summary>
        public static (T View, ListPanel Panel) FullList<T>(
            RectTransform safe,
            string name,
            string title,
            float gap = CardGap * 0.75f,
            float left = UiSpace.Edge,
            float right = 1920f - UiSpace.Edge,
            string[] tabs = null
        )
            where T : Component
        {
            var (view, layer) = GuideFrameAssets.FramedPanel<T>(safe, name);
            GuideFrameAssets.Pane(
                layer,
                "Pane",
                0f,
                Layout.Design.x,
                GuideFrameAssets.HeaderHeight,
                GuideFrameAssets.PaneLight,
                true,
                true
            );
            var categories = System.Array.Empty<GuideTab>();
            if (tabs != null)
            {
                categories = GuideFrameAssets.SideTabs(layer, "Tabs", tabs);
                // 縦の分類の右は区切りの線のため24空ける。
                left = Mathf.Max(left, GuideFrameAssets.SideRailWidth + UiSpace.Pad);
            }
            var panel = new ListPanel
            {
                Layer = layer,
                Tabs = categories,
                ListLeft = left,
                ListRight = right,
                Top = TopOfList(),
            };
            if (HasHeadingRow)
                HeadingRow(panel, title);
            var list = Layout.Box(
                layer,
                "List",
                left,
                panel.Top,
                right - left,
                Layout.Design.y - panel.Top
            );
            var viewport = Layout.Viewport(list);
            panel.Content = Layout.Column(
                viewport,
                "Rows",
                gap,
                new RectOffset(0, 0, (int)CardGap, (int)(CardGap + UiSpace.Edge))
            );
            panel.Scroll = Layout.Scroll(list, viewport, panel.Content);
            panel.Status = GuideFrameAssets.ListStatus(layer, left, right, panel.Top);
            return (view, panel);
        }

        // 一覧の始まり。線のない形は見出しの段を置かず、ヘッダーのすぐ下から始める
        // （選んでいる分類が、一覧が何かを示すため。doc/rules/ui-design.md の線のない形）。
        // 見つけた数を見出しの段に出すライブラリは、段を残す（UiLook.ListHeading）。
        private static bool HasHeadingRow => !UiLook.Lineless || UiLook.ListHeading;

        private static float TopOfList() =>
            HasHeadingRow ? ListTop : GuideFrameAssets.HeaderHeight + UiSpace.Pad;

        // 一覧と詳細の2つの面、左の縦の分類、見出しの段、状態の文字。
        private static (T View, ListPanel Panel) Begin<T>(
            RectTransform safe,
            string name,
            string title,
            string[] tabs,
            int[] locked
        )
            where T : Component
        {
            var (view, layer) = GuideFrameAssets.FramedPanel<T>(safe, name);
            GuideFrameAssets.SplitPanes(layer, Split, "ListPane", "DetailPane");
            var panel = new ListPanel { Layer = layer, Top = TopOfList() };
            float rail = 0f;
            if (tabs != null)
            {
                panel.Tabs = GuideFrameAssets.SideTabs(
                    layer,
                    "Tabs",
                    tabs,
                    locked: locked ?? System.Array.Empty<int>()
                );
                rail = GuideFrameAssets.SideRailWidth;
            }
            // 縦の分類の右は区切りの線のため24、画面の端の側は48空ける。
            panel.ListLeft = rail + (tabs != null ? UiSpace.Pad : UiSpace.Edge);
            panel.ListRight = Split - UiSpace.Pad;
            if (HasHeadingRow)
                HeadingRow(panel, title);
            panel.Status = GuideFrameAssets.ListStatus(
                layer,
                panel.ListLeft,
                panel.ListRight,
                panel.Top
            );
            return (view, panel);
        }

        private static void HeadingRow(ListPanel panel, string title)
        {
            panel.Title = GuideFrameAssets.Heading(
                panel.Layer,
                "ListTitle",
                title,
                panel.ListLeft,
                GuideFrameAssets.HeaderHeight,
                panel.ListRight - panel.ListLeft - CountWidth,
                HeadingHeight
            );
            panel.Count = Layout.Text(
                panel.Layer,
                "Count",
                "",
                UiType.Body,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineRight,
                panel.ListRight - CountWidth,
                GuideFrameAssets.HeaderHeight,
                CountWidth,
                HeadingHeight
            );
        }

        // 一覧の表示範囲は、分類の右から分け目まで。札の影と選んだときの光が切れないよう、余白は中身の側に取る。
        private static RectTransform ListViewport(ListPanel panel, out RectTransform list)
        {
            float rail = panel.Tabs.Length > 0 ? GuideFrameAssets.SideRailWidth : 0f;
            list = Layout.Box(
                panel.Layer,
                "List",
                rail,
                panel.Top,
                Split - rail,
                Layout.Design.y - panel.Top
            );
            return Layout.Viewport(list);
        }

        private static RectOffset Padding(ListPanel panel)
        {
            float rail = panel.Tabs.Length > 0 ? GuideFrameAssets.SideRailWidth : 0f;
            return new RectOffset(
                (int)(panel.ListLeft - rail),
                (int)(Split - panel.ListRight),
                (int)CardGap,
                (int)(CardGap + UiSpace.Edge)
            );
        }
    }
}
