using System.Linq;
using System.Text.RegularExpressions;
using Baryonyx.Combat;
using Baryonyx.Equipment;
using Baryonyx.Inventory;
using Baryonyx.SkillEvolution;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 品質の決まり（doc/features/inventory.md の品質）。サーバー（server/src/shared/quality.test.ts）と同じ区切りになる。
    public sealed class QualityTests
    {
        [TestCase(1f, ItemQuality.C)]
        [TestCase(1.19f, ItemQuality.C)]
        [TestCase(1.2f, ItemQuality.B)]
        [TestCase(1.34f, ItemQuality.B)]
        [TestCase(1.35f, ItemQuality.A)]
        [TestCase(1.44f, ItemQuality.A)]
        [TestCase(1.45f, ItemQuality.S)]
        [TestCase(1.5f, ItemQuality.S)]
        public void TheQualityIsWhereTheMultiplierFallsInTheRange(
            float multiplier,
            ItemQuality quality
        ) => Assert.That(Quality.Of(multiplier, 1.5f), Is.EqualTo(quality));

        [Test]
        public void AMultiplierRightOnABorderGoesToTheHigherQuality()
        {
            // ×1.3 の上限では、A の下端は 100 + 30 × 0.7 = 121。
            Assert.That(Quality.Of(1.21f, 1.3f), Is.EqualTo(ItemQuality.A));
            Assert.That(Quality.Of(1.2f, 1.3f), Is.EqualTo(ItemQuality.B));
        }

        [Test]
        public void TheRangesOfTheQualitiesMeetWithoutGaps()
        {
            foreach (var max in new[] { 1.1f, 1.3f, 1.5f, 2f })
            {
                int next = 100;
                foreach (var quality in Quality.All)
                {
                    var (low, high) = Quality.Range(quality, max);
                    Assert.That(Quality.Percent(low), Is.EqualTo(next), $"{quality} up to {max}");
                    for (
                        int percent = Quality.Percent(low);
                        percent <= Quality.Percent(high);
                        percent++
                    )
                        Assert.That(Quality.Of(percent / 100f, max), Is.EqualTo(quality));
                    Assert.That(Quality.Of(Quality.Middle(quality, max), max), Is.EqualTo(quality));
                    next = Quality.Percent(high) + 1;
                }
                Assert.That(next - 1, Is.EqualTo(Quality.Percent(max)));
            }
        }

        [Test]
        public void EveryNumberGrowsTheWayThatHelpsItsOwner()
        {
            Assert.That(Quality.Amount(100, 1.37f), Is.EqualTo(137));
            Assert.That(Quality.Count(1, 1.5f), Is.EqualTo(2));
            Assert.That(Quality.Count(2, 1.4f), Is.EqualTo(3));
            Assert.That(Quality.Count(2, 1.2f), Is.EqualTo(2));
            Assert.That(Quality.Chance(80, 1.5f), Is.EqualTo(100));
            Assert.That(Quality.Cost(4, 1.5f), Is.EqualTo(3));
            Assert.That(Quality.Cost(1, 1.5f), Is.EqualTo(1));
            Assert.That(Quality.Cost(0, 1.5f), Is.EqualTo(0));
            Assert.That(Quality.Adverse(10, 1.5f), Is.EqualTo(7));
            Assert.That(Quality.Times(1.3f), Is.EqualTo("×1.30"));
        }

        [Test]
        public void TheServersLettersAreRead()
        {
            Assert.That(Quality.TryParse("S", out var s), Is.True);
            Assert.That(s, Is.EqualTo(ItemQuality.S));
            Assert.That(Quality.TryParse("E", out _), Is.False);
            Assert.That(Quality.TryParse(null, out _), Is.False);
        }

        [Test]
        public void AFireAtTheLimitGrowsItsPowerTurnsAndHitsAndCostsLess()
        {
            var fire = CardSkills.Find("Fire");
            Assert.That(fire.Quality, Is.EqualTo(ItemQuality.C));
            Assert.That(fire.WithMultiplier(1f), Is.SameAs(fire));

            var best = fire.WithMultiplier(1.5f);
            Assert.That(best.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(best.Base, Is.SameAs(fire));
            Assert.That(best.Cost, Is.EqualTo(1));
            Assert.That(best.Actions[0].Percent, Is.EqualTo(150));
            Assert.That(best.Actions[0].Hits, Is.EqualTo(2));
            Assert.That(best.Actions[1].Turns, Is.EqualTo(3));
            Assert.That(
                best.Text,
                Is.EqualTo("敵1体に{0}ダメージ×2。[弱点]を突くと[やけど]3ターン。")
            );
            Assert.That(CardRules.Describe(best), Does.Contain("属攻の150%ダメージ×2"));
            // The definition itself stays at ×1.00.
            Assert.That(fire.Actions[0].Percent, Is.EqualTo(100));
        }

        [Test]
        public void ACardThatHitsMoreThanOnceHasALowerLimitAndRewritesItsHits()
        {
            var serpent = CardSkills.Find("SerpentFang");
            Assert.That(CardSkillQuality.LimitOf(serpent), Is.EqualTo(1.3f));

            var best = serpent.WithMultiplier(1.3f);
            Assert.That(best.Quality, Is.EqualTo(ItemQuality.S));
            Assert.That(best.Actions[0].Hits, Is.EqualTo(3));
            Assert.That(best.Text, Does.StartWith("敵1体に{0}ダメージ×3。"));
            // A multiplier past the limit stops at it.
            Assert.That(serpent.WithMultiplier(2f).Multiplier, Is.EqualTo(1.3f));
        }

        [Test]
        public void AmountsAreRewrittenWhereTheirUnitFollows()
        {
            Assert.That(
                CardSkills.Find("Scout").WithMultiplier(1.5f).Text,
                Is.EqualTo("カードを3枚引く。")
            );
            Assert.That(
                CardSkills.Find("WarCry").WithMultiplier(1.5f).Text,
                Is.EqualTo("味方全体の攻撃力を3ターン38%上げる。")
            );
            Assert.That(
                CardSkills.Find("Resurrection").WithMultiplier(1.5f).Text,
                Is.EqualTo("味方1体のHPを最大の75%回復。倒れていれば復活。")
            );
        }

        // どのスキルも、倍率を掛けても説明文の数字の差し込み（{0}）は崩れず、×1.00では元のまま。
        [Test]
        public void EverySkillKeepsItsDescriptionWorkingAtAnyMultiplier()
        {
            var all = CardSkills
                .All.Concat(CardSkills.Prototypes)
                .Concat(UniqueSkillTrees.Stages().Select(stage => stage.Card));
            foreach (var skill in all)
            {
                Assert.That(skill.WithMultiplier(1f).Text, Is.EqualTo(skill.Text), skill.Id);
                foreach (var multiplier in new[] { 1.2f, 1.37f, 1.5f })
                {
                    var grown = skill.WithMultiplier(multiplier);
                    Assert.That(
                        Regex.Matches(grown.Text, "\\{[0-9]+\\}").Count,
                        Is.EqualTo(Regex.Matches(skill.Text, "\\{[0-9]+\\}").Count),
                        skill.Id
                    );
                    Assert.DoesNotThrow(() => CardRules.Describe(grown), skill.Id);
                    Assert.That(grown.Cost, Is.LessThanOrEqualTo(skill.Cost), skill.Id);
                }
            }
        }

        [Test]
        public void AnEquipmentsDescriptionGrowsWithItsMultiplier()
        {
            var dagger = EquipmentCatalog.Find("flame-dagger");
            Assert.That(dagger.MaxMultiplier, Is.EqualTo(1.3f));
            Assert.That(dagger.DetailAt(1f), Is.EqualTo(dagger.Detail));
            Assert.That(dagger.DetailAt(1.3f), Is.EqualTo("速度の 163%｜斬・炎｜3回攻撃"));
            Assert.That(
                EquipmentCatalog.Find("iron-helm").DetailAt(1.5f),
                Is.EqualTo("物防の 173%｜HP +60")
            );
            Assert.That(dagger.QualityOf(1.3f), Is.EqualTo(ItemQuality.S));
        }
    }
}
