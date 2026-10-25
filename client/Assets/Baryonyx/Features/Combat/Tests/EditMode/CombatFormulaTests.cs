using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CombatFormulaTests
    {
        [Test]
        public void StatsGrowFromATenthAtLevelOneToTheWholeAtLevelHundred()
        {
            Assert.That(CombatFormula.GrowthRate(1), Is.EqualTo(0.1).Within(1e-9));
            Assert.That(CombatFormula.GrowthRate(10), Is.EqualTo(0.1233).Within(1e-4));
            Assert.That(CombatFormula.GrowthRate(100), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void TheDefenseConstantGrowsWithTheAttackersLevel()
        {
            Assert.That(CombatFormula.DefenseConstant(1), Is.EqualTo(160).Within(1e-9));
            Assert.That(CombatFormula.DefenseConstant(10), Is.EqualTo(197.3).Within(0.1));
            Assert.That(CombatFormula.DefenseConstant(100), Is.EqualTo(1600).Within(1e-9));
        }

        [Test]
        public void ADefenseAsHighAsTheConstantHalvesTheBlow()
        {
            Assert.That(CombatFormula.Defend(1000, 1600, 100), Is.EqualTo(500));
        }

        [Test]
        public void DefenseSoftensABlowAndRoundsDown()
        {
            // 486 × 197.25 / (197.25 + 160) = 268.3
            Assert.That(CombatFormula.Defend(486, 160, 10), Is.EqualTo(268));
            // 243 × 197.25 / (197.25 + 60) = 186.3
            Assert.That(CombatFormula.Defend(243, 60, 10), Is.EqualTo(186));
        }

        [Test]
        public void NoDefenseLetsTheWholeBlowThrough()
        {
            Assert.That(CombatFormula.Defend(243, 0, 10), Is.EqualTo(243));
        }

        [Test]
        public void AnAllyKeepsHalfItsBlockRoundedDownIntoTheNextTurn()
        {
            Assert.That(CardRules.BlockKept(118), Is.EqualTo(59));
            Assert.That(CardRules.BlockKept(59), Is.EqualTo(29));
            Assert.That(CardRules.BlockKept(1), Is.EqualTo(0));
            Assert.That(CardRules.BlockKept(0), Is.EqualTo(0));
        }

        [Test]
        public void ABlowWithPowerDealsAtLeastOne()
        {
            Assert.That(CombatFormula.Defend(1, 5000, 1), Is.EqualTo(1));
            Assert.That(CombatFormula.Defend(0, 100, 10), Is.EqualTo(0));
        }

        [Test]
        public void EqualLevelsWeighTheSameAtEveryLevel()
        {
            // The same blow and defense at level 100 and at level 10 (both grown alike) let the
            // same part of the blow through.
            double whole = CombatFormula.Defend(10000, 13000, 100) / 10000.0;
            double grown = CombatFormula.GrowthRate(10);
            int power = (int)System.Math.Round(10000 * grown);
            int defense = (int)System.Math.Round(13000 * grown);
            double part = CombatFormula.Defend(power, defense, 10) / (double)power;
            Assert.That(part, Is.EqualTo(whole).Within(0.01));
        }

        // 会心率は5%に、Lv100の会心40ごとに1%を足し、50%で止まる。Lvが上がっても同じ値になる。
        [TestCase(0, 0.05)]
        [TestCase(200, 0.10)]
        [TestCase(1000, 0.30)]
        [TestCase(4000, 0.50)]
        public void TheCriticalChanceGrowsWithTheStatUpToHalf(int at100, double expected)
        {
            Assert.That(
                CombatFormula.CriticalChance(at100, 100),
                Is.EqualTo(expected).Within(1e-9)
            );
            int at10 = (int)System.Math.Round(at100 * CombatFormula.GrowthRate(10));
            Assert.That(CombatFormula.CriticalChance(at10, 10), Is.EqualTo(expected).Within(0.005));
        }

        [Test]
        public void ACriticalHitDealsHalfAsMuchAgain()
        {
            Assert.That(CombatFormula.Critical(100), Is.EqualTo(150));
            Assert.That(CombatFormula.Critical(15), Is.EqualTo(23));
        }

        // doc/features/combat.md の例：Lv10のアリア（幸運37）が森の守り手（幸運111）に付けると約−30%。
        [Test]
        public void LuckMovesTheChanceOfAStatusByTheGapAtLevel100()
        {
            Assert.That(CombatFormula.StatusChance(60, 37, 10, 111), Is.EqualTo(30));
            Assert.That(CombatFormula.StatusChance(60, 111, 10, 37), Is.EqualTo(90));
            Assert.That(CombatFormula.StatusChance(60, 50, 10, 50), Is.EqualTo(60));
        }

        [Test]
        public void TheChanceOfAStatusStaysBetweenFiveAndAHundred()
        {
            Assert.That(CombatFormula.StatusChance(100, 900, 100, 300), Is.EqualTo(100));
            Assert.That(CombatFormula.StatusChance(40, 0, 100, 900), Is.EqualTo(5));
        }

        [Test]
        public void FixedOddsLandTheSameEveryTime()
        {
            try
            {
                BattleOdds.Fix(critical: true, lands: false);
                Assert.That(BattleOdds.Critical(0.05), Is.True);
                Assert.That(BattleOdds.Critical(0), Is.False);
                Assert.That(BattleOdds.Lands(99), Is.False);
                Assert.That(BattleOdds.Lands(100), Is.True);
                BattleOdds.Fix(critical: false, lands: true);
                Assert.That(BattleOdds.Critical(0.5), Is.False);
                Assert.That(BattleOdds.Lands(5), Is.True);
            }
            finally
            {
                BattleOdds.Fix(critical: false, lands: true);
            }
        }

        // 同じ種なら、会心と状態異常が同じ順に同じ結果で出る（自動対戦の再現）。
        [Test]
        public void TheSameSeedRollsTheSameChances()
        {
            static string Rolls()
            {
                var rolls = new System.Text.StringBuilder();
                for (int i = 0; i < 40; i++)
                    rolls
                        .Append(BattleOdds.Critical(0.3) ? 'C' : '-')
                        .Append(BattleOdds.Lands(50) ? 'L' : '-');
                return rolls.ToString();
            }
            try
            {
                BattleOdds.Seed(7);
                string first = Rolls();
                BattleOdds.Seed(7);
                Assert.That(Rolls(), Is.EqualTo(first));
                Assert.That(first, Does.Contain("C").And.Contain("L").And.Contain("-"));
                BattleOdds.Seed(8);
                Assert.That(Rolls(), Is.Not.EqualTo(first));
            }
            finally
            {
                BattleOdds.Fix(critical: false, lands: true);
            }
        }
    }
}
