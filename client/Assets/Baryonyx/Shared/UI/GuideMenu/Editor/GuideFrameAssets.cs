using System.IO;
using Baryonyx.Editor;
using Baryonyx.Editor.Art;
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

        private const float Dot = GuideMenuAssets.DotScale;

        // ヘッダーの下の線の位置（Safe Areaの上端から）と、横線の両端の余白。
        public const float HeaderHeight = 112f;

        // 設計座標の画面の幅（Layout.Design.x）。
        private const float Width = 1920f;
        public const float Margin = 48f;

        // 板や帯は、Safe Areaや設計座標の外（切り欠きの裏、画面の端）までこれだけ伸ばす。
        public const float Bleed = 400f;

        // ヘッダーの帯と、景色が透ける面（左右とも同じ色）。濃い色は、面の上に重ねる窓に使う。
        public static readonly Color HeaderColor = new(0.024f, 0.035f, 0.094f, 0.84f);
        public static readonly Color PaneDark = new(0.024f, 0.035f, 0.094f, 0.965f);
        public static readonly Color PaneLight = new(0.063f, 0.086f, 0.196f, 0.55f);

        // 金の細い線。ヘッダーの区切りは淡く、面の仕切り（2本線）は少し濃い。
        public static readonly Color LineColor = new(1f, 0.86f, 0.59f, 0.43f);
        public static readonly Color SeparatorColor = new(1f, 0.86f, 0.59f, 0.5f);
        public static readonly Color DividerColor = new(1f, 0.86f, 0.59f, 0.6f);

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

        /// <summary>The translucent band behind the header, reaching past the safe area's top.</summary>
        public static GameObject BuildHeaderBand(RectTransform safe)
        {
            var band = Rect("FrameHeader", safe);
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = Vector2.one;
            band.offsetMin = new Vector2(-Bleed, -HeaderHeight);
            band.offsetMax = new Vector2(Bleed, Bleed);
            AddImage(band, HeaderColor, false);
            band.gameObject.SetActive(false);
            return band.gameObject;
        }

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
            root.gameObject.AddComponent<GuideFramedPanel>();
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

        /// <summary>
        /// Turns <paramref name="window"/> into a window over a screen, whatever its size (a
        /// fixed one, or one that a layout group sizes to its contents such as the confirmation
        /// dialog): the dark pane's navy, opaque, and a thin gold line around it with a diamond at
        /// each corner. The line and diamonds are left out of any layout. It takes taps so the
        /// screen behind does not.
        /// </summary>
        public static void WindowFrame(RectTransform window)
        {
            // 後ろの画面の文字が透けないよう、濃い紺（PaneDark）で不透明に塗る（リニア色空間では少しの透けも目立つ）。
            AddImage(window, new Color(PaneDark.r, PaneDark.g, PaneDark.b, 1f), true);
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
            return Layout.Text(
                layer,
                name,
                text,
                size,
                UiPalette.GoldLight,
                TextAlignmentOptions.MidlineLeft,
                x + 4f + diamond + size * 0.4f,
                y,
                width - diamond - size * 0.4f - 4f,
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
            var mark = Rect("Diamond", row);
            mark.SetSiblingIndex(0);
            var element = mark.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = DiamondSize(size);
            SpriteImage(mark, DiamondLargePath, Color.white).preserveAspect = true;
        }

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
