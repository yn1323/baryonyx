using System;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// How far the player has gone on the highway (doc/features/stage-progression.md): the square
    /// the party stands on and the squares cleared. The first town is always cleared. After a
    /// move that cleared a treasure square, it also carries what the chest held.
    /// </summary>
    public sealed class WorldMapProgress
    {
        public WorldMapProgress(
            string at,
            IEnumerable<string> cleared,
            IReadOnlyList<AdventureDrop> chest = null
        )
        {
            var squares = new List<string> { WorldMapCatalog.Start };
            foreach (var id in cleared ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id) && !squares.Contains(id))
                    squares.Add(id);
            Cleared = squares;
            At = string.IsNullOrEmpty(at) ? WorldMapCatalog.Start : at;
            Chest = chest ?? Array.Empty<AdventureDrop>();
        }

        public string At { get; }
        public IReadOnlyList<string> Cleared { get; }

        /// <summary>
        /// What the treasure square cleared by the move that led here held; empty for any other
        /// move and for a read.
        /// </summary>
        public IReadOnlyList<AdventureDrop> Chest { get; }

        /// <summary>A new player: on the first town, which is the only one cleared.</summary>
        public static WorldMapProgress Start => new(WorldMapCatalog.Start, null);

        /// <summary>The prototype's progress without a server (the showcase, or no server URL).</summary>
        public static WorldMapProgress Mock =>
            new(WorldMapCatalog.Start, WorldMapCatalog.MockCleared);

        /// <summary>
        /// The progress after reaching <paramref name="squareId"/>. Clearing a treasure square
        /// for the first time opens its chest (<see cref="WorldMapChests"/>).
        /// </summary>
        public WorldMapProgress Moved(string squareId, bool clear) =>
            new(
                squareId,
                clear ? Cleared.Append(squareId) : Cleared,
                clear && !Cleared.Contains(squareId) ? WorldMapChests.Of(squareId) : null
            );
    }
}
