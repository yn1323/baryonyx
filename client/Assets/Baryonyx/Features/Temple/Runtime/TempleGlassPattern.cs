using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Temple
{
    /// <summary>
    /// How a window breaks (doc/features/screens/temple.md, the summoning's presentation): a glass plate
    /// hit at one point cracks in lines that run out from it, crossed by arcs around it. The
    /// pattern holds the radial lines' angles and, for each sector between two lines, the radii of
    /// three arcs, in a window of radius 1. The shader draws the cracks from these values, and the
    /// shards are the cells they cut, so the window breaks along its cracks.
    /// </summary>
    public sealed class TempleGlassPattern
    {
        public const int MaxAngles = 16;
        public const int RingsPerSector = 3;

        // 弧の半径の目安（窓の半径を1として）。扇ごとにずらす。
        private static readonly float[] RingBase = { 0.2f, 0.45f, 0.72f };

        // 弧を折れ線にするときの、1つの破片の弧の分け方。
        private const int ArcSteps = 4;

        /// <summary>One shard: its outline in the window (radius 1), and where it sits.</summary>
        public readonly struct Shard
        {
            public Shard(Vector2[] outline, int sector, int band)
            {
                Outline = outline;
                Sector = sector;
                Band = band;
                var sum = Vector2.zero;
                foreach (var point in outline)
                    sum += point;
                Centroid = sum / outline.Length;
            }

            public Vector2[] Outline { get; }
            public Vector2 Centroid { get; }
            public int Sector { get; }

            // 中心から何番目の帯か（0が中心）。
            public int Band { get; }
        }

        public TempleGlassPattern(int seed, int lines = 12)
        {
            var random = new System.Random(seed);
            lines = Mathf.Clamp(lines, 3, MaxAngles);
            var angles = new float[lines];
            float step = Mathf.PI * 2f / lines;
            float start = (float)random.NextDouble() * step;
            for (int i = 0; i < lines; i++)
                angles[i] = Mathf.Repeat(
                    start + step * (i + ((float)random.NextDouble() - 0.5f) * 0.55f),
                    Mathf.PI * 2f
                );
            Array.Sort(angles);
            Angles = angles;
            var rings = new float[lines * RingsPerSector];
            for (int i = 0; i < lines; i++)
            for (int m = 0; m < RingsPerSector; m++)
                rings[i * RingsPerSector + m] =
                    RingBase[m] * (0.85f + 0.3f * (float)random.NextDouble());
            Rings = rings;
            Shards = BuildShards();
        }

        /// <summary>The radial lines' angles (0 to 2π, smallest first).</summary>
        public IReadOnlyList<float> Angles { get; }

        /// <summary>For sector i (between lines i and i+1), its arcs' radii at i*3 to i*3+2.</summary>
        public IReadOnlyList<float> Rings { get; }

        public IReadOnlyList<Shard> Shards { get; }

        /// <summary>Hands the pattern to a material of the Temple Summon Glass shader.</summary>
        public void Apply(Material material)
        {
            if (material == null)
                return;
            var angles = new float[MaxAngles];
            var rings = new float[MaxAngles * RingsPerSector];
            for (int i = 0; i < Angles.Count; i++)
                angles[i] = Angles[i];
            for (int i = 0; i < Rings.Count; i++)
                rings[i] = Rings[i];
            material.SetFloatArray("_Angles", angles);
            material.SetFloatArray("_Rings", rings);
            material.SetFloat("_AngleCount", Angles.Count);
        }

        // 扇ごとに、中心から外へ4つの帯（中心〜弧1、弧1〜弧2、弧2〜弧3、弧3〜窓の縁）に分ける。
        private List<Shard> BuildShards()
        {
            var shards = new List<Shard>();
            int count = Angles.Count;
            for (int i = 0; i < count; i++)
            {
                float a0 = Angles[i];
                float a1 = i + 1 < count ? Angles[i + 1] : Angles[0] + Mathf.PI * 2f;
                float inner = 0f;
                for (int band = 0; band <= RingsPerSector; band++)
                {
                    float outer = band < RingsPerSector ? Rings[i * RingsPerSector + band] : 1f;
                    var outline = new List<Vector2>();
                    for (int s = 0; s <= ArcSteps; s++)
                        outline.Add(Polar(outer, Mathf.Lerp(a0, a1, s / (float)ArcSteps)));
                    if (inner <= 0f)
                        outline.Add(Vector2.zero);
                    else
                        for (int s = ArcSteps; s >= 0; s--)
                            outline.Add(Polar(inner, Mathf.Lerp(a0, a1, s / (float)ArcSteps)));
                    shards.Add(new Shard(outline.ToArray(), i, band));
                    inner = outer;
                }
            }
            return shards;
        }

        private static Vector2 Polar(float radius, float angle) =>
            new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
    }
}
