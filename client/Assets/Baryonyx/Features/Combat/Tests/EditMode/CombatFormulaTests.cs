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
    }
}
