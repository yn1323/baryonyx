using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// The encounters an automatic battle run fights: the room's encounter ids of
    /// <see cref="AdventureCatalog"/> ("forest-ruins/elite/5/100"). Each number picks a formation
    /// and the destination's enemies standing in it (<see cref="AdventureCatalog.Encounter"/>),
    /// so a request without numbers fights the first <see cref="Samples"/> of them.
    /// </summary>
    public static class BattleBalanceEncounters
    {
        /// <summary>How many encounters a request without numbers fights.</summary>
        public const int Samples = 24;

        /// <summary>
        /// The encounter ids the request fights, in order: its variants, or the numbers 0 to
        /// <see cref="Samples"/> - 1. Empty for an unknown destination.
        /// </summary>
        public static IReadOnlyList<string> Of(BattleBalanceRequest request)
        {
            if (request == null || AdventureCatalog.Area(request.destination) == null)
                return Array.Empty<string>();
            var kind = request.RoomKind;
            var variants =
                request.variants != null && request.variants.Length > 0
                    ? request.variants
                    : Enumerable.Range(0, Samples).ToArray();
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
