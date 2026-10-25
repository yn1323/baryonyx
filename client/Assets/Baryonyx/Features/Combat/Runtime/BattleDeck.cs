using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The battle's piles of cards (doc/features/combat.md, 手札): the draw pile, drawn from the
    /// top, the discard pile, where played cards and the hand at the end of a turn go, and the
    /// cards out of the battle (消滅). A draw pile that runs dry is made anew from the discard
    /// pile alone, shuffled, so a card in the hand is never drawn twice. A fallen ally's cards
    /// leave both piles and come back into the draw pile, with those it lost to 消滅, when the
    /// ally stands up. Cards are indexes of the battle's cards.
    /// </summary>
    public sealed class BattleDeck
    {
        private readonly List<int> pile = new();
        private readonly List<int> discard = new();
        private readonly List<int> exhausted = new();

        public BattleDeck(BattleDice dice) =>
            Dice = dice ?? throw new ArgumentNullException(nameof(dice));

        public BattleDice Dice { get; set; }

        /// <summary>The cards left to draw, the top first.</summary>
        public IReadOnlyList<int> Pile => pile;

        /// <summary>The discarded cards, the last one put there last.</summary>
        public IReadOnlyList<int> DiscardPile => discard;

        /// <summary>The cards out of the battle (消滅), in the order they left.</summary>
        public IReadOnlyList<int> Exhausted => exhausted;

        /// <summary>
        /// Starts the battle's cards over: the draw pile holds the <paramref name="cards"/>
        /// cards but those <paramref name="benched"/>, shuffled, with those
        /// <paramref name="innate"/> (初手) on top, and the other piles are emptied.
        /// </summary>
        public void Renew(int cards, Func<int, bool> benched, Func<int, bool> innate = null)
        {
            pile.Clear();
            discard.Clear();
            exhausted.Clear();
            var first = new List<int>();
            for (int i = 0; i < cards; i++)
            {
                if (benched(i))
                    continue;
                if (innate != null && innate(i))
                    first.Add(i);
                else
                    pile.Add(i);
            }
            Dice.Shuffle(first);
            Dice.Shuffle(pile);
            pile.InsertRange(0, first);
        }

        /// <summary>
        /// Puts the cards in <paramref name="order"/>, top first, but those
        /// <paramref name="benched"/>, and empties the other piles.
        /// </summary>
        public void Stack(IEnumerable<int> order, Func<int, bool> benched)
        {
            pile.Clear();
            discard.Clear();
            exhausted.Clear();
            foreach (int card in order)
                if (!benched(card))
                    pile.Add(card);
        }

        /// <summary>
        /// Takes the top card. An empty draw pile is first made anew from the discard pile,
        /// shuffled, without the cards <paramref name="benched"/>. Returns -1 when both are empty.
        /// </summary>
        public int Draw(Func<int, bool> benched)
        {
            if (pile.Count == 0)
                Reshuffle(benched);
            if (pile.Count == 0)
                return -1;
            int top = pile[0];
            pile.RemoveAt(0);
            return top;
        }

        /// <summary>
        /// Takes the card nearest the top that <paramref name="wanted"/>, without making the
        /// draw pile anew. Returns -1 when there is none.
        /// </summary>
        public int DrawFirst(Predicate<int> wanted)
        {
            int at = pile.FindIndex(wanted);
            if (at < 0)
                return -1;
            int card = pile[at];
            pile.RemoveAt(at);
            return card;
        }

        /// <summary>Puts <paramref name="card"/> on the discard pile.</summary>
        public void Discard(int card) => discard.Add(card);

        /// <summary>Takes <paramref name="card"/> out of the battle (消滅).</summary>
        public void Exhaust(int card) => exhausted.Add(card);

        /// <summary>Takes <paramref name="card"/> back off the discard pile; false when it is not there.</summary>
        public bool TakeFromDiscard(int card) => discard.Remove(card);

        /// <summary>
        /// Takes the cards <paramref name="leaving"/> out of the draw and discard piles (a fallen
        /// ally's). Those out of the battle stay out.
        /// </summary>
        public void TakeOut(Predicate<int> leaving)
        {
            pile.RemoveAll(leaving);
            discard.RemoveAll(leaving);
        }

        /// <summary>Puts <paramref name="card"/> back into the draw pile at a place at random.</summary>
        public void ShuffleIn(int card) => pile.Insert(Dice.Below(pile.Count + 1), card);

        /// <summary>
        /// Brings the cards <paramref name="returning"/> (an ally who stands up again) back into
        /// the draw pile at places at random: <paramref name="cards"/> are every card of the
        /// battle, and those out of the battle come back too.
        /// </summary>
        public void Return(int cards, Predicate<int> returning)
        {
            exhausted.RemoveAll(returning);
            for (int i = 0; i < cards; i++)
                if (returning(i) && !pile.Contains(i) && !discard.Contains(i))
                    ShuffleIn(i);
        }

        private void Reshuffle(Func<int, bool> benched)
        {
            foreach (int card in discard)
                if (!benched(card))
                    pile.Add(card);
            discard.Clear();
            Dice.Shuffle(pile);
        }
    }
}
