using System.Collections.Generic;
using System.Linq;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.Adventure.Editor
{
    /// <summary>
    /// Builds the end of an adventure (<see cref="AdventureResultPanel"/>): a veil over the
    /// screen and the dialog's window, with the title and the place in a line at the top (a few
    /// soft streaks of gold and twinkles behind them), the party's battle figures in a row on a
    /// band of light, the five marks of the times cleared with a chest after the fifth, two rows
    /// of cards of what was brought back that scroll when there are more, and "ホームへ" at the
    /// bottom. The battle's overlay and the exploration screen both have it. Coordinates follow
    /// the 1920x1080 design, from the window's centre.
    /// </summary>
    public static class AdventureResultAssets
    {
        private static readonly Color Veil = new(0.012f, 0.02f, 0.04f, 0.72f);
        private static readonly Vector2 WindowSize = new(1440, 820);

        // 見出しの行。
        private const float HeaderY = 320f;
        private const float TitleSize = 88f;
        private const float PlaceSize = 40f;

        // 仲間の足元の高さと、横の間隔。ドット1つを3pxで描く（64ドットで192px）。
        private const float FeetY = 56f;
        private const float MemberGap = 220f;
        private const float Dot = 3f;
        private static readonly string[] SampleParty = { "Toma", "Luka", "Aria", "Mina" };
        private const string CharacterArtFolder = "Assets/Baryonyx/Shared/Art/Characters";

        // 持ち帰った物の札（6列、見えるのは2段）。
        private const int Columns = 6;
        private static readonly Vector2 CellSize = new(196, 122);
        private const float CellGap = 16f;
        private const float GridY = -140f;
        private const float ArtSize = 80f;

        // ホームへ。
        private const float HomeY = -336f;
        private static readonly Vector2 HomeSize = new(900, 96);

        private static readonly Color RayColor = new(1f, 0.86f, 0.45f, 0.5f);
        private static readonly Color GlowColor = new(1f, 0.82f, 0.4f, 0.22f);
        private static readonly Color TwinkleColor = new(1f, 0.95f, 0.75f, 1f);

        public static AdventureResultPanel Build(RectTransform parent)
        {
            var root = Rect("Result", parent);
            Stretch(root);
            var panel = root.gameObject.AddComponent<AdventureResultPanel>();
            panel.Panel = root;
            panel.Dot = Dot;
            AddImage(root, Veil, true);
            var safe = SafeArea(root);

            var window = Rect("Window", safe);
            Place(window, Vector2.zero, WindowSize);
            GuideFrameAssets.WindowFrame(window);
            panel.Window = window.gameObject.AddComponent<CanvasGroup>();

            BuildHeader(window, panel);
            BuildParty(window, panel);
            BuildGrid(window, panel);
            BuildHome(window, panel);

            root.gameObject.SetActive(false);
            return panel;
        }

        // 見出しと場所を1行に並べ、後ろにやわらかい金の光と、細い光の筋、きらめきを置く。
        private static void BuildHeader(RectTransform window, AdventureResultPanel panel)
        {
            var glow = Rect("Glow", window);
            Place(glow, new Vector2(-60, HeaderY), new Vector2(760, 170));
            var rays = new Graphic[] { SpriteImage(glow, UiArt.SoftSpotPath, GlowColor) }.ToList();
            foreach (var (length, width, angle, y) in new[]
            {
                (820f, 8f, 5f, 4f),
                (640f, 5f, -4f, -10f),
                (720f, 6f, 13f, 14f),
                (560f, 4f, -12f, -2f),
            })
            {
                var ray = Rect("Ray", window);
                Place(ray, new Vector2(-60, HeaderY + y), new Vector2(length, width));
                ray.localRotation = Quaternion.Euler(0, 0, angle);
                rays.Add(SpriteImage(ray, UiArt.SoftSpotPath, RayColor));
            }
            panel.Rays = rays.ToArray();

            var twinkles = new List<CanvasGroup>();
            foreach (var (at, size) in new[]
            {
                (new Vector2(-420, HeaderY + 40), 40f),
                (new Vector2(-330, HeaderY - 48), 26f),
                (new Vector2(-120, HeaderY + 70), 22f),
                (new Vector2(330, HeaderY + 44), 34f),
                (new Vector2(440, HeaderY - 30), 24f),
            })
                twinkles.Add(Twinkle(window, at, size));
            panel.Twinkles = twinkles.ToArray();

            var header = Rect("Header", window);
            Place(header, new Vector2(0, HeaderY), new Vector2(1300, 120));
            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 32f;
            row.childAlignment = TextAnchor.LowerCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            panel.Title = Label(header, "Title", "クリア！", TitleSize, UiPalette.GoldLight, TextAlignmentOptions.Bottom);
            panel.Place = Label(header, "Place", "", PlaceSize, UiPalette.TextMain, TextAlignmentOptions.Bottom);
            // 大きな見出しと小さな場所の文字の下の線をそろえる。
            panel.Place.margin = new Vector4(0, 0, 0, 14);
        }

        // 十字の細い光。2本の線を重ね、まとめて瞬かせる。
        private static CanvasGroup Twinkle(RectTransform parent, Vector2 at, float size)
        {
            var rect = Rect("Twinkle", parent);
            Place(rect, at, Vector2.one * size);
            foreach (var (w, h) in new[] { (size, size * 0.16f), (size * 0.16f, size) })
            {
                var line = Rect("Line", rect);
                Place(line, Vector2.zero, new Vector2(w, h));
                SpriteImage(line, UiArt.SoftSpotPath, TwinkleColor);
            }
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false;
            return group;
        }

        // 足元の光の帯の上に、仲間の戦闘のドット絵を横一列に立てる。見本は枠ごとの仲間を入れておく。
        private static void BuildParty(RectTransform window, AdventureResultPanel panel)
        {
            var floor = Rect("Floor", window);
            Place(floor, new Vector2(0, FeetY + 8f), new Vector2(1120, 96));
            panel.Floor = SpriteImage(floor, UiArt.SoftSpotPath, panel.FloorCleared);

            var members = new RawImage[SampleParty.Length];
            for (int i = 0; i < members.Length; i++)
            {
                var rect = Rect($"Member{i + 1}", window);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2((i - (members.Length - 1) / 2f) * MemberGap, FeetY - 14f);
                var texture = ArtAssets.LoadTexture($"{CharacterArtFolder}/Battle{SampleParty[i]}.aseprite");
                rect.sizeDelta = new Vector2(texture.width, texture.height) * Dot;
                var image = rect.gameObject.AddComponent<RawImage>();
                image.texture = texture;
                image.raycastTarget = false;
                members[i] = image;
            }
            panel.Members = members;
        }

        // 持ち帰った物の札の格子。2段より多ければ縦に送る。札は1枚を複製して増やす。
        private static void BuildGrid(RectTransform window, AdventureResultPanel panel)
        {
            float width = Columns * CellSize.x + (Columns - 1) * CellGap;
            float height = 2 * CellSize.y + CellGap;
            var viewport = Rect("Things", window);
            Place(viewport, new Vector2(0, GridY), new Vector2(width + 8f, height + 8f));
            viewport.gameObject.AddComponent<RectMask2D>();
            AddImage(viewport, Color.clear, true);

            var content = Rect("Cells", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = CellSize;
            grid.spacing = Vector2.one * CellGap;
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            panel.Scroll = scroll;
            panel.Cells = content;
            panel.CellTemplate = Cell(content);

            panel.Empty = Label(window, "Empty", AdventureTexts.NothingBroughtBack, 36, UiPalette.TextSub, TextAlignmentOptions.Center);
            Place((RectTransform)panel.Empty.transform, new Vector2(0, GridY), new Vector2(width, 60));
            panel.Empty.gameObject.SetActive(false);
        }

        // 札：左に絵、右に品質の文字と☆と数、下に名前。
        private static AdventureResultCell Cell(RectTransform content)
        {
            var rect = Rect("CellTemplate", content);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false, fill: GlassCardAssets.SceneFillColor);
            var cell = rect.gameObject.AddComponent<AdventureResultCell>();
            cell.Group = card.Group;

            var art = Rect("Art", inside);
            Place(art, new Vector2(-CellSize.x / 2f + 14f + ArtSize / 2f, 12f), Vector2.one * ArtSize);
            cell.Art = art.gameObject.AddComponent<Image>();
            cell.Art.preserveAspect = true;
            cell.Art.raycastTarget = false;

            cell.Quality = Label(inside, "Quality", "B", 36, UiPalette.TextMain, TextAlignmentOptions.Left);
            Place((RectTransform)cell.Quality.transform, new Vector2(32f, 28f), new Vector2(52, 44));
            cell.Count = Label(inside, "Count", "×1", 24, UiPalette.TextMain, TextAlignmentOptions.Right);
            Place((RectTransform)cell.Count.transform, new Vector2(70f, 26f), new Vector2(40, 34));
            cell.Stars = Label(inside, "Stars", "★", 20, UiPalette.GoldLight, TextAlignmentOptions.Left);
            Place((RectTransform)cell.Stars.transform, new Vector2(46f, -6f), new Vector2(84, 28));
            GuideMenuAssets.Shrink(cell.Stars, 12);
            cell.Name = Label(inside, "Name", "", 22, UiPalette.TextSub, TextAlignmentOptions.Center);
            Place((RectTransform)cell.Name.transform, new Vector2(0f, -42f), new Vector2(CellSize.x - 16f, 30f));
            GuideMenuAssets.Shrink(cell.Name, 14);

            rect.gameObject.SetActive(false);
            return cell;
        }

        // ダイアログの選択肢と同じ、すりガラスのカードのボタン。
        private static void BuildHome(RectTransform window, AdventureResultPanel panel)
        {
            var row = Rect("Home", window);
            Place(row, new Vector2(0, HomeY), HomeSize);
            var (button, _, card) = GlassCardAssets.Choice(row, pattern: false);
            var label = Label(card, "Label", AdventureTexts.HomeChoice, 44, UiPalette.TextMain, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform);
            panel.Home = button;
        }
    }
}
