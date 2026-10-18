using System.Linq;
using Baryonyx.Combat;
using TMPro;
using UnityEngine;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Bakes the eight stats (<see cref="StatGrid"/>) as label and value cells with a line under
    /// each, in design pixels from the top left of the parent (<see cref="Layout"/>).
    /// </summary>
    public static class StatGridAssets
    {
        // ステータスの8項目を置く枠（列, 行）。4列2行で、物攻・属攻の下に物防・属防を置く。
        private static readonly Vector2Int[] Cells =
        {
            new(0, 0), // HP
            new(1, 0), // 物攻
            new(1, 1), // 物防
            new(2, 0), // 属攻
            new(2, 1), // 属防
            new(0, 1), // 速度
            new(3, 0), // 会心
            new(3, 1), // 幸運
        };

        // 2列のときの枠（列, 行）。HP・物攻、属攻・速度、会心・物防、属防・幸運の順に並べる。
        private static readonly Vector2Int[] CellsNarrow =
        {
            new(0, 0), // HP
            new(1, 0), // 物攻
            new(1, 2), // 物防
            new(0, 1), // 属攻
            new(0, 3), // 属防
            new(1, 1), // 速度
            new(0, 2), // 会心
            new(1, 3), // 幸運
        };

        private const float Gap = 24f;

        /// <summary>
        /// The eight stats in <see cref="CharacterStats"/> order: four columns (HP・物攻・属攻・会心
        /// over 速度・物防・属防・幸運) or two (HP・物攻, 属攻・速度, 会心・物防, 属防・幸運).
        /// Returns the value labels for <see cref="StatGrid.Show"/>.
        /// </summary>
        public static TMP_Text[] Build(
            RectTransform parent,
            float x,
            float y,
            float width,
            int columns,
            float rowHeight = 46f
        )
        {
            var cells = columns >= 4 ? Cells : CellsNarrow;
            int count = columns >= 4 ? 4 : 2;
            float cell = (width - Gap * (count - 1)) / count;
            var stats = Layout.Box(parent, "Stats", x, y, width, rowHeight * (8 / count));
            return Enumerable
                .Range(0, CharacterStats.Count)
                .Select(i =>
                {
                    var row = Layout.Box(
                        stats,
                        "Stat" + i,
                        cells[i].x * (cell + Gap),
                        cells[i].y * rowHeight,
                        cell,
                        rowHeight
                    );
                    Layout.Line(row, "Line", 0, rowHeight - 2, cell, 2);
                    var label = Label(
                        row,
                        "Label",
                        CharacterStats.Labels[i],
                        24,
                        UiPalette.TextSub,
                        TextAlignmentOptions.BottomLeft
                    );
                    Layout.At(label, 0, 0, 80, rowHeight - 4);
                    var value = Label(
                        row,
                        "Value",
                        "",
                        32,
                        UiPalette.TextMain,
                        TextAlignmentOptions.BottomRight
                    );
                    Layout.At(value, 70, 0, cell - 70, rowHeight - 4);
                    return value;
                })
                .ToArray();
        }
    }
}
