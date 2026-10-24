using System.Collections.Generic;
using Baryonyx.Inventory;
using UnityEngine;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The art of an item played as a battle card (doc/features/items.md#戦闘での使い方). A card's
    /// art is 64×58 dots and an item's picture 32×32, so the picture is drawn at twice its size
    /// with its top and bottom three dots cut, keeping every dot square. Made once per kind.
    /// </summary>
    public static class AdventureItemCardArt
    {
        public const int Width = 64;
        public const int Height = 58;

        private static readonly Dictionary<string, Texture> made = new();

        /// <summary>The card art of the item, or null when the item has no picture.</summary>
        public static Texture Of(ItemArt art, string itemId)
        {
            var icon = art != null ? art.TextureOf(itemId) : null;
            if (icon == null)
                return null;
            if (made.TryGetValue(itemId, out var texture) && texture != null)
                return texture;
            var target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32)
            {
                name = "ItemCardArt " + itemId,
                filterMode = FilterMode.Point,
            };
            // 横は絵の幅いっぱい、縦は64ドットのうち中央の58ドットを写す。ドット絵の絵は
            // Pointで読み込んであるため、2倍にしてもぼけない。
            Graphics.Blit(
                icon,
                target,
                new Vector2(1f, Height / (float)Width),
                new Vector2(0f, (Width - Height) / 2f / Width)
            );
            made[itemId] = target;
            return target;
        }

        // Play Modeに入るたびに作り直す（前のPlayのテクスチャを使わない）。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => made.Clear();
    }
}
