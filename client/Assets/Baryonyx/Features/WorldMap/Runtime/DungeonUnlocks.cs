using System.Collections.Generic;
using System.Linq;
using Baryonyx.Adventure;

namespace Baryonyx.WorldMap
{
    /// <summary>
    /// Which dungeons are open (doc/features/stage-progression/destinations.md). A town is reached
    /// on the nested world map (the town's square paid for) or on the old highway (the town
    /// cleared); the first town is always reached. Each town reached opens one dungeon with
    /// contents: its own when it has one, otherwise the lowest-level one still closed, so a way
    /// station without contents opens another town's. A dungeon without contents yet opens with
    /// its town. The server decides the same way when an adventure starts
    /// (openDestinations in server/src/features/adventure/catalog.ts).
    /// </summary>
    public static class DungeonUnlocks
    {
        /// <summary>Whether <paramref name="town"/> is reached; a destination without a town is always open.</summary>
        public static bool Reached(string town, WorldMapProgress highway) =>
            string.IsNullOrEmpty(town)
            || town == WorldMapCatalog.Start
            || (highway != null && highway.Cleared.Contains(town))
            || WorldAtlasSession.IsOpenedAnywhere(town);

        /// <summary>Whether the dungeon with contents <paramref name="area"/> can be set out for.</summary>
        public static bool IsOpen(AdventureArea area, WorldMapProgress highway) =>
            area != null && Open(ReachedTowns(highway)).Contains(area.Id);

        /// <summary>
        /// The IDs of the dungeons with contents open once <paramref name="towns"/> are reached:
        /// the first town's, and one more for each other town.
        /// </summary>
        public static HashSet<string> Open(IEnumerable<string> towns)
        {
            var reached = new HashSet<string>(towns);
            var areas = AdventureCatalog.Areas;
            reached.Remove(areas[0].Town);
            var open = new HashSet<string>(
                areas.Skip(1).Where(area => reached.Contains(area.Town)).Select(area => area.Id)
            );
            foreach (var area in areas.Skip(1))
            {
                if (open.Count >= reached.Count)
                    break;
                open.Add(area.Id);
            }
            open.Add(areas[0].Id);
            return open;
        }

        /// <summary>
        /// The dungeon with contents that reaching <paramref name="town"/> opens, or null (a square
        /// that is not a town, the first town, or every dungeon with contents already open).
        /// </summary>
        public static AdventureArea OpenedBy(string town, WorldMapProgress highway)
        {
            if (
                string.IsNullOrEmpty(town)
                || town == WorldMapCatalog.Start
                || !WorldMapCatalog.Towns.Values.Any(entry => entry.Id == town)
            )
                return null;
            var with = ReachedTowns(highway);
            with.Add(town);
            var without = new HashSet<string>(with);
            without.Remove(town);
            var opened = Open(with);
            opened.ExceptWith(Open(without));
            return AdventureCatalog.Areas.FirstOrDefault(area => opened.Contains(area.Id));
        }

        private static HashSet<string> ReachedTowns(WorldMapProgress highway) =>
            new(
                WorldMapCatalog
                    .Towns.Values.Select(entry => entry.Id)
                    .Where(town => Reached(town, highway))
            );
    }
}
