using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 戦闘で運に任せるところの乱数。種を決めれば、いつも同じ目が出る。
    public sealed class BattleDiceTests
    {
        [Test]
        public void TheSameSeedRollsTheSameNumbers()
        {
            var first = BattleDice.Seeded(7);
            var second = BattleDice.Seeded(7);
            var rolls = Enumerable.Range(0, 20).Select(_ => first.Below(6)).ToArray();
            Assert.That(rolls, Is.EqualTo(Enumerable.Range(0, 20).Select(_ => second.Below(6))));
            Assert.That(rolls, Has.All.InRange(0, 5));
        }

        [Test]
        public void AChanceComesTrueWhenTheRollIsUnderIt()
        {
            var dice = new BattleDice(_ => 29);
            Assert.That(dice.Chance(30), Is.True);
            Assert.That(dice.Chance(29), Is.False);
            Assert.That(dice.Chance(0), Is.False, "Never at no chance.");
            Assert.That(new BattleDice(_ => 99).Chance(100), Is.True, "Always at a hundred.");
        }

        [Test]
        public void APickIsTheItemTheRollPoints()
        {
            var dice = new BattleDice(count => count - 1);
            Assert.That(dice.Pick(new[] { 4, 2, 9 }), Is.EqualTo(9));
        }

        [Test]
        public void AShuffleKeepsEveryItemOnce()
        {
            var cards = Enumerable.Range(0, 16).ToList();
            BattleDice.Seeded(3).Shuffle(cards);
            Assert.That(cards, Is.EquivalentTo(Enumerable.Range(0, 16)));
            Assert.That(cards, Is.Not.EqualTo(Enumerable.Range(0, 16)), "The order changes.");
        }
    }
}
