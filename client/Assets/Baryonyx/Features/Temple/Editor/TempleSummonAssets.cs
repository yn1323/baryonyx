using System.Collections.Generic;
using System.Linq;
using Baryonyx.Inventory;
using Baryonyx.Library;
using Baryonyx.Library.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Frame = Baryonyx.UI.GuideMenu.Editor.GuideFrameAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Temple.Editor
{
    /// <summary>
    /// Builds the temple's summoning (<see cref="TempleSummonView"/>) over the whole screen: the
    /// cathedral's key art as the presentation once starts from it, the buttons that summon once
    /// or ten times at the bottom, and a small button at the bottom left for the rates. The
    /// results window (<see cref="TempleSummonResults"/>) and the rates window
    /// (<see cref="TempleSummonRates"/>) cover the whole screen, so they are built over the
    /// screen (<see cref="BuildOverlay"/>) and handed to the panel.
    /// </summary>
    public static class TempleSummonAssets
    {
        // 召喚のボタン。設計座標、左上から。下の端にそろえ、画面の中央に2つ並べる。
        private const float ButtonWidth = 460f;
        private const float ButtonHeight = 136f;
        private const float ButtonGap = 32f;
        private const float ButtonBottom = 24f;
        private const float RuneIconSize = 24f * 2f;

        // 左下の、確率の窓を開く小さなボタン。
        private static readonly Vector2 RatesButtonSize = new(200f, 96f);
        private const float RatesButtonMargin = 32f;

        // 結果の窓。札を5列で2段まで並べ、右下に「もう一度」を置く。
        private const float WindowWidth = 1400f;
        private const float WindowHeight = 928f;
        private const float Pad = 56f;
        private const float HeadHeight = 120f;
        private static readonly Vector2 Card = new(236f, 300f);
        private const float CardGap = 24f;
        private const float CardArtBox = 128f;

        // 確率の窓。割合の札と、出る物の一覧を上下に流す。
        private const float RatesHeight = 960f;
        private const float HeadingHeight = 72f;
        private const float TileHeight = 132f;
        private const float TileGap = 16f;
        private const float SectionGap = 32f;
        private const int EntryColumns = 5;
        private const float EntryHeight = 64f;
        private const float EntryGap = 12f;

        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.72f);

        public static GameObject BuildPanel(RectTransform safe, GuideMenuView view)
        {
            var panel = Rect("TempleSummon", safe);
            Stretch(panel);
            // 画面いっぱいの絵の上に、上の帯（もどる・パンくず・所持ルーン・×）を残す。
            panel.SetAsFirstSibling();
            var summon = panel.gameObject.AddComponent<TempleSummonView>();
            summon.Guide = view;

            // キャッチイラスト。1回の演出の始まりと同じ置き方（幅いっぱい、上端をそろえる）にし、
            // 演出へ切り替わっても絵が動かないようにする。横長の画面の左右は、同じ絵を暗くして覆う。
            var bleed = Rect("Bleed", panel);
            Stretch(bleed);
            bleed.offsetMin = new Vector2(-400f, -400f);
            bleed.offsetMax = new Vector2(400f, 400f);
            var fill = Rect("BackFill", bleed);
            var fillImage = fill.gameObject.AddComponent<RawImage>();
            var keyArt = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TempleSummonShowAssets.KeyArtPath
            );
            fillImage.texture = keyArt;
            fillImage.color = new Color(0.35f, 0.35f, 0.4f, 1f);
            fillImage.raycastTarget = true;
            var fitter = fill.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1536f / 1024f;
            var artLayer = Layout.Layer(panel, "Art");
            var art = Layout.Box(artLayer, "KeyArt", 0f, 0f, Layout.Design.x, 1280f);
            var artImage = art.gameObject.AddComponent<RawImage>();
            artImage.texture = keyArt;
            artImage.raycastTarget = false;

            var layer = Layout.Layer(panel, "Layout");
            float top = Layout.Design.y - ButtonBottom - ButtonHeight;
            float left = (Layout.Design.x - ButtonGap) / 2f - ButtonWidth;
            summon.Single = SummonButton(
                layer,
                "Single",
                "1回召喚",
                TempleSummon.SinglePrice,
                left,
                top
            );
            float tenLeft = (Layout.Design.x + ButtonGap) / 2f;
            summon.Ten = SummonButton(
                layer,
                "Ten",
                "10回召喚",
                TempleSummon.TenPrice,
                tenLeft,
                top
            );
            Layout.Text(
                layer,
                "Guarantee",
                $"★{TempleSummon.GuaranteedStars}以上 1つ確定",
                28,
                UiPalette.Gold,
                TextAlignmentOptions.Center,
                tenLeft,
                top - 48f,
                ButtonWidth,
                44f
            );

            var (rates, _, ratesInside) = GlassCardAssets.Choice(
                Layout.Box(
                    layer,
                    "RatesButton",
                    RatesButtonMargin,
                    Layout.Design.y - ButtonBottom - RatesButtonSize.y,
                    RatesButtonSize.x,
                    RatesButtonSize.y
                ),
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var ratesLabel = Label(
                ratesInside,
                "Label",
                "確率",
                36,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)ratesLabel.transform);
            summon.RatesButton = rates;
            return panel.gameObject;
        }

        // すりガラスのボタン。上に「1回召喚」、下にルーンのアイコンと値段。
        private static Button SummonButton(
            RectTransform layer,
            string name,
            string text,
            int price,
            float x,
            float y
        )
        {
            var (button, _, inside) = GlassCardAssets.Choice(
                Layout.Box(layer, name, x, y, ButtonWidth, ButtonHeight),
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            var label = Label(
                inside,
                "Label",
                text,
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Band((RectTransform)label.transform, top: true, 16f, 60f, 16f, 16f);
            var row = Rect("Price", inside);
            Band(row, top: false, 14f, 48f, 16f, 16f);
            var group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.childAlignment = TextAnchor.MiddleCenter;
            group.spacing = 10f;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = false;
            var icon = Rect("RuneIcon", row);
            SpriteImage(icon, IconRunePath, Color.white).raycastTarget = false;
            var size = icon.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = RuneIconSize;
            Label(
                row,
                "Price",
                price.ToString("N0"),
                38,
                UiPalette.Gold,
                TextAlignmentOptions.Midline
            );
            return button;
        }

        /// <summary>
        /// The windows over the whole screen, closed: the results and the rates. Hands them to
        /// the summoning panel, and returns the object that steps back first.
        /// </summary>
        public static GameObject BuildOverlay(RectTransform root, GuideMenuView view)
        {
            var overlay = Rect("SummonOverlay", root);
            Stretch(overlay);
            var windows = overlay.gameObject.AddComponent<TempleSummonOverlay>();
            windows.Results = BuildResults(overlay);
            windows.Rates = BuildRates(overlay, windows.Results.Data);
            var summon = System.Array.Find(view.ItemPanels, panel => panel != null);
            if (summon != null && summon.TryGetComponent(out TempleSummonView panelView))
            {
                panelView.Results = windows.Results;
                panelView.Rates = windows.Rates;
            }
            return overlay.gameObject;
        }

        // 確率の窓：種類・レア度・品質の割合の札と、出る物の一覧。窓の中を上下に流す。
        private static TempleSummonRates BuildRates(RectTransform parent, LibraryData data)
        {
            var root = Rect("SummonRates", parent);
            Stretch(root);
            var rates = root.gameObject.AddComponent<TempleSummonRates>();
            var window = Rect("Window", root);
            Stretch(window);
            rates.Window = window.gameObject;
            var veil = Rect("Veil", window);
            Stretch(veil);
            rates.Veil = Baryonyx.Editor.UI.UiBuild.AddButton(veil, AddImage(veil, Veil, true));
            rates.Veil.transition = Selectable.Transition.None;

            var layer = Layout.Layer(SafeArea(window), "Layout");
            var modal = Frame.Window(
                layer,
                "Modal",
                (Layout.Design.x - WindowWidth) / 2f,
                (Layout.Design.y - RatesHeight) / 2f,
                WindowWidth,
                RatesHeight
            );
            Frame.Heading(modal, "Title", "提供割合", Pad, 16f, 900f, 96f, 44f);
            var close = Layout.Box(
                modal,
                "Close",
                WindowWidth - Layout.HitSize - 8f,
                4f,
                Layout.HitSize,
                Layout.HitSize
            );
            rates.Close = Baryonyx.Editor.UI.UiBuild.AddButton(
                close,
                AddImage(close, Color.clear, true)
            );
            var cross = Rect("Icon", close);
            Place(cross, Vector2.zero, Vector2.one * 64f);
            SpriteImage(cross, IconClosePath, Color.white);
            var line = Layout.Box(
                modal,
                "HeadLine",
                Pad,
                HeadHeight - 4f,
                WindowWidth - Pad * 2f,
                2f
            );
            AddImage(line, Frame.LineColor, false);

            var viewport = Layout.Box(
                modal,
                "Viewport",
                0f,
                HeadHeight,
                WindowWidth,
                RatesHeight - HeadHeight - 8f
            );
            Layout.Mask(viewport);
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);

            float width = WindowWidth - Pad * 2f;
            float y = 24f;
            var kinds = new List<(string, int, Color)>();
            foreach (var (_, name, permille) in TempleSummon.Categories)
                kinds.Add((name, permille, UiPalette.TextMain));
            y = Section(content, "Kinds", "種類ごとの確率", kinds, y, width) + SectionGap;
            var stars = new List<(string, int, Color)>();
            for (int i = TempleSummon.StarPermille.Count; i >= 1; i--)
                stars.Add((new string('★', i), TempleSummon.StarPermille[i - 1], UiPalette.Gold));
            y =
                Section(content, "Stars", "レア度ごとの確率（装備・アイテム）", stars, y, width)
                + SectionGap;
            var qualities = new List<(string, int, Color)>();
            foreach (var (quality, permille) in TempleSummon.QualityPermille)
                qualities.Add((Quality.Letter(quality), permille, QualityPalette.Of(quality)));
            y =
                Section(content, "Qualities", "品質ごとの確率（装備）", qualities, y, width)
                + SectionGap;
            y = Entries(content, data, y, width);
            content.sizeDelta = new Vector2(0f, y + 32f);
            rates.Scroll = Layout.Scroll(viewport, viewport, content);
            window.gameObject.SetActive(false);
            return rates;
        }

        // 見出しと、横に並べた割合の札。札は押せない。返すのは札の下端。
        private static float Section(
            RectTransform content,
            string name,
            string title,
            IReadOnlyList<(string Label, int Permille, Color Color)> rates,
            float y,
            float width
        )
        {
            Frame.Heading(content, name + "Title", title, Pad, y, width, HeadingHeight, 34f);
            float top = y + HeadingHeight;
            float tile = (width - TileGap * (rates.Count - 1)) / rates.Count;
            for (int i = 0; i < rates.Count; i++)
            {
                var (label, permille, color) = rates[i];
                var box = Layout.Box(
                    content,
                    name + i,
                    Pad + i * (tile + TileGap),
                    top,
                    tile,
                    TileHeight
                );
                var inside = GlassCardAssets.Plaque(box, pattern: false);
                var labelText = Label(
                    inside,
                    "Label",
                    label,
                    32,
                    color,
                    TextAlignmentOptions.Center
                );
                Band((RectTransform)labelText.transform, top: true, 14f, 46f, 8f, 8f);
                Shrink(labelText, 20);
                var rate = Label(
                    inside,
                    "Rate",
                    TempleSummon.PercentText(permille),
                    48,
                    UiPalette.TextMain,
                    TextAlignmentOptions.Center
                );
                Band((RectTransform)rate.transform, top: false, 14f, 62f, 8f, 8f);
            }
            return top + TileHeight;
        }

        // 出る物の一覧：種類ごとに見出しと、名前と☆の札を並べる（召喚と同じ一覧）。
        private static float Entries(RectTransform content, LibraryData data, float y, float width)
        {
            Frame.Heading(content, "EntriesTitle", "出るもの", Pad, y, width, HeadingHeight, 34f);
            y += HeadingHeight;
            var pool = TempleSummonResults.PoolOf(data);
            float cell = (width - EntryGap * (EntryColumns - 1)) / EntryColumns;
            foreach (var (kind, name, _) in TempleSummon.Categories)
            {
                var entries = pool.Where(entry => entry.Kind == kind)
                    .OrderByDescending(entry => entry.Stars)
                    .ToList();
                if (entries.Count == 0)
                    continue;
                Layout.Text(
                    content,
                    "Kind" + kind,
                    $"{name}（{entries.Count}）",
                    30,
                    UiPalette.TextSub,
                    TextAlignmentOptions.MidlineLeft,
                    Pad,
                    y,
                    width,
                    48f
                );
                y += 52f;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var shown =
                        data != null
                            ? System.Array.Find(
                                data.Entries,
                                e => e.Kind == entry.Kind && e.Id == entry.Id
                            )
                            : null;
                    var box = Layout.Box(
                        content,
                        $"{kind}_{entry.Id}",
                        Pad + (i % EntryColumns) * (cell + EntryGap),
                        y + (i / EntryColumns) * (EntryHeight + EntryGap),
                        cell,
                        EntryHeight
                    );
                    var inside = GlassCardAssets.Plaque(box, pattern: false);
                    string stars =
                        entry.Stars > 0
                            ? $" <color=#{ColorUtility.ToHtmlStringRGB(UiPalette.Gold)}>{new string('★', entry.Stars)}</color>"
                            : "";
                    var text = Label(
                        inside,
                        "Name",
                        (shown != null ? shown.Name : entry.Id) + stars,
                        26,
                        UiPalette.TextMain,
                        TextAlignmentOptions.Center
                    );
                    text.richText = true;
                    Stretch((RectTransform)text.transform);
                    Shrink(text, 16);
                }
                int rows = (entries.Count + EntryColumns - 1) / EntryColumns;
                y += rows * (EntryHeight + EntryGap) + 20f;
            }
            return y;
        }

        // 結果の窓（閉じた状態）。
        private static TempleSummonResults BuildResults(RectTransform parent)
        {
            var overlay = Rect("SummonResults", parent);
            Stretch(overlay);
            var results = overlay.gameObject.AddComponent<TempleSummonResults>();
            results.Data = AssetDatabase.LoadAssetAtPath<LibraryData>(LibraryScreenAssets.DataPath);
            results.ArtBox = CardArtBox;

            var window = Rect("Window", overlay);
            Stretch(window);
            results.Window = window.gameObject;
            // 幕は窓の親にしない。窓の中を押しても、幕のボタンへ伝わらないようにする。
            var veil = Rect("Veil", window);
            Stretch(veil);
            results.Veil = Baryonyx.Editor.UI.UiBuild.AddButton(veil, AddImage(veil, Veil, true));
            results.Veil.transition = Selectable.Transition.None;

            var layer = Layout.Layer(SafeArea(window), "Layout");
            var modal = Frame.Window(
                layer,
                "Modal",
                (Layout.Design.x - WindowWidth) / 2f,
                (Layout.Design.y - WindowHeight) / 2f,
                WindowWidth,
                WindowHeight
            );
            results.Title = Frame.Heading(modal, "Title", "", Pad, 16f, 900f, 96f, 44f);
            var close = Layout.Box(
                modal,
                "Close",
                WindowWidth - Layout.HitSize - 8f,
                4f,
                Layout.HitSize,
                Layout.HitSize
            );
            results.Close = Baryonyx.Editor.UI.UiBuild.AddButton(
                close,
                AddImage(close, Color.clear, true)
            );
            var cross = Rect("Icon", close);
            Place(cross, Vector2.zero, Vector2.one * 64f);
            SpriteImage(cross, IconClosePath, Color.white);
            var line = Layout.Box(
                modal,
                "HeadLine",
                Pad,
                HeadHeight - 4f,
                WindowWidth - Pad * 2f,
                2f
            );
            AddImage(line, Frame.LineColor, false);

            var grid = Layout.Box(
                modal,
                "Cards",
                Pad,
                HeadHeight + 24f,
                WindowWidth - Pad * 2f,
                Card.y * 2f + CardGap
            );
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = Card;
            layout.spacing = Vector2.one * CardGap;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            // 1回の結果は、札の並ぶ範囲の真ん中に置く。
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            var cards = new TempleSummonCard[TempleSummon.TenCount];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = BuildCard(grid, "Card" + i);
            results.Cards = cards;

            var (again, _, inside) = GlassCardAssets.Choice(
                Layout.Box(
                    modal,
                    "Again",
                    WindowWidth - Pad - ConfirmWidth,
                    WindowHeight - 24f - ConfirmHeight,
                    ConfirmWidth,
                    ConfirmHeight
                ),
                pattern: false
            );
            var label = Label(
                inside,
                "Label",
                "もう一度",
                44,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch((RectTransform)label.transform);
            results.Again = again;

            // 停止中もPrefabを開けば札が読めるよう、決まった種で引いた10回の結果を焼いておき、窓を閉じる。
            results.ShowPreviewState(1);
            window.gameObject.SetActive(false);
            return results;
        }

        // 結果の札：右上に品質の文字、上に絵、下に名前と、☆と分類の行。
        internal static TempleSummonCard BuildCard(
            RectTransform grid,
            string name,
            Color? fill = null
        )
        {
            var rect = Rect(name, grid);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false, fill: fill);
            var art = Rect("Art", inside);
            Place(
                art,
                new Vector2(0f, Card.y / 2f - 28f - CardArtBox / 2f),
                Vector2.one * CardArtBox
            );
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var quality = Label(
                inside,
                "Quality",
                "",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.TopRight
            );
            Band((RectTransform)quality.transform, top: true, 10f, 48f, 12f, 16f);
            quality.richText = true;
            var title = Label(
                inside,
                "Name",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Band((RectTransform)title.transform, top: false, 60f, 44f, 12f, 12f);
            Shrink(title, 18);
            var stars = Label(
                inside,
                "Stars",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Band((RectTransform)stars.transform, top: false, 18f, 36f, 8f, 8f);
            stars.richText = true;
            Shrink(stars, 16);
            return new TempleSummonCard
            {
                Card = card,
                Art = image,
                Name = title,
                Stars = stars,
                Quality = quality,
            };
        }
    }
}
