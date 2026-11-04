using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Library;
using Baryonyx.WorldMap;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // ライブラリの入手先：解放したダンジョンのうち、その物が落ちる所（doc/features/library.md の入手先）。
    public sealed class LibraryDropPlacesTests
    {
        private static readonly WorldMapProgress OnlyStart = WorldMapProgress.Start;

        [Test]
        public void ItemsOfAnOpenDestinationNameIt()
        {
            var entry = AdventureDrops
                .Pools[AdventureCatalog.ForestRuins]
                .First(drop => drop.Category == AdventureDropCategory.Item && !drop.BossOnly);
            var places = LibraryDropPlaces.Of(LibraryKind.Item, entry.Id, OnlyStart);
            Assert.That(places.Select(place => place.Name), Is.EqualTo(new[] { "ミストラ遺跡" }));
            Assert.That(places.Single().Destination, Is.EqualTo(AdventureCatalog.ForestRuins));
        }

        [Test]
        public void ClosedDestinationsAreLeftOut()
        {
            var entry = AdventureDrops
                .Pools["port-portoria"]
                .First(drop => drop.Category == AdventureDropCategory.Item);
            Assert.That(LibraryDropPlaces.Of(LibraryKind.Item, entry.Id, OnlyStart), Is.Empty);
            Assert.That(
                LibraryDropPlaces.Of(LibraryKind.Item, entry.Id, new WorldMapProgress(WorldMapCatalog.Start, new[] { "portoria" })),
                Is.Not.Empty
            );
        }

        [Test]
        public void BossOnlyDropsSayTheInnermostRoom()
        {
            var places = LibraryDropPlaces.Of(LibraryKind.Item, "guardian-seed", OnlyStart);
            Assert.That(places.Single().BossOnly, Is.True);
            Assert.That(places.Single().Text, Is.EqualTo("ミストラ遺跡の最奥"));
        }

        // 入手先の札は1枚にしている。物は1つの行き先でしか落ちない（doc/features/drops.md）。
        [Test]
        public void EveryKindFallsInOneDestination()
        {
            var shared = AdventureDrops
                .Pools.SelectMany(pool => pool.Value.Select(entry => (entry.Category, entry.Id)))
                .GroupBy(kind => kind)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key.Id);
            Assert.That(shared, Is.Empty);
        }

        [Test]
        public void AnotherKindWithTheSameIdIsNotMatched()
        {
            var entry = AdventureDrops
                .Pools[AdventureCatalog.ForestRuins]
                .First(drop => drop.Category == AdventureDropCategory.Weapon);
            Assert.That(LibraryDropPlaces.Of(LibraryKind.Armor, entry.Id, OnlyStart), Is.Empty);
            Assert.That(LibraryDropPlaces.Of(LibraryKind.Weapon, entry.Id, OnlyStart), Is.Not.Empty);
        }
    }
}
