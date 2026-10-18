using System;
using System.Collections.Generic;

namespace Baryonyx.Combat
{
    /// <summary>A weakness of a kind of enemy that a card has hit (doc/features/combat.md).</summary>
    public readonly struct RevealedWeakness : IEquatable<RevealedWeakness>
    {
        public RevealedWeakness(string enemyId, CardElement element)
        {
            EnemyId = enemyId ?? "";
            Element = element;
        }

        public string EnemyId { get; }
        public CardElement Element { get; }

        public bool Equals(RevealedWeakness other) =>
            EnemyId == other.EnemyId && Element == other.Element;

        public override bool Equals(object obj) => obj is RevealedWeakness other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(EnemyId, (int)Element);
    }

    /// <summary>
    /// The weaknesses a player has revealed, by kind of enemy. They are only ever added: a
    /// revealed weakness is never hidden again.
    /// </summary>
    public sealed class RevealedWeaknesses
    {
        private readonly HashSet<RevealedWeakness> known = new();
        private readonly List<RevealedWeakness> order = new();

        public int Count => order.Count;

        /// <summary>The weaknesses in the order they were revealed.</summary>
        public IReadOnlyList<RevealedWeakness> All => order;

        public bool Contains(string enemyId, CardElement element) =>
            known.Contains(new RevealedWeakness(enemyId, element));

        /// <summary>Adds a weakness; false when it was revealed already or names no enemy or element.</summary>
        public bool Add(string enemyId, CardElement element)
        {
            if (string.IsNullOrEmpty(enemyId) || element == CardElement.None)
                return false;
            var weakness = new RevealedWeakness(enemyId, element);
            if (!known.Add(weakness))
                return false;
            order.Add(weakness);
            return true;
        }

        public void AddRange(IEnumerable<RevealedWeakness> weaknesses)
        {
            if (weaknesses == null)
                return;
            foreach (var weakness in weaknesses)
                Add(weakness.EnemyId, weakness.Element);
        }

        public void Clear()
        {
            known.Clear();
            order.Clear();
        }
    }
}
