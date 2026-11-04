using System.Linq;
using Baryonyx.Adventure;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// Which dungeons are open (doc/features/stage-progression/destinations.md): a dungeon opens
    /// once its town is reached, on the nested world map (the town's square paid for) or on the
    /// old highway (the town cleared). The first town is always reached. The server decides the
    /// same way when an adventure starts (server/src/features/adventure/run.ts).
    /// </summary>
    public static class DungeonUnlocks
    {
        /// <summary>Whether <paramref name="town"/> is reached; a destination without a town is always open.</summary>
        public static bool Reached(string town, WorldMapProgress highway) =>
            string.IsNullOrEmpty(town)
            || town == WorldMapCatalog.Start
            || (highway != null && highway.Cleared.Contains(town))
            || WorldAtlasSession.IsOpenedAnywhere(town);

        /// <summary>The dungeon whose town is <paramref name="square"/>, or null (also for the first town).</summary>
        public static AdventureArea DungeonAt(string square) =>
            string.IsNullOrEmpty(square) || square == WorldMapCatalog.Start
                ? null
                : AdventureCatalog.Areas.FirstOrDefault(area => area.Town == square);
    }
}
