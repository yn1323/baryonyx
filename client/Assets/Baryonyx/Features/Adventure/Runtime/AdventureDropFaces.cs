using System;
using Baryonyx.Library;
using Baryonyx.TravelOffice;
using Baryonyx.UI;

namespace Baryonyx.Adventure
{
    /// <summary>
    /// What the shared detail window shows for a drop (doc/rules/ui-design.md の装備の見せ方):
    /// the library's entry of the thing, with the drop's quality and multiplier when it has one,
    /// as the travel office's treasures show it.
    /// </summary>
    public static class AdventureDropFaces
    {
        /// <summary>The detail of <paramref name="drop"/>; none for what the library does not hold (the enhancement materials) or without <paramref name="library"/>.</summary>
        public static ItemFace Of(LibraryData library, AdventureDrop drop)
        {
            if (library == null || drop == null || drop.Category == AdventureDropCategory.Material)
                return null;
            var kind = TravelTreasures.KindOf(drop.Category);
            var entry = Array.Find(library.Entries, item => item.Kind == kind && item.Id == drop.Id);
            if (entry == null)
                return null;
            return drop.Quality is { } quality
                ? LibraryFaces.Of(entry, quality, drop.Multiplier)
                : LibraryFaces.Of(entry);
        }
    }
}
