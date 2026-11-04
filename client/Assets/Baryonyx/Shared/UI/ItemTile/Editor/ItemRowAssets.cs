using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Baryonyx.Editor.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the item row (doc/rules/ui-design.md の装備の見せ方) on an <see cref="ItemTile"/>:
    /// the art on the left, the name with the stars and the quality right after it
    /// (<see cref="ItemText.Heading(ItemFace)"/>), the effect on the second line, the attributes
    /// and a mark at the top right and a value (a price, a count, the stats' changes) at the
    /// bottom right. The standard row and the wide row are the same build: the wide row is the
    /// longer one whose value is the stats' changes and whose effect is written out in full.
    /// </summary>
    public static class ItemRowAssets
    {
        // 行の端と中身の間、絵と文字の間。
        public const float Inset = 20f;
        public const float Right = 24f;

        /// <summary>
        /// A row <paramref name="height"/> high in <paramref name="content"/> (a vertical layout),
        /// with the art <paramref name="artBox"/> square and a value
        /// <paramref name="valueWidth"/> wide at the bottom right (0 for none). With
        /// <paramref name="runeValue"/> the value is a price with the rune icon. A row built
        /// while <see cref="UiLook.Lineless"/> has no plate (doc/rules/ui-design.md の線のない形).
        /// </summary>
        public static ItemTile Row(
            RectTransform content,
            string name,
            float height,
            float artBox,
            float valueWidth = 0f,
            bool runeValue = false,
            float nameSize = UiType.Body,
            float detailSize = UiType.Caption
        )
        {
            var rect = GuidePanelLayout.ListItem(content, name, height);
            var (row, inside) = ItemTileAssets.Build(rect, bare: UiLook.Lineless);
            var box = Rect("ArtBox", inside);
            Corner(box, new Vector2(0, 0.5f), new Vector2(Inset, 0), Vector2.one * artBox);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * artBox);
            row.Art = art.gameObject.AddComponent<RawImage>();
            row.Art.raycastTarget = false;

            float textLeft = Inset + artBox + Inset;
            float half = height / 2f;
            // 名前の行は右上の属性の手前まで、効果の行は右下の値の手前まで。
            float nameRight = Right + ElementIconRowAssets.Width() + 16f;
            float detailRight = valueWidth > 0f ? Right + valueWidth + 16f : Right;
            row.Name = Label(inside, "Name", "", nameSize, UiPalette.TextMain, TextAlignmentOptions.BottomLeft);
            row.Name.richText = true;
            GuideMenuAssets.Band((RectTransform)row.Name.transform, top: true, 4f, half - 6f, textLeft, nameRight);
            GuideMenuAssets.Shrink(row.Name, UiType.Tiny);
            row.Detail = Label(inside, "Detail", "", detailSize, UiPalette.TextSub, TextAlignmentOptions.TopLeft);
            row.Detail.richText = true;
            GuideMenuAssets.Band(
                (RectTransform)row.Detail.transform,
                top: false,
                6f,
                half - 10f,
                textLeft + 2f,
                detailRight
            );
            GuideMenuAssets.Shrink(row.Detail, 18f);

            // 右上：属性のアイコン（名前の行の高さの中央）。
            var elements = Rect("Elements", inside);
            Corner(
                elements,
                Vector2.one,
                new Vector2(-Right, -(4f + (half - 6f - ElementIconRowAssets.Size) / 2f)),
                new Vector2(ElementIconRowAssets.Width(), ElementIconRowAssets.Size)
            );
            row.Elements = ElementIconRowAssets.Add(elements, TextAnchor.MiddleRight);

            // 右下：値段・数・増減（効果の行の高さ）。
            if (valueWidth > 0f)
            {
                if (runeValue)
                {
                    row.Price = GuideFrameAssets.RuneAmount(inside, "Value", 0f, 0f, valueWidth, 0f, UiType.Body, TextAnchor.MiddleRight);
                    var price = (RectTransform)row.Price.transform.parent;
                    BottomRight(price, valueWidth, half);
                }
                else
                {
                    row.Price = Label(inside, "Value", "", detailSize, UiPalette.TextMain, TextAlignmentOptions.MidlineRight);
                    row.Price.richText = true;
                    BottomRight((RectTransform)row.Price.transform, valueWidth, half);
                    GuideMenuAssets.Shrink(row.Price, 18f);
                }
            }
            return row;
        }

        // 行の右下（効果の行の高さ）に、幅 width で置く。
        private static void BottomRight(RectTransform rect, float width, float half)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(-Right - width, 6f);
            rect.offsetMax = new Vector2(-Right, half - 4f);
        }
    }
}
