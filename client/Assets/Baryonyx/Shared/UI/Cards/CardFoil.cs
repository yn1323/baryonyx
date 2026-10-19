using Baryonyx.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI.Cards
{
    /// <summary>
    /// The sparkle laid over a card's art (doc/art/direction.md, card sparkle): a raw image of the
    /// same art drawn with the CardFoil shader, which adds a sweep of light, glitter, a holo
    /// rainbow and a golden rim by the card's quality, only where the art is opaque: everything at
    /// S, a lighter sparkle at A, nothing at B and C. The shader draws five levels (made for the
    /// stars before quality); S takes its fifth and A its third. The level and a seed of this
    /// card's own (so cards side by side do not flash together) go to the shader in the vertex
    /// colour, so every card shares one material.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CardFoil : MonoBehaviour
    {
        /// <summary>The shader's levels: 1 draws nothing, 5 everything.</summary>
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        public ItemQuality Quality = ItemQuality.C;

        private RawImage image;

        public RawImage Image => image != null ? image : image = GetComponent<RawImage>();

        /// <summary>The shader's level for <paramref name="quality"/>: S 5, A 3, B and C none.</summary>
        public static int LevelOf(ItemQuality quality) =>
            quality switch
            {
                ItemQuality.S => MaxLevel,
                ItemQuality.A => 3,
                _ => MinLevel,
            };

        /// <summary>Whether <paramref name="quality"/> sparkles at all.</summary>
        public static bool Sparkles(ItemQuality quality) => LevelOf(quality) > MinLevel;

        /// <summary>Shows the sparkle of <paramref name="quality"/> over <paramref name="art"/>; none at B and C.</summary>
        public void Show(Texture art, Rect uvRect, ItemQuality quality)
        {
            Quality = quality;
            int level = LevelOf(quality);
            var raw = Image;
            raw.texture = art;
            raw.uvRect = uvRect;
            raw.raycastTarget = false;
            raw.enabled = art != null && level > MinLevel;
            raw.color = new Color(level / (float)MaxLevel, Seed(), 0f, 1f);
        }

        public void Show(Texture art, ItemQuality quality) =>
            Show(art, new Rect(0f, 0f, 1f, 1f), quality);

        // カードごとの乱数の種（0〜1）。並んだカードの光る時がずれるよう、置かれた場所の名前から決める。
        private float Seed()
        {
            unchecked
            {
                int hash = 17;
                for (var t = transform; t != null; t = t.parent)
                    hash = hash * 31 + t.GetSiblingIndex() * 7 + t.name.Length;
                return Mathf.Repeat(hash * 0.6180339f, 1f);
            }
        }
    }
}
