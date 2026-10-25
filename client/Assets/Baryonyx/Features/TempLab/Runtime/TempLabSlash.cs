using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// A trial slash, sharp and fast: a star glints where the blade starts, then a straight cut
    /// shoots through the enemy from far past one side to far past the other in an instant (a
    /// thin white edge in a translucent blue band, pointed at both ends like a needle), a white
    /// flash and steel sparks where it passes; right after, the cut splits into two thin lines
    /// that open apart and fade while the band wears away from where it began. A second cut
    /// crosses the first (doc: the user's VFX guide, sword slash). A cold light flashes on each.
    /// </summary>
    public sealed class TempLabSlash : TempLabEffect
    {
        [Header("時間（秒）")]
        [Tooltip("1太刀目が走り始める時刻。それまで刃の始まる所が光る（予兆）。")]
        public float FirstCut = 0.22f;

        [Tooltip("2太刀目が走り始める時刻。")]
        public float SecondCut = 0.45f;

        [Tooltip("太刀筋が端から端まで走る長さ。")]
        public float DrawSeconds = 0.07f;

        [Header("形（m）")]
        [Tooltip("太刀筋の長さ。敵の両側の外まで届かせる。")]
        public float Length = 3.6f;

        [Header("色と光")]
        public Color EdgeColor = new(2.6f, 3f, 3.6f);
        public Color BandColor = new(0.45f, 0.75f, 1.5f);
        public Color HaloColor = new(0.08f, 0.18f, 0.45f);
        public Color LightColor = new(0.75f, 0.86f, 1f);
        public float HitLight = 1.2f;

        [Tooltip("1太刀で散る火花の数。")]
        public int SparksPerCut = 26;

        private struct Cut
        {
            public float Start;

            // 太刀筋の向き（度、画面の右を0として反時計回り）。始まりから終わりへ走る。
            public float Angle;
            public float Seed;
        }

        private struct Spark
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        private readonly List<Spark> sparks = new();
        private readonly List<Vector3> points = new();
        private readonly List<Vector3> sides = new();
        private Cut[] cuts;
        private bool[] struck;
        private TempLabBoard air;

        // 最後の太刀の筋が消えるまで。
        public override float Duration => SecondCut + DrawSeconds + 0.7f;

        protected override void Build()
        {
            air = Board("SlashGlows", GlowOrder);
        }

        protected override void OnRestart()
        {
            sparks.Clear();
            // 1太刀目は左上から右下へ、2太刀目は右上から左下へ、当たる所で交わる。
            cuts = new[]
            {
                new Cut
                {
                    Start = FirstCut,
                    Angle = -32f,
                    Seed = 0.21f,
                },
                new Cut
                {
                    Start = SecondCut,
                    Angle = -148f,
                    Seed = 0.67f,
                },
            };
            struck = new bool[cuts.Length];
        }

        private Vector3 Along(Cut cut)
        {
            float a = cut.Angle * Mathf.Deg2Rad;
            return (View.right * Mathf.Cos(a) + View.up * Mathf.Sin(a)).normalized;
        }

        // 太刀筋の上の点（0が始まり、1が終わり）。真ん中が当たる所を通る。
        private Vector3 PointOn(Cut cut, float u) =>
            Hit - View.forward * 0.3f + Along(cut) * (Length * (u - 0.5f));

        // 走り始めは最も速く、終わりで勢いが抜ける。
        private float Reach(Cut cut, float t)
        {
            float s = Mathf.Clamp01((t - cut.Start) / DrawSeconds);
            return 1f - (1f - s) * (1f - s) * (1f - s);
        }

        // 刃先が当たる所（真ん中）を通る時刻。
        private float HitTime(Cut cut) => cut.Start + (1f - Mathf.Pow(0.5f, 1f / 3f)) * DrawSeconds;

        protected override void Step(float t, float dt)
        {
            for (int i = 0; i < cuts.Length; i++)
            {
                var cut = cuts[i];
                if (struck[i] || t + 1e-4f < HitTime(cut))
                    continue;
                struck[i] = true;
                // 火花は刃の走る向きへ飛び、少し上へ跳ねる。
                var along = Along(cut);
                for (int n = 0; n < SparksPerCut; n++)
                    sparks.Add(
                        new Spark
                        {
                            Position = Hit + OnSphere() * 0.08f,
                            Velocity =
                                along * Range(3f, 8.5f)
                                + OnSphere() * 1.6f
                                + Vector3.up * Range(0.5f, 2f),
                            Life = Range(0.18f, 0.5f),
                            Size = Range(0.018f, 0.035f),
                        }
                    );
            }

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    sparks.RemoveAt(i);
                    continue;
                }
                s.Velocity += Vector3.down * (9f * dt);
                s.Velocity *= 1f - Mathf.Clamp01(2f * dt);
                s.Position += s.Velocity * dt;
                sparks[i] = s;
            }
        }

        protected override void Show(float t)
        {
            air.Clear();
            var view = View;
            float light = 0f;
            var lightAt = Hit;

            foreach (var cut in cuts)
            {
                // 予兆：刃の始まる所で星が光り、走り出す直前に最も強くなる。
                float before = cut.Start - t;
                if (before > 0f && before < 0.18f)
                {
                    float glint = Mathf.Sin(Mathf.Clamp01(1f - before / 0.18f) * Mathf.PI * 0.85f);
                    air.Billboard(
                        PointOn(cut, 0.04f),
                        view,
                        0.5f,
                        new Color(2.2f, 2.6f, 3.4f, 1f),
                        TempLabBoard.GlintShape,
                        cut.Seed,
                        new Vector4(1.4f * glint, 0.8f * glint, 26f, 0f)
                    );
                }

                float since = t - cut.Start;
                if (since < 0f)
                    continue;
                float reach = Reach(cut, t);
                float after = Mathf.Max(0f, since - DrawSeconds);
                // 走り終えたら、始まりの側から削れて消える。
                float from = 0.4f * Smooth(0.02f, 0.4f, after);
                float erode = Smooth(0.02f, 0.45f, after);
                float edgeFade = Mathf.Exp(-after * 20f);

                if (erode < 0.995f && reach > from + 0.01f)
                {
                    Line(
                        cut,
                        from,
                        reach,
                        0.28f,
                        0f,
                        HaloColor,
                        TempLabBoard.BandShape,
                        new Vector4(0.05f, 0.4f, erode, t)
                    );
                    Line(
                        cut,
                        from,
                        reach,
                        0.12f,
                        0f,
                        BandColor,
                        TempLabBoard.BandShape,
                        new Vector4(0.3f, 0.2f, erode, t)
                    );
                    if (edgeFade > 0.01f)
                        Line(
                            cut,
                            from,
                            reach,
                            0.04f,
                            0f,
                            EdgeColor * edgeFade,
                            TempLabBoard.BandShape,
                            new Vector4(0.45f, 0f, erode * 0.5f, t)
                        );
                }

                // 斬った直後、筋が2本に割れて開き、薄れて消える。
                if (after > 0f && after < 0.32f)
                {
                    float open = 0.02f + 0.22f * EaseOut(after / 0.32f);
                    var color =
                        new Color(0.6f, 0.9f, 1.8f, 1f) * (1f - Smooth(0.05f, 0.32f, after));
                    var values = new Vector4(0.45f, 0.15f, erode, t);
                    Line(cut, from, 1f, 0.025f, open, color, TempLabBoard.BandShape, values);
                    Line(cut, from, 1f, 0.025f, -open, color, TempLabBoard.BandShape, values);
                }

                // ちぎれた細い筋。太刀筋に沿って横へ流れ、少し長く残る。
                if (after > 0f && after < 0.6f)
                {
                    float wErode = Smooth(0.05f, 0.6f, after);
                    for (int w = 0; w < 2; w++)
                        Line(
                            cut,
                            0.1f + 0.1f * w,
                            0.95f - 0.1f * w,
                            0.1f,
                            (w == 0 ? 1f : -1f) * (0.12f + 0.35f * after),
                            new Color(0.4f, 0.7f, 1.5f, 1f) * 0.7f,
                            TempLabBoard.WispShape,
                            new Vector4(0.55f, 0f, wErode, t * 1.3f)
                        );
                }

                // 刃先が通った瞬間の白い閃光と冷たい光。
                float struckFor = t - HitTime(cut);
                if (struckFor >= 0f)
                {
                    float flash = Mathf.Exp(-struckFor * 18f);
                    if (flash > 0.01f)
                        air.Billboard(
                            Hit,
                            view,
                            0.6f,
                            new Color(1.4f, 1.6f, 2f, 1f),
                            TempLabBoard.GlowShape,
                            cut.Seed,
                            new Vector4(0.06f, 0.3f, 1.1f * flash, 0.18f * flash)
                        );
                    light += HitLight * Mathf.Exp(-struckFor * 10f);
                }
                else
                {
                    light += 0.6f;
                    lightAt = PointOn(cut, reach);
                }
            }

            foreach (var s in sparks)
            {
                float k = s.Age / s.Life;
                var color = EmberColor(1f - k * 0.8f) * (1f - Mathf.SmoothStep(0.7f, 1f, k));
                color.a = 1f;
                air.Streak(
                    s.Position,
                    s.Position - s.Velocity * 0.06f,
                    view,
                    s.Size,
                    color,
                    new Vector4(0.1f, 1f - k, 0f, 0f)
                );
            }
            Draw(air);
            Light(lightAt - view.forward * 0.3f, LightColor, light, 5f);
        }

        // 太刀筋の帯。from から to までを描き、幅は両端が針のように細い（線全体の位置で決める）。
        // 走っている間は刃先も細くする。offset は筋に直交する横へのずれ。
        private void Line(
            Cut cut,
            float from,
            float to,
            float half,
            float offset,
            Color color,
            int shape,
            Vector4 values
        )
        {
            const int Count = 24;
            var across = Vector3.Cross(View.forward, Along(cut)).normalized;
            points.Clear();
            sides.Clear();
            for (int i = 0; i < Count; i++)
            {
                float u = Mathf.Lerp(from, to, i / (Count - 1f));
                float needle = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f);
                float tip =
                    to >= 0.999f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((to - u) / 0.12f));
                points.Add(PointOn(cut, u) + across * offset);
                sides.Add(across * (half * needle * tip));
            }
            color.a = 1f;
            air.Strip(points, sides, color, shape, cut.Seed, values);
        }
    }
}
