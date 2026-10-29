using System;
using UnityEngine;

namespace Baryonyx.Inventory
{
    /// <summary>
    /// The art of each kind of item, accessory, weapon and armour (32×32 dot art in
    /// Shared/Art/Items, Accessories, Weapons and Armor), and the card art of each skill (64×58,
    /// for the things found), by the kind's id. ItemArtAssets bakes
    /// it from the art files, and the screens that show a kind's own picture read it; a kind
    /// without art falls back to the screen's icon for its slot or category.
    /// </summary>
    [CreateAssetMenu(menuName = "Baryonyx/Item Art")]
    public sealed class ItemArt : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Id = "";
            public Sprite Sprite;
        }

        public Entry[] Entries = Array.Empty<Entry>();

        /// <summary>The kind's picture, or null when it has none.</summary>
        public Sprite Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var entry in Entries)
                if (entry.Id == id)
                    return entry.Sprite;
            return null;
        }

        /// <summary>The kind's picture as a texture (the whole canvas), or null.</summary>
        public Texture2D TextureOf(string id)
        {
            var sprite = Find(id);
            return sprite != null ? sprite.texture : null;
        }

        /// <summary>The art file's name for a kind's id: "wooden-sword" is "WoodenSword".</summary>
        public static string FileName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "";
            var parts = id.Split('-');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Concat(parts);
        }
    }
}
