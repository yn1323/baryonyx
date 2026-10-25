using System.Linq;
using Baryonyx.Combat;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    // 戦闘の山札・捨て札・消滅：上から引き、山札が尽きると捨て札だけを混ぜて作り直す。
    // 倒れた味方のカードは山札と捨て札から抜け、起き上がると消滅した分も山札のどこかへ戻る。
    public sealed class BattleDeckTests
    {
        private static bool NoneBenched(int card) => false;

        [Test]
        public void ADeckDealtAnewHoldsEveryCardButTheBenchedShuffled()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            deck.Renew(16, card => card % 4 == 0);
            Assert.That(
                deck.Pile,
                Is.EquivalentTo(Enumerable.Range(0, 16).Where(card => card % 4 != 0))
            );
            var again = new BattleDeck(BattleDice.Seeded(1));
            again.Renew(16, card => card % 4 == 0);
            Assert.That(deck.Pile, Is.EqualTo(again.Pile), "The same seed shuffles alike.");
        }

        [Test]
        public void InnateCardsAreDealtAnewOnTopAndTheOtherPilesEmptied()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            deck.Stack(new[] { 0, 1 }, NoneBenched);
            deck.Draw(NoneBenched);
            deck.Discard(0);
            deck.Exhaust(1);

            deck.Renew(10, NoneBenched, card => card == 3 || card == 7);
            Assert.That(deck.Pile.Take(2), Is.EquivalentTo(new[] { 3, 7 }));
            Assert.That(deck.Pile, Is.EquivalentTo(Enumerable.Range(0, 10)));
            Assert.That(deck.DiscardPile, Is.Empty);
            Assert.That(deck.Exhausted, Is.Empty);
        }

        [Test]
        public void ADryDeckIsMadeAnewFromTheDiscardPileAlone()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            deck.Stack(new[] { 2, 0, 1, 3 }, NoneBenched);
            Assert.That(deck.Draw(NoneBenched), Is.EqualTo(2));
            Assert.That(deck.Draw(NoneBenched), Is.EqualTo(0));
            Assert.That(deck.Draw(NoneBenched), Is.EqualTo(1));
            Assert.That(deck.Draw(NoneBenched), Is.EqualTo(3));
            Assert.That(deck.Pile, Is.Empty);

            // 2 was played, 0 flowed away at the turn's end, 1 left the battle and 3 is in hand.
            deck.Discard(2);
            deck.Discard(0);
            deck.Exhaust(1);
            int next = deck.Draw(NoneBenched);
            Assert.That(new[] { next }.Concat(deck.Pile), Is.EquivalentTo(new[] { 0, 2 }));
            Assert.That(deck.DiscardPile, Is.Empty);
            Assert.That(deck.Exhausted, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void WithBothPilesEmptyNothingIsDrawn()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            Assert.That(deck.Draw(NoneBenched), Is.EqualTo(-1));
            deck.Discard(4);
            Assert.That(deck.Draw(_ => true), Is.EqualTo(-1), "A benched card is not dealt anew.");
        }

        [Test]
        public void TheFirstWantedCardCanBeDrawnFromWithin()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            deck.Stack(new[] { 5, 2, 8, 4 }, NoneBenched);
            Assert.That(deck.DrawFirst(card => card % 2 == 0), Is.EqualTo(2));
            Assert.That(deck.DrawFirst(card => card > 9), Is.EqualTo(-1));
            Assert.That(deck.Pile, Is.EqualTo(new[] { 5, 8, 4 }));
        }

        [Test]
        public void AStackedOrderLeavesTheBenchedOut()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            deck.Stack(new[] { 3, 1, 2, 0 }, card => card == 1);
            Assert.That(deck.Pile, Is.EqualTo(new[] { 3, 2, 0 }));
        }

        [Test]
        public void AFallenAllysCardsLeaveAndComeBackSomewhereWithThoseOutOfTheBattle()
        {
            var deck = new BattleDeck(BattleDice.Seeded(1));
            deck.Stack(Enumerable.Range(0, 6), NoneBenched);
            deck.Draw(NoneBenched);
            deck.Draw(NoneBenched);
            deck.Discard(0);
            deck.Exhaust(1);

            deck.TakeOut(card => card % 2 == 1);
            Assert.That(deck.Pile, Is.EqualTo(new[] { 2, 4 }));
            Assert.That(deck.DiscardPile, Is.EqualTo(new[] { 0 }));
            Assert.That(deck.Exhausted, Is.EqualTo(new[] { 1 }), "Out of the battle stays out.");

            deck.Return(6, card => card % 2 == 1);
            Assert.That(deck.Pile, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(deck.Exhausted, Is.Empty);
            // The cards standing keep their order among themselves.
            Assert.That(deck.Pile.Where(card => card % 2 == 0), Is.EqualTo(new[] { 2, 4 }));
        }
    }
}
