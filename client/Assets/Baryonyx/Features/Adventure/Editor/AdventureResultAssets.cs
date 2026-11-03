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
        private static readonly Vector2 WindowSize = new(UiSpace.WindowMedium, 820);

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
        private static readonly Vector2 CellSize = new(196, 130);
        private const float CellGap = 16f;
        private const float GridY = -140f;
        private const float ArtSize = 56f;

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
            AddImage(root, UiPalette.Veil, true);
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
            foreach (
                var (length, width, angle, y) in new[]
                {
                    (820f, 8f, 5f, 4f),
                    (640f, 5f, -4f, -10f),
                    (720f, 6f, 13f, 14f),
                    (560f, 4f, -12f, -2f),
                }
            )
            {
                var ray = Rect("Ray", window);
                Place(ray, new Vector2(-60, HeaderY + y), new Vector2(length, width));
                ray.localRotation = Quaternion.Euler(0, 0, angle);
                rays.Add(SpriteImage(ray, UiArt.SoftSpotPath, RayColor));
            }
            panel.Rays = rays.ToArray();

            var twinkles = new List<CanvasGroup>();
            foreach (
                var (at, size) in new[]
                {
                    (new Vector2(-420, HeaderY + 40), 40f),
                    (new Vector2(-330, HeaderY - 48), 26f),
                    (new Vector2(-120, HeaderY + 70), 22f),
                    (new Vector2(330, HeaderY + 44), 34f),
                    (new Vector2(440, HeaderY - 30), 24f),
                }
            )
                twinkles.Add(Twinkle(window, at, size));
            panel.Twinkles = twinkles.ToArray();

            var header = Rect("Header", window);
            Place(header, new Vector2(0, HeaderY), new Vector2(1300, 120));
            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 32f;
            row.childAlignment = TextAnchor.LowerCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            panel.Title = Label(
                header,
                "Title",
                "クリア！",
                TitleSize,
                UiPalette.GoldLight,
                TextAlignmentOptions.Bottom
            );
            panel.Place = Label(
                header,
                "Place",
                "",
                PlaceSize,
                UiPalette.TextMain,
                TextAlignmentOptions.Bottom
            );
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
                rect.anchoredPosition = new Vector2(
                    (i - (members.Length - 1) / 2f) * MemberGap,
                    FeetY - 14f
                );
                var texture = ArtAssets.LoadTexture(
                    $"{CharacterArtFolder}/Battle{SampleParty[i]}.aseprite"
                );
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

            panel.Empty = Label(
                window,
                "Empty",
                AdventureTexts.NothingBroughtBack,
                36,
                UiPalette.TextSub,
                TextAlignmentOptions.Center
            );
            Place(
                (RectTransform)panel.Empty.transform,
                new Vector2(0, GridY),
                new Vector2(width, 60)
            );
            panel.Empty.gameObject.SetActive(false);
        }

        // 札の型（doc/rules/ui-design.md の装備の見せ方）：左上に品質の文字、中央に絵、下に名前→☆、
        // 右下に数。分類の文字と属性は出さない（2026-11-03、ユーザー。それまでは絵を左寄せ、☆を絵の右に置いていた）。
        private static ItemTile Cell(RectTransform content)
        {
            var rect = Rect("CellTemplate", content);
            var (cell, inside) = ItemTileAssets.Build(rect, fill: GlassCardAssets.SceneFillColor);

            var art = Rect("Art", inside);
            Place(art, new Vector2(0f, 28f), Vector2.one * ArtSize);
            cell.Art = art.gameObject.AddComponent<RawImage>();
            cell.Art.raycastTarget = false;

            ItemTileAssets.QualityMark(cell, inside).text = "B";
            cell.Name = Label(
                inside,
                "Name",
                "",
                UiType.Tiny,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Place(
                (RectTransform)cell.Name.transform,
                new Vector2(0f, -16f),
                new Vector2(CellSize.x - 16f, 28f)
            );
            cell.Stars = Label(
                inside,
                "Stars",
                "★",
                UiType.Tiny,
                UiPalette.Gold,
                TextAlignmentOptions.Center
            );
            Place((RectTransform)cell.Stars.transform, new Vector2(0f, -44f), new Vector2(110f, 26f));
            // 数は右下の隅に置く。
            ItemTileAssets.CountMark(cell, inside, CellSize.x - 10f, CellSize.y - 6f).text = "×1";

            rect.gameObject.SetActive(false);
            return cell;
        }

        // 先へ進む「ホームへ」は、決めるボタン（doc/rules/ui-design.md#決めるボタン）。
        private static void BuildHome(RectTransform window, AdventureResultPanel panel)
        {
            var row = Rect("Home", window);
            Place(row, new Vector2(0, HomeY), HomeSize);
            panel.Home = ActionButtonAssets
                .Build(row, AdventureTexts.HomeChoice, ButtonRole.Primary, 44)
                .Button;
        }
    }
}
