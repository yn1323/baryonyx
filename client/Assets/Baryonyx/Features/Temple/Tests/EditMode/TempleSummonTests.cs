using System.Linq;
using Baryonyx.Library;
using Baryonyx.Temple;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 神殿の召喚：画面に出す割合と、仮の抽選（doc/features/monetization.md）。
    public sealed class TempleSummonTests
    {
        // 分類・☆・品質の割合は、それぞれ合わせて100%になる。
        [Test]
        public void EachRateTableAddsUpToAHundredPercent()
        {
            Assert.That(
                TempleSummon.Categories.Sum(category => category.Permille),
                Is.EqualTo(1000)
            );
            Assert.That(TempleSummon.StarPermille.Sum(), Is.EqualTo(1000));
            Assert.That(TempleSummon.QualityPermille.Sum(rate => rate.Permille), Is.EqualTo(1000));
            Assert.That(
                TempleSummon
                    .Categories.First(category => category.Kind == LibraryKind.Character)
                    .Permille,
                Is.EqualTo(20)
            );
        }

        [Test]
        public void RatesAreShownAsPercentages()
        {
            Assert.That(TempleSummon.PercentText(480), Is.EqualTo("48%"));
            Assert.That(TempleSummon.PercentText(35), Is.EqualTo("3.5%"));
            Assert.That(TempleSummon.PercentText(5), Is.EqualTo("0.5%"));
        }

        // 10回のうち☆3以上もキャラも出なければ、最後の1つを☆3以上から引き直す。1回では引き直さない。
        [Test]
        public void TenAtOnceGuaranteeOneWithThreeStars()
        {
            var pool = new[]
            {
                new SummonEntry(LibraryKind.Item, "low", 1),
                new SummonEntry(LibraryKind.Item, "high", 3),
            };
            var ten = TempleSummon.Roll(pool, TempleSummon.TenCount, () => 0.0);
            Assert.That(ten.Count, Is.EqualTo(TempleSummon.TenCount));
            Assert.That(ten.Take(9).Select(result => result.Entry.Id), Is.All.EqualTo("low"));
            Assert.That(ten[9].Entry.Id, Is.EqualTo("high"));

            var once = TempleSummon.Roll(pool, 1, () => 0.0);
            Assert.That(once.Single().Entry.Id, Is.EqualTo("low"));
        }

        // 品質は装備にだけ付く。キャラとアイテムは品質を持たない。
        [Test]
        public void OnlyEquipmentHasAQuality()
        {
            foreach (
                var (kind, quality) in new[]
                {
                    (LibraryKind.Weapon, true),
                    (LibraryKind.Armor, true),
                    (LibraryKind.Accessory, true),
                    (LibraryKind.Item, false),
                    (LibraryKind.Character, false),
                }
            )
            {
                var pool = new[]
                {
                    new SummonEntry(kind, "only", kind == LibraryKind.Character ? 0 : 2),
                };
                var result = TempleSummon.Roll(pool, 1, () => 0.5).Single();
                Assert.That(result.Quality.HasValue, Is.EqualTo(quality), kind.ToString());
            }
        }

        // 仮の一覧は、キャラと、冒険の行き先のアイテム・装備で、スキルとACTボーナスを含まない。
        [Test]
        public void MockPoolHasCharactersItemsAndEquipment()
        {
            var pool = TempleSummon.MockPool(new[] { "toma", "aria" });
            Assert.That(
                pool.Where(entry => entry.Kind == LibraryKind.Character).Select(entry => entry.Id),
                Is.EquivalentTo(new[] { "toma", "aria" })
            );
            Assert.That(
                pool.Select(entry => entry.Kind).Distinct(),
                Is.EquivalentTo(
                    new[]
                    {
                        LibraryKind.Character,
                        LibraryKind.Weapon,
                        LibraryKind.Armor,
                        LibraryKind.Accessory,
                        LibraryKind.Item,
                    }
                )
            );
            Assert.That(pool.Select(entry => (entry.Kind, entry.Id)), Is.Unique);
        }

        // 多く引くと、分類はおおよそ画面の割合どおりに出る。
        [Test]
        public void ManyDrawsFollowTheCategoryRates()
        {
            var random = new System.Random(7);
            var pool = TempleSummon.MockPool(new[] { "toma" });
            const int draws = 20000;
            var results = TempleSummon.Roll(pool, draws, random.NextDouble);
            foreach (var (kind, name, permille) in TempleSummon.Categories)
            {
                double share = results.Count(result => result.Entry.Kind == kind) / (double)draws;
                Assert.That(share, Is.EqualTo(permille / 1000.0).Within(0.015), name);
            }
        }
    }
}
