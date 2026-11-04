using System.Linq;
using Baryonyx.Party;
using Baryonyx.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Tavern.Editor
{
    /// <summary>
    /// The parts the formation's adventurer cards share (the adventurers' list and the
    /// growth picker): the scrolling grid of glass cards, the standing figure on the left of
    /// a card, and the name and level beside it. Design pixels from the top left of the card or
    /// the 1920x1080 layer (<see cref="Layout"/>).
    /// </summary>
    internal static class AdventurerCardAssets
    {
        public const float CardGap = 16f;

        // カードの中：立ち姿（余白を切って4倍）の中心、文字の左端。
        public const float FigureCenter = 120f;
        public const float TextLeft = 262f;

        // 名前・Lv・属性の行の左端。立ち姿へ寄せ、「Lv」・2桁のLv・属性のアイコン3つが縮まずに入るようにする。
        public const float NameLeft = 232f;

        private const float Dot = Guide.DotScale;

        /// <summary>
        /// The cards' visible area from (<paramref name="x"/>, <paramref name="top"/>) to the
        /// layer's right edge and <paramref name="bottom"/> (the layer's bottom edge unless
        /// given), and the grid the cards go in. The area is wider than the cards so their
        /// shadows and the chosen card's glow are not cut off.
        /// </summary>
        public static (RectTransform List, RectTransform Viewport, RectTransform Cards) Grid(
            RectTransform layer,
            float x,
            float top,
            Vector2 cell,
            RectOffset padding,
            int columns,
            float bottom = 1080f
        )
        {
            var list = Layout.Box(layer, "List", x, top, Layout.Design.x - x, bottom - top);
            var viewport = Layout.Viewport(list);
            var cards = Layout.Grid(viewport, "Cards", cell, CardGap, padding, columns);
            return (list, viewport, cards);
        }

        /// <summary>
        /// The figure (trimmed and at 4x) and the shadow at its feet, with the feet a little above
        /// the bottom of a card <paramref name="cardHeight"/> tall.
        /// </summary>
        public static (RawImage Figure, GameObject Shadow) Figure(
            RectTransform card,
            PartyMember member,
            Sprite shadow,
            float cardHeight
        )
        {
            var size = new Vector2(PartyArt.Figure.width, PartyArt.Figure.height) * Dot;
            float foot = cardHeight - 12f;
            var feet = Picture(card, "Shadow", shadow, Vector2.zero, new Vector2(150, 24), 0.55f);
            Layout.At(feet, FigureCenter - 75f, foot - 14f, 150, 24);
            var figure = Layout.Box(
                card,
                "Figure",
                FigureCenter - size.x / 2f,
                foot - size.y,
                size.x,
                size.y
            );
            var image = figure.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            PartyArt.Paint(image, member, trim: true);
            figure.gameObject.AddComponent<PixelPerfectRawImage>().DotSize = Dot;
            return (image, feet.gameObject);
        }

        /// <summary>
        /// The name right of the figure, <paramref name="y"/> from the card's top and
        /// <paramref name="left"/> (<see cref="TextLeft"/> unless given) from its left.
        /// </summary>
        public static TMP_Text Name(
            RectTransform card,
            string text,
            float y,
            float width,
            float left = TextLeft
        )
        {
            var name = Layout.Text(
                card,
                "Name",
                text,
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft,
                left,
                y,
                width,
                60f
            );
            Guide.Shrink(name, 28);
            return name;
        }

        /// <summary>
        /// The name and, under it, "Lv", the level and the icons of the elements the person can
        /// use (12x12 at 3x), centred down a card <paramref name="cardHeight"/> tall and
        /// <paramref name="width"/> wide, from <paramref name="left"/>. The party's cards and the
        /// list's cards share it, so the two read the same.
        /// </summary>
        public static (TMP_Text Name, GameObject Line, TMP_Text Level, Image[] Elements) NameLines(
            RectTransform card,
            float width,
            float cardHeight,
            float left
        )
        {
            float middle = cardHeight / 2f;
            float textWidth = width - left - NameGap;
            var name = Name(card, "", middle - 66f, textWidth, left);
            var line = Layout.Row(card, "LevelLine", left, middle + 4f, textWidth, 56f, 10f);
            var level = Level(line, "");
            var elements = Enumerable
                .Range(0, 3)
                .Select(i => Layout.RowIcon(line, "Element" + i, 12f * 3f))
                .ToArray();
            return (name, line.gameObject, level, elements);
        }

        // 名前とLvの行の右の余白。
        private const float NameGap = 24f;

        /// <summary>"Lv" and the level at the start of a row (<see cref="Layout.Row"/>).</summary>
        public static TMP_Text Level(RectTransform row, string text)
        {
            Layout.Fit(
                Label(row, "LevelTag", "Lv", 28, UiPalette.Gold, TextAlignmentOptions.Bottom),
                0
            );
            var level = Label(row, "Level", text, 40, UiPalette.Gold, TextAlignmentOptions.Bottom);
            Layout.Fit(level, 0);
            return level;
        }
    }
}
