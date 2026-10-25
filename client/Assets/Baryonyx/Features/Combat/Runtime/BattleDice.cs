using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>
    /// The luck of a battle: shuffling the deck, picking a target at random and things that
    /// happen at a chance. The battle screen rolls Unity's random numbers with it; a test hands
    /// in dice with a seed, so the same battle goes the same way.
    /// </summary>
    public sealed class BattleDice
    {
        private readonly Func<int, int> below;

        /// <param name="below">A whole number from 0 up to, but not including, the one given.</param>
        public BattleDice(Func<int, int> below) =>
            this.below = below ?? throw new ArgumentNullException(nameof(below));

        /// <summary>Dice that roll the same numbers for the same seed.</summary>
        public static BattleDice Seeded(int seed) => new(new Random(seed).Next);

        /// <summary>A whole number from 0 up to, but not including, <paramref name="count"/>.</summary>
        public int Below(int count) => below(count);

        /// <summary>True <paramref name="percent"/> times in a hundred.</summary>
        public bool Chance(int percent) => Below(100) < percent;

        /// <summary>One of <paramref name="items"/>, each as likely.</summary>
        public T Pick<T>(IReadOnlyList<T> items) => items[Below(items.Count)];

        /// <summary>Puts <paramref name="items"/> in an order at random, every order as likely.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Below(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
