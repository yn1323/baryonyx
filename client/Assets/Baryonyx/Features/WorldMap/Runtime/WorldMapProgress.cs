using System;
using System.Collections.Generic;
using System.Linq;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// How far the player has gone on the highway (doc/features/stage-progression.md): the square
    /// the party stands on and the squares cleared. The first town is always cleared.
    /// </summary>
    public sealed class WorldMapProgress
    {
        public WorldMapProgress(string at, IEnumerable<string> cleared)
        {
            var squares = new List<string> { WorldMapCatalog.Start };
            foreach (var id in cleared ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(id) && !squares.Contains(id))
                    squares.Add(id);
            Cleared = squares;
            At = string.IsNullOrEmpty(at) ? WorldMapCatalog.Start : at;
        }

        public string At { get; }
        public IReadOnlyList<string> Cleared { get; }

        /// <summary>A new player: on the first town, which is the only one cleared.</summary>
        public static WorldMapProgress Start => new(WorldMapCatalog.Start, null);

        /// <summary>The prototype's progress without a server (the showcase, or no server URL).</summary>
        public static WorldMapProgress Mock =>
            new(WorldMapCatalog.Start, WorldMapCatalog.MockCleared);

        /// <summary>The progress after reaching <paramref name="squareId"/>.</summary>
        public WorldMapProgress Moved(string squareId, bool clear) =>
            new(squareId, clear ? Cleared.Append(squareId) : Cleared);
    }
}
