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
    /// ◀ at the left end, the 64x64 battle sprite at 2x, the name over the level and the
    /// elements, and ▶ at the right end.
    /// </summary>
    public static class PartyHeaderAssets
    {
        public const float Height = 128f;
        private const float FigureDot = 2f;

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
            var widget = new PartyPersonHeader
            {
                Prev = Layout.ArrowButton(header, "Prev", 72, Height / 2f, flip: true),
                Next = Layout.ArrowButton(header, "Next", width - 72, Height / 2f, flip: false),
            };

            var figure = Layout.Box(header, "Figure", 144, 0, 64 * FigureDot, 64 * FigureDot);
            widget.Figure = figure.gameObject.AddComponent<RawImage>();
            widget.Figure.raycastTarget = false;
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = FigureDot;

            float textX = 144 + 64 * FigureDot + 16;
            float textWidth = width - textX - 144;
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

            widget.Show(member, member?.Level ?? 1, null, canSwitch: true);
            return widget;
        }
    }
}
