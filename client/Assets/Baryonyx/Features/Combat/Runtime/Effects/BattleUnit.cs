using System;

namespace Baryonyx.Combat
{
    /// <summary>
    /// One character of a battle: an ally by its place on the field, or an enemy by its index.
    /// <see cref="None"/> stands for no one (a happening without a target).
    /// </summary>
    public readonly struct BattleUnit : IEquatable<BattleUnit>
    {
        public static readonly BattleUnit None = new(true, -1);

        private BattleUnit(bool ally, int index)
        {
            IsAlly = ally;
            Index = index;
        }

        /// <summary>True for an ally of the party, false for an enemy.</summary>
        public bool IsAlly { get; }

        /// <summary>The ally's place, or the enemy's index; -1 for <see cref="None"/>.</summary>
        public int Index { get; }

        public bool IsNone => Index < 0;
        public bool IsEnemy => !IsNone && !IsAlly;

        public static BattleUnit Ally(int place) => place < 0 ? None : new BattleUnit(true, place);

        public static BattleUnit Enemy(int index) =>
            index < 0 ? None : new BattleUnit(false, index);

        public bool Equals(BattleUnit other) =>
            (IsNone && other.IsNone) || (IsAlly == other.IsAlly && Index == other.Index);

        public override bool Equals(object obj) => obj is BattleUnit other && Equals(other);

        public override int GetHashCode() => IsNone ? -1 : (Index << 1) | (IsAlly ? 1 : 0);

        public static bool operator ==(BattleUnit a, BattleUnit b) => a.Equals(b);

        public static bool operator !=(BattleUnit a, BattleUnit b) => !a.Equals(b);

        public override string ToString() =>
            IsNone ? "none"
            : IsAlly ? $"ally {Index}"
            : $"enemy {Index}";
    }
}
