using System;
using System.Collections.Generic;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.UI;
using Baryonyx.UI.Cards;

namespace Baryonyx.Library
{
    /// <summary>
    /// The item views' face (<see cref="ItemFace"/>) of a library entry, the same on every screen
    /// that shows a thing (doc/rules/ui-design.md の装備の見せ方): the effect at the thing's own
    /// multiplier (doc/features/inventory.md の品質), and the attributes of equipment and skills.
    /// </summary>
    public static class LibraryFaces
    {
        // まだ見つけていない物の名前。
        public const string HiddenName = "？？？";

        /// <summary>
        /// The face of <paramref name="entry"/> without a quality (items, the library's entries,
        /// things on sale), at ×1.00; a thing not <paramref name="found"/> is a shadow with no name.
        /// </summary>
        public static ItemFace Of(LibraryEntryData entry, bool found = true)
        {
            var face = Base(entry, found);
            if (entry != null && found)
                face.Effect = EffectAt(entry, Quality.MinMultiplier);
            return face;
        }

        /// <summary>
        /// The face of one piece of <paramref name="entry"/>, with its quality and multiplier. A
        /// thing that has only its quality (a clear reward, a draw on the device) passes 0 and
        /// shows the middle of the quality's range.
        /// </summary>
        public static ItemFace Of(LibraryEntryData entry, ItemQuality quality, float multiplier)
        {
            var face = Base(entry, true);
            face.HasQuality = HasQuality(entry?.Kind ?? LibraryKind.Item);
            face.Quality = quality;
            face.Multiplier =
                multiplier > 0f || entry == null
                    ? multiplier
                    : Quality.Middle(quality, LimitOf(entry.Kind, entry.Id));
            if (entry != null)
                face.Effect = EffectAt(entry, face.HasQuality ? face.Multiplier : Quality.MinMultiplier);
            return face;
        }

        /// <summary>Whether things of <paramref name="kind"/> have a quality (equipment and skills).</summary>
        public static bool HasQuality(LibraryKind kind) =>
            kind is LibraryKind.Weapon or LibraryKind.Armor or LibraryKind.Accessory or LibraryKind.Skill;

        /// <summary>
        /// The effect of <paramref name="entry"/> at <paramref name="multiplier"/>: the equipment's
        /// effects and the skill's text with every number grown, the item's own text.
        /// </summary>
        public static string EffectAt(LibraryEntryData entry, float multiplier)
        {
            if (entry == null)
                return "";
            switch (entry.Kind)
            {
                case LibraryKind.Weapon:
                case LibraryKind.Armor:
                case LibraryKind.Accessory:
                    return EquipmentCatalog.Find(entry.Id)?.DetailAt(multiplier) ?? entry.Description;
                case LibraryKind.Skill:
                    var card = CardSkills.Find(entry.Id);
                    return card != null
                        ? CardText.Description(card.WithMultiplier(multiplier))
                        : entry.Description;
                default:
                    return entry.Description;
            }
        }

        /// <summary>The attributes of equipment and the element of a skill; none for others.</summary>
        public static IReadOnlyList<CardElement> ElementsOf(LibraryKind kind, string id)
        {
            switch (kind)
            {
                case LibraryKind.Weapon:
                case LibraryKind.Armor:
                case LibraryKind.Accessory:
                    return EquipmentCatalog.Find(id)?.Elements ?? Array.Empty<CardElement>();
                case LibraryKind.Skill:
                    var card = CardSkills.Find(id);
                    return card != null && card.Element != CardElement.None
                        ? new[] { card.Element }
                        : Array.Empty<CardElement>();
                default:
                    return Array.Empty<CardElement>();
            }
        }

        /// <summary>The upper limit of the multiplier of things of the kind (1 for things without a quality).</summary>
        public static float LimitOf(LibraryKind kind, string id) =>
            kind switch
            {
                LibraryKind.Weapon or LibraryKind.Armor or LibraryKind.Accessory =>
                    EquipmentCatalog.Find(id)?.MaxMultiplier ?? Quality.MinMultiplier,
                LibraryKind.Skill => CardSkillQuality.LimitOf(CardSkills.Find(id)),
                _ => Quality.MinMultiplier,
            };

        private static ItemFace Base(LibraryEntryData entry, bool found)
        {
            var face = new ItemFace();
            if (entry == null)
                return face;
            face.Name = found ? entry.Name : HiddenName;
            face.Rarity = entry.Rarity;
            face.Elements = found ? ElementsOf(entry.Kind, entry.Id) : Array.Empty<CardElement>();
            face.Art = entry.Art;
            face.ArtUv = entry.ArtUv;
            face.ArtTint = found ? entry.ArtTint : LibraryCatalogView.ShadowTint;
            face.ArtDots = entry.ArtDots;
            return face;
        }
    }
}
