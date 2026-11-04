using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.UI.GuideMenu.Editor
{
    /// <summary>
    /// The guide screen's frame for feature panels that fill the screen
    /// (<see cref="GuideFramedPanel"/>): the background blurred, a translucent band behind the
    /// header with thin lines between "back", the breadcrumb, the rune balance and ×, and a gold
    /// line under the header. Panels inside the frame use
    /// <see cref="Pane"/>, <see cref="Divider"/> and <see cref="Heading"/> for their translucent
    /// panes, the gold double line between them and their headings, so every framed screen looks
    /// the same. The diamonds are pixel art at 4 design pixels per dot, like the guide frames.
    /// </summary>
    public static class GuideFrameAssets
    {
        public const string DiamondPath = GuideMenuAssets.ArtFolder + "/FrameDiamond.png";
        public const string DiamondLargePath = GuideMenuAssets.ArtFolder + "/FrameDiamondLarge.png";
        public const string BlurFolder = GuideMenuAssets.ArtFolder + "/Blur";

        // 未解放のスキル・枠と準備中のタブに重ねる、9×10ドットの錠前。
        public const string LockPath = GuideMenuAssets.ArtFolder + "/GuideLock.png";

        // 線のない形のヘッダーの帯の下端を、中身へなじませるぼかし（上が帯の濃さ、下が透明）。
        public const string HeaderFadePath = GuideMenuAssets.ArtFolder + "/FrameHeaderFade.png";

        private const float Dot = GuideMenuAssets.DotScale;

        // ヘッダーの下の線の位置（Safe Areaの上端から）と、横線の両端の余白。
        public const float HeaderHeight = 112f;

        // 設計座標の画面の幅（Layout.Design.x）。
        private const float Width = 1920f;
        public const float Margin = 48f;

        // 板や帯は、Safe Areaや設計座標の外（切り欠きの裏、画面の端）までこれだけ伸ばす。
        public const float Bleed = 400f;

        // ヘッダーの帯、面、金の細い線。値は UiPalette の役割の名前で持つ。
        public static readonly Color HeaderColor = UiPalette.Band;
        public static readonly Color PaneDark = UiPalette.SurfaceDark;
        public static readonly Color PaneLight = UiPalette.SurfaceLight;
        public static readonly Color LineColor = UiPalette.LineFaint;
        public static readonly Color SeparatorColor = UiPalette.LineSeparator;
        public static readonly Color DividerColor = UiPalette.LineDivider;

        // 背景をぼかすために縮める幅。Bilinearで引き伸ばすと、ぼけた絵になる。
        private const int BlurWidth = 240;

        private static readonly string[] DiamondDots =
        {
            "..D..",
            ".DGD.",
            "DGLGD",
            ".DGD.",
            "..D..",
        };

        private static readonly string[] DiamondLargeDots =
        {
            "...D...",
            "..DGD..",
            ".DGGGD.",
            "DGGLGGD",
            ".DGGGD.",
            "..DGD..",
            "...D...",
        };

        private static readonly string[] LockDots =
        {
            "..ooooo..",
            ".ohhhhho.",
            ".oh...ho.",
            ".oh...ho.",
            "ooooooooo",
            "olllllllo",
            "ogggkgggo",
            "ogggkgggo",
            "ossssssso",
            ".ooooooo.",
        };

        public static void EnsureArt()
        {
            ArtAssets.WritePattern(DiamondPath, DiamondDots, Gold);
            ArtAssets.WritePattern(DiamondLargePath, DiamondLargeDots, Gold);
            ArtAssets.WritePattern(LockPath, LockDots, Steel);
            ArtAssets.WriteSmooth(
                HeaderFadePath,
                4,
                64,
                Vector4.zero,
                (u, v) => new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, v))
            );
        }

        private static Color Gold(char dot) =>
            dot switch
            {
                'D' => new Color32(0xa6, 0x50, 0x0d, 0xff),
                'G' => new Color32(0xff, 0xd7, 0x66, 0xff),
                'L' => new Color32(0xfd, 0xe4, 0x5a, 0xff),
                _ => Color.clear,
            };

        private static Color Steel(char dot) =>
            dot switch
            {
                'o' => new Color32(10, 13, 28, 255),
                'h' => new Color32(195, 201, 220, 255),
                'l' => new Color32(228, 232, 244, 255),
                'g' => new Color32(140, 150, 178, 255),
                's' => new Color32(92, 101, 132, 255),
                'k' => new Color32(10, 13, 28, 255),
                _ => Color.clear,
            };

        // --- The guide screen's frame (hidden until a framed panel opens) ---------------------

        /// <summary>The blurred copy of the background, over the background itself.</summary>
        public static GameObject BuildBackdrop(RectTransform root, Texture2D background)
        {
            var rect = Rect("FrameBackdrop", root);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = Blurred(background);
            image.raycastTarget = false;
            rect.gameObject.AddComponent<ResponsiveBackground>().AspectRatio =
                background.width / (float)background.height;
            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        /// <summary>
        /// The translucent band behind the header, reaching past the safe area's top. While
        /// <see cref="UiLook.Lineless"/>, its lower edge fades into the contents instead of ending
        /// on a line (doc/rules/ui-design.md の線のない形).
        /// </summary>
        public static GameObject BuildHeaderBand(RectTransform safe)
        {
            var band = Rect("FrameHeader", safe);
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = Vector2.one;
            float solid = UiLook.Lineless ? HeaderHeight - HeaderFadeAbove : HeaderHeight;
            band.offsetMin = new Vector2(-Bleed, -solid);
            band.offsetMax = new Vector2(Bleed, Bleed);
            AddImage(band, HeaderColor, false);
            if (UiLook.Lineless)
            {
                // 帯の下端から、帯の濃さを下へ薄くしていく。帯の中で始め、中身の上で消える。
                var fade = Rect("Fade", band);
                fade.anchorMin = Vector2.zero;
                fade.anchorMax = new Vector2(1, 0);
                fade.pivot = new Vector2(0.5f, 1f);
                fade.offsetMin = new Vector2(0, -HeaderFadeHeight);
                fade.offsetMax = Vector2.zero;
                var image = fade.gameObject.AddComponent<RawImage>();
                image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HeaderFadePath);
                image.color = HeaderColor;
                image.raycastTarget = false;
            }
            band.gameObject.SetActive(false);
            return band.gameObject;
        }

        // 線のない形のヘッダーで、帯の濃さを薄くし始める位置（ヘッダーの下端から上へ）と、薄くする長さ。
        private const float HeaderFadeAbove = 24f;
        private const float HeaderFadeHeight = 88f;

        /// <summary>The gold line under the header with a diamond at each end, over the panels.</summary>
        public static GameObject BuildHeaderLine(RectTransform safe)
        {
            var line = Rect("FrameHeaderLine", safe);
            line.anchorMin = new Vector2(0, 1);
            line.anchorMax = Vector2.one;
            line.pivot = new Vector2(0.5f, 0.5f);
            line.offsetMin = new Vector2(Margin, -HeaderHeight - 1f);
            line.offsetMax = new Vector2(-Margin, -HeaderHeight + 1f);
            AddImage(line, LineColor, false);
            EndDiamond(line, 0f);
            EndDiamond(line, 1f);
            line.gameObject.SetActive(false);
            return line.gameObject;
        }

        /// <summary>
        /// A thin line between two parts of the header, at <paramref name="x"/> from the left
        /// (<paramref name="side"/> 0) or right (1) edge of <paramref name="part"/>, from the
        /// screen's top down to the line under the header. Hidden until a framed panel opens.
        /// </summary>
        public static GameObject Separator(RectTransform part, float side, float x)
        {
            var line = Rect("FrameSeparator", part);
            line.anchorMin = new Vector2(side, 0);
            line.anchorMax = new Vector2(side, 1);
            line.offsetMin = new Vector2(x - 1f, -(HeaderHeight - part.rect.height));
            line.offsetMax = new Vector2(x + 1f, Bleed);
            AddImage(line, SeparatorColor, false);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.gameObject.SetActive(false);
            return line.gameObject;
        }

        private static void EndDiamond(RectTransform line, float side)
        {
            var diamond = Rect("Diamond", line);
            diamond.anchorMin = diamond.anchorMax = new Vector2(side, 0.5f);
            diamond.pivot = new Vector2(0.5f, 0.5f);
            diamond.sizeDelta = Vector2.one * DiamondDots.Length * Dot;
            diamond.anchoredPosition = Vector2.zero;
            SpriteImage(diamond, DiamondPath, Color.white);
        }

        // --- Panels inside the frame (design pixels from the top left of a 1920x1080 layer) ----

        /// <summary>
        /// A feature panel that fills the guide screen's safe area inside the frame: a stretched
        /// root named <paramref name="name"/> with the view <typeparamref name="T"/> and
        /// <see cref="GuideFramedPanel"/>, and the 1920x1080 layer its parts are placed on.
        /// </summary>
        public static (T View, RectTransform Layer) FramedPanel<T>(RectTransform safe, string name)
            where T : Component
        {
            var root = Rect(name, safe);
            Stretch(root);
            var view = root.gameObject.AddComponent<T>();
            // 線のない形で組み立てる画面は、開いている間ヘッダーの線も隠す。
            root.gameObject.AddComponent<GuideFramedPanel>().Lineless = UiLook.Lineless;
            return (view, Layout.LayerBelow(root, "Layout", HeaderHeight));
        }

        /// <summary>
        /// The two panes of a framed panel split at <paramref name="split"/>, the person or the
        /// party on the left and a list on the right, from the header's line down. Both are the
        /// pane the scene shows through, so the two sides read as one surface. The gold double
        /// line goes between them unless
        /// <paramref name="divider"/> is false, for a panel that draws it over its own contents.
        /// The panes span <paramref name="from"/> to <paramref name="to"/> (the whole width unless
        /// given) and reach past the screen's edge only where they touch it.
        /// </summary>
        public static void SplitPanes(
            RectTransform layer,
            float split,
            string leftName,
            string rightName,
            bool divider = true,
            float from = 0f,
            float to = Width
        )
        {
            Pane(layer, leftName, from, split, HeaderHeight, PaneLight, from <= 0f, false);
            Pane(layer, rightName, split, to, HeaderHeight, PaneLight, false, to >= Width);
            if (divider)
                Divider(layer, split, HeaderHeight);
        }

        /// <summary>
        /// A translucent pane from <paramref name="left"/> to <paramref name="right"/> and from
        /// <paramref name="top"/> to the bottom; it reaches past the layer at the screen's bottom
        /// and at the sides marked to bleed. It takes taps so the screen behind does not.
        /// </summary>
        public static RectTransform Pane(
            RectTransform layer,
            string name,
            float left,
            float right,
            float top,
            Color color,
            bool bleedLeft,
            bool bleedRight
        )
        {
            float x0 = bleedLeft ? left - Bleed : left;
            float x1 = bleedRight ? right + Bleed : right;
            var pane = Layout.Box(layer, name, x0, top, x1 - x0, Layout.Design.y - top + Bleed);
            AddImage(pane, color, true);
            return pane;
        }

        /// <summary>
        /// The gold double line between two panes at <paramref name="x"/>, from the header's line
        /// at <paramref name="top"/> to the bottom, with a diamond where it meets the header's line.
        /// </summary>
        public static void Divider(RectTransform layer, float x, float top)
        {
            if (UiLook.Lineless)
                return;
            float height = Layout.Design.y - top + Bleed;
            foreach (float offset in new[] { -4f, 2f })
                AddImage(
                    Layout.Box(layer, "DividerLine", x + offset, top, 2f, height),
                    DividerColor,
                    false
                );
            float size = DiamondLargeDots.Length * Dot;
            var diamond = Layout.Box(
                layer,
                "DividerDiamond",
                x - size / 2f,
                top - size / 2f,
                size,
                size
            );
            SpriteImage(diamond, DiamondLargePath, Color.white);
        }

        /// <summary>
        /// A window over a framed screen (e.g. the level-up dialog) at (x, y): the dark pane's
        /// navy, a thin gold line around it with a diamond at each corner, like the header's line.
        /// It takes taps so the screen behind does not.
        /// </summary>
        public static RectTransform Window(
            RectTransform layer,
            string name,
            float x,
            float y,
            float width,
            float height
        )
        {
            var window = Layout.Box(layer, name, x, y, width, height);
            WindowFrame(window);
            return window;
        }

        /// <summary>The three widths of the windows (<see cref="UiSpace"/>).</summary>
        public enum WindowSize
        {
            // 確かめる窓（レベルアップ）。
            Small,

            // 知らせる窓、連携、結果、召喚の結果と確率。
            Medium,

            // 一覧の窓（お宝）。
            Large,
        }

        public static float WidthOf(WindowSize size) =>
            size switch
            {
                WindowSize.Small => UiSpace.WindowSmall,
                WindowSize.Medium => UiSpace.WindowMedium,
                _ => UiSpace.WindowLarge,
            };

        /// <summary>
        /// A window of <paramref name="size"/> in the middle of a 1920x1080 layer,
        /// <paramref name="height"/> high (<see cref="Window"/>).
        /// </summary>
        public static RectTransform CenteredWindow(
            RectTransform layer,
            string name,
            WindowSize size,
            float height
        )
        {
            float width = WidthOf(size);
            return Window(
                layer,
                name,
                (Layout.Design.x - width) / 2f,
                (Layout.Design.y - height) / 2f,
                width,
                height
            );
        }

        // 中身で高さが決まる窓の、内側の余白（左右・上・下）。
        private static readonly RectOffset MessagePadding = new(72, 72, 56, 64);

        /// <summary>
        /// A window of <paramref name="size"/> in the middle of the safe area whose height follows
        /// its contents (the shared dialog, the link modal): <see cref="WindowFrame"/> with a
        /// column that places the title in light gold at the top, then whatever the caller adds
        /// (a picture, the message, the buttons) <paramref name="spacing"/> apart.
        /// </summary>
        public static (RectTransform Window, TMP_Text Title) MessageWindow(
            RectTransform safe,
            WindowSize size,
            float spacing = 28f,
            string name = "Window"
        )
        {
            var window = Rect(name, safe);
            window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(WidthOf(size), 0f);
            WindowFrame(window);
            var fitter = window.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                MessagePadding.left,
                MessagePadding.right,
                MessagePadding.top,
                MessagePadding.bottom
            );
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var title = Label(
                window,
                "Title",
                "",
                UiType.Heading,
                UiPalette.GoldLight,
                TextAlignmentOptions.Center
            );
            return (window, title);
        }

        /// <summary>
        /// The row of buttons along the bottom of a <see cref="MessageWindow"/>, centred; add them
        /// with the laid-out <see cref="WindowButton(RectTransform, string, string, float, float, bool, float)"/>,
        /// the one that decides on the right.
        /// </summary>
        public static RectTransform ButtonRow(RectTransform window, float spacing = 32f)
        {
            var buttons = Rect("Buttons", window);
            var row = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.padding = new RectOffset(0, 0, (int)UiSpace.Inset, 0);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            return buttons;
        }

        // 一覧の窓の、見出しの段の高さと左右の余白。
        public const float WindowHeadHeight = 120f;
        public const float WindowPad = 56f;

        /// <summary>The parts of a list window (<see cref="ListWindow"/>).</summary>
        public readonly struct ListWindowParts
        {
            public ListWindowParts(
                RectTransform root,
                Button veil,
                RectTransform window,
                TMP_Text title,
                Button close
            )
            {
                Root = root;
                Veil = veil;
                Window = window;
                Title = title;
                Close = close;
            }

            /// <summary>The whole screen (veil and window); hide it to close the window.</summary>
            public RectTransform Root { get; }

            /// <summary>The dark veil over the screen; a tap on it closes the window.</summary>
            public Button Veil { get; }

            /// <summary>The window, laid out from its top left in design coordinates.</summary>
            public RectTransform Window { get; }
            public TMP_Text Title { get; }
            public Button Close { get; }
        }

        /// <summary>
        /// A list window in the middle of the screen (the treasures, the summon's results and
        /// rates): the shared veil over the whole screen (a tap closes), the window
        /// (<see cref="WindowFrame"/>), the heading with its diamond at the top left, × at the top
        /// right and the gold line under the heading. Lay the body out from
        /// <see cref="WindowHeadHeight"/> down and a bottom-right button with
        /// <see cref="WindowButton"/>. The root is left shown.
        /// </summary>
        public static ListWindowParts ListWindow(
            RectTransform parent,
            string name,
            WindowSize size,
            float height,
            string title = ""
        )
        {
            float width = WidthOf(size);
            var root = Rect(name, parent);
            Stretch(root);
            // 幕は窓の親にしない。窓の中を押しても、幕のボタンへ伝わらないようにする。
            var veil = Rect("Veil", root);
            Stretch(veil);
            var veilButton = AddButton(veil, AddImage(veil, UiPalette.Veil, true));
            veilButton.transition = Selectable.Transition.None;

            var layer = Layout.Layer(SafeArea(root), "Layout");
            var window = CenteredWindow(layer, "Modal", size, height);
            var heading = Heading(
                window,
                "Title",
                title,
                WindowPad,
                12f,
                width - WindowPad - Layout.HitSize - 24f,
                96f,
                UiType.Title
            );
            var close = CloseButton(window, width - Layout.HitSize - 8f, 4f);
            AddImage(
                Layout.Box(
                    window,
                    "HeadLine",
                    WindowPad,
                    WindowHeadHeight - 4f,
                    width - WindowPad * 2f,
                    2f
                ),
                LineColor,
                false
            );
            return new ListWindowParts(root, veilButton, window, heading, close);
        }

        // ×の絵の大きさ。押せる範囲（Layout.HitSize）の中央に置く。
        public const float CloseIconSize = 64f;

        /// <summary>
        /// The × that closes a window or an overlay, at (x, y) from the top left of
        /// <paramref name="parent"/>: a tap area <see cref="Layout.HitSize"/> square with the
        /// cross in the middle, which tints when pressed. Every window and overlay uses this one.
        /// </summary>
        public static Button CloseButton(
            RectTransform parent,
            float x,
            float y,
            float iconSize = CloseIconSize,
            string name = "Close"
        ) => CloseMark(Layout.Box(parent, name, x, y, Layout.HitSize, Layout.HitSize), iconSize);

        /// <summary>
        /// Turns <paramref name="rect"/>, placed by the caller (e.g. at the right end of a band),
        /// into the same ×: a clear tap area with the cross in the middle that tints when pressed.
        /// Make it <see cref="Layout.HitSize"/> square.
        /// </summary>
        public static Button CloseMark(RectTransform rect, float iconSize = CloseIconSize)
        {
            var button = AddTintButton(rect, AddImage(rect, Color.clear, true));
            CrossIcon(rect, Vector2.zero, iconSize);
            return button;
        }

        /// <summary>
        /// The face of a dashed card that takes something off (外す): the cross
        /// <see cref="GuideMenuAssets.IconSize"/> square with its left edge at
        /// <paramref name="iconLeft"/>, and the label from <paramref name="textLeft"/> to
        /// <paramref name="textRight"/>, both centred in a card <paramref name="height"/> high.
        /// </summary>
        public static void RemoveFace(
            RectTransform inside,
            string label,
            float iconLeft,
            float textLeft,
            float textRight,
            float height
        )
        {
            float icon = GuideMenuAssets.IconSize;
            var box = Layout.Box(inside, "Icon", iconLeft, (height - icon) / 2f, icon, icon);
            SpriteImage(box, GuideMenuAssets.IconClosePath, Color.white);
            var text = Layout.Text(
                inside,
                "Label",
                label,
                UiType.Lead,
                UiPalette.TextSub,
                TextAlignmentOptions.MidlineLeft,
                textLeft,
                0f,
                textRight - textLeft,
                height
            );
            // 「パーティーから外す」が細い札の枠を越えないよう縮める。
            GuideMenuAssets.Shrink(text, UiType.Lead * 0.75f);
        }

        /// <summary>
        /// The cross alone, <paramref name="size"/> square centred at <paramref name="center"/>
        /// from the middle of <paramref name="parent"/>, for a card that removes something.
        /// </summary>
        public static Image CrossIcon(RectTransform parent, Vector2 center, float size)
        {
            var cross = Rect("Icon", parent);
            Place(cross, center, Vector2.one * size);
            return SpriteImage(cross, GuideMenuAssets.IconClosePath, Color.white);
        }

        /// <summary>
        /// The text in a list's place while it reads, fails or is empty ("読み込み中…",
        /// "取得できませんでした"): centred in the list's top row, from <paramref name="left"/> to
        /// <paramref name="right"/> and <paramref name="top"/> down. Every list uses this one.
        /// </summary>
        public static TMP_Text ListStatus(
            RectTransform parent,
            float left,
            float right,
            float top,
            string name = "Status"
        ) =>
            Layout.Text(
                parent,
                name,
                "",
                UiType.Body,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                left,
                top + UiSpace.Pad,
                right - left,
                GuideMenuAssets.EntryHeight
            );

        /// <summary>
        /// An amount of runes at (x, y): the rune icon and the number after it, the pair aligned in
        /// the box by <paramref name="alignment"/>. The icon is as high as the number's
        /// <paramref name="size"/> unless <paramref name="iconSize"/> is given; the number is gold
        /// (a price or a reward) unless <paramref name="color"/> is given (a cost under a button).
        /// Returns the number's label; its parent is the row to show or hide. Prices, rewards and
        /// costs all use this one.
        /// </summary>
        public static TMP_Text RuneAmount(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            float size,
            TextAnchor alignment,
            float iconSize = 0f,
            Color? color = null
        )
        {
            float icon = iconSize > 0f ? iconSize : size;
            var row = Layout.Row(parent, name, x, y, width, height, size * 0.25f, alignment);
            var cell = Rect("Icon", row);
            Layout.Fit(cell, icon);
            var image = Rect("Image", cell);
            Place(image, Vector2.zero, Vector2.one * icon);
            var sprite = SpriteImage(image, GuideMenuAssets.IconRunePath, Color.white);
            sprite.preserveAspect = true;
            sprite.raycastTarget = false;
            var label = Label(
                row,
                "Value",
                "",
                size,
                color ?? UiPalette.Gold,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.Fit(label, 0);
            return label;
        }

        /// <summary>
        /// A button of a window at (x, y) from the window's top left, with the label in the
        /// middle: the one that decides (<paramref name="primary"/>) is the shared primary button,
        /// the others (やめる・あとで) the unlit frosted glass card. Returns the button and
        /// the label, and the card's inside for more parts (a cost under the label).
        /// </summary>
        public static (Button Button, TMP_Text Label, RectTransform Content) WindowButton(
            RectTransform window,
            string name,
            string label,
            float x,
            float y,
            float width,
            float height,
            bool primary,
            float size = UiType.Title
        ) => Decorate(Layout.Box(window, name, x, y, width, height), label, primary, size);

        /// <summary>
        /// The same window button for a window whose layout group places its parts (the shared
        /// dialog's choices, the buttons in a row under a message): <paramref name="width"/> and
        /// <paramref name="height"/> become the button's preferred size in the group.
        /// </summary>
        public static (Button Button, TMP_Text Label, RectTransform Content) WindowButton(
            RectTransform parent,
            string name,
            string label,
            float width,
            float height,
            bool primary,
            float size = UiType.Title
        )
        {
            var rect = Rect(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
                element.preferredWidth = width;
            element.preferredHeight = element.minHeight = height;
            return Decorate(rect, label, primary, size);
        }

        // 決める方はプライマリー（青い板）、ほかはセカンダリー（灯さない札）の共通のボタン
        // （doc/rules/ui-design.md#決めるボタン）。
        private static (Button Button, TMP_Text Label, RectTransform Content) Decorate(
            RectTransform rect,
            string label,
            bool primary,
            float size
        ) =>
            ActionButtonAssets.Build(
                rect,
                label,
                primary ? ButtonRole.Primary : ButtonRole.Secondary,
                size
            );

        /// <summary>
        /// Turns <paramref name="window"/> into a window over a screen, whatever its size (a
        /// fixed one, or one that a layout group sizes to its contents such as the confirmation
        /// dialog): the dark pane's navy, opaque, and a thin gold line around it with a diamond at
        /// each corner. The line and diamonds are left out of any layout. It takes taps so the
        /// screen behind does not.
        /// </summary>
        public static void WindowFrame(RectTransform window)
        {
            AddImage(window, UiPalette.WindowFill, true);
            // 上・下・左・右の辺に、2px の線を辺に沿わせて伸ばす。
            foreach (
                var (min, max, offsetMin, offsetMax) in new[]
                {
                    (new Vector2(0, 1), Vector2.one, new Vector2(0, -2), Vector2.zero),
                    (Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2)),
                    (Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(2, 0)),
                    (new Vector2(1, 0), Vector2.one, new Vector2(-2, 0), Vector2.zero),
                }
            )
            {
                var line = Rect("Line", window);
                line.anchorMin = min;
                line.anchorMax = max;
                line.offsetMin = offsetMin;
                line.offsetMax = offsetMax;
                AddImage(line, DividerColor, false);
                Unlaid(line);
            }
            // 四隅のひし形は、角から1pxだけ内へ寄せて線の上に置く。
            float size = DiamondDots.Length * Dot;
            foreach (
                var corner in new[]
                {
                    new Vector2(0, 1),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(1, 0),
                }
            )
            {
                var diamond = Rect("Diamond", window);
                diamond.anchorMin = diamond.anchorMax = corner;
                diamond.pivot = new Vector2(0.5f, 0.5f);
                diamond.sizeDelta = Vector2.one * size;
                diamond.anchoredPosition = new Vector2(
                    corner.x > 0 ? -1f : 1f,
                    corner.y > 0 ? -1f : 1f
                );
                SpriteImage(diamond, DiamondPath, Color.white);
                Unlaid(diamond);
            }
        }

        private static void Unlaid(RectTransform rect) =>
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        /// <summary>
        /// The lock over a skill's icon that is not unlocked yet, in the middle of
        /// <paramref name="icon"/> at 3 design pixels per dot. Returns it to show or hide.
        /// </summary>
        public static GameObject IconLock(RectTransform icon)
        {
            var locked = Rect("Lock", icon);
            Stretch(locked);
            var image = Rect("Image", locked);
            Place(image, Vector2.zero, new Vector2(9, 10) * 3f);
            SpriteImage(image, LockPath, Color.white);
            return locked.gameObject;
        }

        /// <summary>A pane's heading at (x, y): a gold diamond and the title in light gold.</summary>
        public static TMP_Text Heading(
            RectTransform layer,
            string name,
            string text,
            float x,
            float y,
            float width,
            float height,
            float size = 34f
        )
        {
            // 線のない形では、ひし形の飾りを置かず、金の文字だけにする（doc/rules/ui-design.md の線のない形）。
            float lead = 0f;
            if (!UiLook.Lineless)
            {
                float diamond = DiamondSize(size);
                var mark = Layout.Box(
                    layer,
                    name + "Diamond",
                    x + 4f,
                    y + (height - diamond) / 2f,
                    diamond,
                    diamond
                );
                SpriteImage(mark, DiamondLargePath, Color.white);
                lead = diamond + size * 0.4f;
            }
            return Layout.Text(
                layer,
                name,
                text,
                size,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineLeft,
                x + 4f + lead,
                y,
                width - lead - 4f,
                height
            );
        }

        /// <summary>
        /// The heading's diamond as the first child of a row (a title followed by a note), with
        /// the row's first label in the heading's light gold.
        /// </summary>
        public static void HeadingRow(RectTransform row, float size)
        {
            var title = row.GetComponentInChildren<TMP_Text>();
            if (title != null)
                title.color = UiPalette.GoldLight;
            if (UiLook.Lineless)
                return;
            var mark = Rect("Diamond", row);
            mark.SetSiblingIndex(0);
            var element = mark.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = DiamondSize(size);
            SpriteImage(mark, DiamondLargePath, Color.white).preserveAspect = true;
        }

        /// <summary>
        /// One tab of a column of tabs at (x, y) from the top left of <paramref name="parent"/>:
        /// the label in the middle, a thin line under it, and while chosen the gold bar on its
        /// left (as gold as the row's underline).
        /// </summary>
        public static GuideTab SideTab(
            RectTransform parent,
            string name,
            string label,
            float x,
            float y,
            float width,
            float height,
            bool locked = false
        )
        {
            var rect = Layout.Box(parent, name, x, y, width, height);
            var tab = rect.gameObject.AddComponent<GuideTab>();
            tab.Button = GuideMenuAssets.AddButton(rect, AddImage(rect, Color.clear, true));
            var bar = Layout.Box(rect, "Selected", 6f, 20f, SideTabBar, height - 40f);
            AddImage(bar, UiPalette.Gold, false);
            bar.gameObject.SetActive(false);
            tab.Selected = bar.gameObject;
            tab.Label = Layout.Text(
                rect,
                "Label",
                label,
                34,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                SideTabBar + 12f,
                0f,
                width - SideTabBar - 24f,
                height
            );
            GuideMenuAssets.Shrink(tab.Label, 24);
            Layout.Line(rect, "Line", 20f, height - 2f, width - 40f, 2f);
            if (locked)
            {
                // 準備中のタブは、文字の左に錠前を置き、押せなくする。文字は錠前の右に寄せ、重ならないようにする。
                float textLeft = SideTabBar + 16f + 27f + 8f;
                Layout.At(tab.Label, textLeft, 0f, width - textLeft - 12f, height);
                var lockIcon = Layout.Box(
                    rect,
                    "Lock",
                    SideTabBar + 16f,
                    (height - 30f) / 2f,
                    27f,
                    30f
                );
                SpriteImage(lockIcon, LockPath, new Color(1f, 1f, 1f, 0.6f));
                tab.Lock = lockIcon.gameObject;
                tab.Button.interactable = false;
                tab.Chosen = false;
            }
            return tab;
        }

        // 左の縦の分類の幅と、1つの高さ。
        public const float SideRailWidth = 256f;
        public const float SideTabHeight = 120f;

        /// <summary>
        /// The column of categories down the left edge of a framed panel (the shop, the library's
        /// gear, the loadout's skills and items): one <see cref="SideTab"/> under another from the
        /// header down, the gold double line on the column's right, and the first tab chosen. The
        /// tabs whose index is in <paramref name="locked"/> show the lock and take no taps. Panels
        /// put their lists right of <see cref="SideRailWidth"/>. Categories never go across the top
        /// (doc/rules/ui-design.md の共通の部品).
        /// </summary>
        public static GuideTab[] SideTabs(
            RectTransform layer,
            string name,
            string[] labels,
            float left = 0f,
            float top = HeaderHeight + 24f,
            float width = SideRailWidth,
            bool divider = true,
            params int[] locked
        )
        {
            var column = Layout.Box(layer, name, left, top, width, labels.Length * SideTabHeight);
            var tabs = new GuideTab[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                tabs[i] = SideTab(
                    column,
                    "Tab" + i,
                    labels[i],
                    0f,
                    i * SideTabHeight,
                    width,
                    SideTabHeight,
                    System.Array.IndexOf(locked, i) >= 0
                );
            if (tabs.Length > 0 && tabs[0].Lock == null)
                tabs[0].Chosen = true;
            if (divider)
                Divider(layer, left + width, HeaderHeight);
            return tabs;
        }

        // 縦のタブで、選んだときに左に引く金の縦線の太さ。
        private const float SideTabBar = 8f;

        /// <summary>
        /// A row of tabs at (x, y) from the top left of <paramref name="parent"/>, each
        /// <paramref name="width"/> wide, over a thin gold line across <paramref name="lineWidth"/>.
        /// The tabs whose index is in <paramref name="locked"/> show the lock and take no taps.
        /// </summary>
        public static GuideTab[] Tabs(
            RectTransform parent,
            string name,
            string[] labels,
            float x,
            float y,
            float width,
            float height,
            float lineWidth,
            params int[] locked
        )
        {
            var row = Layout.Box(parent, name, x, y, lineWidth, height);
            AddImage(Layout.Box(row, "Line", 0, height - 1f, lineWidth, 2f), LineColor, false);
            var tabs = new GuideTab[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var rect = Layout.Box(row, "Tab" + i, i * width, 0, width, height);
                var tab = rect.gameObject.AddComponent<GuideTab>();
                var hit = AddImage(rect, Color.clear, true);
                tab.Button = GuideMenuAssets.AddButton(rect, hit);

                // 選んだときの金の下線（3ドットの高さ）。下の細い線に重ねる。
                var underline = Layout.Box(rect, "Selected", 12f, height - 6f, width - 24f, 8f);
                AddImage(underline, UiPalette.Gold, false);
                underline.gameObject.SetActive(false);
                tab.Selected = underline.gameObject;

                bool isLocked = System.Array.IndexOf(locked, i) >= 0;
                // 準備中のタブは、左に錠前、その右に文字を並べる。
                tab.Label = Layout.Text(
                    rect,
                    "Label",
                    labels[i],
                    34,
                    UiPalette.TextSub,
                    isLocked ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center,
                    isLocked ? 54f : 0f,
                    0f,
                    isLocked ? width - 62f : width,
                    height - 6f
                );
                GuideMenuAssets.Shrink(tab.Label, 24);
                if (isLocked)
                {
                    var lockIcon = Layout.Box(
                        rect,
                        "Lock",
                        16f,
                        (height - 6f - 30f) / 2f,
                        27f,
                        30f
                    );
                    SpriteImage(lockIcon, LockPath, new Color(1f, 1f, 1f, 0.6f));
                    tab.Lock = lockIcon.gameObject;
                    tab.Button.interactable = false;
                    tab.Chosen = false;
                }
                tabs[i] = tab;
            }
            return tabs;
        }

        // 見出しの文字の大きさに合わせたひし形（7ドット）。小さい見出しでは1ドットを3pxにする。
        private static float DiamondSize(float size) =>
            DiamondLargeDots.Length * (size >= 32f ? Dot : 3f);

        // --- The blurred background ----------------------------------------------------

        /// <summary>
        /// A small, blurred copy of <paramref name="background"/>, saved beside the guide frames.
        /// Shown stretched with bilinear filtering, it reads as the scene out of focus.
        /// </summary>
        public static Texture2D Blurred(Texture2D background)
        {
            string source = AssetDatabase.GetAssetPath(background);
            string path = $"{BlurFolder}/{Path.GetFileNameWithoutExtension(source)}Blur.png";
            AssetFolders.Ensure(BlurFolder);
            var full = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var small = default(Texture2D);
            try
            {
                full.LoadImage(File.ReadAllBytes(source));
                int width = BlurWidth;
                int height = Mathf.Max(
                    1,
                    Mathf.RoundToInt(BlurWidth * full.height / (float)full.width)
                );
                var pixels = Shrink(full, width, height);
                for (int pass = 0; pass < 3; pass++)
                {
                    pixels = BoxBlur(pixels, width, height, 2, horizontal: true);
                    pixels = BoxBlur(pixels, width, height, 2, horizontal: false);
                }
                small = new Texture2D(width, height, TextureFormat.RGBA32, false);
                small.SetPixels(pixels);
                small.Apply();
                File.WriteAllBytes(path, small.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(full);
                if (small != null)
                    Object.DestroyImmediate(small);
            }
            return ArtAssets.ImportTexture(path, FilterMode.Bilinear);
        }

        // 縮めた1画素に、元の画像のその範囲の平均の色を入れる。
        private static Color[] Shrink(Texture2D full, int width, int height)
        {
            var source = full.GetPixels();
            var pixels = new Color[width * height];
            float sx = full.width / (float)width;
            float sy = full.height / (float)height;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int x0 = Mathf.FloorToInt(x * sx);
                int y0 = Mathf.FloorToInt(y * sy);
                int x1 = Mathf.Min(full.width, Mathf.CeilToInt((x + 1) * sx));
                int y1 = Mathf.Min(full.height, Mathf.CeilToInt((y + 1) * sy));
                var sum = Color.clear;
                for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                    sum += source[yy * full.width + xx];
                pixels[y * width + x] = sum / ((x1 - x0) * (y1 - y0));
            }
            return pixels;
        }

        private static Color[] BoxBlur(
            Color[] pixels,
            int width,
            int height,
            int radius,
            bool horizontal
        )
        {
            var result = new Color[pixels.Length];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var sum = Color.clear;
                int count = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int xx = horizontal ? Mathf.Clamp(x + k, 0, width - 1) : x;
                    int yy = horizontal ? y : Mathf.Clamp(y + k, 0, height - 1);
                    sum += pixels[yy * width + xx];
                    count++;
                }
                result[y * width + x] = sum / count;
            }
            return result;
        }
    }
}
