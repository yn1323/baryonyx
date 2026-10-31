using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The light swirl played over the whole screen when something is crafted
    /// (doc/features/synthesis.md 合成の演出): the screen sinks, the materials fly to the middle
    /// and spin in toward it faster and faster on thin spiral lines, are pressed into a ball of
    /// light that bursts into rays and a ring of the quality's colour, and the made thing shows
    /// large and goes back to its place. A ☆4 thing makes it gold and a ☆5 one faintly rainbow,
    /// each a little richer and longer (<see cref="Look"/>). <see cref="Show"/> draws it at a
    /// time since the press, so <see cref="Play"/> runs it frame by frame and any moment can be
    /// shown still.
    /// </summary>
    public sealed class WorkshopCraftSwirl : MonoBehaviour
    {
        /// <summary>How rich it is: plain for ☆3 and under, gold for ☆4, rainbow for ☆5.</summary>
        public enum Look
        {
            Plain,
            Gold,
            Rainbow,
        }

        /// <summary>
        /// The beats (seconds from the press; doc/features/synthesis.md 合成の演出). Gold and
        /// rainbow hold a little longer before the burst and linger a little after it.
        /// </summary>
        public readonly struct Beats
        {
            public const float FlyStart = 0.03f;
            public const float FlyEnd = 0.3f;
            public const float SwirlStart = 0.27f;
            public const float CollapseStart = 0.78f;

            public Beats(float hold, float linger)
            {
                Burst = 0.92f + hold;
                ReturnStart = Burst + 0.28f + linger;
                Seconds = ReturnStart + 0.3f;
            }

            public float Burst { get; }
            public float ReturnStart { get; }
            public float Seconds { get; }

            // 渦が中央で消えきる時刻（弾ける直前）。
            public float SwirlEnd => Burst - 0.02f;

            public static Beats Of(Look look) =>
                look switch
                {
                    Look.Rainbow => new Beats(0.18f, 0.1f),
                    Look.Gold => new Beats(0.06f, 0.04f),
                    _ => new Beats(0f, 0f),
                };
        }

        // 暗転の色（紺）と、光の色（金・白）。
        private static readonly Color Night = new(0.02f, 0.03f, 0.08f, 1f);
        private static readonly Color Gold = new(1f, 0.8f, 0.45f, 1f);
        private static readonly Color DeepGold = new(1f, 0.7f, 0.28f, 1f);
        private static readonly Color WarmWhite = new(1f, 0.95f, 0.86f, 1f);

        [Tooltip("画面全体を沈める暗転。押すと演出を飛ばす。")]
        public Image Dim;
        public Button Skip;

        [Tooltip("渦の線の周りの光った空気と、細い線（Craft Swirlシェーダーの2つの層）。")]
        public BattleVfxImage SwirlHalo;
        public BattleVfxImage SwirlLine;

        [Tooltip("渦の見た目ごとのマテリアル（Lookの順：通常・金・虹）。")]
        public Material[] HaloLooks = Array.Empty<Material>();
        public Material[] LineLooks = Array.Empty<Material>();

        [Tooltip("材料の絵と、その後ろの光、飛ぶ間の光の筋（材料の数まで使う）。")]
        public RawImage[] Pieces = Array.Empty<RawImage>();
        public BattleVfxImage[] PieceGlows = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] PieceTrails = Array.Empty<BattleVfxImage>();

        [Tooltip("渦から外へ飛ぶ火の粉。")]
        public BattleVfxImage[] Sparks = Array.Empty<BattleVfxImage>();

        [Tooltip(
            "☆4・☆5：渦の腕に沿って光るきらめきと、弾ける前に画面の端から吸い込まれる細い線。"
        )]
        public BattleVfxImage[] Glints = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Converge = Array.Empty<BattleVfxImage>();

        [Tooltip("中央の光の球：広い光と白い芯。")]
        public BattleVfxImage OrbHalo;
        public BattleVfxImage OrbCore;

        [Tooltip("弾けたときの光線と、品質の色の輪。☆4・☆5は2つめの光線と、輪を重ねる。")]
        public BattleVfxImage Rays;
        public BattleVfxImage MoreRays;
        public BattleVfxImage Ring;
        public BattleVfxImage[] MoreRings = Array.Empty<BattleVfxImage>();

        [Tooltip("☆4・☆5：弾けて飛び、舞い落ちる金箔・プリズムの小片と、画面に散るきらめき。")]
        public BattleVfxImage[] Flakes = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Prisms = Array.Empty<BattleVfxImage>();

        [Tooltip("出来た物の絵と、その後ろの光、まわりのきらめき。")]
        public RawImage Item;
        public BattleVfxImage ItemGlow;
        public BattleVfxImage[] Twinkles = Array.Empty<BattleVfxImage>();

        [Tooltip("☆4・☆5：出来た物の上を走る光と、☆5：後ろで回る淡い虹の輪。")]
        public BattleVfxImage Sheen;
        public BattleVfxImage[] ItemRings = Array.Empty<BattleVfxImage>();

        [Tooltip("暗転の濃さ、渦の板の一辺、材料が渦へ入る半径、出来た物を中央で見せる倍率。")]
        public float DimAlpha = 0.86f;
        public float SwirlSize = 1180f;
        public float OrbitRadius = 300f;
        public float ItemScale = 2.6f;

        private Plan plan;
        private Beats beats = Beats.Of(Look.Plain);
        private float time;
        private Action finished;
        private Coroutine running;

        /// <summary>
        /// What one craft shows: each material's art and where its tile is, the made thing's art
        /// and where it goes back to, the quality's colour, how rich it is and the seed of its
        /// shapes. Positions are in this effect's own space (0 at its middle).
        /// </summary>
        public sealed class Plan
        {
            public readonly List<Piece> Pieces = new();
            public Texture ItemArt;
            public Rect ItemUv = new(0f, 0f, 1f, 1f);
            public Color ItemTint = Color.white;
            public Vector2 ItemSize = new(192f, 192f);
            public Vector2 ItemHome;
            public Color Quality = Color.white;
            public Look Look;
            public float Seed;
        }

        public struct Piece
        {
            public Texture Art;
            public Rect Uv;
            public Color Tint;
            public Vector2 Size;
            public Vector2 From;
        }

        /// <summary>True while it plays.</summary>
        public bool Playing => running != null;

        /// <summary>How rich a thing of <paramref name="stars"/> makes it.</summary>
        public static Look LookOf(int stars) =>
            stars >= 5 ? Look.Rainbow
            : stars >= 4 ? Look.Gold
            : Look.Plain;

        private void Awake() => BattleVfxImage.EnableShapeChannel(transform);

        private void OnDisable()
        {
            // 画面を閉じたら、残りを飛ばして終わらせる（隠れる途中なので、隠し直さない）。
            if (running != null)
            {
                running = null;
                End(hide: false);
            }
        }

        /// <summary>The point <paramref name="target"/>'s middle is at, in this effect's space.</summary>
        public Vector2 PlaceOf(RectTransform target)
        {
            var root = (RectTransform)transform;
            if (target == null)
                return Vector2.zero;
            Vector3 world = target.TransformPoint(target.rect.center);
            return (Vector2)root.InverseTransformPoint(world) - root.rect.center;
        }

        /// <summary>Plays <paramref name="next"/> from the press; <paramref name="done"/> runs when it ends or is skipped to its end.</summary>
        public void Play(Plan next, Action done)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
                End();
            }
            plan = next ?? new Plan();
            beats = Beats.Of(plan.Look);
            finished = done;
            gameObject.SetActive(true);
            if (Skip != null)
            {
                Skip.onClick.RemoveListener(SkipToReturn);
                Skip.onClick.AddListener(SkipToReturn);
            }
            time = 0f;
            Show(0f, plan);
            running = StartCoroutine(Run());
        }

        /// <summary>Jumps to where the made thing goes back (pressing anywhere does this).</summary>
        public void SkipToReturn() => time = Mathf.Max(time, beats.ReturnStart);

        /// <summary>Ends it at once, as if it had played to its end.</summary>
        public void Finish()
        {
            if (running == null)
                return;
            StopCoroutine(running);
            running = null;
            End();
        }

        private IEnumerator Run()
        {
            while (time < beats.Seconds)
            {
                yield return null;
                time += Time.unscaledDeltaTime;
                Show(Mathf.Min(time, beats.Seconds), plan);
            }
            running = null;
            End();
        }

        private void End(bool hide = true)
        {
            if (hide)
                gameObject.SetActive(false);
            var done = finished;
            finished = null;
            done?.Invoke();
        }

        /// <summary>Draws the effect <paramref name="seconds"/> after the press.</summary>
        public void Show(float seconds, Plan shown)
        {
            plan = shown ?? new Plan();
            beats = Beats.Of(plan.Look);
            float t = seconds;
            float seed = plan.Seed;
            bool rich = plan.Look != Look.Plain;

            // 暗転：押すとすぐ沈み、弾けた瞬間だけ少し緩み、戻るときに解ける。虹は溜める間もう一段沈む。
            if (Dim != null)
            {
                float sink =
                    Smooth(Ramp(t, 0f, 0.15f))
                    * (1f - Smooth(Ramp(t, beats.ReturnStart, beats.Seconds)));
                float deeper =
                    plan.Look == Look.Rainbow
                        ? 0.08f
                            * Smooth(Ramp(t, Beats.CollapseStart, beats.Burst - 0.05f))
                            * (1f - Ramp(t, beats.Burst, beats.Burst + 0.1f))
                        : 0f;
                float ease = 0.22f * Pulse(t, beats.Burst + 0.02f, 0.05f);
                Dim.color = WithAlpha(Night, (DimAlpha + deeper) * sink - ease);
            }

            // 渦：進み具合は渦の拍の中で0から1。入ってから現れ、詰まるときに消える。
            float k = Ramp(t, Beats.SwirlStart, beats.SwirlEnd);
            bool swirling = t > Beats.SwirlStart - 0.04f && t < beats.Burst + 0.06f;
            float swirlIn = Smooth(Ramp(t, Beats.SwirlStart - 0.04f, Beats.SwirlStart + 0.16f));
            float swirlGone = Ramp(t, Beats.CollapseStart + 0.06f, beats.Burst + 0.05f);
            var swirlTint = plan.Look switch
            {
                Look.Rainbow => WarmWhite,
                Look.Gold => DeepGold,
                _ => Gold,
            };
            UseLook(SwirlHalo, HaloLooks);
            UseLook(SwirlLine, LineLooks);
            ShowBoard(
                SwirlHalo,
                swirling,
                Vector2.zero,
                Vector2.one * SwirlSize,
                0f,
                swirlTint,
                swirlIn,
                seed,
                k,
                swirlGone,
                1f
            );
            ShowBoard(
                SwirlLine,
                swirling,
                Vector2.zero,
                Vector2.one * SwirlSize,
                0f,
                swirlTint,
                swirlIn,
                seed,
                k,
                swirlGone,
                1f
            );

            ShowPieces(t);
            ShowSparks(t);
            ShowGlints(t, rich);
            ShowConverge(t, rich);
            ShowOrb(t);
            ShowBurst(t);
            ShowFlakes(t, rich);
            ShowItem(t);
        }

        private void UseLook(BattleVfxImage board, Material[] looks)
        {
            int index = (int)plan.Look;
            if (
                board != null
                && index < looks.Length
                && looks[index] != null
                && board.material != looks[index]
            )
                board.material = looks[index];
        }

        // 材料：札から弧を描いて渦の入口へ飛び、中央の周りを回りながら近づく（中心ほど速い）。
        // 詰まるときに白くなって縮み、中央へ消える。
        private void ShowPieces(float t)
        {
            int count = plan.Pieces.Count;
            for (int i = 0; i < Pieces.Length; i++)
            {
                bool used = i < count && t >= Beats.FlyStart && t < beats.Burst;
                var piece = Pieces[i];
                var glow = i < PieceGlows.Length ? PieceGlows[i] : null;
                var trail = i < PieceTrails.Length ? PieceTrails[i] : null;
                if (piece != null)
                    piece.gameObject.SetActive(used);
                if (!used)
                {
                    Hide(glow);
                    Hide(trail);
                    continue;
                }
                var info = plan.Pieces[i];
                Vector2 at = PieceAt(info, i, count, t);
                Vector2 before = PieceAt(info, i, count, t - 0.025f);
                Vector2 velocity = (at - before) / 0.025f;
                float collapse = Ramp(t, Beats.CollapseStart, beats.Burst - 0.03f);
                float size =
                    Mathf.Lerp(1f, 0.8f, Ramp(t, Beats.FlyStart, Beats.FlyEnd))
                    * (1f - Smooth(collapse));

                if (piece != null)
                {
                    piece.texture = info.Art;
                    piece.uvRect = info.Uv;
                    piece.color = Color.Lerp(info.Tint, Color.white, collapse);
                    var rect = piece.rectTransform;
                    rect.anchoredPosition = at;
                    rect.sizeDelta = info.Size;
                    rect.localScale = Vector3.one * size;
                }
                // 後ろの光：飛び立つと灯り、詰まるときに白く強くなる。
                float lit = Smooth(Ramp(t, Beats.FlyStart, Beats.FlyStart + 0.12f));
                ShowBoard(
                    glow,
                    true,
                    at,
                    Vector2.one * (info.Size.x * 1.7f * (0.6f + 0.4f * size)),
                    0f,
                    Color.Lerp(Gold, Color.white, collapse),
                    0.55f * lit + 0.45f * collapse,
                    Hash(i, plan.Seed),
                    k: t,
                    eaten: 0f,
                    brightness: 1f + collapse
                );
                // 光の筋：進む向きの後ろへ、速いほど長く伸びる。
                float speed = velocity.magnitude;
                float length = Mathf.Clamp(speed * 0.1f, 0f, 240f);
                if (length > 12f && collapse < 0.95f)
                {
                    Vector2 dir = velocity / Mathf.Max(speed, 1e-3f);
                    ShowBoard(
                        trail,
                        true,
                        at - dir * length * 0.38f,
                        new Vector2(34f, length),
                        AngleOf(dir),
                        Gold,
                        0.85f * lit * (1f - collapse),
                        Hash(i + 7, plan.Seed),
                        k: t,
                        eaten: 0f,
                        brightness: 1.2f
                    );
                }
                else
                {
                    Hide(trail);
                }
            }
        }

        private Vector2 PieceAt(Piece info, int index, int count, float t)
        {
            float start = StartAngle(index, count);
            Vector2 entry = Polar(OrbitRadius, start);
            if (t <= Beats.FlyStart)
                return info.From;
            if (t < Beats.FlyEnd)
            {
                // 札から入口へ、上へふくらむ弧で飛ぶ。
                float u = Smooth(Ramp(t, Beats.FlyStart, Beats.FlyEnd));
                Vector2 lift = (info.From + entry) * 0.5f + new Vector2(0f, 220f);
                return Bezier(info.From, lift, entry, u);
            }
            return Orbit(OrbitRadius, start, Ramp(t, Beats.FlyEnd, beats.SwirlEnd));
        }

        // 渦の周り：中心ほど速く回る。半径は加速しながら縮み、角度は二乗で進む（渦の線と同じ回り方）。
        private static Vector2 Orbit(float radius, float start, float v) =>
            Polar(
                radius * Mathf.Pow(1f - v, 1.4f),
                start + Mathf.PI * 2f * (0.45f * v + 1.5f * v * v)
            );

        private static float StartAngle(int index, int count) =>
            Mathf.PI * 0.5f + index * Mathf.PI * 2f / Mathf.Max(1, count) + 0.35f;

        // 火の粉：渦の縁から、回る向きと外向きの間へ飛び、空気に押されて遅くなりながら消える。
        private void ShowSparks(float t)
        {
            for (int i = 0; i < Sparks.Length; i++)
            {
                var spark = Sparks[i];
                float a = Hash(i, plan.Seed + 0.31f);
                float b = Hash(i + 40, plan.Seed + 0.31f);
                float born = 0.35f + 0.45f * a;
                float life = 0.25f + 0.2f * b;
                float age = t - born;
                if (age < 0f || age > life)
                {
                    Hide(spark);
                    continue;
                }
                float v = Ramp(born, Beats.FlyEnd, beats.SwirlEnd);
                Vector2 from = Orbit(OrbitRadius, b * Mathf.PI * 2f, v) * 1.1f;
                Vector2 outward = from.sqrMagnitude > 1f ? from.normalized : Vector2.up;
                Vector2 along = new(-outward.y, outward.x);
                Vector2 dir = (along * 0.75f + outward * 0.65f).normalized;
                float speed = 700f + 500f * a;
                float travel = speed * (1f - Mathf.Exp(-age * 4.5f)) / 4.5f;
                ShowBoard(
                    spark,
                    true,
                    from + dir * travel,
                    Vector2.one * (16f + 14f * b),
                    0f,
                    i % 3 == 0 ? WarmWhite : Gold,
                    Mathf.Sin(Mathf.Clamp01(age / life) * Mathf.PI),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.4f
                );
            }
        }

        // ☆4・☆5：渦の腕の上で、ずれて瞬くきらめき（金、虹は淡い色）。
        private void ShowGlints(float t, bool rich)
        {
            int used =
                !rich ? 0
                : plan.Look == Look.Rainbow ? Glints.Length
                : Glints.Length * 3 / 4;
            for (int i = 0; i < Glints.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.17f);
                float b = Hash(i + 21, plan.Seed + 0.17f);
                float born = Beats.SwirlStart + 0.06f + 0.35f * a;
                float life = 0.28f;
                float age = t - born;
                if (i >= used || age < 0f || age > life || t > Beats.CollapseStart + 0.05f)
                {
                    Hide(Glints[i]);
                    continue;
                }
                float v = Ramp(t, Beats.FlyEnd, beats.SwirlEnd);
                Vector2 at = Orbit(SwirlSize * 0.42f * (0.55f + 0.45f * b), a * Mathf.PI * 2f, v);
                ShowBoard(
                    Glints[i],
                    true,
                    at,
                    Vector2.one * (46f + 26f * b),
                    0f,
                    plan.Look == Look.Rainbow ? Pastel(a) : WarmWhite,
                    Mathf.Sin(age / life * Mathf.PI),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.3f
                );
            }
        }

        // ☆4・☆5：弾ける前の溜め。画面の端から中央へ、細い光の線が次々に吸い込まれる。
        private void ShowConverge(float t, bool rich)
        {
            int used =
                !rich ? 0
                : plan.Look == Look.Rainbow ? Converge.Length
                : Converge.Length * 3 / 5;
            float from = Beats.CollapseStart - 0.04f;
            float window = beats.Burst - 0.02f - from;
            for (int i = 0; i < Converge.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.53f);
                float b = Hash(i + 31, plan.Seed + 0.53f);
                const float life = 0.14f;
                float born = from + (window - life) * a;
                float age = t - born;
                if (i >= used || age < 0f || age > life)
                {
                    Hide(Converge[i]);
                    continue;
                }
                float u = age / life;
                float angle = (i + 0.5f * b) / Mathf.Max(1, used) * Mathf.PI * 2f;
                Vector2 outward = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float radius = Mathf.Lerp(1150f, 80f, u * u);
                float length = 200f + 120f * b;
                ShowBoard(
                    Converge[i],
                    true,
                    outward * (radius + length * 0.38f),
                    new Vector2(26f, length),
                    AngleOf(-outward),
                    plan.Look == Look.Rainbow ? Pastel(i / (float)Mathf.Max(1, used)) : DeepGold,
                    Mathf.Sin(u * Mathf.PI),
                    b,
                    k: age,
                    eaten: 0f,
                    brightness: 1.3f
                );
            }
        }

        // 光の球：渦の中央に育ち、詰まると白く強くなり、弾けて消える。溜める間は脈打つ。
        private void ShowOrb(float t)
        {
            float grow = Smooth(Ramp(t, 0.5f, beats.Burst));
            float pop = Ramp(t, beats.Burst, beats.Burst + 0.1f);
            float after = Ramp(t, beats.Burst, beats.Burst + 0.2f);
            float hold = Ramp(t, 0.92f, beats.Burst);
            float pulse = 1f + 0.08f * hold * Mathf.Sin((t - 0.92f) * 60f);
            ShowBoard(
                OrbHalo,
                t > 0.45f && t < beats.Burst + 0.2f,
                Vector2.zero,
                Vector2.one * Mathf.Lerp(140f, 460f, grow) * (1f + 0.4f * after) * pulse,
                0f,
                plan.Look == Look.Rainbow ? WarmWhite : Gold,
                0.7f * grow * (1f - after),
                plan.Seed,
                k: t,
                eaten: 0f,
                brightness: 1f
            );
            float hot = Smooth(Ramp(t, Beats.CollapseStart, beats.Burst));
            ShowBoard(
                OrbCore,
                t > 0.5f && t < beats.Burst + 0.1f,
                Vector2.zero,
                Vector2.one * (Mathf.Lerp(70f, 200f, hot) * pulse + 160f * pop),
                0f,
                Color.Lerp(Gold, Color.white, hot),
                Mathf.Lerp(0.35f, 1f, hot) * (1f - pop),
                plan.Seed + 0.5f,
                k: t,
                eaten: 0f,
                brightness: 1f + 1.2f * hot
            );
        }

        // 弾け：細い光線が伸びて縮み、輪が外へ広がって欠けながら消える。
        // ☆4は光線を倍にして金と白の2重の輪、☆5は白と淡い虹の輪を重ねる。
        private void ShowBurst(float t)
        {
            float burst = beats.Burst;
            bool rich = plan.Look != Look.Plain;
            float rays = Ramp(t, burst, burst + 0.4f);
            var rayTint = plan.Look switch
            {
                Look.Rainbow => WarmWhite,
                Look.Gold => DeepGold,
                _ => Color.Lerp(plan.Quality, Gold, 0.5f),
            };
            float raySize = rich ? 1180f : 980f;
            ShowBoard(
                Rays,
                t >= burst && rays < 1f,
                Vector2.zero,
                Vector2.one * raySize,
                0f,
                rayTint,
                1f - rays * rays,
                plan.Seed,
                rays,
                rays,
                1.3f
            );
            ShowBoard(
                MoreRays,
                rich && t >= burst && rays < 1f,
                Vector2.zero,
                Vector2.one * raySize * 0.9f,
                22.5f,
                rayTint,
                0.85f * (1f - rays * rays),
                plan.Seed + 0.41f,
                rays,
                rays,
                1.3f
            );

            // 輪は出来た物のまわりに居座らず、すぐ外へ抜ける。
            float ring = Ramp(t, burst, burst + 0.35f);
            float spread = 1f - (1f - ring) * (1f - ring);
            var ringTint = plan.Look switch
            {
                Look.Rainbow => Color.white,
                Look.Gold => DeepGold,
                _ => Color.Lerp(plan.Quality, Color.white, 0.2f),
            };
            ShowBoard(
                Ring,
                t >= burst && ring < 1f,
                Vector2.zero,
                Vector2.one * Mathf.Lerp(240f, 1600f, spread),
                0f,
                ringTint,
                1f,
                plan.Seed + 0.25f,
                ring,
                ring,
                1.2f
            );

            // 重ねる輪：金は少し遅れる白い輪、虹は少しずつ大きさの違う淡い3色の輪。
            for (int i = 0; i < MoreRings.Length; i++)
            {
                bool used = plan.Look == Look.Rainbow || (plan.Look == Look.Gold && i == 0);
                float delay = plan.Look == Look.Gold ? 0.05f : 0.02f;
                float r = Ramp(t, burst + delay, burst + delay + 0.38f);
                float s = 1f - (1f - r) * (1f - r);
                float scale = plan.Look == Look.Rainbow ? 0.94f + 0.04f * i : 0.88f;
                ShowBoard(
                    MoreRings[i],
                    used && t >= burst + delay && r < 1f,
                    Vector2.zero,
                    Vector2.one * Mathf.Lerp(240f, 1600f, s) * scale,
                    0f,
                    plan.Look == Look.Rainbow ? Pastel(i / 3f + 0.05f) : Color.white,
                    plan.Look == Look.Rainbow ? 0.8f : 1f,
                    plan.Seed + 0.6f + i * 0.1f,
                    r,
                    r,
                    1.1f
                );
            }
        }

        // ☆4・☆5：弾けて外へ飛び、空気に押されて止まってから舞い落ちる小片（金箔、虹は淡い色）と、
        // ☆5で画面いっぱいに散って瞬くきらめき。
        private void ShowFlakes(float t, bool rich)
        {
            float burst = beats.Burst;
            int used =
                !rich ? 0
                : plan.Look == Look.Rainbow ? Flakes.Length
                : Flakes.Length * 3 / 4;
            float end = beats.ReturnStart + 0.2f;
            for (int i = 0; i < Flakes.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.71f);
                float b = Hash(i + 50, plan.Seed + 0.71f);
                float age = t - burst;
                float life = end - burst - 0.1f * b;
                if (i >= used || age < 0f || age > life)
                {
                    Hide(Flakes[i]);
                    continue;
                }
                float angle = (i + a) / Mathf.Max(1, used) * Mathf.PI * 2f;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = 900f + 700f * b;
                Vector2 at =
                    dir * (speed * (1f - Mathf.Exp(-age * 6f)) / 6f)
                    + Vector2.down * (90f * age * age);
                at.x += Mathf.Sin(age * 7f + a * 6f) * 18f * Ramp(age, 0.15f, 0.4f);
                float flicker = 0.65f + 0.35f * Mathf.Sin(age * 22f + b * 9f);
                ShowBoard(
                    Flakes[i],
                    true,
                    at,
                    new Vector2(30f, 62f) * (0.8f + 0.5f * a),
                    age * (200f + 300f * a) * (i % 2 == 0 ? 1f : -1f),
                    plan.Look == Look.Rainbow ? Pastel(a) : Gold,
                    flicker * (1f - Ramp(age, life * 0.7f, life)),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.7f
                );
            }
            for (int i = 0; i < Prisms.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.93f);
                float b = Hash(i + 60, plan.Seed + 0.93f);
                float born = burst + 0.02f + 0.35f * a;
                const float life = 0.3f;
                float age = t - born;
                if (plan.Look != Look.Rainbow || age < 0f || age > life)
                {
                    Hide(Prisms[i]);
                    continue;
                }
                Vector2 at = new((a - 0.5f) * 1700f, (b - 0.5f) * 900f);
                ShowBoard(
                    Prisms[i],
                    true,
                    at,
                    Vector2.one * (64f + 60f * b),
                    0f,
                    Pastel(b),
                    Mathf.Sin(age / life * Mathf.PI),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.4f
                );
            }
        }

        // 出来た物：弾けた所に大きく現れ、少し浮いてから、右上の絵の位置へ縮みながら戻る。
        private void ShowItem(float t)
        {
            float burst = beats.Burst;
            bool on = t >= burst;
            if (Item != null)
                Item.gameObject.SetActive(on && plan.ItemArt != null);
            if (!on)
            {
                Hide(ItemGlow);
                Hide(Sheen);
                foreach (var twinkle in Twinkles)
                    Hide(twinkle);
                foreach (var ring in ItemRings)
                    Hide(ring);
                return;
            }
            float appear = Ramp(t, burst, burst + 0.2f);
            float back = Smooth(Ramp(t, beats.ReturnStart, beats.Seconds));
            float bob = Mathf.Sin((t - burst) * 6f) * 6f * (1f - back);
            Vector2 at = Vector2.Lerp(new Vector2(0f, bob), plan.ItemHome, back);
            float scale = Mathf.Lerp(ItemScale * OvershootOut(appear), 1f, back);
            if (Item != null && plan.ItemArt != null)
            {
                Item.texture = plan.ItemArt;
                Item.uvRect = plan.ItemUv;
                Item.color = plan.ItemTint;
                var rect = Item.rectTransform;
                rect.anchoredPosition = at;
                rect.sizeDelta = plan.ItemSize;
                rect.localScale = Vector3.one * scale;
            }
            float glow = (1f - Ramp(t, burst, beats.ReturnStart)) * 0.55f + 0.2f * (1f - back);
            ShowBoard(
                ItemGlow,
                true,
                at,
                plan.ItemSize * (scale * 1.35f),
                0f,
                plan.Look == Look.Rainbow ? WarmWhite : Color.Lerp(plan.Quality, Gold, 0.4f),
                glow * Smooth(appear),
                plan.Seed + 0.75f,
                k: t,
                eaten: 0f,
                brightness: 1f
            );
            // きらめき：絵のまわりで、ずれて瞬く（☆4・☆5は数を倍にする）。
            int twinkles =
                plan.Look == Look.Plain ? Mathf.Min(3, Twinkles.Length) : Twinkles.Length;
            for (int i = 0; i < Twinkles.Length; i++)
            {
                float born = burst + 0.06f + 0.06f * i;
                const float life = 0.3f;
                float age = t - born;
                if (i >= twinkles || age < 0f || age > life)
                {
                    Hide(Twinkles[i]);
                    continue;
                }
                float a = Hash(i, plan.Seed + 0.9f);
                Vector2 offset = Polar(
                    plan.ItemSize.x * scale * (0.38f + 0.18f * a),
                    a * Mathf.PI * 2f
                );
                ShowBoard(
                    Twinkles[i],
                    true,
                    at + offset,
                    Vector2.one * (70f + 40f * a),
                    0f,
                    plan.Look == Look.Rainbow ? Pastel(a) : WarmWhite,
                    Mathf.Sin(age / life * Mathf.PI),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.3f
                );
            }
            // ☆4・☆5：光が絵の左下から右上へ走る（刃に沿う光）。
            float sheen = Ramp(t, burst + 0.12f, burst + 0.34f);
            Vector2 diagonal = plan.ItemSize * (scale * 0.42f);
            ShowBoard(
                Sheen,
                plan.Look != Look.Plain && sheen > 0f && sheen < 1f,
                at + Vector2.Lerp(-diagonal, diagonal, Smooth(sheen)),
                Vector2.one * 90f,
                0f,
                Color.white,
                Mathf.Sin(sheen * Mathf.PI),
                plan.Seed + 0.3f,
                k: sheen,
                eaten: 0f,
                brightness: 1.5f
            );
            // ☆5：絵の後ろで、淡い虹の細い輪がゆっくり回る。
            for (int i = 0; i < ItemRings.Length; i++)
            {
                float lit = Smooth(Ramp(t, burst + 0.1f, burst + 0.25f)) * (1f - back);
                ShowBoard(
                    ItemRings[i],
                    plan.Look == Look.Rainbow && lit > 0f,
                    at,
                    plan.ItemSize * (scale * (1.55f + 0.08f * i)),
                    (t - burst) * (i == 0 ? 50f : -35f),
                    Pastel(0.1f + 0.45f * i),
                    0.45f * lit,
                    plan.Seed + 0.2f * i,
                    k: 0.12f,
                    eaten: 0.12f,
                    brightness: 1f
                );
            }
        }

        private static void ShowBoard(
            BattleVfxImage board,
            bool on,
            Vector2 at,
            Vector2 size,
            float angle,
            Color tint,
            float alpha,
            float seed,
            float k,
            float eaten,
            float brightness
        )
        {
            if (board == null)
                return;
            bool shown = on && alpha > 0.002f;
            board.gameObject.SetActive(shown);
            if (!shown)
                return;
            var rect = board.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            rect.localEulerAngles = new Vector3(0f, 0f, angle);
            board.color = WithAlpha(tint, alpha);
            board.Shape = new Vector4(seed, k, eaten, brightness);
        }

        private static void Hide(Component board)
        {
            if (board != null)
                board.gameObject.SetActive(false);
        }

        // 光の筋の形（+yへ進む）を、向き dir へ回す角度（度）。
        private static float AngleOf(Vector2 dir) =>
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

        // 淡い虹の1色（色相 hue、濃くしない）。
        private static Color Pastel(float hue) => Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.38f, 1f);

        private static Vector2 Polar(float radius, float angle) =>
            new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float u) =>
            (1f - u) * (1f - u) * a + 2f * (1f - u) * u * b + u * u * c;

        private static float Ramp(float t, float from, float to) =>
            Mathf.Clamp01((t - from) / Mathf.Max(1e-4f, to - from));

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        private static float Pulse(float t, float at, float width) =>
            Mathf.Exp(-((t - at) * (t - at)) / (width * width));

        // 少し行き過ぎてから戻る（0から1）。
        private static float OvershootOut(float x)
        {
            const float back = 1.6f;
            float u = x - 1f;
            return 1f + (back + 1f) * u * u * u + back * u * u;
        }

        private static float Hash(int i, float seed) =>
            Mathf.Repeat(Mathf.Sin(i * 12.9898f + seed * 78.233f) * 43758.547f, 1f);

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
