using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Adventure;
using Baryonyx.Inventory;
using Baryonyx.Networking;
using Baryonyx.WorldMap;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 街道の宝箱の中身（doc/features/stage-progression.md の街道の宝箱と街）。
    public sealed class WorldMapChestsTests
    {
        [SetUp]
        public void ResetSession()
        {
            WorldMapSession.Reset();
            SaveQueue.Reset();
        }

        [TearDown]
        public void Restore()
        {
            WorldMapSession.Reset();
            SaveQueue.Reset();
        }

        [Test]
        public void EveryTreasureSquareHoldsSomethingAndNothingElseDoes()
        {
            var graph = WorldMapCatalog.Build();
            var treasures = graph
                .Nodes.Where(node => node.Kind == WorldNodeKind.Treasure)
                .Select(node => node.Id)
                .ToArray();
            Assert.That(treasures, Is.Not.Empty);
            Assert.That(WorldMapChests.Squares, Is.EquivalentTo(treasures));
            foreach (var id in treasures)
                Assert.That(WorldMapChests.Of(id), Is.Not.Empty, id);
            Assert.That(WorldMapChests.Of("n10-10"), Is.Empty);
        }

        [Test]
        public void TheMapHasNoSquareOfAnUnknownKind()
        {
            // 歩数の関所はなくした（2026-10-25）。地図の文字は、道・街・マスの種類だけにする。
            var squares = WorldMapCatalog.Build().Nodes.Count;
            int letters = WorldMapCatalog.Rows.Sum(row =>
                row.Count(c => c is not ('.' or '-' or '|'))
            );
            Assert.That(letters, Is.EqualTo(squares));
            Assert.That(string.Concat(WorldMapCatalog.Rows), Does.Not.Contain("g"));
        }

        [Test]
        public void EquipmentAndSkillsComeWithQualityAAndKnownKinds()
        {
            foreach (var id in WorldMapChests.Squares)
            foreach (var drop in WorldMapChests.Of(id))
            {
                Assert.That(drop.RoomId, Is.EqualTo(id));
                Assert.That(drop.Stars, Is.InRange(1, 5), drop.Id);
                if (drop.Category == AdventureDropCategory.Material)
                {
                    Assert.That(drop.Quality, Is.Null);
                    continue;
                }
                float max = AdventureTexts.MaxMultiplierOf(drop.Category, drop.Id);
                Assert.That(max, Is.GreaterThan(Quality.MinMultiplier), drop.Id);
                Assert.That(drop.Quality, Is.EqualTo(ItemQuality.A));
                Assert.That(Quality.Of(drop.Multiplier, max), Is.EqualTo(ItemQuality.A), drop.Id);
            }
        }

        [Test]
        public void AChestOpensOnlyTheFirstTimeItsSquareIsCleared()
        {
            var first = WorldMapProgress.Start.Moved("n8-5", clear: true);
            Assert.That(first.Chest, Is.Not.Empty);
            Assert.That(first.Moved("n8-5", clear: true).Chest, Is.Empty);
            Assert.That(first.Moved("n8-5", clear: false).Chest, Is.Empty);
            Assert.That(first.Moved("n10-10", clear: true).Chest, Is.Empty);
        }

        [Test]
        public async Task WithoutAServerOpeningAChestGivesItsContents()
        {
            await WorldMapSession.LoadAsync(CancellationToken.None);
            var chest = await WorldMapSession.OpenChestAsync("n8-5");
            Assert.That(
                chest.Select(drop => drop.Id),
                Is.EqualTo(WorldMapChests.Of("n8-5").Select(drop => drop.Id))
            );
            Assert.That(WorldMapSession.Known.Cleared, Does.Contain("n8-5"));
        }

        [Test]
        public void TheServersChestLeavesOutWhatItCannotRead()
        {
            var progress = WorldMapServerSource.ToProgress(
                new WorldMapApiClient.State
                {
                    at = "n16-18",
                    cleared = new[] { "n16-18" },
                    chest = new[]
                    {
                        new WorldMapApiClient.Drop
                        {
                            category = "skill",
                            id = "HolyLight",
                            multiplier = 1.5f,
                            quality = "A",
                        },
                        new WorldMapApiClient.Drop
                        {
                            category = "material",
                            id = "awakening-shard",
                        },
                        // 倍率のないスキル、知らない分類は外す。
                        new WorldMapApiClient.Drop { category = "skill", id = "HolyLight" },
                        new WorldMapApiClient.Drop { category = "rune", id = "rune" },
                    },
                }
            );
            Assert.That(
                progress.Chest.Select(drop => (drop.Category, drop.Id, drop.Quality)),
                Is.EqualTo(
                    new (AdventureDropCategory, string, ItemQuality?)[]
                    {
                        (AdventureDropCategory.Skill, "HolyLight", ItemQuality.A),
                        (AdventureDropCategory.Material, "awakening-shard", null),
                    }
                )
            );
            Assert.That(progress.Chest.All(drop => drop.RoomId == "n16-18"), Is.True);
        }
    }
}
