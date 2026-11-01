using TMPro;
using UnityEngine;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the parts of the shared item card (<see cref="ItemTile"/>) on a frosted glass card
    /// (<see cref="GlassCardAssets"/>), so every list puts them in the same places
    /// (doc/rules/ui-design.md の持ち物の札): the quality's letter at the top left, the count at
    /// the bottom right, a mark at the top right and the "？" over the art of what is not found
    /// yet. The card's size, its art and the name's line stay the screen's own. Positions are
    /// design pixels from the top left of the card's inside.
    /// </summary>
    public static class ItemTileAssets
    {
        // 札の端から、左上の品質・右上の印までの余白。
        public const float CornerInset = 10f;

        // 右上の印の板の大きさ（「E」）と、板の色。
        public const float TagSize = 36f;
        private static readonly Color TagPlate = new(0.03f, 0.04f, 0.1f, 0.85f);

        // まだ見つけていない物の「？」の色。影と同じく、暗い青灰にする。
        public static readonly Color UnknownColor = new(0.42f, 0.47f, 0.62f, 1f);

        /// <summary>
        /// The card on <paramref name="rect"/> (already placed by the caller) with the item card's
        /// component; returns the component and the card's inside for the parts.
        /// </summary>
        public static (ItemTile Tile, RectTransform Inside) Build(
            RectTransform rect,
            bool pattern = false,
            Color? fill = null
        )
        {
            var (card, inside) = GlassCardAssets.Build(rect, pattern: pattern, fill: fill);
            var tile = rect.gameObject.AddComponent<ItemTile>();
            tile.Card = card;
            tile.Group = card.Group;
            return (tile, inside);
        }

        /// <summary>
        /// The quality's letter at the card's top left, in front of the art, in a box of its own
        /// (the label's parent), so a view hides the box for things without a quality.
        /// </summary>
        public static TMP_Text QualityMark(
            ItemTile tile,
            RectTransform inside,
            float size = UiType.Body
        )
        {
            var box = Layout.Box(inside, "Quality", CornerInset, 4f, size * 1.6f, size * 1.4f);
            tile.Quality = Label(
                box,
                "Label",
                "",
                size,
                UiPalette.TextMain,
                TextAlignmentOptions.TopLeft
            );
            Stretch((RectTransform)tile.Quality.transform);
            tile.Quality.richText = true;
            return tile.Quality;
        }

        /// <summary>
        /// The count ("×2", "3/2") with its bottom right at (<paramref name="right"/>,
        /// <paramref name="bottom"/>): the card's bottom right, or the art's when the card's
        /// bottom has the name and the price.
        /// </summary>
        public static TMP_Text CountMark(
            ItemTile tile,
            RectTransform inside,
            float right,
            float bottom,
            float size = UiType.Caption
        )
        {
            float height = size * 1.4f;
            tile.Count = Layout.Text(
                inside,
                "Count",
                "",
                size,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomRight,
                right - 96f,
                bottom - height,
                96f,
                height
            );
            tile.Count.richText = true;
            return tile.Count;
        }

        /// <summary>
        /// The place of the mark at the card's top right, <paramref name="size"/> square on a card
        /// <paramref name="width"/> wide, for a picture mark (the boss's crown, a check).
        /// </summary>
        public static RectTransform CornerBox(
            RectTransform inside,
            string name,
            float width,
            float size
        ) => Layout.Box(inside, name, width - CornerInset - size, CornerInset, size, size);

        /// <summary>
        /// A letter mark at the card's top right ("E" while someone uses the thing on a dark
        /// plate, "NEW" in gold without one), hidden; it becomes the card's
        /// <see cref="ItemTile.Mark"/>.
        /// </summary>
        public static GameObject CornerTag(
            ItemTile tile,
            RectTransform inside,
            float width,
            string text,
            Color color,
            bool plate,
            string name = "Mark"
        )
        {
            float tagWidth = plate ? TagSize : TagSize * 2.8f;
            var tag = CornerBox(inside, name, width, TagSize);
            tag.sizeDelta = new Vector2(tagWidth, TagSize);
            tag.anchoredPosition = new Vector2(width - CornerInset - tagWidth, -CornerInset);
            if (plate)
                AddImage(tag, TagPlate, false);
            var letter = Label(
                tag,
                "Label",
                text,
                UiType.Caption,
                color,
                plate ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineRight
            );
            Stretch((RectTransform)letter.transform);
            tag.gameObject.SetActive(false);
            tile.Mark = tag.gameObject;
            return tile.Mark;
        }

        /// <summary>
        /// The "？" over the art of a thing not found yet, for art that shows no shape as a shadow
        /// (the skills' square pictures), <paramref name="box"/> square centred at
        /// <paramref name="center"/> from the inside's middle. Hidden until the view shows it.
        /// </summary>
        public static GameObject UnknownMark(
            ItemTile tile,
            RectTransform inside,
            Vector2 center,
            float box,
            float size = UiType.Display
        )
        {
            var mark = Label(
                inside,
                "Unknown",
                "？",
                size,
                UnknownColor,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)mark.transform, center, Vector2.one * box);
            tile.Unknown = mark.gameObject;
            return tile.Unknown;
        }
    }
}
