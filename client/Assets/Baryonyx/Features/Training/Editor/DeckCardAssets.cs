using Baryonyx.Combat.Editor;
using Baryonyx.Editor.Art;
using Baryonyx.Editor.UI;
using Baryonyx.UI;
using Baryonyx.UI.GuideMenu.Editor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;
using Guide = Baryonyx.UI.GuideMenu.Editor.GuideMenuAssets;
using Layout = Baryonyx.UI.GuideMenu.Editor.GuidePanelLayout;

namespace Baryonyx.Training.Editor
{
    /// <summary>
    /// A deck card laid out like the battle's (BattleInspectAssets), at any whole number of pixels
    /// per dot: the frame, the art over its top, the cost and element on the top corners, the
    /// type, name and kind on a dark band, the effect below, and a veil with the level for a unique
    /// skill not unlocked yet. A small card leaves the effect out: it shows the frame down to the
    /// band and then the frame's bottom edge, so it is only as tall as it is wide. The adventurer's
    /// page, the card skills change screen and its deck view build their cards with it, and
    /// <see cref="TrainingView.ShowCard"/> fills them.
    /// </summary>
    public static class DeckCardAssets
    {
        // 戦闘のカードの大きさ（ドット）。小さなカードは帯の下までと、枠の下端だけを出す。
        public static readonly Vector2 Dots = new(70, 98);
        public static readonly Vector2 SmallDots = new(70, TopRows + EdgeRows);

        // 小さなカードに出す、枠の上からの行数（挿絵と効果の間の点線の手前まで）と、下端の行数。
        private const int TopRows = 62;
        private const int EdgeRows = 4;

        // 文字の大きさは3倍のときの値で、倍率に合わせて大きさを変える。
        private const float BaseDot = 3f;

        private static readonly Color CardBand = new(0.02f, 0.03f, 0.07f, 0.68f);
        private static readonly Color CardOwner = new(1f, 0.93f, 0.76f);
        private static readonly Color CardVeil = new(0.01f, 0.015f, 0.04f, 0.72f);

        /// <summary>The size in pixels of a card at <paramref name="dot"/> pixels per dot.</summary>
        public static Vector2 Size(float dot, bool small) => (small ? SmallDots : Dots) * dot;

