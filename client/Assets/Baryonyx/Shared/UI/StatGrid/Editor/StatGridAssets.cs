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

        // 縦に積むとき（stacked）の、項目名の行の高さ。数字はその下の残りに置く。
        private const float StackedLabelHeight = 32f;

        /// <summary>
        /// The eight stats in <see cref="CharacterStats"/> order: four columns (HP・物攻・属攻・会心
        /// over 速度・物防・属防・幸運) or two (HP・物攻, 属攻・速度, 会心・物防, 属防・幸運).
        /// Returns the value labels for <see cref="StatGrid.Show"/>. With <paramref name="centerValue"/>
        /// each value sits in the middle of its cell instead of at its right end. With
        /// <paramref name="stacked"/> the label sits on top and the value under it, both centred
        /// in the cell, so the label and the value never pull to opposite sides.
        /// </summary>
        public static TMP_Text[] Build(
            RectTransform parent,
            float x,
            float y,
            float width,
            int columns,
            float rowHeight = 46f,
            float labelSize = 24f,
            float valueSize = 32f,
            bool centerValue = false,
            bool stacked = false,
            float gap = Gap
        )
        {
            var cells = columns >= 4 ? Cells : CellsNarrow;
            int count = columns >= 4 ? 4 : 2;
            float cell = (width - gap * (count - 1)) / count;
            var stats = Layout.Box(parent, "Stats", x, y, width, rowHeight * (8 / count));
            return Enumerable
                .Range(0, CharacterStats.Count)
                .Select(i =>
                {
                    var row = Layout.Box(
                        stats,
                        "Stat" + i,
                        cells[i].x * (cell + gap),
                        cells[i].y * rowHeight,
                        cell,
                        rowHeight
                    );
                    Layout.Line(row, "Line", 0, rowHeight - 2, cell, 2);
                    if (stacked)
                        return StackedCell(row, i, cell, rowHeight, labelSize, valueSize);
                    var label = Label(
                        row,
                        "Label",
                        CharacterStats.Labels[i],
                        labelSize,
                        UiPalette.TextSub,
                        TextAlignmentOptions.BottomLeft
                    );
                    // 項目名の幅と値の左端は、文字の大きさ（24ptのとき80・70）に合わせて広げる。
                    float scale = labelSize / 24f;
                    Layout.At(label, 0, 0, 80f * scale, rowHeight - 4);
                    // 値は右寄せ。centerValue なら、項目名を左に残して枠の中央に置く（幅の広い2列）。
                    var value = Label(
                        row,
                        "Value",
                        "",
                        valueSize,
                        UiPalette.TextMain,
                        centerValue ? TextAlignmentOptions.Bottom : TextAlignmentOptions.BottomRight
                    );
                    if (centerValue)
                        Layout.At(value, 0, 0, cell, rowHeight - 4);
                    else
                        Layout.At(value, 70f * scale, 0, cell - 70f * scale, rowHeight - 4);
                    return value;
                })
                .ToArray();
        }

        // 項目名を上、数字をその下に、どちらも枠の中央に置く。上昇分を添えても収まるよう、数字は縮めて入れる。
        private static TMP_Text StackedCell(
            RectTransform row,
            int index,
            float cell,
            float rowHeight,
            float labelSize,
            float valueSize
        )
        {
            var label = Label(
                row,
                "Label",
                CharacterStats.Labels[index],
                labelSize,
                UiPalette.TextSub,
                TextAlignmentOptions.Bottom
            );
            Layout.At(label, 0, 0, cell, StackedLabelHeight);
            var value = Label(
                row,
                "Value",
                "",
                valueSize,
                UiPalette.TextMain,
                TextAlignmentOptions.Bottom
            );
            Layout.At(value, 0, StackedLabelHeight, cell, rowHeight - StackedLabelHeight - 6);
            value.enableAutoSizing = true;
            value.fontSizeMin = valueSize * 0.75f;
            value.fontSizeMax = valueSize;
            return value;
        }
    }
}
