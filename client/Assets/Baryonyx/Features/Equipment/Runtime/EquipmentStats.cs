using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat;

namespace Baryonyx.Equipment
{
    /// <summary>
    /// What a character's equipment adds to their stats (doc/features/equipment.md#装備の効果):
    /// each stat grows by the sum of the worn items' percents for it, each grown by the item's
    /// multiplier, taken of the stat with the level and the bond and rounded half away from zero.
    /// The items' chances to play an attack skill twice add up to at most 100%.
    /// </summary>
    public static class EquipmentStats
    {
        /// <summary>The kinds and multipliers of the items <paramref name="characterId"/> wears.</summary>
        public static IReadOnlyList<(EquipmentDefinition Kind, float Multiplier)> Worn(
            EquipmentState state,
            string characterId
        ) =>
            state == null || string.IsNullOrEmpty(characterId)
                ? Array.Empty<(EquipmentDefinition, float)>()
                : state
                    .Worn.Where(entry => entry.Character == characterId)
                    .Select(entry => state.Find(entry.Item))
                    .Where(item => item != null)
                    .Select(item => (EquipmentCatalog.Find(item.EquipmentId), item.Multiplier))
                    .Where(entry => entry.Item1 != null)
                    .ToArray();

        /// <summary>The percent each stat grows by (<see cref="CharacterStats"/> order).</summary>
        public static int[] Percents(
            IEnumerable<(EquipmentDefinition Kind, float Multiplier)> items
        )
        {
            var percents = new int[CharacterStats.Count];
            foreach (var (kind, multiplier) in items ?? Array.Empty<(EquipmentDefinition, float)>())
            foreach (var effect in kind.Effects)
                if (!effect.Twice && effect.Stat >= 0 && effect.Stat < percents.Length)
                    percents[effect.Stat] += effect.PercentAt(multiplier);
            return percents;
        }

        /// <summary>How much the items raise each of <paramref name="stats"/>.</summary>
        public static CharacterStats Bonus(
            CharacterStats stats,
            IEnumerable<(EquipmentDefinition Kind, float Multiplier)> items
        )
        {
            var percents = Percents(items);
            int Raise(int index) =>
                (int)
                    Math.Round(
                        stats[index] * percents[index] / 100.0,
                        MidpointRounding.AwayFromZero
                    );
            return new CharacterStats(
                Raise(0),
                Raise(1),
                Raise(2),
                Raise(3),
                Raise(4),
                Raise(5),
                Raise(6),
                Raise(7)
            );
        }

        /// <summary>How much what <paramref name="characterId"/> wears raises each of <paramref name="stats"/>.</summary>
        public static CharacterStats Bonus(
            CharacterStats stats,
            EquipmentState state,
            string characterId
        ) => Bonus(stats, Worn(state, characterId));

        /// <summary><paramref name="stats"/> with what <paramref name="characterId"/> wears.</summary>
        public static CharacterStats With(
            CharacterStats stats,
            EquipmentState state,
            string characterId
        ) => stats + Bonus(stats, state, characterId);

        /// <summary>The chance in percent that an attack skill of <paramref name="characterId"/> plays twice.</summary>
        public static int TwiceChance(EquipmentState state, string characterId) =>
            Math.Min(
                100,
                Worn(state, characterId)
                    .Sum(entry =>
                        entry
                            .Kind.Effects.Where(effect => effect.Twice)
                            .Sum(effect => effect.PercentAt(entry.Multiplier))
                    )
            );
    }
}