        /// <summary>
        /// Builds a card at (<paramref name="x"/>, <paramref name="y"/>) from the top left of
        /// <paramref name="parent"/>, <paramref name="dot"/> pixels per dot. A
        /// <paramref name="button"/> card takes taps on its frame.
        /// </summary>
        public static TrainingCardWidget Build(
            RectTransform parent,
            string name,
            float x,
            float y,
            float dot,
            bool button,
            bool small = false
        )
        {
            var size = Size(dot, small);
            var card = Layout.Box(parent, name, x, y, size.x, size.y);
            var widget = new TrainingCardWidget();
            if (small)
            {
                widget.Frame = Image(card, "Frame", 0, 0, new Vector2(Dots.x, TopRows), dot, null);
                widget.Frame.uvRect = new Rect(0, 1f - TopRows / Dots.y, 1, TopRows / Dots.y);
                widget.FrameEdge = Image(
                    card,
                    "FrameEdge",
                    0,
                    TopRows,
                    new Vector2(Dots.x, EdgeRows),
                    dot,
                    null
                );
                widget.FrameEdge.uvRect = new Rect(0, 0, 1, EdgeRows / Dots.y);
            }
            else
                widget.Frame = Image(card, "Frame", 0, 0, Dots, dot, null);
            widget.Frame.raycastTarget = button;
            widget.FrameFoil = CardFoilAssets.AddFrame(widget.Frame);
            if (button)
                widget.Button = Guide.AddButton(card, widget.Frame);
            widget.Art = Image(card, "Art", 3, 3, new Vector2(64, 58), dot, null);
            widget.Foil = CardFoilAssets.Add(widget.Art);

            float text = dot / BaseDot;
            var band = Layout.Box(
                card,
                "Band",
                3 * dot,
                39 * dot,
                64 * dot,
                (small ? TopRows - 39 : 26) * dot
            );
            AddImage(band, CardBand, false);
            widget.Band = band.gameObject;
            if (small)
                BuildSmallText(card, widget, dot);
            else
            {
                widget.Type = Label(
                    card,
                    "Type",
                    "",
                    15 * text,
                    CardOwner,
                    TextAlignmentOptions.MidlineLeft
                );
                Layout.At(widget.Type, 6 * dot, 42.4f * dot, 58 * dot, 6.4f * dot);
                widget.Name = Label(
                    card,
                    "Name",
                    "",
                    22 * text,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft
                );
                Layout.At(widget.Name, 6 * dot, 48.6f * dot, 58 * dot, 8.4f * dot);
                Guide.Shrink(widget.Name, 15 * text);
                widget.Kind = Label(
                    card,
                    "Kind",
                    "",
                    15 * text,
                    UiPalette.TextMain,
                    TextAlignmentOptions.MidlineLeft
                );
                Layout.At(widget.Kind, 7 * dot, 56.8f * dot, 56 * dot, 6 * dot);
                widget.Kind.richText = true;
            }
            if (!small)
            {
                widget.Description = Label(
                    card,
                    "Description",
                    "",
                    15 * text,
                    UiPalette.TextSub,
                    TextAlignmentOptions.TopLeft,
                    shadow: false
                );
                Layout.At(widget.Description, 7.5f * dot, 66.6f * dot, 55 * dot, 26 * dot);
                widget.Description.richText = true;
                widget.Description.textWrappingMode = TextWrappingModes.Normal;
            }

            var costFrame = CardArt("CardCostFrame");
            var costDigits = CardArt("CardCostDigits");
            var cost = Image(
                card,
                "Cost",
                5,
                5,
                new Vector2(costFrame.width, costFrame.height),
                dot,
                costFrame
            );
            widget.Cost = cost.gameObject;
            var digit = Rect("Digit", cost.rectTransform);
            Place(
                digit,
                Vector2.zero,
                new Vector2(costDigits.width / 10f, costDigits.height) * dot
            );
            widget.CostDigit = digit.gameObject.AddComponent<RawImage>();
            widget.CostDigit.texture = costDigits;
            widget.CostDigit.raycastTarget = false;
            var element = Layout.Box(
                card,
                "Element",
                (70 - 5 - 12) * dot,
                5 * dot,
                12 * dot,
                12 * dot
            );
            widget.Element = element.gameObject.AddComponent<Image>();
            widget.Element.raycastTarget = false;

            // 未解放の固有スキル：カード全体を暗くして、錠前と解放するLvを重ねる。
            // 2倍の小さなカードでも読めるよう、錠前と文字は倍率ほどには小さくしない。
            text = Mathf.Max(text, 0.9f);
            var locked = Rect("Lock", card);
            Stretch(locked);
            AddImage(locked, CardVeil, false);
            var lockSize = new Vector2(9, 10) * 4 * text;
            float whenHeight = 30 * text;
            // 大きなカードは挿絵の中ほどに、小さなカードは上下の真ん中に置く。
            float lockTop = small ? (size.y - lockSize.y - whenHeight) / 2f : 54 * dot / BaseDot;
            var lockImage = Layout.Box(
                locked,
                "Image",
                (size.x - lockSize.x) / 2f,
                lockTop,
                lockSize.x,
                lockSize.y
            );
            SpriteImage(lockImage, GuideFrameAssets.LockPath, Color.white);
            widget.When = Label(
                locked,
                "When",
                "",
                22 * text,
                UiPalette.Gold,
                TextAlignmentOptions.Center
            );
            Layout.At(widget.When, 0, lockTop + lockSize.y + 6 * text, size.x, whenHeight);
            Guide.Shrink(widget.When, 12);
            widget.Lock = locked.gameObject;
            locked.gameObject.SetActive(false);
            return widget;
        }

        /// <summary>
        /// The small card's band: its name alone in the type's place, read at a glance (the
        /// heading over the cards tells unique skills from custom ones), and at 3x and up the
        /// kind line below it. The name keeps a size readable on a phone even at 2x.
        /// </summary>
        private static void BuildSmallText(RectTransform card, TrainingCardWidget widget, float dot)
        {
            bool kind = dot >= BaseDot;
            widget.Name = Label(
                card,
                "Name",
                "",
                Mathf.Max(22 * dot / BaseDot, 20),
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            if (kind)
                Layout.At(widget.Name, 6 * dot, 40.5f * dot, 58 * dot, 10 * dot);
            else
                Layout.At(widget.Name, 5 * dot, 39 * dot, 60 * dot, (TopRows - 39) * dot);
            Guide.Shrink(widget.Name, 14);
            if (!kind)
                return;
            widget.Kind = Label(
                card,
                "Kind",
                "",
                15 * dot / BaseDot,
                UiPalette.TextMain,
                TextAlignmentOptions.MidlineLeft
            );
            Layout.At(widget.Kind, 7 * dot, 51.5f * dot, 56 * dot, 7 * dot);
            widget.Kind.richText = true;
        }

        // ドット絵の画像を、左上からのドットの位置と大きさで dot 倍に置く。
        private static RawImage Image(
            RectTransform parent,
            string name,
            float x,
            float y,
            Vector2 size,
            float dot,
            Texture texture
        )
        {
            var rect = Layout.Box(parent, name, x * dot, y * dot, size.x * dot, size.y * dot);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A battle card's picture (frame, cost), borrowed from the battle mock like the skills' art.</summary>
        public static Texture2D CardArt(string name) =>
            ArtAssets.LoadTexture($"{BattleInspectAssets.ArtFolder}/{name}.aseprite");
    }
}
