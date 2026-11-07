using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.WorldMap;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 町に1つ着くたびに、中身のあるダンジョンが1つずつ解放される（2026-11-08、ユーザー）。
    // サーバーの openDestinations（server/src/features/adventure/catalog.test.ts）と同じ例を確かめる。
    public sealed class DungeonUnlocksTests
    {
        private static string[] Open(params string[] towns) =>
            AdventureCatalog
                .Areas.Select(area => area.Id)
                .Where(DungeonUnlocks.Open(towns).Contains)
                .ToArray();

        [Test]
        public void OnlyTheFirstTownsDungeonIsOpenBeforeAnyTown()
        {
            Assert.That(Open(), Is.EqualTo(new[] { "forest-ruins" }));
            Assert.That(Open("porta"), Is.EqualTo(new[] { "forest-ruins" }));
        }

        [Test]
        public void AWayStationOpensTheLowestDungeonStillClosed()
        {
            Assert.That(Open("brezza"), Is.EqualTo(new[] { "forest-ruins", "port-portoria" }));
            Assert.That(
                Open("brezza", "pino"),
                Is.EqualTo(new[] { "forest-ruins", "port-portoria", "glaciera-cave" })
            );
        }

        [Test]
        public void ATownOpensItsOwnDungeonFirstAndOneForEachTown()
        {
            Assert.That(Open("cima"), Is.EqualTo(new[] { "forest-ruins", "neve-peaks" }));
            Assert.That(
                Open("brezza", "portoria"),
                Is.EqualTo(new[] { "forest-ruins", "port-portoria", "glaciera-cave" })
            );
        }

        [Test]
        public void MoreTownsThanDungeonsOpenEveryDungeon()
        {
            var towns = WorldMapCatalog.Towns.Values.Select(entry => entry.Id).ToArray();
            Assert.That(DungeonUnlocks.Open(towns).Count, Is.EqualTo(AdventureCatalog.Areas.Count));
        }
    }
}
