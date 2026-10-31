using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Shape of one ember. Points (a round glowing head with a short tail) are the base; crosses
    /// twinkle as glints (a four-pointed star, or the emitter's own glint shape) and streaks trail
    /// a long tail.
    /// </summary>
    public enum Hd2dEmberShape
    {
        Point,
        Cross,
        Streak,
    }

    /// <summary>
    /// One particle source such as torch embers, magic motes or a reward burst.
    /// The anchor is normalized to the parent rect; the other lengths are canvas pixels.
    /// </summary>
    [Serializable]
    public sealed class Hd2dEmberSource
    {
        public string Name = "Embers";

        [Tooltip("発生位置の中心。親の正規化座標です。")]
        public Vector2 Anchor = new Vector2(0.5f, 0.5f);

        [Tooltip("発生位置の広がり（Canvas基準のpx）。この範囲の中からランダムに出ます。")]
        public Vector2 SpawnArea = new Vector2(24f, 8f);

        [Min(0)]
        [Tooltip("同時に表示する粒子の数です。")]
        public int Count = 10;

        [Tooltip(
            "オンのときは消えた粒子がすぐ出直します。オフのときはBurstを呼んだときだけ出ます。"
        )]
        public bool Loop = true;

        public Vector2 LifetimeRange = new Vector2(1.2f, 2.4f);

        [Tooltip("出る向き（度）。90で真上、270で真下です。")]
        public float Direction = 90f;

        [Range(0f, 360f)]
        [Tooltip("向きのばらつき（度）。360で全方向に散ります。")]
        public float Spread = 30f;

        [Tooltip("初速の範囲（px/秒）。")]
        public Vector2 SpeedRange = new Vector2(30f, 70f);

        [Tooltip("上向きの加速度（px/秒²）。負の値で落ちます。")]
        public float Buoyancy = 12f;

        [Min(0f)]
        [Tooltip("左右にゆれる幅（px）。カールノイズと併用すると周期的なゆれが目立ちます。")]
        public float Sway = 0f;

        [Min(0f)]
        public float SwaySpeed = 1.6f;

        [Min(0f)]
        [Tooltip(
            "カールノイズの流れの強さ（px/秒）。渦を巻く空気に乗って、粒が不規則に曲がりながら昇ります。0で使いません。"
        )]
        public float Curl = 36f;

        [Min(1f)]
        [Tooltip("渦の大きさ（px）。小さいほど細かく曲がります。")]
        public float CurlScale = 56f;

        [Min(0f)]
        [Tooltip("流れの形が変わる速さです。0で流れが止まったまま固定されます。")]
        public float CurlSpeed = 0.5f;

        [Min(1)]
        [Tooltip(
            "粒の大きさの単位（px）。点の頭は1〜2単位、尾を引く粒の頭は1.5単位、きらめきは発生器のきらめきの大きさの単位数で描きます。"
        )]
        public int DotSize = 2;

        [Range(0f, 1f)]
        [Tooltip("きらめく粒の割合です。白い芯から四方へ細い光が伸び、光る粒に見えます。")]
        public float CrossShare = 0.3f;

        [Range(0f, 1f)]
        [Tooltip("長い尾を引く粒の割合です。進む向きの反対へ細い尾が伸びます。残りは短い尾の点になります。")]
        public float StreakShare = 0.15f;

        [Tooltip("出たときの色です。")]
        public Color StartColor = new Color(1f, 0.82f, 0.42f, 1f);

        [Tooltip("消える直前の色です。Aを0にすると冷めながら消えます。")]
        public Color EndColor = new Color(1f, 0.32f, 0.08f, 0f);

        [Range(0f, 1f)]
        [Tooltip("ちらつきの量です。")]
        public float Twinkle = 0.35f;
    }

    /// <summary>
    /// UI-based particles emitted from points, drawn above a painted background. Each particle is
    /// a plain board that the ambient shader (<c>Baryonyx/HD2D/UI Ambient</c>) fills without an
    /// image, and the boards of one material are drawn together as one mesh (<see cref="Hd2dEmberBatch"/>): an ember is a round white-hot head with a thin tail that cools behind it, turned to
    /// its direction of travel and lengthened with its speed; a glint is a four-pointed twinkle.
    /// The launch, buoyancy and sway are evaluated from each particle's age; the curl-noise
    /// drift is integrated in fixed steps and replayed on rebuild, so an Editor preview
    /// matches the first frame of Play Mode.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Hd2dEmberEmitter : MonoBehaviour
    {
        [Header("レイヤーと素材")]
        public RectTransform ParticleLayer;

        [Tooltip("火の粉（Baryonyx/HD2D/UI Ambient の Ember）のマテリアル。点と尾を引く粒に使います。")]
        public Material EmberMaterial;

        [Tooltip(
            "きらめく粒のマテリアル。既定は四芒星（Star）で、もや（Puff）などに差し替えられます。未指定の場合は点で描きます。"
        )]
        public Material GlintMaterial;

        [Min(1)]
        [Tooltip("きらめく粒の大きさ（発生源の大きさの単位の数）です。")]
        public int GlintDots = 5;

        [Range(0f, 360f)]
        [Tooltip("きらめく粒を傾ける角度のばらつき（度）です。")]
        public float GlintSpin = 20f;

        [Header("再生")]
        public bool PlayOnEnable = true;
        public bool Animate = true;
        public bool UseUnscaledTime = true;
        public int RandomSeed = 7070;

        [Header("Editorプレビュー")]
        [Tooltip("再生しなくてもScene/Gameビューに現在の設定を表示します。")]
        public bool PreviewInEditor = true;

        [Header("全体")]
        [Range(0f, 2f)]
        [Tooltip("全粒子の濃さに掛ける倍率です。")]
        public float Intensity = 1f;

        [Header("発生源")]
        public List<Hd2dEmberSource> Sources = new List<Hd2dEmberSource>();

        private const float FadeInPortion = 0.12f;

        // Embers leave the flame straight up first, then the eddies take over.
        private const float CurlRampSeconds = 0.35f;

        // Upper bound of one integration step, so the path stays smooth on slow frames.
        private const float MaxCurlStep = 1f / 30f;

        // Offset in noise space for the finite differences of the stream function.
        private const float CurlEpsilon = 0.01f;

        // An ember's tail is the path of its last moment: the eye keeps the hot head for about
        // this long. Streaks are faster in the eye and leave a longer trail.
        private const float PointTailSeconds = 0.1f;
        private const float StreakTailSeconds = 0.25f;

        // The board is this many head widths high, so the head's glow fits inside it.
        private const float HeadBoardRatio = 3f;

        private readonly List<SourceState> states = new List<SourceState>();

        // 点と尾を引く粒、きらめく粒を、それぞれ1つのメッシュで描く部品。
        private Hd2dEmberBatch emberBatch;
        private Hd2dEmberBatch glintBatch;

        // 前のフレームに出ていた粒があったか。粒が1つもないままのフレームは、メッシュを作り直さない。
        private bool anyAlive;
        private System.Random random;
        private float fieldTime;
        private bool initialized;
        private bool rebuildRequested;

        private void OnEnable()
        {
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            ClearParticles();
        }

        private void Update()
        {
            var playing = Application.IsPlaying(gameObject);
            if (!playing && !PreviewInEditor)
            {
                if (initialized)
                    ClearParticles();
                return;
            }

            if (rebuildRequested && (!playing || PlayOnEnable || initialized))
                RebuildParticles();

            if (!initialized || !Animate || !playing)
                return;

            Hd2dAmbientClock.Publish(UseUnscaledTime);
            var deltaTime = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime > 0f)
            {
                Step(deltaTime);
                Redraw();
            }
        }

        private void OnValidate()
        {
            Intensity = Mathf.Clamp(Intensity, 0f, 2f);
            if (Sources != null)
            {
                foreach (var source in Sources)
                {
                    if (source == null)
                        continue;
                    source.Count = Mathf.Max(0, source.Count);
                    source.SpawnArea = new Vector2(
                        Mathf.Max(0f, source.SpawnArea.x),
                        Mathf.Max(0f, source.SpawnArea.y)
                    );
                    source.LifetimeRange = NormalizeRange(source.LifetimeRange, 0.05f);
                    source.SpeedRange = NormalizeRange(source.SpeedRange, 0f);
                    source.DotSize = Mathf.Max(1, source.DotSize);
                    source.StreakShare = Mathf.Clamp01(source.StreakShare);
                    source.CrossShare = Mathf.Clamp(source.CrossShare, 0f, 1f - source.StreakShare);
                    source.Spread = Mathf.Clamp(source.Spread, 0f, 360f);
                    source.Sway = Mathf.Max(0f, source.Sway);
                    source.SwaySpeed = Mathf.Max(0f, source.SwaySpeed);
                    source.Curl = Mathf.Max(0f, source.Curl);
                    source.CurlScale = Mathf.Max(1f, source.CurlScale);
                    source.CurlSpeed = Mathf.Max(0f, source.CurlSpeed);
                    source.Twinkle = Mathf.Clamp01(source.Twinkle);
                }
            }
            rebuildRequested = true;
        }

        /// <summary>
        /// Recreates the particle pool using the current Inspector values. Looping sources
        /// start with spread-out ages so the stream already looks steady on the first frame.
        /// </summary>
        public void RebuildParticles()
        {
            // Prefab assets must never receive generated children. Prefab Mode has a scene.
            if (!gameObject.scene.IsValid() || !isActiveAndEnabled)
                return;

            ClearParticles();
            rebuildRequested = false;
            if (!Application.IsPlaying(gameObject) && !PreviewInEditor)
                return;

            CacheParticleLayer();
            if (ParticleLayer == null || Sources == null)
                return;

            random = new System.Random(RandomSeed);
            fieldTime = 0f;
            emberBatch = CreateBatch("Embers", EmberMaterial, false);
            glintBatch = GlintMaterial != null ? CreateBatch("Glints", GlintMaterial, true) : null;
            for (var index = 0; index < Sources.Count; index++)
            {
                var source = Sources[index];
                if (source != null)
                    states.Add(CreateSource(index, source));
            }

            initialized = true;
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                    ApplyParticle(state.Settings, particle);
            }
            Redraw();
        }

        private Hd2dEmberBatch CreateBatch(string name, Material material, bool glints)
        {
            var batchObject = new GameObject(
                "EmberBatch_" + name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Hd2dEmberBatch)
            );
            batchObject.hideFlags = HideFlags.DontSave;
            batchObject.layer = ParticleLayer.gameObject.layer;
            var rect = (RectTransform)batchObject.transform;
            rect.SetParent(ParticleLayer, false);
            // 粒の層いっぱいに広げ、軸をそろえて、層と同じ座標で頂点を置く。
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = ParticleLayer.pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var batch = batchObject.GetComponent<Hd2dEmberBatch>();
            batch.Emitter = this;
            batch.Glints = glints;
            batch.material = material;
            batch.raycastTarget = false;
            return batch;
        }

        // 出ている粒が変わったときだけ、メッシュを作り直す。
        private void Redraw()
        {
            var alive = false;
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                {
                    if (particle.Alive)
                    {
                        alive = true;
                        break;
                    }
                }
                if (alive)
                    break;
            }
            if (!alive && !anyAlive)
                return;
            anyAlive = alive;
            if (emberBatch != null)
                emberBatch.SetVerticesDirty();
            if (glintBatch != null)
                glintBatch.SetVerticesDirty();
        }

        /// <summary>
        /// Adds the boards of the live particles of one material, in the particle layer's
        /// coordinates: the same quads, UVs and colours their own images used to draw.
        /// </summary>
        internal void FillBatch(VertexHelper vertices, bool glints)
        {
            if (ParticleLayer == null)
                return;
            var layer = ParticleLayer.rect;
            var vertex = UIVertex.simpleVert;
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                {
                    if (!particle.Alive || particle.Glint != glints)
                        continue;
                    var anchor = layer.min + Vector2.Scale(layer.size, particle.Anchor);
                    var center = anchor + particle.Position;
                    var min = -Vector2.Scale(particle.Pivot, particle.Size);
                    var max = min + particle.Size;
                    var radians = particle.Angle * Mathf.Deg2Rad;
                    var cos = Mathf.Cos(radians);
                    var sin = Mathf.Sin(radians);
                    var start = vertices.currentVertCount;
                    vertex.color = particle.Color;
                    AddCorner(vertices, ref vertex, center, min.x, min.y, cos, sin, 0f, 0f);
                    AddCorner(vertices, ref vertex, center, min.x, max.y, cos, sin, 0f, 1f);
                    AddCorner(vertices, ref vertex, center, max.x, max.y, cos, sin, 1f, 1f);
                    AddCorner(vertices, ref vertex, center, max.x, min.y, cos, sin, 1f, 0f);
                    vertices.AddTriangle(start, start + 1, start + 2);
                    vertices.AddTriangle(start + 2, start + 3, start);
                }
            }
        }

        private static void AddCorner(
            VertexHelper vertices,
            ref UIVertex vertex,
            Vector2 center,
            float x,
            float y,
            float cos,
            float sin,
            float u,
            float v
        )
        {
            vertex.position = new Vector3(center.x + x * cos - y * sin, center.y + x * sin + y * cos);
            vertex.uv0 = new Vector4(u, v, 0f, 0f);
            vertices.AddVert(vertex);
        }

        /// <summary>
        /// Shows the particles as they are <paramref name="time"/> seconds into play, without
        /// playing: for captures and previews.
        /// </summary>
        public void ShowAt(float time)
        {
            RebuildParticles();
            for (var elapsed = 0f; elapsed < time; elapsed += 1f / 30f)
                Step(Mathf.Min(1f / 30f, time - elapsed));
            Redraw();
            Hd2dAmbientClock.Set(time);
        }

        /// <summary>
        /// Emits up to <paramref name="count"/> particles from a source at once, reusing the
        /// oldest ones. Use it with a non-looping source for rewards or transitions.
        /// </summary>
        public void Burst(int sourceIndex, int count) =>
            EmitBurst(sourceIndex, count, Vector2.zero);

        /// <summary>
        /// Emits like <see cref="Burst(int, int)"/>, centred on a world position instead of
        /// the source anchor. Use it for targets that move or come from layout, such as the
        /// icon a reward flies into.
        /// </summary>
        public void BurstAt(int sourceIndex, int count, Vector3 worldPosition)
        {
            CacheParticleLayer();
            if (ParticleLayer == null || Sources == null)
                return;
            if (sourceIndex < 0 || sourceIndex >= Sources.Count || Sources[sourceIndex] == null)
                return;
            var rect = ParticleLayer.rect;
            var anchor = rect.min + Vector2.Scale(rect.size, Sources[sourceIndex].Anchor);
            Vector2 point = ParticleLayer.InverseTransformPoint(worldPosition);
            EmitBurst(sourceIndex, count, point - anchor);
        }

        // Counts the bursts, so a burst takes each particle once.
        private int burstStamp;

        private void EmitBurst(int sourceIndex, int count, Vector2 offset)
        {
            if (!initialized || sourceIndex < 0 || sourceIndex >= states.Count || count <= 0)
                return;

            var state = states[sourceIndex];
            // Dead particles first, then the oldest live ones: each time the one with the least
            // time left that this burst has not taken yet. No list is made or sorted, since a
            // trail bursts one at a time many times a second.
            burstStamp++;
            for (var index = 0; index < count; index++)
            {
                ParticleState next = null;
                foreach (var particle in state.Particles)
                    if (
                        particle.Burst != burstStamp
                        && (next == null || Remaining(particle) < Remaining(next))
                    )
                        next = particle;
                if (next == null)
                    break;
                next.Burst = burstStamp;
                Respawn(state.Settings, next, 0f);
                next.Origin += offset;
                ApplyParticle(state.Settings, next);
            }
            Redraw();
        }

        /// <summary>
        /// Offset of a particle from its spawn point after <paramref name="age"/> seconds.
        /// </summary>
        public static Vector2 EvaluateOffset(
            Vector2 velocity,
            float buoyancy,
            float sway,
            float swaySpeed,
            float swayPhase,
            float age
        )
        {
            var drift = velocity * age + new Vector2(0f, 0.5f * buoyancy * age * age);
            var swing =
                sway > 0f ? Mathf.Sin(swayPhase + age * swaySpeed) - Mathf.Sin(swayPhase) : 0f;
            return drift + new Vector2(swing * sway, 0f);
        }

        /// <summary>
        /// Divergence-free velocity of the curl-noise field at <paramref name="position"/>
        /// (canvas pixels). In 2D it is the curl of a scalar stream function ψ built from
        /// Perlin noise: v = (∂ψ/∂y, -∂ψ/∂x) (Bridson et al. 2007). The flow swirls without
        /// sources or sinks, so nearby embers curl together instead of jittering apart.
        /// The length is about 1 on average; multiply by the strength in px/s.
        /// </summary>
        public static Vector2 CurlVelocity(Vector2 position, float time, float scale, float speed)
        {
            var point = position / Mathf.Max(1f, scale);
            var t = time * speed;
            var dx =
                StreamFunction(point + new Vector2(CurlEpsilon, 0f), t)
                - StreamFunction(point - new Vector2(CurlEpsilon, 0f), t);
            var dy =
                StreamFunction(point + new Vector2(0f, CurlEpsilon), t)
                - StreamFunction(point - new Vector2(0f, CurlEpsilon), t);
            return new Vector2(dy, -dx) / (2f * CurlEpsilon);
        }

        /// <summary>
        /// Stream function ψ: two octaves of Perlin noise drifting in different directions,
        /// so the eddies change shape over time instead of sliding as one picture.
        /// </summary>
        private static float StreamFunction(Vector2 point, float t)
        {
            var coarse = Mathf.PerlinNoise(point.x + t * 0.31f + 11.3f, point.y - t * 0.53f + 4.7f);
            var fine = Mathf.PerlinNoise(
                point.x * 2.03f - t * 0.47f + 37.1f,
                point.y * 2.03f + t * 0.29f + 23.9f
            );
            return coarse + fine * 0.5f;
        }

        /// <summary>
        /// Opacity over the particle life: a quick fade-in, then an eased change from the
        /// start alpha to the end alpha. An end alpha of 0 makes the particle fade out.
        /// </summary>
        public static float EvaluateLifeAlpha(float life01, float startAlpha, float endAlpha)
        {
            life01 = Mathf.Clamp01(life01);
            var fadeIn = Mathf.Clamp01(life01 / FadeInPortion);
            return Mathf.Lerp(startAlpha, endAlpha, Hd2dMath.SmoothUnit(life01)) * fadeIn;
        }

        /// <summary>
        /// Picks a shape from a roll in [0, 1). Streaks come first, then crosses, and the
        /// remainder are points. The cross share is limited to what the streaks leave.
        /// </summary>
        public static Hd2dEmberShape ChooseShape(float roll, float crossShare, float streakShare)
        {
            streakShare = Mathf.Clamp01(streakShare);
            crossShare = Mathf.Clamp(crossShare, 0f, 1f - streakShare);
            if (roll < streakShare)
                return Hd2dEmberShape.Streak;
            if (roll < streakShare + crossShare)
                return Hd2dEmberShape.Cross;
            return Hd2dEmberShape.Point;
        }

        /// <summary>
        /// Length (px) of an ember's tail moving at <paramref name="speed"/> px/s: the path of
        /// the last <paramref name="seconds"/>, at least one head long and at most twelve.
        /// </summary>
        public static float EvaluateTailLength(float speed, float seconds, float head)
        {
            return Mathf.Clamp(speed * seconds, head, head * 12f);
        }

        private void Step(float deltaTime)
        {
            fieldTime += deltaTime;
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                {
                    // 消えた粒は、出直すか Burst で出るまで動かさない。
                    if (!state.Settings.Loop && particle.Age >= particle.Lifetime)
                        continue;
                    var previousAge = particle.Age;
                    particle.Age += deltaTime;
                    if (particle.Age >= particle.Lifetime && state.Settings.Loop)
                    {
                        Respawn(state.Settings, particle, particle.Age - particle.Lifetime);
                        AdvanceCurl(state.Settings, particle, 0f, particle.Age);
                    }
                    else if (previousAge < particle.Lifetime)
                    {
                        AdvanceCurl(
                            state.Settings,
                            particle,
                            previousAge,
                            Mathf.Min(particle.Age, particle.Lifetime)
                        );
                    }
                    ApplyParticle(state.Settings, particle);
                }
            }
        }

        /// <summary>
        /// Moves a particle along the curl-noise field from age <paramref name="fromAge"/> to
        /// <paramref name="toAge"/>. The field is sampled where the particle actually is, so
        /// it follows the swirl. <see cref="fieldTime"/> is the field clock at the end age.
        /// </summary>
        private void AdvanceCurl(
            Hd2dEmberSource source,
            ParticleState particle,
            float fromAge,
            float toAge
        )
        {
            if (source.Curl <= 0f || toAge <= fromAge)
                return;

            var age = fromAge;
            while (age < toAge)
            {
                var step = Mathf.Min(MaxCurlStep, toAge - age);
                var middle = age + step * 0.5f;
                var position =
                    particle.Origin + BaseOffset(source, particle, middle) + particle.CurlOffset;
                var velocity = CurlVelocity(
                    position + particle.FieldOffset,
                    fieldTime - (toAge - middle),
                    source.CurlScale,
                    source.CurlSpeed
                );
                var ramp = Hd2dMath.SmoothUnit(middle / CurlRampSeconds);
                particle.CurlOffset += velocity * (source.Curl * ramp * step);
                age += step;
            }
        }

        private static Vector2 BaseOffset(Hd2dEmberSource source, ParticleState particle, float age)
        {
            return EvaluateOffset(
                particle.Velocity,
                source.Buoyancy,
                source.Sway,
                source.SwaySpeed,
                particle.SwayPhase,
                age
            );
        }

        private void CacheParticleLayer()
        {
            if (ParticleLayer == null)
            {
                var candidate = transform.Find("ParticleLayer");
                if (candidate != null)
                    ParticleLayer = candidate as RectTransform;
            }

            if (ParticleLayer != null)
            {
                ParticleLayer.anchorMin = Vector2.zero;
                ParticleLayer.anchorMax = Vector2.one;
                ParticleLayer.offsetMin = Vector2.zero;
                ParticleLayer.offsetMax = Vector2.zero;
            }
        }

        private SourceState CreateSource(int index, Hd2dEmberSource source)
        {
            var state = new SourceState { Settings = source };
            // Each source reads its own part of the field, so torches do not swirl in step.
            var fieldOffset = new Vector2(index * 997f, index * 613f);
            for (var particleIndex = 0; particleIndex < source.Count; particleIndex++)
            {
                var particle = new ParticleState { Anchor = source.Anchor, FieldOffset = fieldOffset };
                Respawn(source, particle, 0f);
                // Looping streams start mid-flow; one-shot sources wait for a Burst.
                if (source.Loop)
                {
                    particle.Age = RandomRange(0f, particle.Lifetime);
                    // Replays the path so far, so the first frame already shows the swirl.
                    AdvanceCurl(source, particle, 0f, particle.Age);
                }
                else
                    particle.Age = particle.Lifetime;
                state.Particles.Add(particle);
            }
            return state;
        }

        private void Respawn(Hd2dEmberSource source, ParticleState particle, float age)
        {
            var angle =
                (source.Direction + RandomRange(-source.Spread, source.Spread) * 0.5f)
                * Mathf.Deg2Rad;
            var speed = RandomRange(source.SpeedRange.x, source.SpeedRange.y);
            particle.Origin = new Vector2(
                RandomRange(-source.SpawnArea.x, source.SpawnArea.x) * 0.5f,
                RandomRange(-source.SpawnArea.y, source.SpawnArea.y) * 0.5f
            );
            particle.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            particle.Lifetime = RandomRange(source.LifetimeRange.x, source.LifetimeRange.y);
            particle.Age = Mathf.Clamp(age, 0f, particle.Lifetime);
            particle.SwayPhase = RandomRange(0f, Mathf.PI * 2f);
            particle.CurlOffset = Vector2.zero;
            particle.TwinklePhase = RandomRange(0f, 100f);
            ApplyShape(source, particle);
        }

        private void ApplyShape(Hd2dEmberSource source, ParticleState particle)
        {
            var shape = ChooseShape(RandomRange(0f, 1f), source.CrossShare, source.StreakShare);
            if (shape == Hd2dEmberShape.Cross && GlintMaterial == null)
                shape = Hd2dEmberShape.Point;

            var unit = Mathf.Max(1, source.DotSize);
            var pointHead = RandomRange(0f, 1f) < 0.5f ? 1f : 2f;
            particle.Shape = shape;
            particle.Head =
                shape == Hd2dEmberShape.Streak ? unit * 1.5f
                : shape == Hd2dEmberShape.Cross ? unit * Mathf.Max(1, GlintDots)
                : unit * pointHead;
            particle.GlintAngle = RandomRange(-GlintSpin, GlintSpin) * 0.5f;
            particle.Glint = shape == Hd2dEmberShape.Cross;
            particle.Pivot = new Vector2(0.5f, 0.5f);
            particle.Angle = 0f;
        }

        // Turns an ember's board to its direction of travel and stretches its tail with the
        // speed; a glint keeps its square board and tilt.
        private void ApplyTrail(Hd2dEmberSource source, ParticleState particle)
        {
            if (particle.Shape == Hd2dEmberShape.Cross)
            {
                particle.Size = Vector2.one * particle.Head;
                particle.Angle = particle.GlintAngle;
                return;
            }

            var velocity = Velocity(source, particle);
            var speed = velocity.magnitude;
            var height = particle.Head * HeadBoardRatio;
            var seconds =
                particle.Shape == Hd2dEmberShape.Streak ? StreakTailSeconds : PointTailSeconds;
            var tail = speed > 0.01f ? EvaluateTailLength(speed, seconds, particle.Head) : 0f;
            var width = height + tail;
            particle.Size = new Vector2(width, height);
            // The head's centre is the particle's position; the tail trails behind it.
            particle.Pivot = new Vector2(1f - height * 0.5f / width, 0.5f);
            particle.Angle =
                speed > 0.01f
                    ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg
                    : source.Direction;
        }

        // Velocity (px/s) of a particle now: the launch, the buoyancy, the sway and the eddies.
        private Vector2 Velocity(Hd2dEmberSource source, ParticleState particle)
        {
            var age = particle.Age;
            var velocity = particle.Velocity + new Vector2(0f, source.Buoyancy * age);
            if (source.Sway > 0f)
                velocity.x +=
                    Mathf.Cos(particle.SwayPhase + age * source.SwaySpeed)
                    * source.Sway
                    * source.SwaySpeed;
            if (source.Curl > 0f)
            {
                var position =
                    particle.Origin + BaseOffset(source, particle, age) + particle.CurlOffset;
                velocity +=
                    CurlVelocity(
                        position + particle.FieldOffset,
                        fieldTime,
                        source.CurlScale,
                        source.CurlSpeed
                    )
                    * (source.Curl * Hd2dMath.SmoothUnit(age / CurlRampSeconds));
            }
            return velocity;
        }

        private void ApplyParticle(Hd2dEmberSource source, ParticleState particle)
        {
            particle.Alive = particle.Age < particle.Lifetime;
            if (!particle.Alive)
                return;

            var life01 = particle.Lifetime > 0f ? particle.Age / particle.Lifetime : 1f;
            particle.Position =
                particle.Origin + BaseOffset(source, particle, particle.Age) + particle.CurlOffset;
            ApplyTrail(source, particle);

            var twinkle =
                source.Twinkle > 0f
                    ? Mathf.Lerp(
                        1f - source.Twinkle,
                        1f,
                        Mathf.PerlinNoise(particle.TwinklePhase + particle.Age * 6f, 0.37f)
                    )
                    : 1f;
            var color = Color.Lerp(source.StartColor, source.EndColor, life01);
            color.a = Mathf.Clamp01(
                EvaluateLifeAlpha(life01, source.StartColor.a, source.EndColor.a)
                    * twinkle
                    * Intensity
            );
            particle.Color = color;
        }

        private static float Remaining(ParticleState particle)
        {
            return particle.Lifetime - particle.Age;
        }

        private void ClearParticles()
        {
            initialized = false;
            DestroyBatch(emberBatch);
            DestroyBatch(glintBatch);
            emberBatch = null;
            glintBatch = null;
            anyAlive = false;
            states.Clear();
        }

        private void DestroyBatch(Hd2dEmberBatch batch)
        {
            if (batch == null)
                return;
            batch.gameObject.SetActive(false);
            if (Application.IsPlaying(gameObject))
                Destroy(batch.gameObject);
            else
                DestroyImmediate(batch.gameObject);
        }

        private float RandomRange(float min, float max)
        {
            random ??= new System.Random(RandomSeed);
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private static Vector2 NormalizeRange(Vector2 value, float minimum)
        {
            var min = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
            var max = Mathf.Max(min, Mathf.Max(value.x, value.y));
            return new Vector2(min, max);
        }

        private sealed class SourceState
        {
            public Hd2dEmberSource Settings;
            public readonly List<ParticleState> Particles = new List<ParticleState>();
        }

        // 今の粒。テストで、板の形と出ている粒を確かめる。
        internal IEnumerable<ParticleState> Particles
        {
            get
            {
                foreach (var state in states)
                    foreach (var particle in state.Particles)
                        yield return particle;
            }
        }

        internal sealed class ParticleState
        {
            // 板の置き方（粒の層の中の基準点、そこからの位置、大きさ、軸、向き）と色。
            // 1つのメッシュに描くとき（FillBatch）に、RectTransform と同じ形で頂点にする。
            public Vector2 Anchor;
            public Vector2 Position;
            public Vector2 Size;
            public Vector2 Pivot;
            public float Angle;
            public Color Color;
            public bool Alive;
            public bool Glint;
            public Vector2 Origin;
            public Vector2 Velocity;
            public Vector2 CurlOffset;
            public Vector2 FieldOffset;
            public float Lifetime;
            public float Age;
            public float SwayPhase;
            public float TwinklePhase;
            public Hd2dEmberShape Shape;

            // Diameter (px) of an ember's head, or the size of a glint's board.
            public float Head;
            public float GlintAngle;

            // The last burst that took this particle.
            public int Burst;
        }
    }
}
