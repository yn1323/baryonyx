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

        // ◀▶の押せる範囲。見た目の枠より大きく、指で押しやすい大きさにする。
        public const float HitSize = 128f;
        public const float ArrowFrame = 88f;

        /// <summary>A 1920x1080 layer in the middle that shrinks on screens narrower than 16:9.</summary>
        public static RectTransform Layer(RectTransform parent, string name)
        {
            var layer = Rect(name, parent);
            Place(layer, Vector2.zero, Design);
            layer.gameObject.AddComponent<WorldLayerFit>().DesignSize = Design;
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

        /// <summary>A guide frame at (x, y), taking taps so the screen behind does not.</summary>
        public static RectTransform FrameBox(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            bool selected = false
        )
        {
            var rect = Box(parent, name, x, y, width, height);
            GuideMenuAssets
                .Frame(
                    rect,
                    selected ? GuideMenuAssets.FrameSelectedPath : GuideMenuAssets.FramePath,
                    Color.white
                )
                .raycastTarget = true;
            return rect;
        }

        /// <summary>
        /// A button in a guide frame at (x, y), with a gold frame over it that the view turns on
        /// when it is chosen (returned as <c>Selected</c>, hidden).
        /// </summary>
        public static (Button Button, GameObject Selected) Choice(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var rect = Box(parent, name, x, y, width, height);
            var button = GuideMenuAssets.AddButton(
                rect,
                GuideMenuAssets.Frame(rect, GuideMenuAssets.FramePath, new Color(1f, 1f, 1f, 0.9f))
            );
            // Under the contents, so the gold frame's fill does not cover them.
            var selected = Rect("Selected", rect);
            Stretch(selected);
            GuideMenuAssets
                .Frame(selected, GuideMenuAssets.FrameSelectedPath, Color.white)
                .raycastTarget = false;
            selected.gameObject.SetActive(false);
            return (button, selected.gameObject);
        }

        /// <summary>
        /// A ◀ or ▶ centred on (x, y): a frame of <see cref="ArrowFrame"/> that takes taps on
        /// <see cref="HitSize"/> square.
        /// </summary>
        public static Button ArrowButton(
            RectTransform parent,
            string name,
            float x,
            float y,
            bool flip
        )
        {
            var rect = Box(parent, name, x - HitSize / 2f, y - HitSize / 2f, HitSize, HitSize);
            AddImage(rect, Color.clear, true);
            var frame = Rect("Frame", rect);
            Place(frame, Vector2.zero, Vector2.one * ArrowFrame);
            var image = GuideMenuAssets.Frame(frame, GuideMenuAssets.FramePath, Color.white);
            image.raycastTarget = false;
            var button = GuideMenuAssets.AddButton(rect, image);
            var arrow = Rect("Arrow", frame);
            Place(arrow, Vector2.zero, new Vector2(5, 9) * 6f);
            if (flip)
                arrow.localScale = new Vector3(-1, 1, 1);
            SpriteImage(arrow, GuideMenuAssets.ArrowPath, UiPalette.Gold);
            return button;
        }

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
