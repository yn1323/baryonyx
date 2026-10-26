using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The rules of the tap sparkle, apart from input and drawing (doc/features/screens/common.md):
    /// a touch shows a sign at once; the sign thins away over <see cref="CancelFade"/> when the finger
    /// moves over <see cref="Slop"/> design pixels, rests for <see cref="Hold"/>, or a second finger
    /// touches; lifting the finger without that bursts the sparkle where it touched. At most
    /// <see cref="MaxBursts"/> bursts show at once, the oldest giving way. Times are the caller's
    /// clock in seconds.
    /// </summary>
    public sealed class TapSparkleGestures
    {
        public const float Slop = 24f;
        public const float Hold = 0.5f;
        public const float CancelFade = 0.1f;
        public const int MaxBursts = 4;

        public enum Phase
        {
            Sign,
            Fading,
            Burst,
        }

        public sealed class Spark
        {
            public Vector2 At;
            public Phase Phase;

            /// <summary>When the sign showed, or when it burst.</summary>
            public float Start;

            /// <summary>When the sign began to thin away.</summary>
            public float FadeStart;

            /// <summary>The look's own random values, made once per touch.</summary>
            public object State;
        }

        private sealed class Finger
        {
            public Vector2 Start;
            public Spark Spark;
        }

        private readonly List<Spark> sparks = new();
        private readonly Dictionary<int, Finger> fingers = new();
        private readonly System.Func<object> newState;
        private readonly float burstSeconds;

        /// <param name="burstSeconds">How long a burst lasts before it is gone.</param>
        /// <param name="newState">Makes a new spark's own random values.</param>
        public TapSparkleGestures(float burstSeconds, System.Func<object> newState = null)
        {
            this.burstSeconds = burstSeconds;
            this.newState = newState;
        }

        /// <summary>The sparks showing, oldest first.</summary>
        public IReadOnlyList<Spark> Sparks => sparks;

        /// <summary>How long a burst lasts.</summary>
        public float BurstSeconds => burstSeconds;

        /// <summary>A finger touched. A second finger calls off every sign and shows none of its own.</summary>
        public void Down(int finger, Vector2 at, float now)
        {
            if (fingers.Count > 0)
            {
                foreach (var other in fingers.Values)
                    Fade(other.Spark, now);
                fingers[finger] = new Finger { Start = at };
                return;
            }
            var spark = new Spark
            {
                At = at,
                Phase = Phase.Sign,
                Start = now,
                State = newState?.Invoke(),
            };
            sparks.Add(spark);
            fingers[finger] = new Finger { Start = at, Spark = spark };
        }

        public void Move(int finger, Vector2 at, float now)
        {
            if (
                fingers.TryGetValue(finger, out var held)
                && Vector2.Distance(at, held.Start) > Slop
            )
                Fade(held.Spark, now);
        }

        /// <summary>A finger lifted (or the touch was lost, with <paramref name="cancelled"/>).</summary>
        public void Up(int finger, float now, bool cancelled = false)
        {
            if (!fingers.TryGetValue(finger, out var held))
                return;
            fingers.Remove(finger);
            var spark = held.Spark;
            if (spark == null || spark.Phase != Phase.Sign)
                return;
            if (cancelled)
            {
                Fade(spark, now);
                return;
            }
            var bursts = 0;
            foreach (var other in sparks)
                if (other.Phase == Phase.Burst)
                    bursts++;
            if (bursts >= MaxBursts)
                sparks.Remove(sparks.Find(other => other.Phase == Phase.Burst));
            spark.Phase = Phase.Burst;
            spark.Start = now;
        }

        /// <summary>Calls off resting signs and takes away the sparks that are over.</summary>
        public void Tick(float now)
        {
            foreach (var held in fingers.Values)
                if (held.Spark != null && now - held.Spark.Start >= Hold)
                    Fade(held.Spark, now);
            sparks.RemoveAll(spark =>
                spark.Phase == Phase.Fading
                    ? now - spark.FadeStart >= CancelFade
                    : spark.Phase == Phase.Burst && now - spark.Start >= burstSeconds
            );
        }

        /// <summary>How much of a fading sign is left (1 for a sign that is not fading).</summary>
        public static float SignLeft(Spark spark, float now) =>
            spark.Phase == Phase.Fading
                ? Mathf.Clamp01(1f - (now - spark.FadeStart) / CancelFade)
                : 1f;

        /// <summary>Lets go of every finger and spark.</summary>
        public void Clear()
        {
            sparks.Clear();
            fingers.Clear();
        }

        private static void Fade(Spark spark, float now)
        {
            if (spark == null || spark.Phase != Phase.Sign)
                return;
            spark.Phase = Phase.Fading;
            spark.FadeStart = now;
        }
    }
}
