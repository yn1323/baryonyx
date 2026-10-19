using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI.Cards
{
    /// <summary>
    /// The sparkle laid over a card's art (doc/art/direction.md, card sparkle): a raw image of the
    /// same art drawn with the CardFoil shader, which adds a sweep of light, glitter, a holo
    /// rainbow and a golden rim by the card's rarity, only where the art is opaque. The rarity and
    /// a seed of this card's own (so cards side by side do not flash together) go to the shader
    /// in the vertex colour, so every card shares one material.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class CardFoil : MonoBehaviour
    {
        public const int MinRarity = 1;
        public const int MaxRarity = 5;

        [Range(MinRarity, MaxRarity)]
        public int Rarity = MinRarity;

        private RawImage image;

        public RawImage Image => image != null ? image : image = GetComponent<RawImage>();

        /// <summary>Shows the sparkle of <paramref name="rarity"/> over <paramref name="art"/>; none at ☆1.</summary>
        public void Show(Texture art, Rect uvRect, int rarity)
        {
            Rarity = Mathf.Clamp(rarity, MinRarity, MaxRarity);
            var raw = Image;
            raw.texture = art;
            raw.uvRect = uvRect;
            raw.raycastTarget = false;
            raw.enabled = art != null && Rarity > MinRarity;
            raw.color = new Color(Rarity / (float)MaxRarity, Seed(), 0f, 1f);
        }

        public void Show(Texture art, int rarity) => Show(art, new Rect(0f, 0f, 1f, 1f), rarity);

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
