using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class CombatTurnOrderTests
    {
        [Test]
        public void EnemiesNotFarFasterOrSlowerActOnceAfterEachPartyTurn()
        {
            Assert.That(CombatTurnOrder.ActionsAfter(1, 70, 95), Is.EqualTo(1));
            Assert.That(CombatTurnOrder.ActionsAfter(2, 70, 30), Is.EqualTo(1));
            Assert.That(CombatTurnOrder.ActionsAfter(3, 70, 30), Is.EqualTo(1));
        }

        [Test]
        public void AnEnemyTwoAndAHalfTimesAsFastActsTwice()
        {
            Assert.That(CombatTurnOrder.ActionsAfter(1, 40, 100), Is.EqualTo(2));
            Assert.That(CombatTurnOrder.ActionsAfter(2, 40, 100), Is.EqualTo(2));
        }

        [Test]
        public void AnEnemyThatSlowLetsThePartyActTwiceInARow()
        {
            Assert.That(CombatTurnOrder.ActionsAfter(1, 100, 40), Is.EqualTo(0));
            Assert.That(CombatTurnOrder.ActionsAfter(2, 100, 40), Is.EqualTo(1));
            Assert.That(CombatTurnOrder.ActionsAfter(3, 100, 40), Is.EqualTo(0));
        }

        [Test]
        public void WithoutSpeedsEveryEnemyActsOnce()
        {
            Assert.That(CombatTurnOrder.ActionsAfter(1, 0, 40), Is.EqualTo(1));
            Assert.That(CombatTurnOrder.ActionsAfter(1, 70, 0), Is.EqualTo(1));
        }

        [Test]
        public void TheFasterEnemiesActFirstAndTheVeryFastAgainAtTheEnd()
        {
            // The slime (39), the wolf (95), nobody, and a bat three times the party's speed.
            var order = CombatTurnOrder.After(1, 70, new double[] { 39, 95, 0, 210 });
            Assert.That(order, Is.EqualTo(new[] { 3, 1, 0, 3 }));
        }

        [Test]
        public void EnemiesAsFastGoByPlace()
        {
            var order = CombatTurnOrder.After(1, 70, new double[] { 60, 60, 60 });
            Assert.That(order, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void ThePartysSpeedIsTheAverageOfItsMembers()
        {
            Assert.That(CombatTurnOrder.PartySpeed(new[] { 60, 80, 70, 70 }), Is.EqualTo(70));
            Assert.That(CombatTurnOrder.PartySpeed(new int[0]), Is.EqualTo(0));
        }
    }
}
