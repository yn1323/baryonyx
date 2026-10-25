using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The encounters an automatic battle run fights: the room's encounter ids of
    /// <see cref="AdventureCatalog"/> ("forest-ruins/elite/5/100"), one for each combination of the
    /// destination's enemies. <see cref="AdventureCatalog.Encounter"/> picks the small enemy from
    /// the variant's ones place, the middle-sized one from the next place and the master from the
    /// one after, so the variants 0 to <see cref="CombinationCount"/> - 1 meet every combination once.
    /// </summary>
    public static class BattleBalanceEncounters
    {
        /// <summary>How many combinations of enemies the room's battle has at the destination.</summary>
        public static int CombinationCount(string destinationId, AdventureRoomKind kind)
        {
            var area = AdventureCatalog.Area(destinationId);
            if (area == null)
                return 0;
            int small = Math.Max(1, area.Small.Length);
            int medium = Math.Max(1, area.Medium.Length);
            return kind switch
            {
                // ミミックの部屋は、小さな敵1体とミミック。
                AdventureRoomKind.ShadyChest => small,
                AdventureRoomKind.Boss => small * medium * Math.Max(1, area.Bosses.Length),
                _ => small * medium,
            };
        }

        /// <summary>
        /// The encounter ids the request fights, in order: its variants, or every combination.
        /// Empty for an unknown destination.
        /// </summary>
        public static IReadOnlyList<string> Of(BattleBalanceRequest request)
        {
            if (request == null)
                return Array.Empty<string>();
            var kind = request.RoomKind;
            int count = CombinationCount(request.destination, kind);
            if (count == 0)
                return Array.Empty<string>();
            var variants =
                request.variants != null && request.variants.Length > 0
                    ? request.variants
                    : Enumerable.Range(0, count).ToArray();
            return variants
                .Select(variant =>
                    AdventureCatalog.EncounterOf(
                        request.destination,
                        kind,
                        Math.Max(0, variant),
                        kind == AdventureRoomKind.Boss ? 100 : Math.Max(1, request.depthPercent)
                    )
                )
                .ToArray();
        }

        /// <summary>The enemies of an encounter, by place ("dew-slime+nectar-bloom").</summary>
        public static string EnemiesOf(string encounterId) =>
            string.Join(
                "+",
                AdventureCatalog
                    .Encounter(encounterId)
                    .Kinds.Where(kind => !string.IsNullOrEmpty(kind))
            );
    }
}
