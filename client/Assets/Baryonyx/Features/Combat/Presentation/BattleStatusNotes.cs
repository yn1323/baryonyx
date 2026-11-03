using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// When and how the notes of the statuses put on a character show (doc/features/screens/battle-deck.md,
    /// 状態と浮かぶ文字): small names on dark plates that come out of the character's middle a
    /// beat after the blow's number, one after another, and glide up and a little to the right at
    /// one speed, fading in at the start and out at the end. They leave at the same speed one
    /// gap apart, so they never run into one another.
    /// </summary>
    public sealed class BattleStatusNotes
    {
        /// <summary>How long after its blow's number the first note comes out.</summary>
        public const float Lead = 0.2f;

        /// <summary>The time between two notes over one character.</summary>
        public const float Gap = 0.25f;

        /// <summary>How long a note shows, and how long it takes to fade in and out.</summary>
        public const float Life = 0.75f;
        public const float FadeIn = 0.12f;
        public const float FadeOut = 0.25f;

        /// <summary>How fast a note rises (per second) and how much it leans right on the way.</summary>
        public const float Speed = 160f;
        public const float Lean = 0.35f;

        private readonly Dictionary<Object, float> free = new();

        /// <summary>
        /// When a note over <paramref name="anchor"/>, asked for at <paramref name="at"/> (when its
        /// blow's number shows), comes out: <see cref="Lead"/> later, or a <see cref="Gap"/> after
        /// the note before it.
        /// </summary>
        public float Start(Object anchor, float at)
        {
            float start = at + Lead;
            if (free.TryGetValue(anchor, out float next))
                start = Mathf.Max(start, next);
            free[anchor] = start + Gap;
            return start;
        }

        /// <summary>How far a note has moved from where it came out, <paramref name="age"/> s after.</summary>
        public static Vector2 Offset(float age) =>
            new Vector2(Lean, 1f) * (Speed * Mathf.Clamp(age, 0f, Life));

        /// <summary>How clear a note is, <paramref name="age"/> s after it came out.</summary>
        public static float Alpha(float age)
        {
            if (age <= 0f || age >= Life)
                return 0f;
            return Mathf.Min(1f, age / FadeIn, (Life - age) / FadeOut);
        }
    }
}
