using System.Linq;
using Baryonyx.Adventure;
using Baryonyx.Library;
using Baryonyx.TravelOffice;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 旅の案内所の、行き先ごとのお宝の一覧と見つけた数（doc/features/screens/guide.md）。
    public sealed class TravelTreasuresTests
    {
        [Test]
        public void TreasuresAreTheDestinationsDropList()
        {
            var treasures = TravelTreasures.Of(AdventureCatalog.ForestRuins);
            var pool = AdventureDrops.Pools[AdventureCatalog.ForestRuins];
            Assert.That(
                treasures.Select(treasure => treasure.Id),
                Is.EquivalentTo(pool.Select(entry => entry.Id))
            );
        }

        [Test]
        public void SectionsKeepTheirOrderAndPutWeaponsBeforeArmor()
        {
            var sections = Enumerable
                .Range(0, TravelTreasures.Sections.Count)
                .Select(section => TravelTreasures.Of("port-portoria", section))
                .ToArray();
            // 段の順は、武器・防具→アクセサリー→スキル→アイテム（doc/rules/ui-design.md の分類の切り替え）。
            Assert.That(
                TravelTreasures.Sections.Select(section => section.Title),
                Is.EqualTo(new[] { "武器・防具", "アクセサリー", "スキル", "アイテム" })
            );
            Assert.That(
                sections[3].All(treasure => treasure.Category == AdventureDropCategory.Item),
                Is.True
            );
            // 武器・防具の段は、武器を先にまとめてから防具を並べる。
            Assert.That(
                sections[0].Select(treasure => treasure.Category),
                Is.EqualTo(
                    sections[0]
                        .Select(treasure => treasure.Category)
                        .OrderBy(category => category == AdventureDropCategory.Armor)
                )
            );
            Assert.That(
                sections[0].Select(treasure => treasure.Category).Distinct(),
                Is.EqualTo(new[] { AdventureDropCategory.Weapon, AdventureDropCategory.Armor })
            );
        }

        [Test]
        public void BossOnlyTreasuresAreMarked()
        {
            var seed = TravelTreasures
                .Of(AdventureCatalog.ForestRuins)
                .Single(treasure => treasure.Id == "guardian-seed");
            Assert.That(seed.BossOnly, Is.True);
            Assert.That(seed.Kind, Is.EqualTo(LibraryKind.Item));
        }

        [Test]
        public void CountsTheTreasuresTheLibraryFound()
        {
            var records = new LibraryRecords(
                new[]
                {
                    (LibraryKind.Item, "herb", 0),
                    (LibraryKind.Weapon, "oak-staff", 0),
                    (LibraryKind.Skill, "Slash", 0),
                    // 別の行き先の物は数えない。
                    (LibraryKind.Item, "dried-fish", 0),
                },
                System.Array.Empty<LibraryStepDay>()
            );
            int total = AdventureDrops.Pools[AdventureCatalog.ForestRuins].Length;
            Assert.That(
                TravelTreasures.Count(AdventureCatalog.ForestRuins, records),
                Is.EqualTo((3, total))
            );
            Assert.That(
                TravelTreasures.CountText(AdventureCatalog.ForestRuins, records),
                Is.EqualTo($"3/{total}")
            );
        }

        [Test]
        public void CountShowsADashBeforeTheRecordsAreRead()
        {
            int total = AdventureDrops.Pools[AdventureCatalog.ForestRuins].Length;
            Assert.That(
                TravelTreasures.CountText(AdventureCatalog.ForestRuins, null),
                Is.EqualTo($"-/{total}")
            );
            Assert.That(TravelTreasures.Of("unknown"), Is.Empty);
        }
    }
}
