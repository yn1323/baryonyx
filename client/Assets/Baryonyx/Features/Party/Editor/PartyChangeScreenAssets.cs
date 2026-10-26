using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// The layout the adventurer's equipment and card skills tabs share: in the guide frame,
    /// between the people's column and the tabs (<see cref="PartySheetAssets"/>), the person's
    /// slots on the left pane, which takes the flick that changes the person, and the tabs and
    /// the list on the right pane; the scene shows through both. Each screen splits the panes
    /// where its own contents fit. Design pixels from the top left of the 1920x1080 layer
    /// (<see cref="Layout"/>).
    /// </summary>
    public static class PartyChangeScreenAssets
    {
        private const float Top = Frame.HeaderHeight + 6;
        private const float Height = 956f;

        /// <summary>The left pane's contents when the panes split at <paramref name="split"/>.</summary>
        public static Rect LeftOf(float split) =>
            new(
                PartySheetAssets.ContentLeft + 24,
                Top,
                split - PartySheetAssets.ContentLeft - 48,
                Height
            );

        /// <summary>The right pane's contents when the panes split at <paramref name="split"/>.</summary>
        public static Rect RightOf(float split) =>
            new(split, Top, PartySheetAssets.ContentRight - split - 16, Height);

        // 左の上のステータスと、その下に引いて枠と分ける線。
        public const float StatsTop = 20f;
        public const float StatsLineTop = 126f;

        // 右の上の、面の幅いっぱいのタブ。
        public const float TabHeight = 76f;

        /// <summary>
        /// The two panes split at <paramref name="split"/>, the left frame and the right frame
        /// named <paramref name="rightName"/>.
        /// </summary>
        public static (RectTransform Left, RectTransform Right, GuideSwipe Swipe) Build(
            RectTransform layer,
            float split,
            string rightPane,
            string rightName
        )
        {
            Frame.SplitPanes(
                layer,
                split,
                "PersonPane",
                rightPane,
                from: PartySheetAssets.ContentLeft,
                to: PartySheetAssets.ContentRight
            );
            var leftRect = LeftOf(split);
            var rightRect = RightOf(split);
            var left = Layout.Box(
                layer,
                "Person",
                leftRect.x,
                leftRect.y,
                leftRect.width,
                leftRect.height
            );
            // 左の面の上の左右のフリックで人を替える。枠を押したところから動かしても届くよう、左全体で受ける。
            AddImage(left, Color.clear, true);
            var swipe = left.gameObject.AddComponent<GuideSwipe>();
            var right = Layout.Box(
                layer,
                rightName,
                rightRect.x,
                rightRect.y,
                rightRect.width,
                rightRect.height
            );
            return (left, right, swipe);
        }

        /// <summary>The person's stats at the top of the left frame, with a line below.</summary>
        public static TMP_Text[] Stats(RectTransform left)
        {
            float width = left.rect.width;
            var stats = StatGridAssets.Build(left, 6, StatsTop, width - 12, 4);
            Layout.Line(left, "StatsLine", 0, StatsLineTop, width, 2);
            return stats;
        }

        /// <summary>The tabs across the top of the right frame.</summary>
        public static GuideTab[] Tabs(RectTransform right, params string[] labels)
        {
            float width = right.rect.width - 48;
            return Frame.Tabs(
                right,
                "Tabs",
                labels,
                24,
                4,
                width / labels.Length,
                TabHeight,
                width
            );
        }

        // 一覧の上下の縁で行を薄く消す幅。途中で切れた行が、縁でぱっつり切れて見えないようにする。
        public const int ListFade = 24;

        // 一覧の上の縁を、渡された位置より少し上（タブの下の行のすぐ下）から始める。
        private const float ListRaise = 12f;

        /// <summary>
        /// The list's visible area in the right frame from <paramref name="top"/> down, and the
        /// column its rows go in. The area reaches the frame's bottom and the rows fade out over
        /// <see cref="ListFade"/> at the top and bottom edges; the column is padded as much, so the
        /// first and last rows rest clear of the fade.
        /// </summary>
        public static (RectTransform Viewport, RectTransform Rows) List(
            RectTransform right,
            float top
        )
        {
            var viewport = Layout.Viewport(
                right,
                new Vector2(24, 0),
                new Vector2(-24, -(top - ListRaise))
            );
            viewport.GetComponent<RectMask2D>().softness = new Vector2Int(0, ListFade);
            return (
                viewport,
                Layout.Column(viewport, "Rows", 10, new RectOffset(0, 0, ListFade, ListFade))
            );
        }

        /// <summary>The text the right frame shows while reading, when the read failed, or when the list is empty.</summary>
        public static TMP_Text Status(RectTransform right) =>
            Layout.Text(
                right,
                "Status",
                "",
                36,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                24,
                100,
                right.rect.width - 48,
                140
            );
    }
}
