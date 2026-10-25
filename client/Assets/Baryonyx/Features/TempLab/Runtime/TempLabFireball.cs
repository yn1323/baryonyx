using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.TempLab
{
    /// <summary>
    /// A trial fireball, hot and heavy: the caster gathers a seed of fire at the hand while sparks
    /// spiral into it, flings it on an arc with a streaming tail that drops smoke and embers, and
    /// it bursts on the enemy in a flash, a ring running over the ground and a flung spray of
    /// embers; the fire swells, cools into soot and climbs away in torn smoke, and a scorch with
    /// dying embers is left on the ground. Its light rides the fire and lights the stage.
    /// </summary>
    public sealed class TempLabFireball : TempLabEffect
    {
        private const int PuffPool = 20;

        [Header("時間（秒）")]
        [Tooltip("手元に火種を溜める長さ（予兆）。")]
        public float ChargeSeconds = 0.45f;

        [Tooltip("火球が飛ぶ長さ。着弾はその直後。")]
        public float FlightSeconds = 0.4f;

        [Header("大きさ（m）")]
        public float BallRadius = 0.34f;
        public float BlastRadius = 1.1f;

        [Tooltip("飛ぶ弧の高さ。")]
        public float ArcHeight = 0.75f;

        [Tooltip("地面を走る衝撃の輪が届く半径。")]
        public float ShockRadius = 2.6f;

        [Header("光")]
        public Color LightColor = new(1f, 0.52f, 0.2f);
        public float ChargeLight = 3f;
        public float FlightLight = 5f;
        public float ImpactLight = 9f;
        public float LightRange = 6f;

        [Header("粒")]
        [Tooltip("溜めの間に手元へ吸い込まれる火の粉の数。")]
        public int ChargeMotes = 30;

        [Tooltip("飛ぶ間に尾から落ちる火の粉（1秒あたり）。")]
        public float ShedPerSecond = 110f;

        [Tooltip("着弾で飛び散る火の粉の数。")]
        public int BurstEmbers = 90;

        [Tooltip("飛ぶ間に煙の塊を落とす間隔（秒）。")]
        public float SmokeInterval = 0.035f;

        private struct Spark
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public int Kind;
        }

        private struct Puff
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Life;
            public float Radius;
            public float Heat;
            public float Seed;
            public bool Plume;
        }

        private const int MoteKind = 0;
        private const int ShedKind = 1;
        private const int BurstKind = 2;

        private readonly List<Spark> sparks = new();
        private readonly List<Puff> puffs = new();
        private readonly List<MeshRenderer> puffRenderers = new();
        private float nextSmoke;
        private float shedCarry;
        private bool burst;
        private bool plume;
        private int motesLeft;
        private MeshRenderer ball;
        private MeshRenderer blast;
        private TempLabBoard air;
        private TempLabBoard ground;

        /// <summary>The moment the fireball strikes the enemy.</summary>
        public float ImpactTime => ChargeSeconds + FlightSeconds;

        // 着弾のあと、焦げ跡の煤が消えるまで。
        public override float Duration => ImpactTime + 2.75f;

        protected override void Build()
        {
            ball = Part("Fireball", VolumeMaterial, QuadMesh, FrontOrder);
            blast = Part("Blast", VolumeMaterial, QuadMesh, FrontOrder);
            for (int i = 0; i < PuffPool; i++)
                puffRenderers.Add(Part("Smoke", VolumeMaterial, QuadMesh, TrailOrder));
            air = Board("FireballGlows", GlowOrder);
            ground = Board("FireballGround", GroundOrder, false);
        }

        protected override void OnRestart()
        {
            sparks.Clear();
            puffs.Clear();
            nextSmoke = ChargeSeconds;
            shedCarry = 0f;
            burst = false;
            plume = false;
            motesLeft = ChargeMotes;
        }

        // 火球の位置と進む向き。溜めの間は手元、飛ぶ間は弧（2次のベジェ）の上を加速して進む。
        private Vector3 BallAt(float t, out Vector3 heading, out float speed)
        {
            heading = (Hit - Hand).normalized;
            speed = 0f;
            if (t < ChargeSeconds)
                return Hand + Vector3.up * (Mathf.Sin(t * 18f) * 0.015f);
            float u = Mathf.Clamp01((t - ChargeSeconds) / FlightSeconds);
            // 放った直後はためらい、当たる前ほど速い（重い物を投げた手応え）。
            float e = u * (0.35f + 0.65f * u);
            var control = (Hand + Hit) * 0.5f + Vector3.up * (ArcHeight * 2f);
            var p = (1f - e) * (1f - e) * Hand + 2f * (1f - e) * e * control + e * e * Hit;
            var d = 2f * (1f - e) * (control - Hand) + 2f * e * (Hit - control);
            speed = d.magnitude * (0.35f + 1.3f * u) / FlightSeconds;
            heading = d.sqrMagnitude > 1e-6f ? d.normalized : heading;
            return p;
        }

        protected override void Step(float t, float dt)
        {
            var at = BallAt(t, out var heading, out _);

            // 溜め：周りから火の粉が渦を巻いて手元へ吸い込まれる（予兆）。
            if (t < ChargeSeconds * 0.85f && motesLeft > 0)
            {
                int due = Mathf.CeilToInt(ChargeMotes * Mathf.Clamp01(t / (ChargeSeconds * 0.85f)));
                while (ChargeMotes - motesLeft < due && motesLeft > 0)
                {
                    motesLeft--;
                    var dir = OnSphere();
                    sparks.Add(
                        new Spark
                        {
                            Position = Hand + dir * Range(0.6f, 1.15f),
                            Velocity = Vector3.Cross(Vector3.up, dir) * Range(1.5f, 3f),
                            Life = Range(0.25f, 0.45f),
                            Size = Range(0.04f, 0.07f),
                            Kind = MoteKind,
                        }
                    );
                }
            }

            // 飛ぶ間：尾から煙と火の粉を落とす。
            if (t >= ChargeSeconds && t < ImpactTime)
            {
                shedCarry += ShedPerSecond * dt;
                while (shedCarry >= 1f)
                {
                    shedCarry -= 1f;
                    sparks.Add(
                        new Spark
                        {
                            Position = at + OnSphere() * BallRadius * 0.55f,
                            Velocity =
                                -heading * Range(0.4f, 2.2f)
                                + OnSphere() * 0.8f
                                + Vector3.up * 0.5f,
                            Life = Range(0.3f, 0.75f),
                            Size = Range(0.06f, 0.11f),
                            Kind = ShedKind,
                        }
                    );
                }
                while (t >= nextSmoke)
                {
                    nextSmoke += SmokeInterval;
                    puffs.Add(
                        new Puff
                        {
                            Position = at - heading * BallRadius * 0.6f + OnSphere() * 0.05f,
                            Velocity = -heading * 0.35f + Vector3.up * Range(0.35f, 0.6f),
                            Life = Range(0.8f, 1.2f),
                            Radius = BallRadius * Range(0.6f, 0.85f),
                            Heat = 0.45f,
                            Seed = Range(0f, 1f),
                        }
                    );
                }
            }

            // 着弾：火の粉が当たった向きと上へ飛び散る。小さい粒ほど速く遠くへ。
            if (!burst && t >= ImpactTime)
            {
                burst = true;
                var flung = (Hit - Hand).normalized;
                for (int i = 0; i < BurstEmbers; i++)
                {
                    float size = Range(0.06f, 0.14f);
                    var dir = (flung * 0.55f + OnSphere() + Vector3.up * 0.75f).normalized;
                    sparks.Add(
                        new Spark
                        {
                            Position = Hit + OnSphere() * 0.15f,
                            Velocity = dir * Range(2.5f, 7.5f) * (0.09f / size) * 0.7f,
                            Life = Range(0.5f, 1.4f),
                            Size = size,
                            Kind = BurstKind,
                        }
                    );
                }
            }

            // 余韻：着弾の少しあとから、冷えた煤が塊になって敵の奥で昇る。
            if (!plume && t >= ImpactTime + 0.16f)
            {
                plume = true;
                for (int i = 0; i < 8; i++)
                {
                    var side = new Vector3(
                        Range(-0.5f, 0.5f),
                        Range(-0.1f, 0.5f),
                        Range(-0.1f, 0.25f)
                    );
                    puffs.Add(
                        new Puff
                        {
                            Position = Hit + Away * 0.35f + side,
                            Velocity = Vector3.up * Range(0.55f, 1.05f) + side * 0.4f,
                            Life = Range(1.7f, 2.3f),
                            Radius = Range(0.36f, 0.55f),
                            Heat = 0.6f,
                            Seed = Range(0f, 1f),
                            Plume = true,
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
                if (s.Kind == MoteKind)
                {
                    // 手元へ引かれながら回り込む。近づくほど速く巻く。
                    var toHand = at - s.Position;
                    float d = Mathf.Max(toHand.magnitude, 0.05f);
                    var pull = toHand / d * (14f / (0.3f + d));
                    var swirl = Vector3.Cross(Vector3.up, toHand / d) * (5f / (0.4f + d));
                    s.Velocity += (pull + swirl) * dt;
                    s.Velocity *= 1f - Mathf.Clamp01(3.5f * dt);
                    if (d < 0.07f)
                        s.Age = s.Life;
                }
                else
                {
                    // 熱い粒は少し昇り、冷えると落ちる。空気の渦で揺らす。
                    float heat = 1f - s.Age / s.Life;
                    var p = s.Position;
                    var curl = new Vector3(
                        Mathf.Sin(p.y * 3.1f + t * 5.3f + p.z),
                        Mathf.Cos(p.x * 2.7f + t * 4.1f),
                        Mathf.Sin(p.x * 3.3f + t * 4.7f + p.y)
                    );
                    s.Velocity += (Vector3.up * (heat * 3.5f - 6.5f) + curl * 1.6f) * dt;
                    s.Velocity *= 1f - Mathf.Clamp01((s.Kind == BurstKind ? 1.6f : 2.4f) * dt);
                }
                s.Position += s.Velocity * dt;
                sparks[i] = s;
            }

            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var puff = puffs[i];
                puff.Age += dt;
                if (puff.Age >= puff.Life)
                {
                    puffs.RemoveAt(i);
                    continue;
                }
                puff.Velocity += Vector3.up * (0.35f * dt);
                puff.Velocity *= 1f - Mathf.Clamp01(0.9f * dt);
                puff.Position += puff.Velocity * dt;
                puffs[i] = puff;
            }
        }

        protected override void Show(float t)
        {
            var at = BallAt(t, out var heading, out float speed);
            float since = t - ImpactTime;

            // 火球の本体。溜めでは小さな火種から膨らみ、飛ぶ間は速さに合わせて尾を伸ばす。
            if (t < ImpactTime)
            {
                float grow = t < ChargeSeconds ? EaseOut(Mathf.Clamp01(t / ChargeSeconds)) : 1f;
                Volume(
                    ball,
                    at,
                    BallRadius * Mathf.Lerp(0.18f, 1f, grow),
                    heading,
                    Mathf.Clamp(speed * 0.2f, 0f, 2.4f),
                    t,
                    0.13f,
                    1f,
                    0f,
                    1.15f,
                    0f,
                    0.95f,
                    0.4f,
                    at,
                    0f,
                    1f
                );
            }
            else
                ball.enabled = false;

            // 着弾の炎の塊。急に膨らんで昇り、冷えて煤になり、ノイズの低い所から削れて消える。
            if (since >= 0f && since < 2.1f)
            {
                float swell = 0.3f + 0.7f * (1f - Mathf.Exp(-since * 8f));
                float heat = Mathf.Exp(-since * 2.4f);
                float soot = Mathf.Clamp01((since - 0.12f) / 0.55f);
                Volume(
                    blast,
                    Hit + Vector3.up * (0.08f + 0.45f * since),
                    BlastRadius * swell * (1f + 0.22f * since),
                    Vector3.up,
                    0f,
                    t,
                    0.61f,
                    heat,
                    soot,
                    1.3f * Mathf.Lerp(1f, 0.5f, soot),
                    Smooth(0.15f, 1.25f, since),
                    1.7f,
                    0.38f,
                    Hit,
                    heat * 3f,
                    1f
                );
            }
            else
                blast.enabled = false;

            // 煙の塊。昇りながら広がり、削れて消える。中の火が残る間は内側から暖かく照らす。
            for (int i = 0; i < puffRenderers.Count; i++)
            {
                var renderer = puffRenderers[i];
                if (i >= puffs.Count)
                {
                    renderer.enabled = false;
                    continue;
                }
                var puff = puffs[i];
                float k = puff.Age / puff.Life;
                renderer.sortingOrder = puff.Plume ? BehindOrder : TrailOrder;
                Volume(
                    renderer,
                    puff.Position,
                    puff.Radius * (1f + puff.Age * (puff.Plume ? 0.65f : 1.3f)),
                    Vector3.up,
                    0f,
                    t,
                    puff.Seed,
                    puff.Heat * Mathf.Exp(-puff.Age * 4f),
                    1f,
                    puff.Plume ? 0.7f : 0.85f,
                    Mathf.SmoothStep(0.05f, 1f, k),
                    1.1f,
                    1f,
                    puff.Plume ? Hit : puff.Position,
                    puff.Plume ? Mathf.Exp(-Mathf.Max(since, 0f) * 2f) * 2f : 0.4f * (1f - k),
                    0.9f * (1f - Mathf.SmoothStep(0.7f, 1f, k))
                );
            }

            DrawGlows(at, t, since);
            DrawGround(t, since);
            DrawLight(at, t, since);
        }

        private void DrawGlows(Vector3 at, float t, float since)
        {
            air.Clear();
            var view = View;

            // 火球の芯とにじみ。芯は細く鋭く、にじみは広く弱い。
            if (since < 0f)
            {
                float grow = t < ChargeSeconds ? EaseOut(Mathf.Clamp01(t / ChargeSeconds)) : 1f;
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(t * 9f, 0.3f);
                air.Billboard(
                    at,
                    view,
                    BallRadius * 3.4f * Mathf.Lerp(0.3f, 1f, grow),
                    new Color(1f, 0.62f, 0.28f, 1f),
                    TempLabBoard.GlowShape,
                    0.1f,
                    new Vector4(0.09f, 0.4f, 2.2f * grow * flicker, 0.55f * grow * flicker)
                );
            }
            else
            {
                // 着弾の一瞬の白い閃光（約0.1秒）。画面全体ではなく当たった所だけを焼く。
                float flash = Mathf.Exp(-since * 15f);
                if (flash > 0.01f)
                    air.Billboard(
                        Hit,
                        view,
                        2.3f,
                        new Color(1f, 0.86f, 0.62f, 1f),
                        TempLabBoard.GlowShape,
                        0.3f,
                        new Vector4(0.07f, 0.32f, 9f * flash, 1.4f * flash)
                    );
                // 爆ぜたあとの炎の中の熱い光。冷えるのに合わせてゆっくり沈む。
                float inner = Mathf.Exp(-since * 2.3f) * (1f - Mathf.Exp(-since * 30f));
                if (inner > 0.01f)
                    air.Billboard(
                        Hit + Vector3.up * (0.1f + 0.35f * since),
                        view,
                        BlastRadius * 1.9f,
                        new Color(1f, 0.5f, 0.18f, 1f),
                        TempLabBoard.GlowShape,
                        0.6f,
                        new Vector4(0.16f, 0.5f, 1.1f * inner, 0.45f * inner)
                    );
            }

            // 火の粉。丸く光る頭に短い尾が付き、熱いうちは白く、冷えると赤くなって消える。
            // 燃えながら飛ぶので、粒ごとにちらつかせる。
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                float k = s.Age / s.Life;
                float heat = s.Kind == MoteKind ? 0.75f + 0.25f * k : 1f - k;
                float fade =
                    s.Kind == MoteKind
                        ? Mathf.Clamp01(s.Age * 8f)
                        : 1f - Mathf.SmoothStep(0.75f, 1f, k);
                float flicker = 0.65f + 0.35f * Mathf.PerlinNoise(s.Age * 16f, i * 0.37f);
                var color = EmberColor(heat) * (fade * flicker);
                color.a = 1f;
                air.Streak(
                    s.Position,
                    s.Position - s.Velocity * (s.Kind == MoteKind ? 0.04f : 0.03f),
                    view,
                    s.Size,
                    color,
                    new Vector4(0.12f, heat, 0f, 0f)
                );
            }
            Draw(air);
        }

        private void DrawGround(float t, float since)
        {
            ground.Clear();
            if (since >= 0f)
            {
                // 地面を走る衝撃の輪。速く広がり、すぐ弱まる。
                if (since < 0.5f)
                    ground.Flat(
                        Feet,
                        ShockRadius,
                        new Color(1f, 0.66f, 0.32f, 1f) * (3.2f * Mathf.Exp(-since * 6.5f)),
                        TempLabBoard.ShockShape,
                        0.4f,
                        new Vector4(
                            0.1f + 0.85f * (1f - Mathf.Exp(-since * 9f)),
                            0.045f + 0.1f * since,
                            0.65f,
                            t
                        )
                    );
                // 焦げ跡。中の熾火は冷えて消え、煤はしばらく残ってから薄れる。
                float soot = Smooth(0f, 0.15f, since) * (1f - Smooth(1.8f, 2.7f, since));
                if (soot > 0.01f)
                    ground.Flat(
                        Feet,
                        1.4f,
                        new Color(1f, 1f, 1f, soot),
                        TempLabBoard.ScorchShape,
                        0.8f,
                        new Vector4(Mathf.Exp(-since * 1.4f), 1f, t, 0f)
                    );
            }
            Draw(ground);
        }

        // 光は火と一緒に動く。溜めで灯り、飛ぶ間は少し揺らぎ、着弾で一気に強まってから冷める。
        private void DrawLight(Vector3 at, float t, float since)
        {
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(t * 11f, 1.7f);
            if (since < 0f)
            {
                float grow = t < ChargeSeconds ? EaseOut(Mathf.Clamp01(t / ChargeSeconds)) : 1f;
                float intensity =
                    Mathf.Lerp(0f, ChargeLight, grow)
                    + (t >= ChargeSeconds ? FlightLight - ChargeLight : 0f);
                Light(at, LightColor, intensity * flicker, LightRange);
            }
            else
            {
                float intensity =
                    ImpactLight * Mathf.Exp(-since * 3.4f)
                    + FlightLight * 0.4f * Mathf.Exp(-since * 1.2f);
                Light(
                    Hit + Vector3.up * (0.3f + 0.3f * since),
                    LightColor,
                    intensity * flicker,
                    LightRange
                );
            }
        }
    }
}
