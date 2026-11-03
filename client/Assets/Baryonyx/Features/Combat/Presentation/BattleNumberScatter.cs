using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Scatters the numbers of blows landing one after another on one character (a card's ×4),
    /// so each pops up in a place of its own instead of on top of the one before: the first
    /// where a number starts, the next to its upper right, then its lower left, then under it,
    /// each moved a little at random. They go lower as well as higher, as a number under
    /// another character's HP bar cannot go up and would land on the one beside it. Once they
    /// have all gone, the next starts in the middle again. Its own dice leave the battle's
    /// random numbers as they are.
    /// </summary>
    public sealed class BattleNumberScatter
    {
        // 2つめから順にずらす先（上が正）と、その揺らぎ。4つより多ければくり返す。
        public static readonly Vector2[] Slots = { new(76f, 16f), new(-76f, -20f), new(8f, -50f) };
        public const float Jitter = 12f;

        private readonly Dictionary<Object, List<float>> shown = new();
        private readonly System.Random dice;

        public BattleNumberScatter(int seed = 1) => dice = new System.Random(seed);

        /// <summary>
        /// How far from its start a number over <paramref name="anchor"/>, showing from
        /// <paramref name="now"/> for <paramref name="duration"/>, pops up.
        /// </summary>
        public Vector2 Place(Object anchor, float now, float duration)
        {
            if (!shown.TryGetValue(anchor, out var until))
                shown[anchor] = until = new List<float>();
            until.RemoveAll(end => end <= now);
            int count = until.Count;
            until.Add(now + duration);
            if (count == 0)
                return Vector2.zero;
            var slot = Slots[(count - 1) % Slots.Length];
            return slot + new Vector2(Shake(), Shake());
        }

        private float Shake() => Mathf.Lerp(-Jitter, Jitter, (float)dice.NextDouble());
    }
}
