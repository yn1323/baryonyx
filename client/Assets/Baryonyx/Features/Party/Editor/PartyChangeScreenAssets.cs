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
    /// The layout the formation's change screens share (the equipment and the card skills, opened
    /// from the adventurer's page): in the guide frame, the person, their stats and slots on the
    /// left pane, which takes the flick that changes the person, and the tabs and the list on
    /// the right pane; the scene shows through both. Design pixels from the top left of
    /// the 1920x1080 layer (<see cref="Layout"/>).
    /// </summary>
    public static class PartyChangeScreenAssets
    {
        public const float Split = 800f;
        public static readonly Rect Left = new(36, Frame.HeaderHeight + 6, 740, 956);
        public static readonly Rect Right = new(
            Split,
            Frame.HeaderHeight + 6,
            1920 - Split - 24,
            956
        );

        // 左は上から人・ステータス・枠。ステータスの下に線を引いて枠と分ける。
        public const float StatsTop = 156f;
        public const float StatsLineTop = 262f;

        // 右の上の、面の幅いっぱいのタブ。
        public const float TabHeight = 76f;

        /// <summary>
        /// The two panes, the left frame with the person at its top (drawn with
        /// <paramref name="first"/> so the prefab reads in the editor) and the right frame named
        /// <paramref name="rightName"/>.
        /// </summary>
        public static (
            RectTransform Left,
            RectTransform Right,
            GuideSwipe Swipe,
            PartyPersonHeader Person
        ) Build(RectTransform layer, string rightPane, string rightName, PartyMember first)
        {
            Frame.SplitPanes(layer, Split, "PersonPane", rightPane);
            var left = Layout.Box(layer, "Person", Left.x, Left.y, Left.width, Left.height);
            // 左の面の上の左右のフリックで人を替える。枠を押したところから動かしても届くよう、左全体で受ける。
            AddImage(left, Color.clear, true);
            var swipe = left.gameObject.AddComponent<GuideSwipe>();
            var right = Layout.Box(layer, rightName, Right.x, Right.y, Right.width, Right.height);

            var person = PartyHeaderAssets.Build(left, 10, Left.width, first);
            Layout.Line(left, "HeaderLine", 24, 142, Left.width - 48, 2);
            return (left, right, swipe, person);
        }

        /// <summary>The person's stats right under them, with a line below.</summary>
        public static TMP_Text[] Stats(RectTransform left)
        {
            var stats = StatGridAssets.Build(left, 30, StatsTop, Left.width - 60, 4);
            Layout.Line(left, "StatsLine", 24, StatsLineTop, Left.width - 48, 2);
            return stats;
        }

        /// <summary>The tabs across the top of the right frame.</summary>
        public static GuideTab[] Tabs(RectTransform right, params string[] labels) =>
            Frame.Tabs(
                right,
                "Tabs",
                labels,
                24,
                4,
                (Right.width - 48) / labels.Length,
                TabHeight,
                Right.width - 48
            );

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
                Right.width - 48,
                140
            );
    }
}
