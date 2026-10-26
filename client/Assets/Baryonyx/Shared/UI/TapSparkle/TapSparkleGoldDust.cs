using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// The look of the tap sparkle, "gold dust" (the fourth of the six trials, chosen on 2026-10-26):
    /// while the finger rests, fine gold dust circles it slowly; a tap flicks 22 grains outwards,
    /// which the air slows at once and which then drift down, twinkling, and go. Numbers are in the
    /// design's pixels and seconds, at the size of the browser trial
    /// (<see cref="TapSparkleLayer.Size"/> scales it on screen).
    /// </summary>
    public static class TapSparkleGoldDust
    {
        /// <summary>How long the burst lasts (the last grain goes by then).</summary>
        public const float Duration = 0.75f;

        private const int Grains = 22;
        private const int Orbiters = 9;
        private const float Tau = Mathf.PI * 2f;

        // 空気の抵抗（大きいほど早く止まる）と、落ちる重さ（設計座標の画素/秒²の半分）。
        private const float Drag = 6.5f;
        private const float Fall = 210f;

        private static readonly Color Gold = new(1f, 214f / 255f, 102f / 255f);
        private static readonly Color Pale = new(1f, 241f / 255f, 196f / 255f);
        private static readonly Color White = new(1f, 252f / 255f, 242f / 255f);

        private sealed class State
        {
            public readonly float[] Angle = new float[Grains];
            public readonly float[] Speed = new float[Grains];
            public readonly float[] Life = new float[Grains];
            public readonly float[] Size = new float[Grains];
            public readonly float[] Phase = new float[Grains];
            public readonly Color[] Tint = new Color[Grains];
            public readonly float[] OrbitAngle = new float[Orbiters];
            public readonly float[] OrbitRadius = new float[Orbiters];
            public readonly float[] OrbitSpeed = new float[Orbiters];
            public readonly float[] OrbitSize = new float[Orbiters];
            public readonly float[] OrbitPhase = new float[Orbiters];
        }

        /// <summary>One touch's own random values.</summary>
        public static object NewState(TapRandom r)
        {
            var tints = new[] { Gold, Pale, White, Gold };
            var s = new State();
            for (int i = 0; i < Grains; i++)
            {
                s.Angle[i] = r.Next() * Tau;
                s.Speed[i] = 380f + r.Next() * 620f;
                s.Life[i] = 0.42f + r.Next() * 0.3f;
                s.Size[i] = 1.4f + r.Next() * 2.8f;
                s.Phase[i] = r.Next() * Tau;
                s.Tint[i] = tints[(int)(r.Next() * 4f) % 4];
            }
            for (int i = 0; i < Orbiters; i++)
            {
                s.OrbitAngle[i] = r.Next() * Tau;
                s.OrbitRadius[i] = 52f + r.Next() * 26f;
                s.OrbitSpeed[i] = (r.Next() < 0.5f ? -1f : 1f) * (1f + r.Next() * 1.2f);
                s.OrbitSize[i] = 1.5f + r.Next() * 1.5f;
                s.OrbitPhase[i] = r.Next() * Tau;
            }
            return s;
        }

        /// <summary>The sign while the finger rests; <paramref name="left"/> thins it out when the touch is called off.</summary>
        public static void DrawSign(TapSparkleMarks m, object state, float age, float left)
        {
            var s = (State)state;
            var a = Mathf.Clamp01(age / 0.1f) * left;
            m.Glow(0f, 0f, 72f, Gold, 0.1f * a);
            for (int i = 0; i < Orbiters; i++)
            {
                float angle = s.OrbitAngle[i] + age * s.OrbitSpeed[i],
                    radius = s.OrbitRadius[i] * (0.7f + 0.3f * left),
                    x = Mathf.Cos(angle) * radius,
                    y = Mathf.Sin(angle) * radius,
                    twinkle = 0.5f + 0.5f * Mathf.Sin(age * 9f + s.OrbitPhase[i]);
                m.Glow(x, y, s.OrbitSize[i] * 4f, Gold, 0.35f * a * twinkle);
                m.Dot(x, y, s.OrbitSize[i] * 0.7f, Pale, 0.9f * a * twinkle);
            }
        }

        /// <summary>The burst after a tap, <paramref name="age"/> seconds in.</summary>
        public static void DrawBurst(TapSparkleMarks m, object state, float age)
        {
            var s = (State)state;
            if (age >= Duration)
                return;
            // 離した瞬間の白い閃き（0.035秒で最も明るく、0.08秒で消える）。
            var flash = age < 0.035f ? age / 0.035f : Mathf.Max(0f, 1f - (age - 0.035f) / 0.08f);
            m.Glow(0f, 0f, 60f, White, 0.6f * flash);
            for (int i = 0; i < Grains; i++)
            {
                if (age >= s.Life[i])
                    continue;
                // 空気で急に遅くなり（指数の減速）、重さでゆっくり落ちる。
                float d = 26f + s.Speed[i] / Drag * (1f - Mathf.Exp(-Drag * age)),
                    x = Mathf.Cos(s.Angle[i]) * d,
                    y = Mathf.Sin(s.Angle[i]) * d + Fall * age * age,
                    u = age / s.Life[i],
                    twinkle = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(age * 26f + s.Phase[i])),
                    fade = Mathf.Pow(1f - u, 1.1f) * twinkle;
                m.Glow(x, y, s.Size[i] * 3.5f, s.Tint[i], 0.4f * fade);
                m.Dot(
                    x,
                    y,
                    s.Size[i] * 0.75f * (1f - u * 0.5f),
                    s.Tint[i] == Gold ? Pale : White,
                    fade
                );
            }
        }
    }

    /// <summary>A small repeatable random source (mulberry32), so a seed draws the same sparkle again.</summary>
    public sealed class TapRandom
    {
        private uint state;

        public TapRandom(uint seed) => state = seed;

        /// <summary>The next value from 0 up to (not including) 1.</summary>
        public float Next()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return ((t ^ (t >> 14)) >> 8) / 16777216f;
            }
        }
    }
}
