using System;
using System.Linq;
using Baryonyx.Combat;
using Baryonyx.Combat.Editor;
using Baryonyx.Editor.Art;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Editor.UI.UiBuild;

namespace Baryonyx.UI.Editor
{
    /// <summary>
    /// Bakes an <see cref="ElementIconRow"/> into a rect: three icon places lined up from the
    /// given side, all hidden until the view shows a piece of equipment's attributes.
    /// </summary>
    public static class ElementIconRowAssets
    {
        // 装備の属性は2つか3つ（魔法のローブ）。目立ちすぎないよう、名前の文字より少し小さくする
        // （2026-11-03、ユーザーの指示。doc/features/equipment.md#装備の属性）。
        public const int Places = 3;
        public const float Size = 30f;
        public const float Gap = 5f;

        // 遠目に読む詳しい面（商会の買う）では少し大きくする。
        public const float DetailSize = 38f;

        /// <summary>The width that <paramref name="count"/> icons take side by side.</summary>
        public static float Width(int count = Places) => count * Size + (count - 1) * Gap;

        public static ElementIconRow Add(
            RectTransform rect,
            TextAnchor alignment,
            float size = Size
        )
        {
            var row = rect.gameObject.AddComponent<ElementIconRow>();
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = alignment;
            layout.spacing = Gap;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.Icons = Enum.GetValues(typeof(CardElement))
                .Cast<CardElement>()
                .Select(element =>
                    element == CardElement.None
                        ? null
                        : ArtAssets.LoadSprite(AttributeIconArt.IconPath(element))
                )
                .ToArray();
            row.Slots = Enumerable
                .Range(0, Places)
                .Select(i =>
                {
                    var place = Rect("Element" + i, rect);
                    place.sizeDelta = Vector2.one * size;
                    var image = place.gameObject.AddComponent<Image>();
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    place.gameObject.SetActive(false);
                    return image;
                })
                .ToArray();
            return row;
        }
    }
}
