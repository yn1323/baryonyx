using System.Linq;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Party.Editor
{
    /// <summary>
    /// Bakes the person at the top of the formation's change screens (equipment, card skills):
    /// the 64x64 battle sprite at 2x, and the name over the level and the elements. The people
    /// change on a flick over the left pane, so there are no arrows.
    /// </summary>
    public static class PartyHeaderAssets
    {
        public const float Height = 128f;
        private const float FigureDot = 2f;
        private const float FigureLeft = 24f;

        /// <summary>
        /// Builds the header across <paramref name="panel"/>, <paramref name="top"/> below its
        /// top edge, drawn with <paramref name="member"/> so the prefab reads in the editor.
        /// </summary>
        public static PartyPersonHeader Build(
            RectTransform panel,
            float top,
            float width,
            PartyMember member
        )
        {
            var header = Layout.Box(panel, "PersonHeader", 0, top, width, Height);
            var widget = new PartyPersonHeader();

            var figure = Layout.Box(
                header,
                "Figure",
                FigureLeft,
                0,
                64 * FigureDot,
                64 * FigureDot
            );
            widget.Figure = figure.gameObject.AddComponent<RawImage>();
            widget.Figure.raycastTarget = false;
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = FigureDot;

            float textX = FigureLeft + 64 * FigureDot + 16;
            float textWidth = width - textX - 24;
            widget.Name = Layout.Text(
                header,
                "Name",
                "",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft,
                textX,
                8,
                textWidth,
                60
            );
            Guide.Shrink(widget.Name, 28);
            var level = Layout.Row(header, "LevelLine", textX, 70, textWidth, 50, 10);
            Layout.Fit(
                Label(level, "Tag", "Lv", 24, UiPalette.TextSub, TextAlignmentOptions.Midline),
                0
            );
            widget.Level = Label(
                level,
                "Level",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(widget.Level, 0);
            widget.Elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(level, "Element" + i, 12f * 3f))
                .ToArray();

            widget.Show(member, member?.Level ?? 1, null);
            return widget;
        }
    }
}
