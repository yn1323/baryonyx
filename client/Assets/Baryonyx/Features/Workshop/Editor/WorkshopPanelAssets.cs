using System;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Party.Editor;
using Baryonyx.Training.Editor;
using Baryonyx.UI;
using Baryonyx.UI.Editor;
using Baryonyx.UI.GuideMenu;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using static Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Workshop.Editor
{
    /// <summary>
    /// The parts the trading house's full-screen panels share (buy, sell, craft and enhance) inside
    /// the shared templates (<see cref="GuidePanelTemplate"/>): the scrolling column of chosen
    /// rows, the cards and rows, the big art on the right with a soft light behind it, and a price
    /// with the rune icon. Positions are design pixels
    /// from the top left of the 1920x1080 layer, as in the mockups.
    /// </summary>
    public static class WorkshopPanelAssets
    {
        // 一覧の面と、選んだ物を出す右の面の境。どの画面も右の面を同じ幅にする。
        public const float Split = GuidePanelTemplate.Split;

        // 面の上の見出し（売る物・覚醒できる固有スキル）と、その下の中身の始まり。
        public const float TabTop = GuideFrameAssets.HeaderHeight;
        public const float TabHeight = GuidePanelTemplate.HeadingHeight;
        public const float BodyTop = TabTop + TabHeight + 8f;

        // 面の端と中身の間。左右の面とも、画面の端の側は枠と同じ48、区切りの線の側は24空ける。
        public const float Edge = GuideFrameAssets.Margin;
        public const float Inner = ListGap;

        // 下端の決めるボタンの上端。右の面の中身はこれより上に置く。
        public const float ConfirmTop = 1080f - ConfirmBottom - ConfirmHeight;

        // 一覧の行・カードの間。
        public const float Gap = 22f;

        private static readonly Color Glow = new(1f, 0.82f, 0.5f, 0.14f);

        /// <summary>A scrolling column of rows between <paramref name="left"/> and <paramref name="right"/>.</summary>
        public static (RectTransform Content, ScrollRect Scroll) Column(
            RectTransform layer,
            string name,
            float left,
            float right,
            float top,
            float bottom,
            int padLeft,
            int padRight
        )
        {
            var viewport = Layout.Box(
                layer,
                name + "Viewport",
                left,
                top,
                right - left,
                bottom - top
            );
            Layout.Mask(viewport);
            var content = Layout.Column(
                viewport,
                name,
                Gap * 0.6f,
                new RectOffset(padLeft, padRight, 14, (int)(Gap * 2f))
            );
            return (content, Layout.Scroll(viewport, viewport, content));
        }

        /// <summary>
        /// A good's card for the buy and sell grids: the quality's letter at the top left, the art
        /// in the middle of the top, the name, the stars, and the price with the rune icon. With
        /// <paramref name="held"/> it also has the held thing's marks (<see cref="HeldMarks"/>),
        /// the count at the art's bottom right.
        /// </summary>
        public static ItemTile GoodCard(
            RectTransform content,
            string name,
            Vector2 cell,
            float artBox,
            bool held = false
        )
        {
            var (row, inside) = ItemTileAssets.Build(Rect(name, content));
            row.Art = ArtBox(inside, (cell.x - artBox) / 2f, 18f, artBox);
            float y = 18f + artBox + 6f;
            row.Name = Layout.Text(
                inside,
                "Name",
                "",
                30,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                8f,
                y,
                cell.x - 16f,
                40f
            );
            Shrink(row.Name, 20);
            row.Detail = Layout.Text(
                inside,
                "Stars",
                "",
                24,
                UiPalette.Gold,
                TextAlignmentOptions.Center,
                8f,
                y + 38f,
                cell.x - 16f,
                28f
            );
            row.Price = RunePrice(
                inside,
                "Price",
                0f,
                y + 66f,
                cell.x,
                40f,
                32f,
                TextAnchor.MiddleCenter
            );
            ItemTileAssets.QualityMark(row, inside);
            if (held)
                HeldMarks(inside, row, cell, 18f + artBox);
            return row;
        }

        /// <summary>
        /// A craft material's tile at (x, y) on <paramref name="parent"/>: a square card with the
        /// art in the middle, the chosen piece's quality at the top left, the held and needed
        /// counts at the bottom right, and the name under the card.
        /// </summary>
        public static ItemTile MaterialTile(
            RectTransform parent,
            string name,
            float x,
            float y,
            float size,
            float artBox
        )
        {
            var tile = Layout.Box(parent, name, x, y, size, size + TileNameHeight);
            var row = tile.gameObject.AddComponent<ItemTile>();
            var face = Layout.Box(tile, "Card", 0f, 0f, size, size);
            var (card, inside) = GlassCardAssets.Build(face, pattern: false);
            row.Card = card;
            row.Art = ArtBox(inside, (size - artBox) / 2f, (size - artBox) / 2f - 6f, artBox);
            ItemTileAssets.QualityMark(row, inside);
            HeldMarks(inside, row, new Vector2(size, size), size - 8f, UiType.Small);
            row.Name = Layout.Text(
                tile,
                "Name",
                "",
                24,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                -8f,
                size + 4f,
                size + 16f,
                TileNameHeight - 4f
            );
            Shrink(row.Name, 16);
            return row;
        }

        // 材料の札の下の名前の行の高さ。
        public const float TileNameHeight = 44f;

        /// <summary>
        /// A held piece's card for the craft's picker: the quality's letter at the top left, the
        /// art, the multiplier and the effect at it, painted opaque; a piece in use is shaded
        /// with an "E" at the top right.
        /// </summary>
        public static ItemTile PieceCard(
            RectTransform content,
            string name,
            Vector2 cell,
            float artBox
        )
        {
            var (row, inside) = ItemTileAssets.Build(Rect(name, content), fill: PieceFill);
            row.Art = ArtBox(inside, (cell.x - artBox) / 2f, 24f, artBox);
            ItemTileAssets.QualityMark(row, inside);
            row.Price = Layout.Text(
                inside,
                "Multiplier",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.Center,
                0f,
                130f,
                cell.x,
                44f
            );
            row.Detail = Layout.Text(
                inside,
                "Effect",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.Center,
                8f,
                176f,
                cell.x - 16f,
                40f
            );
            Shrink(row.Detail, 18);

            // 使っている物：札ごと影を重ね、右上に「E」を出す。
            var used = Rect("InUse", inside);
            Stretch(used);
            AddImage(used, new Color(0.01f, 0.01f, 0.04f, 0.55f), false);
            ItemTileAssets
                .CornerTag(row, used, cell.x, "E", UiPalette.TextSub, plate: true, "Tag")
                .SetActive(true);
            used.gameObject.SetActive(false);
            row.Mark = used.gameObject;
            return row;
        }

        // 使う物を選ぶ画面の札の、透けない地の色。
        private static readonly Color PieceFill = new(0.09f, 0.11f, 0.22f, 1f);

        /// <summary>
        /// A held thing's marks on its card: the count with its bottom right at (cell's right
        /// edge, <paramref name="countBottom"/>), and an "E" tag at the top right while someone
        /// uses it.
        /// </summary>
        private static void HeldMarks(
            RectTransform inside,
            ItemTile row,
            Vector2 cell,
            float countBottom,
            float countSize = UiType.Caption
        )
        {
            ItemTileAssets.CountMark(row, inside, cell.x - 14f, countBottom, countSize);
            ItemTileAssets.CornerTag(
                row,
                inside,
                cell.x,
                "E",
                UiPalette.TextSub,
                plate: true,
                "InUse"
            );
        }

        /// <summary>
        /// Puts the rune icon before a row's value (<see cref="ListRow"/>): the value becomes a
        /// price <paramref name="valueWidth"/> wide at the row's right end.
        /// </summary>
        public static void RuneValue(ItemTile row, float valueWidth, float size)
        {
            var inside = (RectTransform)row.Price.transform.parent;
            UnityEngine.Object.DestroyImmediate(row.Price.gameObject);
            row.Price = RunePrice(
                inside,
                "Value",
                0f,
                0f,
                valueWidth,
                0f,
                size,
                TextAnchor.MiddleRight
            );
            var price = (RectTransform)row.Price.transform.parent;
            price.anchorMin = new Vector2(1f, 0f);
            price.anchorMax = Vector2.one;
            price.pivot = new Vector2(1f, 0.5f);
            price.offsetMin = new Vector2(-24f - valueWidth, 0f);
            price.offsetMax = new Vector2(-24f, 0f);
        }

        /// <summary>
        /// A row in a list (the craft's recipes, the enhance targets, the things to sell): the art
        /// on the left, the name, a line under it, and a text <paramref name="valueWidth"/> wide at
        /// the right end, which the name and the line stop short of.
        /// </summary>
        public static ItemTile ListRow(
            RectTransform content,
            string name,
            float height,
            float artBox,
            float nameSize = 40f,
            float valueWidth = 0f
        )
        {
            var rect = Layout.ListItem(content, name, height);
            var (card, inside) = GlassCardAssets.Build(rect, pattern: false);
            var row = rect.gameObject.AddComponent<ItemTile>();
            row.Card = card;
            var box = Rect("ArtBox", inside);
            Corner(box, new Vector2(0, 0.5f), new Vector2(20f, 0), Vector2.one * artBox);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * artBox);
            row.Art = art.gameObject.AddComponent<RawImage>();
            row.Art.raycastTarget = false;

            float textLeft = 20f + artBox + 20f;
            float half = height / 2f;
            row.Name = Label(
                inside,
                "Name",
                "",
                nameSize,
                UiPalette.TextMain,
                TextAlignmentOptions.BottomLeft
            );
            float textRight = valueWidth > 0f ? valueWidth + 40f : 24f;
            Band((RectTransform)row.Name.transform, top: true, 4f, half - 6f, textLeft, textRight);
            Shrink(row.Name, 24);
            row.Detail = Label(
                inside,
                "Detail",
                "",
                26,
                UiPalette.TextSub,
                TextAlignmentOptions.TopLeft
            );
            row.Detail.richText = true;
            Band(
                (RectTransform)row.Detail.transform,
                top: false,
                6f,
                half - 10f,
                textLeft + 2f,
                textRight
            );
            Shrink(row.Detail, 18);
            row.Price = Label(
                inside,
                "Value",
                "",
                32,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineRight
            );
            row.Price.richText = true;
            var value = (RectTransform)row.Price.transform;
            value.anchorMin = new Vector2(1f, 0);
            value.anchorMax = Vector2.one;
            value.offsetMin = new Vector2(-24f - valueWidth, 0);
            value.offsetMax = new Vector2(-24f, 0);
            Shrink(row.Price, 22);
            return row;
        }

        /// <summary>
        /// The chosen thing's big art on the right, with a soft warm light behind it, hidden with
        /// its light until something is chosen.
        /// </summary>
        public static RawImage ArtStage(
            RectTransform layer,
            string name,
            float x,
            float y,
            float box
        )
        {
            var stage = Layout.Box(layer, name, x, y, box, box);
            var glow = Rect("Glow", stage);
            Place(glow, Vector2.zero, Vector2.one * box * 1.5f);
            SpriteImage(glow, SoftSpotPath, Glow);
            var art = Rect("Art", stage);
            Place(art, Vector2.zero, Vector2.one * box);
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            // 選ぶまでは、光ごと隠しておく。
            stage.gameObject.SetActive(false);
            return image;
        }

        /// <summary>A price: the rune icon and the number after it (<see cref="GuideFrameAssets.RuneAmount"/>).</summary>
        public static TMP_Text RunePrice(
            RectTransform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            float size,
            TextAnchor alignment
        ) => GuideFrameAssets.RuneAmount(parent, name, x, y, width, height, size, alignment);

        // 戦闘のターン終了と同じ丸いチェックの絵。
        private const string CheckFolder = "Assets/Baryonyx/Features/Combat/UI/Art";

        /// <summary>
        /// The battle's round check at the bottom right of a full-screen overlay (the craft's
        /// picker, the enhance's combine prompt), as a button that tints the tick with the face.
        /// </summary>
        public static Button RoundCheck(RectTransform layer)
        {
            var circle = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                CheckFolder + "/EndTurnCircle.png"
            );
            var tickArt = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                CheckFolder + "/EndTurnCheck.png"
            );
            var check = Layout.Box(
                layer,
                "Check",
                Layout.Design.x - 48f - circle.width,
                Layout.Design.y - 40f - circle.height,
                circle.width,
                circle.height
            );
            var face = check.gameObject.AddComponent<RawImage>();
            face.texture = circle;
            var tick = Rect("Tick", check);
            Place(tick, new Vector2(-1f, 0f), new Vector2(tickArt.width, tickArt.height));
            var tickImage = tick.gameObject.AddComponent<RawImage>();
            tickImage.texture = tickArt;
            tickImage.raycastTarget = false;
            var button = (TintGroupButton)AddTintButton(check, face);
            button.SetTintGraphics(new Graphic[] { tickImage });
            return button;
        }

        private static RawImage ArtBox(RectTransform parent, float x, float y, float size)
        {
            var box = Layout.Box(parent, "ArtBox", x, y, size, size);
            var art = Rect("Art", box);
            Place(art, Vector2.zero, Vector2.one * size);
            var image = art.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The deck card's frames, by element (the buy's detail, the enhancement's cards).</summary>
        public static Texture[] DeckCardFrames() =>
            Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element => (Texture)DeckCardAssets.CardArt($"CardFrame{element}"))
                .ToArray();

        /// <summary>The element icons by element, none for no element.</summary>
        public static Sprite[] ElementIcons() =>
            Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element => Icon(AttributeIconArt.IconPath(element)))
                .ToArray();
    }
}
