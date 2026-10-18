using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.GuideMenu.Editor
{
    /// <summary>
    /// Places the parts of a guide screen's own panels (the formation's adventurers, their page
    /// and the change screens) in design pixels from the top left of a 1920x1080 layer, which
    /// shrinks on screens narrower than 16:9. The positions match the mockups, which were drawn
    /// on the same 1920x1080 screen.
    /// </summary>
    public static class GuidePanelLayout
    {
        public static readonly Vector2 Design = new(1920, 1080);

        // 小さなボタン（レベルアップの－＋など）の押せる範囲。指で押しやすい大きさにする。
        public const float HitSize = 128f;

        /// <summary>A 1920x1080 layer in the middle that shrinks on screens narrower than 16:9.</summary>
        public static RectTransform Layer(RectTransform parent, string name)
        {
            var layer = Rect(name, parent);
            Place(layer, Vector2.zero, Design);
            layer.gameObject.AddComponent<WorldLayerFit>().DesignSize = Design;
            return layer;
        }

        /// <summary>
        /// A 1920x1080 layer under a header <paramref name="top"/> high, which shrinks toward the
        /// header's bottom instead of the middle, so its panes start right under the header on
        /// screens narrower than 16:9 too.
        /// </summary>
        public static RectTransform LayerBelow(RectTransform parent, string name, float top)
        {
            var layer = Layer(parent, name);
            layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 1f);
            layer.pivot = new Vector2(0.5f, 1f - top / Design.y);
            layer.anchoredPosition = new Vector2(0, -top);
            return layer;
        }

        /// <summary>A rect at (x, y) from the top left of its parent.</summary>
        public static RectTransform Box(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var rect = Rect(name, parent);
            Corner(rect, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
            return rect;
        }

        public static void At(Component item, float x, float y, float width, float height) =>
            Corner(
                (RectTransform)item.transform,
                new Vector2(0, 1),
                new Vector2(x, -y),
                new Vector2(width, height)
            );

        /// <summary>A text at (x, y) with the guide screens' font and shadow.</summary>
        public static TMP_Text Text(
            RectTransform parent,
            string name,
            string text,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            float x,
            float y,
            float width,
            float height
        )
        {
            var label = Label(parent, name, text, size, color, alignment);
            At(label, x, y, width, height);
            return label;
        }

        /// <summary>
        /// A row that lays its children out side by side from the left (or the middle), each at
        /// its preferred width, e.g. a name followed by the level and the element icons.
        /// </summary>
        public static RectTransform Row(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            float spacing,
            TextAnchor alignment = TextAnchor.MiddleLeft
        )
        {
            var row = Box(parent, name, x, y, width, height);
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = alignment;
            group.spacing = spacing;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            return row;
        }

        /// <summary>Sets a row child's width; 0 leaves it to the text's preferred width.</summary>
        public static void Fit(Component item, float width)
        {
            var element = item.gameObject.AddComponent<LayoutElement>();
            if (width > 0)
                element.minWidth = element.preferredWidth = width;
        }

        /// <summary>A square icon cell in a row, e.g. an element icon (12x12 dots at <paramref name="size"/>).</summary>
        public static Image RowIcon(RectTransform row, string name, float size)
        {
            var cell = Rect(name, row);
            Fit(cell, size);
            var icon = Rect("Icon", cell);
            Place(icon, Vector2.zero, Vector2.one * size);
            var image = icon.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // --- Scrolling lists -------------------------------------------------------------

        /// <summary>
        /// A list's visible area filling <paramref name="parent"/>, inset by the offsets
        /// (<see cref="RectTransform.offsetMin"/> and <see cref="RectTransform.offsetMax"/>).
        /// </summary>
        public static RectTransform Viewport(
            RectTransform parent,
            Vector2 offsetMin = default,
            Vector2 offsetMax = default
        )
        {
            var viewport = Rect("Viewport", parent);
            Stretch(viewport);
            viewport.offsetMin = offsetMin;
            viewport.offsetMax = offsetMax;
            Mask(viewport);
            return viewport;
        }

        /// <summary>
        /// Clips a list's rows to <paramref name="viewport"/> and takes drags anywhere in it, also
        /// between the rows.
        /// </summary>
        public static void Mask(RectTransform viewport)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);
        }

        /// <summary>A column of rows that grows downward from the top of the viewport.</summary>
        public static RectTransform Column(
            RectTransform viewport,
            string name,
            float spacing,
            RectOffset padding = null
        )
        {
            var content = Content(viewport, name);
            var group = content.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            if (padding != null)
                group.padding = padding;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            FitHeight(content);
            return content;
        }

        /// <summary>
        /// Cards of <paramref name="cell"/> size, <paramref name="columns"/> to a row, that grow
        /// downward from the top of the viewport.
        /// </summary>
        public static RectTransform Grid(
            RectTransform viewport,
            string name,
            Vector2 cell,
            float gap,
            RectOffset padding,
            int columns
        )
        {
            var content = Content(viewport, name);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = Vector2.one * gap;
            grid.padding = padding;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            FitHeight(content);
            return content;
        }

        /// <summary>A row of a <see cref="Column"/> at a fixed height.</summary>
        public static RectTransform ListItem(RectTransform content, string name, float height)
        {
            var rect = Rect(name, content);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = height;
            return rect;
        }

        /// <summary>Scrolls <paramref name="content"/> up and down inside the viewport.</summary>
        public static ScrollRect Scroll(
            RectTransform host,
            RectTransform viewport,
            RectTransform content
        )
        {
            var scroll = host.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            return scroll;
        }

        private static RectTransform Content(RectTransform viewport, string name)
        {
            var content = Rect(name, viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            return content;
        }

        private static void FitHeight(RectTransform content) =>
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

        /// <summary>A thin line, e.g. under a stat or between two parts of a frame.</summary>
        public static void Line(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var line = Box(parent, name, x, y, width, height);
            AddImage(line, new Color(0.27f, 0.32f, 0.48f, 0.55f), false);
        }
    }
}
