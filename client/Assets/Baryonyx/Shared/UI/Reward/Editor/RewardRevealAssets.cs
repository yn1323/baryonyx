using Baryonyx.Editor.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Builds the plate that shows what was found (<see cref="RewardReveal"/>), for the screens
    /// that show a drop or a chest's content. Call it inside a <see cref="UiBuild"/> session.
    /// </summary>
    public static class RewardRevealAssets
    {
        public static readonly Vector2 PlateSize = new(600f, 236f);
        private const float IconSize = 144f;
        private static readonly Color Gold = new(0.98f, 0.8f, 0.36f);

        /// <summary>
        /// The plate over <paramref name="parent"/>, its middle at <paramref name="center"/>: a dark plate whose rim takes the quality's colour, the icon (with its
        /// sparkle) at the left, the kind, the name and what became of it at the right, and the
        /// quality's letter large at the top right.
        /// </summary>
        public static RewardReveal Build(RectTransform parent, Vector2 center)
        {
            var host = Rect("RewardReveal", parent);
            Stretch(host);
            var reveal = host.gameObject.AddComponent<RewardReveal>();

            var plate = Rect("Plate", host);
            Place(plate, center, PlateSize);
            var group = plate.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var content = GlassCardAssets.Plaque(
                plate,
                pattern: false,
                fill: GlassCardAssets.SceneFillColor
            );
            reveal.Plate = plate;
            reveal.Group = group;
            reveal.Rim = GlassCardAssets.RimOf(plate);

            var iconRect = Rect("Icon", content);
            Corner(iconRect, new Vector2(0f, 0.5f), new Vector2(40f, 0f), Vector2.one * IconSize);
            var icon = iconRect.gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            reveal.Icon = icon;
            reveal.Foil = CardFoilAssets.Add(icon);

            const float textLeft = 40f + IconSize + 32f;
            float textWidth = PlateSize.x - textLeft - 130f;
            reveal.Kind = TextLine(
                content,
                "Kind",
                26,
                UiPalette.TextSub,
                58f,
                textLeft,
                textWidth
            );
            reveal.Name = TextLine(
                content,
                "Name",
                44,
                UiPalette.TextMain,
                4f,
                textLeft,
                textWidth
            );
            reveal.Note = TextLine(
                content,
                "Note",
                26,
                UiPalette.TextSub,
                -58f,
                textLeft,
                PlateSize.x - textLeft - 32f
            );

            var quality = Label(content, "Quality", "S", 104, Gold, TextAlignmentOptions.Center);
            Corner(
                quality.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(-24f, -10f),
                new Vector2(110f, 120f)
            );
            reveal.Quality = quality;

            var nothing = Label(
                content,
                "Nothing",
                "何も見つからなかった",
                40,
                UiPalette.TextMain,
                TextAlignmentOptions.Center
            );
            Stretch(nothing.rectTransform);
            reveal.Nothing = nothing;
            nothing.gameObject.SetActive(false);
            plate.gameObject.SetActive(false);
            return reveal;
        }

        // 左寄せの1行（中身は表示のたびに入れる）。長い文は小さくして収める。
        private static TMP_Text TextLine(
            RectTransform parent,
            string name,
            float size,
            Color color,
            float y,
            float left,
            float width
        )
        {
            var label = Label(parent, name, "", size, color, TextAlignmentOptions.Left);
            Corner(
                label.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(left, y),
                new Vector2(width, size + 12f)
            );
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.6f;
            label.fontSizeMax = size;
            return label;
        }
    }
}
