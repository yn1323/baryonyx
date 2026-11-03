using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the detail face (<see cref="ItemDetail"/>) and the small window that holds it
    /// (<see cref="ItemDetailWindow"/>), so the shop's, the library's and the window's details put
    /// the same lines in the same order (doc/rules/ui-design.md の装備の見せ方): the quality large
    /// with the multiplier, the art, the name, the stars, the effect and the attributes.
    /// </summary>
    public static class ItemDetailAssets
    {
        // 品質の1行の高さ（文字は大きく、横に倍率）。
        public const float QualityHeight = 130f;

        // 名前・★の行の高さと、行の間。
        public const float NameHeight = 72f;
        public const float StarsHeight = 48f;
        public const float Gap = 12f;

        // 下に材料などを並べて縦が足りない面（合成）の、詰めた高さ。
        private const float CompactQualityHeight = 96f;
        private const float CompactNameHeight = 56f;
        private const float CompactStarsHeight = 40f;

        // 窓の大きさと、窓の中の絵の大きさ。
        public const float WindowHeight = 860f;
        public const float WindowArt = 192f;
        private const float WindowPadTop = 72f;
        private const float WindowPadBottom = 56f;

        private static readonly Color Glow = new(1f, 0.82f, 0.5f, 0.14f);

        /// <summary>
        /// The face's lines added to <paramref name="column"/>, a vertical layout
        /// <paramref name="width"/> wide that the caller placed (the screen adds its own lines
        /// after). The art is <paramref name="artBox"/> square with a soft light behind it. A
        /// <paramref name="compact"/> face sets the lines closer and smaller, for a panel that puts
        /// many things under it.
        /// </summary>
        public static ItemDetail Face(RectTransform column, float width, float artBox, bool compact = false)
        {
            var detail = column.gameObject.AddComponent<ItemDetail>();
            detail.ArtBox = artBox;
            detail.Quality = Line(
                column,
                "Quality",
                compact ? UiType.Caption : UiType.Lead,
                UiPalette.TextMain,
                width,
                compact ? CompactQualityHeight : QualityHeight
            );
            detail.Quality.richText = true;

            var stage = Layout.Box(column, "ArtStage", 0f, 0f, artBox, artBox);
            Fixed(stage, artBox + (compact ? 8f : Gap * 2f));
            var glow = Rect("Glow", stage);
            Place(glow, Vector2.zero, Vector2.one * artBox * 1.5f);
            SpriteImage(glow, GuideMenuAssets.SoftSpotPath, Glow);
            var art = Rect("Art", stage);
            Place(art, Vector2.zero, Vector2.one * artBox);
            detail.Art = art.gameObject.AddComponent<RawImage>();
            detail.Art.raycastTarget = false;
            detail.ArtStage = stage.gameObject;

            detail.Name = Line(
                column,
                "Name",
                compact ? UiType.Title : UiType.Heading,
                UiPalette.TextMain,
                width,
                compact ? CompactNameHeight : NameHeight
            );
            GuideMenuAssets.Shrink(detail.Name, UiType.Small);
            detail.Stars = Line(
                column,
                "Stars",
                compact ? UiType.Small : UiType.Body,
                UiPalette.Gold,
                width,
                compact ? CompactStarsHeight : StarsHeight
            );
            detail.Effect = Layout.Text(
                column,
                "Effect",
                "",
                UiType.Small,
                UiPalette.TextSub,
                TextAlignmentOptions.Top,
                0f,
                0f,
                width,
                100f
            );
            detail.Effect.richText = true;
            detail.Effect.textWrappingMode = TextWrappingModes.Normal;

            var elements = Layout.Box(column, "Elements", 0f, 0f, width, ElementIconRowAssets.DetailSize);
            detail.Elements = ElementIconRowAssets.Add(
                elements,
                TextAnchor.MiddleCenter,
                ElementIconRowAssets.DetailSize
            );
            Fixed(elements, ElementIconRowAssets.DetailSize);
            return detail;
        }

        /// <summary>
        /// A vertical layout <paramref name="width"/> wide at (x, y) on <paramref name="parent"/>
        /// that puts the face's lines one under another, centred up and down.
        /// </summary>
        public static RectTransform Column(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            float gap = Gap
        )
        {
            var column = Layout.Box(parent, name, x, y, width, height);
            var stack = column.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = alignment;
            stack.spacing = gap;
            stack.childControlWidth = false;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            return column;
        }

        /// <summary>
        /// The detail window over the whole of <paramref name="parent"/> (a screen's canvas or safe
        /// area), hidden: the shared veil, a small window with × at the top right and the face in
        /// it. The screen calls <see cref="ItemDetailWindow.Show"/> from its cards.
        /// </summary>
        public static ItemDetailWindow Window(RectTransform parent, string name = "ItemDetailWindow")
        {
            var holder = Rect(name, parent);
            Stretch(holder);
            var window = holder.gameObject.AddComponent<ItemDetailWindow>();
            var root = Rect("Window", holder);
            Stretch(root);
            // 幕は窓の親にしない。窓の中を押しても、幕のボタンへ伝わらないようにする。
            var veil = Rect("Veil", root);
            Stretch(veil);
            window.Veil = AddButton(veil, AddImage(veil, UiPalette.Veil, true));
            window.Veil.transition = Selectable.Transition.None;

            var layer = Layout.Layer(SafeArea(root), "Layout");
            float width = GuideFrameAssets.WidthOf(GuideFrameAssets.WindowSize.Small);
            var modal = GuideFrameAssets.CenteredWindow(
                layer,
                "Modal",
                GuideFrameAssets.WindowSize.Small,
                WindowHeight
            );
            window.Close = GuideFrameAssets.CloseButton(modal, width - Layout.HitSize - 8f, 4f);
            float inner = width - GuideFrameAssets.WindowPad * 2f;
            var column = Column(
                modal,
                "Face",
                GuideFrameAssets.WindowPad,
                WindowPadTop,
                inner,
                WindowHeight - WindowPadTop - WindowPadBottom
            );
            window.Face = Face(column, inner, WindowArt);
            // 中身を焼くまでは空の面にする（絵のない白い四角を出さない）。画面が見本の品を焼いて上書きする。
            window.Face.Show(null);
            window.Root = root.gameObject;
            root.gameObject.SetActive(false);
            return window;
        }

        private static TMP_Text Line(
            RectTransform column,
            string name,
            float size,
            Color color,
            float width,
            float height
        )
        {
            var label = Layout.Text(column, name, "", size, color, TextAlignmentOptions.Center, 0f, 0f, width, height);
            label.richText = true;
            Fixed(label.transform, height);
            return label;
        }

        public static void Fixed(Transform part, float height) =>
            part.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }
}
