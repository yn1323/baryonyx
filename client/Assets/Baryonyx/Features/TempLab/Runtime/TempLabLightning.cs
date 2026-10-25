using System.Collections.Generic;
using Baryonyx.Vfx.Hd2d;
using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// A trial lightning strike, sudden and jagged: the stage's sun dims (the land keeps its
    /// colours), a faint leader steps down from the sky while thin streamers rise from the enemy,
    /// then the return stroke strikes it, a branching bolt with fixed ends (a white core in a blue
    /// glow), a flash, arcs crawling out over the ground, a ring, molten sparks and dust; the
    /// bolt strikes again at
    /// uneven moments, each a new shape held for an instant, leaves a fading purple channel, and
    /// small sparkles keep flickering on the stunned enemy (doc: the user's VFX guide, lightning).
    /// </summary>
    public sealed class TempLabLightning : TempLabEffect
    {
        private const int DustPool = 5;

        [Header("時間（秒）")]
        [Tooltip("主放電が落ちる時刻。それまで先駆放電が空から降りる（予兆）。")]
        public float StrikeAt = 0.4f;

        [Tooltip("再放電の時刻。間隔をそろえない。")]
        public float[] Restrikes = { 0.5f, 0.58f, 0.71f };

        [Tooltip("再放電の強さ（主放電を1として）。")]
        public float[] RestrikeStrengths = { 0.8f, 0.6f, 0.45f };

        [Tooltip("1回の放電の形を保つ長さ。そのあと急に消え、次の放電で形を作り替える。")]
        public float HoldSeconds = 0.045f;

        [Header("色と光")]
        public Color CoreColor = new(6f, 7f, 10f);
        public Color HaloColor = new(0.45f, 0.6f, 1.7f);
        public Color ChannelColor = new(0.8f, 0.4f, 1.5f);
        public Color LightColor = new(0.68f, 0.78f, 1f);
        public float StrikeLight = 26f;

        [Tooltip("予兆の間に舞台の主光源を落とす割合（1で落とさない）。")]
        [Range(0f, 1f)]
        public float SunDim = 0.55f;

        private struct Spark
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        private struct Puff
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Radius;
            public float Seed;
        }

        private sealed class Bolt
        {
            public readonly List<Vector3> Points = new();
            public readonly List<List<Vector3>> Branches = new();
        }

        private readonly List<Spark> sparks = new();
        private readonly List<Puff> dust = new();
        private readonly List<MeshRenderer> dustRenderers = new();
        private readonly List<Vector3> sides = new();
        private readonly List<Vector3> part = new();
        private readonly Dictionary<int, Bolt> bolts = new();
        private TempLabBoard air;
        private TempLabBoard ground;
        private Light sun;
        private float sunIntensity;
        private bool struck;

        public override float Duration => 1.6f;

        // 稲妻の根元（空）と命中点。両端は固定し、間だけを折る。
        private Vector3 End => Hit + Vector3.up * 0.3f;

        private Vector3 Top => End + Vector3.up * 6.2f - View.right * 0.9f + Away * 1.2f;

        protected override void Build()
        {
            for (int i = 0; i < DustPool; i++)
                dustRenderers.Add(Part("Dust", VolumeMaterial, QuadMesh, TrailOrder));
            ground = Board("LightningGround", GroundOrder, false);
            air = Board("LightningGlows", GlowOrder);
            var stage = StageCamera != null ? StageCamera.GetComponent<Hd2dStageCamera>() : null;
            sun = stage != null ? stage.KeyLight : null;
            if (sun != null)
                sunIntensity = sun.intensity;
        }

        protected override void OnRestart()
        {
            sparks.Clear();
            dust.Clear();
            bolts.Clear();
            struck = false;
        }

        public override void Hide()
        {
            base.Hide();
            if (sun != null)
                sun.intensity = sunIntensity;
        }

        protected override void OnDestroy()
        {
            if (sun != null)
                sun.intensity = sunIntensity;
            base.OnDestroy();
        }

        protected override void Step(float t, float dt)
        {
            if (!struck && t + 1e-4f >= StrikeAt)
            {
                struck = true;
                // 溶けた火花と土ぼこり。火花は上と外へ、ぼこりは足元からゆっくり昇る。
                for (int i = 0; i < 50; i++)
                {
                    var dir = (OnSphere() + Vector3.up * 1.1f).normalized;
                    sparks.Add(
                        new Spark
                        {
                            Position = End + OnSphere() * 0.1f,
                            Velocity = dir * Range(2f, 7f),
                            Life = Range(0.3f, 0.9f),
                            Size = Range(0.02f, 0.04f),
                        }
                    );
                }
                for (int i = 0; i < DustPool; i++)
                {
                    var side = new Vector3(Range(-0.7f, 0.7f), 0f, Range(-0.3f, 0.4f));
                    dust.Add(
                        new Puff
                        {
                            Position = Feet + side + Vector3.up * 0.15f,
                            Velocity = side * 0.6f + Vector3.up * Range(0.25f, 0.5f),
                            Life = Range(1.0f, 1.3f),
                            Radius = Range(0.28f, 0.42f),
                            Seed = Range(0f, 1f),
                        }
                    );
                }
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
                s.Velocity *= 1f - Mathf.Clamp01(1.2f * dt);
                s.Position += s.Velocity * dt;
                sparks[i] = s;
            }

            for (int i = dust.Count - 1; i >= 0; i--)
            {
                var d = dust[i];
                d.Age += dt;
                if (d.Age >= d.Life)
                {
                    dust.RemoveAt(i);
                    continue;
                }
                d.Velocity *= 1f - Mathf.Clamp01(1.4f * dt);
                d.Position += d.Velocity * dt;
                dust[i] = d;
            }
        }

        // 放電ごとの明るさ（主放電を1として）。形を保つ間は満ち、そのあと急に消える（F11）。
        private float Strength(float t, out int stroke, out float since)
        {
            stroke = -1;
            since = 0f;
            float best = 0f;
            for (int i = -1; i < Restrikes.Length; i++)
            {
                float at = i < 0 ? StrikeAt : Restrikes[i];
                float power =
                    i < 0 ? 1f : RestrikeStrengths[Mathf.Min(i, RestrikeStrengths.Length - 1)];
                if (t + 1e-4f < at)
                    continue;
                float age = t - at;
                float b = power * (age < HoldSeconds ? 1f : Mathf.Exp(-(age - HoldSeconds) * 45f));
                stroke = i + 1;
                since = age;
                best = Mathf.Max(best, b);
            }
            return best;
        }

        protected override void Show(float t)
        {
            air.Clear();
            ground.Clear();
            var view = View;
            float strength = Strength(t, out int stroke, out float sinceStroke);
            float sinceStrike = t - StrikeAt;

            // 予兆：主光源を落とす。真っ黒にはせず、放電が終わると戻す。
            if (sun != null)
            {
                float dim = Smooth(0f, 0.3f, t) * (1f - Smooth(0.95f, 1.4f, t));
                sun.intensity = sunIntensity * Mathf.Lerp(1f, SunDim, dim);
            }

            // 先駆放電：空から段を踏んで降りる、細く暗い枝分かれの線。
            if (t < StrikeAt)
            {
                // 段は0.045秒ごとに1割ずつ進み、主放電の直前に命中点の近くまで届く。
                float steps = Mathf.Floor(Mathf.Max(0f, t - 0.04f) / 0.045f);
                float reach = Mathf.Min(0.86f, steps * 0.11f);
                if (reach > 0f)
                {
                    var leader = BoltOf(-1);
                    float flicker = 0.6f + 0.4f * Mathf.PerlinNoise(t * 40f, 2.1f);
                    DrawBolt(leader, reach, 0.35f * flicker, 0.55f, false);
                }
            }

            // 主放電と再放電。放電ごとに形を作り替え、保つ間は同じ形のまま光る。
            if (stroke >= 0)
            {
                var bolt = BoltOf(stroke);
                if (strength > 0.01f)
                    DrawBolt(bolt, 1f, strength, 1f, true);
                // 消えたあとの通り道の淡い紫の残光。
                float channel = 0.4f * Mathf.Exp(-sinceStroke * 6f) * (1f - Smooth(1.0f, 1.3f, t));
                if (channel > 0.01f)
                    Strip(
                        bolt.Points,
                        1f,
                        0.5f,
                        ChannelColor * channel,
                        new Vector4(0.03f, 0.7f, 0f, 0f),
                        false
                    );
            }

            // 命中の閃光。主放電で最も強く、再放電でも小さく光る。
            if (strength > 0.01f)
                air.Billboard(
                    End,
                    view,
                    2.2f,
                    new Color(1.3f, 1.5f, 2.2f, 1f),
                    TempLabBoard.GlowShape,
                    0.4f,
                    new Vector4(0.05f, 0.24f, 6f * strength, 0.8f * strength)
                );

            // 落ちる前：敵の背から上へ、先駆放電を迎えに行く細く短い放電（上向きストリーマー）。
            float reachUp = t < StrikeAt ? Smooth(0.2f, 0.38f, t) : 0f;
            if (reachUp > 0.01f)
                DrawStreamers(t, reachUp);
            // 当たった瞬間：足元から地面を這って広がる放電。放電ごとに形を変える。
            if (stroke >= 0 && strength > 0.05f)
                DrawGroundArcs(stroke, strength);
            // 痺れ：体の上で小さな光がまたたき、弱まっていく。
            float crawl = t >= StrikeAt ? 0.9f * (1f - Smooth(0.6f, 1.5f, t)) : reachUp * 0.4f;
            if (t >= StrikeAt && crawl > 0.01f)
                DrawSparkles(t, crawl);

            if (sinceStrike >= 0f)
            {
                // 地面を走る青白い衝撃の輪と、煤だけの焦げ跡（熾火は残らない）。
                if (sinceStrike < 0.4f)
                    ground.Flat(
                        Feet,
                        2.4f,
                        new Color(0.7f, 0.85f, 1.6f, 1f) * (3f * Mathf.Exp(-sinceStrike * 8f)),
                        TempLabBoard.ShockShape,
                        0.7f,
                        new Vector4(
                            0.1f + 0.85f * (1f - Mathf.Exp(-sinceStrike * 11f)),
                            0.04f + 0.08f * sinceStrike,
                            0.75f,
                            t
                        )
                    );
                float soot =
                    Smooth(0f, 0.08f, sinceStrike) * (1f - Smooth(0.8f, 1.2f, sinceStrike));
                if (soot > 0.01f)
                    ground.Flat(
                        Feet,
                        0.9f,
                        new Color(1f, 1f, 1f, soot * 0.8f),
                        TempLabBoard.ScorchShape,
                        0.3f,
                        new Vector4(0f, 1f, t, 0f)
                    );
            }

            foreach (var s in sparks)
            {
                float k = s.Age / s.Life;
                var color = EmberColor(1f - k) * (1f - Mathf.SmoothStep(0.7f, 1f, k));
                color.a = 1f;
                air.Streak(
                    s.Position,
                    s.Position - s.Velocity * 0.065f,
                    view,
                    s.Size,
                    color,
                    new Vector4(0.1f, 1f - k, 0f, 0f)
                );
            }

            // 土ぼこり。煤ではなく明るい灰色の砂で、すぐ広がって薄れる。
            for (int i = 0; i < dustRenderers.Count; i++)
            {
                var renderer = dustRenderers[i];
                if (i >= dust.Count)
                {
                    renderer.enabled = false;
                    continue;
                }
                var d = dust[i];
                float k = d.Age / d.Life;
                Volume(
                    renderer,
                    d.Position,
                    d.Radius * (1f + d.Age * 1.4f),
                    Vector3.up,
                    0f,
                    t,
                    d.Seed,
                    0f,
                    1f,
                    0.55f,
                    Mathf.SmoothStep(0.1f, 1f, k),
                    1.2f,
                    1f,
                    End,
                    0f,
                    0.7f * (1f - Mathf.SmoothStep(0.6f, 1f, k))
                );
            }

            Draw(ground);
            Draw(air);
            float leaderGlow = t < StrikeAt ? 1.2f * Smooth(0.1f, 0.38f, t) : 0f;
            Light(
                End + Vector3.up * 0.9f - view.forward * 0.4f,
                LightColor,
                StrikeLight * strength + leaderGlow + crawl * 1.5f,
                7f
            );
        }

        private Bolt BoltOf(int stroke)
        {
            if (bolts.TryGetValue(stroke, out var bolt))
                return bolt;
            bolt = new Bolt();
            var random = new System.Random(Seed * 97 + stroke * 13 + 5);
            Jag(bolt.Points, Top, End, 5, 0.3f, random);
            // 枝：本筋の途中から下向きに分かれ、先へ行くほど細い（F7）。
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                int at = (int)(
                    bolt.Points.Count * Mathf.Lerp(0.18f, 0.8f, (float)random.NextDouble())
                );
                var from = bolt.Points[at];
                var ahead = (
                    bolt.Points[Mathf.Min(at + 1, bolt.Points.Count - 1)] - from
                ).normalized;
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                var dir = (
                    ahead
                    + View.right * side * Mathf.Lerp(0.35f, 0.8f, (float)random.NextDouble())
                    + Vector3.down * 0.6f
                ).normalized;
                var branch = new List<Vector3>();
                Jag(
                    branch,
                    from,
                    from + dir * Mathf.Lerp(1.2f, 2.8f, (float)random.NextDouble()),
                    4,
                    0.28f,
                    random
                );
                bolt.Branches.Add(branch);
            }
            bolts[stroke] = bolt;
            return bolt;
        }

        // 両端を固定し、線分の真ん中を横へずらす分割を重ねて、折れ曲がった道を作る。
        private void Jag(
            List<Vector3> points,
            Vector3 from,
            Vector3 to,
            int levels,
            float rough,
            System.Random random
        )
        {
            points.Clear();
            points.Add(from);
            points.Add(to);
            var buffer = new List<Vector3>();
            for (int level = 0; level < levels; level++)
            {
                buffer.Clear();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var segment = b - a;
                    float length = segment.magnitude;
                    var across = Vector3.Cross(View.forward, segment).normalized;
                    var depth = Vector3.Cross(segment, across).normalized;
                    float scale = length * rough;
                    var middle =
                        (a + b) * 0.5f
                        + across * (Gauss(random) * scale)
                        + depth * (Gauss(random) * scale * 0.5f);
                    buffer.Add(a);
                    buffer.Add(middle);
                }
                buffer.Add(points[^1]);
                points.Clear();
                points.AddRange(buffer);
            }
        }

        private static float Gauss(System.Random random)
        {
            double u = 1.0 - random.NextDouble();
            double v = random.NextDouble();
            return (float)(
                    System.Math.Sqrt(-2.0 * System.Math.Log(u))
                    * System.Math.Cos(2.0 * System.Math.PI * v)
                ) * 0.5f;
        }

        // 稲妻を、淡く広いにじみと細く白い芯の2本の帯で描く。reach は空から何割まで降りたか。
        private void DrawBolt(Bolt bolt, float reach, float strength, float width, bool branches)
        {
            Strip(
                bolt.Points,
                reach,
                0.45f * width,
                HaloColor * strength,
                new Vector4(0.04f, 0.6f, 0f, 0f),
                false
            );
            Strip(
                bolt.Points,
                reach,
                0.09f * width,
                CoreColor * strength,
                new Vector4(0.5f, 0.1f, 0f, 0f),
                false
            );
            foreach (var branch in bolt.Branches)
            {
                if (!branches && reach < 1f)
                {
                    // 先駆放電の枝は、本筋が届いた所より上のものだけ。
                    if (Vector3.Dot(branch[0] - Top, End - Top) / (End - Top).sqrMagnitude > reach)
                        continue;
                }
                Strip(
                    branch,
                    1f,
                    0.28f * width,
                    HaloColor * (strength * 0.7f),
                    new Vector4(0.05f, 0.6f, 0f, 0f),
                    true
                );
                Strip(
                    branch,
                    1f,
                    0.05f * width,
                    CoreColor * (strength * 0.6f),
                    new Vector4(0.5f, 0.1f, 0f, 0f),
                    true
                );
            }
        }

        // 折れ線を、カメラの面の中で横へ幅を取った帯にする。taper なら先ほど細くする。
        private void Strip(
            List<Vector3> points,
            float reach,
            float half,
            Color color,
            Vector4 values,
            bool taper
        )
        {
            int count = Mathf.Clamp(Mathf.CeilToInt(points.Count * reach), 2, points.Count);
            part.Clear();
            sides.Clear();
            var forward = View.forward;
            for (int i = 0; i < count; i++)
            {
                // 向きは前後2つ先までで取り、鋭い折れ目で帯がねじれて横の筋が出ないようにする。
                var ahead = points[Mathf.Min(i + 2, count - 1)] - points[Mathf.Max(i - 2, 0)];
                var side = Vector3.Cross(forward, ahead).normalized;
                float u = i / (count - 1f);
                part.Add(points[i]);
                sides.Add(side * (half * (taper ? Mathf.Lerp(1f, 0.15f, u) : 1f)));
            }
            color.a = 1f;
            air.Strip(part, sides, color, TempLabBoard.BandShape, 0.5f, values);
        }

        // 敵の背から上へ伸びる、細く短い放電。2本だけを、ほぼ真上へ少し揺らして伸ばす。
        private void DrawStreamers(float t, float reach)
        {
            var bounds = TargetBounds;
            int frame = Mathf.FloorToInt(t / 0.05f);
            for (int k = 0; k < 2; k++)
            {
                var random = new System.Random(Seed * 7 + frame * 31 + k * 101);
                var from =
                    new Vector3(
                        Mathf.Lerp(bounds.min.x, bounds.max.x, 0.35f + 0.3f * k),
                        bounds.min.y + bounds.size.y * 0.62f,
                        bounds.center.z
                    )
                    - View.forward * 0.3f;
                var lean = Vector3.up + View.right * (k == 0 ? -0.25f : 0.2f);
                var to = from + lean.normalized * (0.35f + 0.35f * reach);
                var arc = new List<Vector3>();
                Jag(arc, from, to, 3, 0.18f, random);
                float flicker = 0.55f + 0.45f * (float)random.NextDouble();
                Strip(
                    arc,
                    1f,
                    0.1f,
                    HaloColor * (reach * 0.6f * flicker),
                    new Vector4(0.06f, 0.6f, 0f, 0f),
                    true
                );
                Strip(
                    arc,
                    1f,
                    0.025f,
                    CoreColor * (reach * 0.35f * flicker),
                    new Vector4(0.5f, 0.1f, 0f, 0f),
                    true
                );
            }
        }

        // 足元から地面を這って外へ広がる放電。地面すれすれを通り、先ほど細い。
        private void DrawGroundArcs(int stroke, float strength)
        {
            var random = new System.Random(Seed * 53 + stroke * 17);
            for (int k = 0; k < 4; k++)
            {
                float angle = (k * 90f + (float)random.NextDouble() * 60f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float length = Mathf.Lerp(0.7f, 1.5f, (float)random.NextDouble());
                var arc = new List<Vector3>();
                Jag(
                    arc,
                    Feet + Vector3.up * 0.08f,
                    Feet + dir * length + Vector3.up * 0.05f,
                    4,
                    0.2f,
                    random
                );
                Strip(
                    arc,
                    1f,
                    0.14f,
                    HaloColor * (strength * 0.55f),
                    new Vector4(0.06f, 0.6f, 0f, 0f),
                    true
                );
                Strip(
                    arc,
                    1f,
                    0.03f,
                    CoreColor * (strength * 0.4f),
                    new Vector4(0.5f, 0.1f, 0f, 0f),
                    true
                );
            }
        }

        // 痺れの小さな光。体の上のあちこちで、短くまたたく。
        private void DrawSparkles(float t, float strength)
        {
            var bounds = TargetBounds;
            int frame = Mathf.FloorToInt(t / 0.07f);
            for (int k = 0; k < 3; k++)
            {
                var random = new System.Random(Seed * 11 + frame * 37 + k * 211);
                if (random.NextDouble() < 0.45)
                    continue;
                float x = Mathf.Lerp(0.2f, 0.8f, (float)random.NextDouble());
                float y = Mathf.Lerp(0.25f, 0.7f, (float)random.NextDouble());
                var at =
                    new Vector3(
                        bounds.min.x + bounds.size.x * x,
                        bounds.min.y + bounds.size.y * y,
                        bounds.center.z
                    )
                    - View.forward * 0.3f;
                air.Billboard(
                    at,
                    View,
                    0.22f,
                    new Color(1.2f, 1.5f, 2.4f, 1f) * strength,
                    TempLabBoard.GlintShape,
                    0.2f,
                    new Vector4(1.6f, 0.8f, 30f, 0f)
                );
            }
        }
    }
}
