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
        public void TheEnemiesFasterThanThePartyActBeforeItsTurnAndTheSlowerAfter()
        {
            // The slime (39), the wolf (95), nobody, and a bat three times the party's speed.
            var speeds = new double[] { 39, 95, 0, 210 };
            // 戦闘の最初は、パーティより速いこうもりと狼から。
            Assert.That(CombatTurnOrder.Opening(70, speeds), Is.EqualTo(new[] { 3, 1 }));
            // 味方の番のあとは、遅いスライム、2回動くこうもり、次のラウンドのこうもりと狼。
            Assert.That(CombatTurnOrder.After(1, 70, speeds), Is.EqualTo(new[] { 0, 3, 3, 1 }));
        }

        [Test]
        public void ASpeedUpPutsThePartyBeforeAFasterEnemy()
        {
            var speeds = new double[] { 80 };
            Assert.That(CombatTurnOrder.Opening(70, speeds), Is.EqualTo(new[] { 0 }));
            Assert.That(CombatTurnOrder.Opening(70 * 1.3, speeds), Is.Empty);
            Assert.That(CombatTurnOrder.After(1, 70 * 1.3, speeds), Is.EqualTo(new[] { 0 }));
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
            Assert.That(CombatTurnOrder.PartySpeed(new[] { 60.0, 80, 70, 70 }), Is.EqualTo(70));
            Assert.That(CombatTurnOrder.PartySpeed(new double[0]), Is.EqualTo(0));
        }
    }
}
