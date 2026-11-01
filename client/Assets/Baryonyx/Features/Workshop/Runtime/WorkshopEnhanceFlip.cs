using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Workshop
{
    /// <summary>
    /// The flip played over the whole screen when something is enhanced
    /// (doc/features/enhancement.md 品質強化の演出): the screen sinks, the card leaves its place
    /// for the middle, spins and takes in the stones flying from their tiles, slows to show its
    /// back, and turns over as the card after. Every enhancement plays it; one that makes the
    /// thing A charges with gold light on the back and turns over with a gold flash, and one that
    /// makes it S charges with faint rainbow light and turns over with the richest flash
    /// (<see cref="Look"/>). <see cref="Show"/> draws it at a time since the press, so
    /// <see cref="Play"/> runs it frame by frame and any moment can be shown still.
    /// </summary>
    public sealed class WorkshopEnhanceFlip : MonoBehaviour
    {
        /// <summary>How rich it is: plain under A, gold for A, rainbow for S.</summary>
        public enum Look
        {
            Plain,
            Gold,
            Rainbow,
        }

        /// <summary>
        /// The beats (seconds from the press; doc/features/enhancement.md 品質強化の演出). Gold
        /// and rainbow spin a little longer and hold the back while they charge; every look stops
        /// still in the middle for a moment after the turn before going back.
        /// </summary>
        public readonly struct Beats
        {
            public const float FlyEnd = 0.26f;
            public const float SpinStart = 0.18f;
            public const float StoneStart = 0.04f;
            public const float StoneFlight = 0.32f;
            public const float StoneGap = 0.035f;
            public const float TurnTime = 0.12f;
            public const float ReturnTime = 0.3f;

            public Beats(float spinEnd, float hold, float linger)
            {
                SpinEnd = spinEnd;
                Turn = spinEnd + hold;
                Burst = Turn + TurnTime;
                ReturnStart = Burst + linger;
                Seconds = ReturnStart + ReturnTime;
            }

            /// <summary>The spin has slowed to show the back.</summary>
            public float SpinEnd { get; }

            /// <summary>The last turn starts, from the back to the card after.</summary>
            public float Turn { get; }

            /// <summary>The card after faces the front and the light bursts.</summary>
            public float Burst { get; }

            public float ReturnStart { get; }
            public float Seconds { get; }

            public static Beats Of(Look look) =>
                look switch
                {
                    Look.Rainbow => new Beats(0.7f, 0.5f, 0.55f),
                    Look.Gold => new Beats(0.66f, 0.3f, 0.55f),
                    _ => new Beats(0.6f, 0f, 0.5f),
                };
        }

        // 回る角度（度）：速く回って裏を見せて止まり、最後の半回転で強化した後の表を向く。
        private const float SpinTurns = 540f;
        private const float AfterFrom = 450f;

        // 暗転の色（紺）と、光の色（金・白）。
        private static readonly Color Night = new(0.02f, 0.03f, 0.08f, 1f);
        private static readonly Color Gold = new(1f, 0.8f, 0.45f, 1f);
        private static readonly Color DeepGold = new(1f, 0.7f, 0.28f, 1f);
        private static readonly Color WarmWhite = new(1f, 0.95f, 0.86f, 1f);

        [Tooltip("画面全体を沈める暗転。押すと演出を飛ばす。")]
        public Image Dim;
        public Button Skip;

        [Tooltip("中央で回るカード：強化する前と後の表（複製を入れる箱）と、裏の絵。")]
        public RectTransform Card;
        public RectTransform BeforeFace;
        public RectTransform AfterFace;
        public RawImage Back;

        [Tooltip("カードの上に重ねる影（裏の絵の形）。横向きに近いほど暗くし、回る面の向きを見せる。")]
        public RawImage Shade;

        [Tooltip("金・虹：裏を見せて溜める間、カードの縁から漏れる光と、その後ろの広い光。")]
        public Image Rim;
        public BattleVfxImage CardGlow;

        [Tooltip("磨き石の絵と、その後ろの光、飛ぶ間の光の筋（使う石の数まで使う）。")]
        public RawImage[] Stones = Array.Empty<RawImage>();
        public BattleVfxImage[] StoneGlows = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] StoneTrails = Array.Empty<BattleVfxImage>();

        [Tooltip(
            "回るカードのまわりを巡る光の軌跡（Enhance Orbitシェーダー）。奥の半分はカードの奥、手前の半分はカードの手前に置き、"
                + "同じ番号の2枚で1本の軌跡を描く。"
        )]
        public BattleVfxImage[] OrbitBacks = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] OrbitFronts = Array.Empty<BattleVfxImage>();

        [Tooltip("石が軌跡の入口に飛び込んだときの光。")]
        public BattleVfxImage Absorb;

        [Tooltip("金・虹：溜める間にカードの縁から昇る火の粉と、虹で画面の端から吸い込まれる細い線。")]
        public BattleVfxImage[] Embers = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Converge = Array.Empty<BattleVfxImage>();

        [Tooltip("表を向いた瞬間に、カードの縁から外へ飛び散る火花（どの段階も。金・虹は数を増やす）。")]
        public BattleVfxImage[] Bursts = Array.Empty<BattleVfxImage>();

        [Tooltip("表を向いたときの光線と輪。金・虹は光線を重ね、輪を増やす。")]
        public BattleVfxImage Rays;
        public BattleVfxImage MoreRays;
        public BattleVfxImage Ring;
        public BattleVfxImage[] MoreRings = Array.Empty<BattleVfxImage>();

        [Tooltip("表を向いたときの、カードの四隅のきらめきと、まわりで瞬くきらめき。")]
        public BattleVfxImage[] Corners = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Twinkles = Array.Empty<BattleVfxImage>();

        [Tooltip("金・虹：カードの上を斜めに走る光。虹：弾けて舞う淡い小片と、画面に散るきらめき。")]
        public BattleVfxImage Sheen;
        public BattleVfxImage[] Flakes = Array.Empty<BattleVfxImage>();
        public BattleVfxImage[] Prisms = Array.Empty<BattleVfxImage>();

        [Tooltip("暗転の濃さと、カードを中央で見せる倍率。")]
        public float DimAlpha = 0.84f;
        public float CardScale = 1.6f;

        private Plan plan = new();
        private Beats beats = Beats.Of(Look.Plain);
        private float time;
        private Action finished;
        private Coroutine running;

        /// <summary>
        /// What one enhancement shows: where the card sits and how big it is, each stone's art
        /// and where its tile is, the stones' colour, how rich it is and the seed of its shapes.
        /// Positions are in this effect's own space (0 at its middle). The faces are copied in
        /// with <see cref="UseFaces"/>.
        /// </summary>
        public sealed class Plan
        {
            public readonly List<Stone> Stones = new();
            public Vector2 CardHome;
            public Vector2 CardSize = new(280f, 392f);
            public Color StoneColor = Color.white;
            public Look Look;
            public float Seed;
        }

        public struct Stone
        {
            public Texture Art;
            public Vector2 Size;
            public Vector2 From;
        }

        /// <summary>True while it plays.</summary>
        public bool Playing => running != null;

        /// <summary>How rich an enhancement that leaves the thing at <paramref name="after"/> makes it.</summary>
        public static Look LookOf(ItemQuality after) =>
            after switch
            {
                ItemQuality.S => Look.Rainbow,
                ItemQuality.A => Look.Gold,
                _ => Look.Plain,
            };

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

        /// <summary>
        /// Copies the card as it is before (<paramref name="before"/>) and as it will be after
        /// (<paramref name="after"/>) to turn in the middle, each drawn as it is now on the panel.
        /// </summary>
        public void UseFaces(RectTransform before, RectTransform after)
        {
            CopyFace(BeforeFace, before);
            CopyFace(AfterFace, after);
        }

        private static void CopyFace(RectTransform holder, RectTransform face)
        {
            if (holder == null)
                return;
            Clear(holder);
            if (face == null)
                return;
            var copy = (RectTransform)Instantiate(face.gameObject, holder, false).transform;
            copy.gameObject.SetActive(true);
            copy.anchorMin = copy.anchorMax = copy.pivot = new Vector2(0.5f, 0.5f);
            copy.anchoredPosition = Vector2.zero;
            copy.sizeDelta = face.rect.size;
            copy.localScale = Vector3.one;
            copy.localRotation = Quaternion.identity;
            // 複製は押せないようにする（下の画面の札と同じ物が反応しないよう）。
            foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        private static void Clear(RectTransform holder)
        {
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                var child = holder.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
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

        /// <summary>Jumps to where the card goes back (pressing anywhere does this).</summary>
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
                // 録画中（Recorderがコマの時間を決めている間）は、そのコマの時間で進め、動画で実際の速さに見せる。
                time += Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
                Show(Mathf.Min(time, beats.Seconds), plan);
            }
            running = null;
            End();
        }

        private void End(bool hide = true)
        {
            if (BeforeFace != null)
                Clear(BeforeFace);
            if (AfterFace != null)
                Clear(AfterFace);
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

            // 暗転：押すとすぐ沈み、戻るときに解ける。虹は溜める間もう一段沈み、表を向いた瞬間だけ少し緩む。
            if (Dim != null)
            {
                float sink =
                    Smooth(Ramp(t, 0f, 0.15f))
                    * (1f - Smooth(Ramp(t, beats.ReturnStart, beats.Seconds)));
                float deeper =
                    plan.Look == Look.Rainbow
                        ? 0.08f
                            * Smooth(Ramp(t, beats.SpinEnd, beats.Turn))
                            * (1f - Ramp(t, beats.Burst, beats.Burst + 0.1f))
                        : 0f;
                float ease = plan.Look == Look.Plain ? 0f : 0.2f * Pulse(t, beats.Burst + 0.02f, 0.05f);
                Dim.color = WithAlpha(Night, (DimAlpha + deeper) * sink - ease);
            }

            var (at, scale) = CardAt(t);
            ShowGlow(t, at, scale);
            ShowCard(t, at, scale);
            ShowStones(t, at);
            ShowOrbits(t, at);
            ShowAbsorb(t, at);
            ShowEmbers(t, at, scale);
            ShowConverge(t, at);
            ShowBurst(t, at);
            ShowBurstSparks(t, at, scale);
            ShowCorners(t, at, scale);
            ShowTwinkles(t, at, scale);
            ShowSheen(t, at, scale);
            ShowFlakes(t, at);
        }

        // カードの位置と倍率：その場から中央へ弧を描いて大きくなり、表を向くと少し弾み、元の場所へ縮みながら戻る。
        private (Vector2 at, float scale) CardAt(float t)
        {
            float go = Smooth(Ramp(t, 0f, Beats.FlyEnd));
            float back = Smooth(Ramp(t, beats.ReturnStart, beats.Seconds));
            Vector2 lift = plan.CardHome * 0.5f + new Vector2(0f, 90f);
            Vector2 middle = Vector2.zero;
            Vector2 at =
                back > 0f ? Vector2.Lerp(middle, plan.CardHome, back)
                : Bezier(plan.CardHome, lift, middle, go);
            // 溜める間は、ゆっくり息をするように揺れる。
            float hold = Ramp(t, beats.SpinEnd, beats.Turn) * (1f - Ramp(t, beats.Turn, beats.Burst));
            at.y += Mathf.Sin((t - beats.SpinEnd) * 9f) * 6f * hold;
            float pop = plan.Look == Look.Plain ? 0.07f : 0.1f;
            float scale =
                Mathf.Lerp(1f, CardScale, go) * (1f + pop * Pulse(t, beats.Burst + 0.04f, 0.07f));
            scale = Mathf.Lerp(scale, 1f, back);
            return (at, scale);
        }

        // 回る角度（度）：初めは速く、だんだん遅くなって裏を見せ、最後の半回転で表を向く。
        private float AngleAt(float t)
        {
            float spin = Ramp(t, Beats.SpinStart, beats.SpinEnd);
            float eased = 1f - Mathf.Pow(1f - spin, 2.4f);
            float turn = Smooth(Ramp(t, beats.Turn, beats.Burst));
            return SpinTurns * eased + 180f * turn;
        }

        private void ShowCard(float t, Vector2 at, float scale)
        {
            if (Card == null)
                return;
            float angle = AngleAt(t);
            float facing = Mathf.Cos(angle * Mathf.Deg2Rad);
            Card.anchoredPosition = at;
            Card.sizeDelta = plan.CardSize;
            // 縦の軸で回る：横の幅だけ縮め、薄い所でも消えないよう線の太さを残す。
            Card.localScale = new Vector3(scale * Mathf.Max(Mathf.Abs(facing), 0.03f), scale, 1f);
            bool back = facing < 0f;
            bool after = angle >= AfterFrom;
            if (Back != null)
            {
                Back.gameObject.SetActive(back);
                Back.rectTransform.sizeDelta = plan.CardSize;
            }
            if (BeforeFace != null)
                BeforeFace.gameObject.SetActive(!back && !after);
            if (AfterFace != null)
                AfterFace.gameObject.SetActive(!back && after);
            // 面が横を向くほど、光の当たり方が変わって暗くなる。正面では消す。
            if (Shade != null)
            {
                float dark = 0.6f * (1f - Mathf.Abs(facing));
                Shade.gameObject.SetActive(dark > 0.01f);
                Shade.color = new Color(0f, 0f, 0f, dark);
            }
        }

        // 縁から漏れる光と広い光：金・虹は裏を見せて溜める間に強まり、表を向くと消える。普通は回る間だけ薄く灯る。
        private void ShowGlow(float t, Vector2 at, float scale)
        {
            float charge =
                plan.Look == Look.Plain ? 0f
                : Smooth(Ramp(t, beats.SpinEnd - 0.1f, beats.Turn))
                    * (1f - Ramp(t, beats.Burst, beats.Burst + 0.25f));
            Color tint = LookColor(t);
            if (Rim != null)
            {
                bool on = charge > 0.01f;
                Rim.gameObject.SetActive(on);
                if (on)
                {
                    float facing = Mathf.Abs(Mathf.Cos(AngleAt(t) * Mathf.Deg2Rad));
                    var rect = Rim.rectTransform;
                    rect.anchoredPosition = at;
                    float pulse = 1f + 0.03f * Mathf.Sin(t * 26f);
                    rect.sizeDelta = new Vector2(
                        plan.CardSize.x * scale * Mathf.Max(facing, 0.03f) + 40f * pulse,
                        plan.CardSize.y * scale + 40f * pulse
                    );
                    Rim.color = WithAlpha(Color.Lerp(tint, Color.white, 0.3f), 0.75f * charge);
                }
            }
            float spinLit =
                Smooth(Ramp(t, Beats.SpinStart, Beats.SpinStart + 0.15f))
                * (1f - Ramp(t, beats.SpinEnd, beats.Burst + 0.2f));
            float glow = Mathf.Max(0.3f * spinLit, 0.45f * charge);
            float after = plan.Look == Look.Plain
                ? 0.25f * Pulse(t, beats.Burst + 0.03f, 0.08f)
                : 0.45f * (1f - Ramp(t, beats.Burst, beats.ReturnStart));
            ShowBoard(
                CardGlow,
                t < beats.ReturnStart + 0.1f,
                at,
                plan.CardSize * (scale * (1.25f + 0.2f * charge)),
                0f,
                plan.Look == Look.Plain ? Color.Lerp(plan.StoneColor, WarmWhite, 0.4f) : tint,
                Mathf.Max(glow, after * Ramp(t, beats.Burst - 0.02f, beats.Burst + 0.04f)),
                plan.Seed + 0.11f,
                k: t,
                eaten: 0f,
                brightness: 1f
            );
        }

        // 磨き石：札から1つずつ、上へふくらむ弧で回るカードへ飛び込み、着く前に白くなって縮む。
        private void ShowStones(float t, Vector2 card)
        {
            int count = plan.Stones.Count;
            for (int i = 0; i < Stones.Length; i++)
            {
                float start = Beats.StoneStart + Beats.StoneGap * i;
                float u = Ramp(t, start, start + Beats.StoneFlight);
                bool used = i < count && t >= start && u < 1f;
                var art = Stones[i];
                var glow = i < StoneGlows.Length ? StoneGlows[i] : null;
                var trail = i < StoneTrails.Length ? StoneTrails[i] : null;
                if (art != null)
                    art.gameObject.SetActive(used);
                if (!used)
                {
                    Hide(glow);
                    Hide(trail);
                    continue;
                }
                var info = plan.Stones[i];
                Vector2 entry = OrbitEntry(i, card);
                Vector2 at = StoneAt(info, i, start, t, entry);
                Vector2 before = StoneAt(info, i, start, t - 0.02f, entry);
                Vector2 velocity = (at - before) / 0.02f;
                float near = Ramp(u, 0.7f, 1f);
                float size = Mathf.Lerp(1.15f, 0.35f, near * near);
                if (art != null)
                {
                    art.texture = info.Art;
                    art.color = Color.Lerp(Color.white, WarmWhite, near);
                    var rect = art.rectTransform;
                    rect.anchoredPosition = at;
                    rect.sizeDelta = info.Size;
                    rect.localScale = Vector3.one * size;
                }
                float lit = Smooth(Ramp(u, 0f, 0.2f));
                ShowBoard(
                    glow,
                    true,
                    at,
                    Vector2.one * (info.Size.x * 1.9f * (0.5f + 0.5f * size)),
                    0f,
                    Color.Lerp(plan.StoneColor, Color.white, near),
                    0.6f * lit + 0.4f * near,
                    Hash(i, plan.Seed),
                    k: t,
                    eaten: 0f,
                    brightness: 1f + near
                );
                // 光の筋：進む向きの後ろへ、速いほど長く伸びる。
                float speed = velocity.magnitude;
                float length = Mathf.Clamp(speed * 0.11f, 0f, 260f);
                if (length > 12f)
                {
                    Vector2 dir = velocity / Mathf.Max(speed, 1e-3f);
                    ShowBoard(
                        trail,
                        true,
                        at - dir * length * 0.38f,
                        new Vector2(30f, length),
                        AngleOf(dir),
                        plan.StoneColor,
                        0.9f * lit,
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

        private Vector2 StoneAt(Stone info, int index, float start, float t, Vector2 entry)
        {
            float u = Smooth(Ramp(t, start, start + Beats.StoneFlight));
            // 札から、上へふくらむ弧で軌跡の入口へ飛ぶ。石ごとにふくらみをずらす。
            Vector2 bend =
                (info.From + entry) * 0.5f + new Vector2(0f, 200f + 80f * Hash(index, plan.Seed + 0.2f));
            return Bezier(info.From, bend, entry, u);
        }

        // 軌跡の楕円：カードの中心の近くを通り、番号ごとに傾き（度）・高さ・幅・平たさを変えて、斜めに交差させる。
        // 幅はカードの約1.2倍（すぐそばを小さく回る）から約3.1倍（大きく広く回る）まで。広いほど平たい。
        private static readonly float[] OrbitTilts =
        {
            -22f, 18f, -8f, 28f, -30f, 10f, 24f, -16f, 34f, -4f,
        };
        private static readonly float[] OrbitHeights =
        {
            0f, 0.08f, -0.1f, -0.04f, 0.12f, -0.14f, 0.16f, 0.04f, -0.06f, 0.1f,
        };
        private static readonly float[] OrbitWidths =
        {
            1.6f, 2.7f, 1.25f, 3.1f, 1.9f, 1.2f, 2.3f, 1.45f, 2.9f, 1.75f,
        };

        // 平たさ（高さ÷幅）：小さく回る軌跡ほど丸く、大きく回る軌跡ほど平たい。
        private static float OrbitFlat(float width) => Mathf.Lerp(0.34f, 0.17f, Mathf.InverseLerp(1.2f, 3.1f, width));

        // 楕円の線の中心の半径（板の大きさに対する割合）。シェーダーの Radius と同じ。
        private const float OrbitRadius = 0.86f;

        // 石が飛び込む入口：手前の右下（周の割合 -0.12）。
        private const float EntryAngle = -0.12f;

        private (Vector2 center, Vector2 size, float tilt) OrbitOf(int index, Vector2 card)
        {
            float scale = CardScale;
            float ratio = OrbitWidths[index % OrbitWidths.Length];
            float width = plan.CardSize.x * scale * ratio;
            float height = OrbitHeights[index % OrbitHeights.Length] * plan.CardSize.y * scale * 0.5f;
            return (
                card + new Vector2(0f, height),
                new Vector2(width, width * OrbitFlat(ratio)),
                OrbitTilts[index % OrbitTilts.Length]
            );
        }

        private Vector2 OrbitEntry(int index, Vector2 card)
        {
            var (center, size, tilt) = OrbitOf(index, card);
            float angle = EntryAngle * Mathf.PI * 2f;
            Vector2 local =
                new Vector2(Mathf.Cos(angle) * size.x, Mathf.Sin(angle) * size.y) * (0.5f * OrbitRadius);
            return center + (Vector2)(Quaternion.Euler(0f, 0f, tilt) * local);
        }

        // 軌跡を何本使うか：普通は6本、金は8本、虹は10本（板の数まで）。
        private int OrbitCount()
        {
            int slots = Mathf.Min(OrbitBacks.Length, OrbitFronts.Length);
            return plan.Look switch
            {
                Look.Rainbow => Mathf.Min(10, slots),
                Look.Gold => Mathf.Min(8, slots),
                _ => Mathf.Min(6, slots),
            };
        }

        // 軌跡の生まれる時刻と入口の角度：石の数までは石が入口に着いた時刻から、残りは回る間（金・虹は溜める間）に。
        private (float born, float from) OrbitStart(int index)
        {
            if (index < plan.Stones.Count && index < Stones.Length)
                return (Beats.StoneStart + Beats.StoneGap * index + Beats.StoneFlight, EntryAngle);
            float a = Hash(index, plan.Seed + 0.4f);
            int extra = index - Mathf.Min(plan.Stones.Count, Stones.Length);
            // 普通は回る間に次々と、金・虹は回り始めから溜めの半ばまでに散らす。
            float born =
                plan.Look == Look.Plain
                    ? Beats.SpinStart + 0.02f + 0.05f * extra
                    : Mathf.Lerp(
                        Beats.SpinStart + 0.02f,
                        (beats.SpinEnd + beats.Turn) * 0.5f,
                        extra / (float)Mathf.Max(1, OrbitCount() - 1)
                    );
            return (born, a);
        }

        // 光の軌跡：入口から反時計回りに楕円を巡り、表を向くまで回り続ける。小さく回る軌跡ほど速く（周の長さの
        // 平方根に反比例）、回るうちに少しずつ遅くなる。金・虹は溜める間にもう一度速まる。表を向いた瞬間に外へ
        // 大きく広がりながら薄れて消える。奥の半分はカードの奥の板、手前の半分は手前の板に描く（同じ値を渡す）。
        private void ShowOrbits(float t, Vector2 card)
        {
            int used = OrbitCount();
            float burst = beats.Burst;
            for (int i = 0; i < Mathf.Max(OrbitBacks.Length, OrbitFronts.Length); i++)
            {
                var back = i < OrbitBacks.Length ? OrbitBacks[i] : null;
                var front = i < OrbitFronts.Length ? OrbitFronts[i] : null;
                var (born, from) = OrbitStart(i);
                float age = t - born;
                float fling = Ramp(t, burst, burst + 0.32f);
                if (i >= used || age < 0f || fling >= 1f)
                {
                    Hide(back);
                    Hide(front);
                    continue;
                }
                float ratio = OrbitWidths[i % OrbitWidths.Length];
                float speed = 2.6f / Mathf.Sqrt(ratio / 1.2f);
                float head = from + speed * (1f - Mathf.Exp(-age * 1.6f)) / 1.6f;
                if (plan.Look != Look.Plain)
                {
                    float charge = Ramp(t, beats.SpinEnd, beats.Turn);
                    head += 0.9f * speed * charge * charge * (beats.Turn - beats.SpinEnd) * 0.5f;
                }
                float tail = (0.26f + 0.12f * Mathf.InverseLerp(1.2f, 3.1f, ratio)) * Smooth(Ramp(age, 0f, 0.14f));
                float alpha = Smooth(Ramp(age, 0f, 0.06f)) * (1f - fling * fling);
                var (center, size, tilt) = OrbitOf(i, card);
                // 表を向くと、カードの中心から外へ大きく広がる。
                float spread = 1f - (1f - fling) * (1f - fling);
                size *= 1f + 1.6f * spread;
                var tint = OrbitColor(i);
                foreach (var board in new[] { back, front })
                    ShowBoard(
                        board,
                        true,
                        center,
                        size,
                        tilt,
                        tint,
                        alpha,
                        Hash(i, plan.Seed + 0.45f),
                        k: head,
                        eaten: tail,
                        brightness: 1.25f + 0.5f * fling
                    );
            }
        }

        private Color OrbitColor(int i) =>
            plan.Look switch
            {
                Look.Rainbow => Pastel(0.13f * i + Hash(i, plan.Seed + 0.8f) * 0.2f),
                Look.Gold => i % 2 == 0 ? Gold : Color.Lerp(plan.StoneColor, WarmWhite, 0.5f),
                _ => Color.Lerp(plan.StoneColor, Color.white, 0.25f),
            };

        // 石が軌跡の入口に飛び込むたびに、そこが石の色で短く光る。
        private void ShowAbsorb(float t, Vector2 card)
        {
            float lit = 0f;
            int hit = -1;
            for (int i = 0; i < plan.Stones.Count && i < Stones.Length; i++)
            {
                float at = Beats.StoneStart + Beats.StoneGap * i + Beats.StoneFlight;
                float pulse = Pulse(t, at, 0.06f);
                if (pulse > lit)
                {
                    lit = pulse;
                    hit = i;
                }
            }
            ShowBoard(
                Absorb,
                hit >= 0 && lit > 0.01f,
                hit >= 0 ? OrbitEntry(hit, card) : card,
                Vector2.one * 180f,
                0f,
                Color.Lerp(plan.StoneColor, Color.white, 0.4f),
                0.85f * lit,
                plan.Seed + 0.33f,
                k: t,
                eaten: 0f,
                brightness: 1.4f
            );
        }

        // 金・虹：溜める間、カードの縁から火の粉が昇り、表を向くと外へ散る。
        private void ShowEmbers(float t, Vector2 at, float scale)
        {
            int used =
                plan.Look == Look.Plain ? 0
                : plan.Look == Look.Rainbow ? Embers.Length
                : Embers.Length * 2 / 3;
            Vector2 half = plan.CardSize * (scale * 0.5f);
            float from = beats.SpinEnd - 0.05f;
            float window = beats.Burst - from;
            for (int i = 0; i < Embers.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.61f);
                float b = Hash(i + 30, plan.Seed + 0.61f);
                float born = from + window * a;
                float life = 0.35f + 0.2f * b;
                float age = t - born;
                if (i >= used || age < 0f || age > life)
                {
                    Hide(Embers[i]);
                    continue;
                }
                // 縁の上のどこか（周を1本の線に伸ばして選ぶ）から、外向きと上向きの間へ。
                Vector2 edge = OnEdge(b, half);
                Vector2 outward = edge.normalized;
                Vector2 dir = (outward * 0.6f + Vector2.up * 0.8f).normalized;
                float travel = (220f + 160f * a) * (1f - Mathf.Exp(-age * 3f)) / 3f;
                ShowBoard(
                    Embers[i],
                    true,
                    at + edge + dir * travel,
                    Vector2.one * (14f + 12f * b),
                    0f,
                    plan.Look == Look.Rainbow ? Pastel(a) : (i % 3 == 0 ? WarmWhite : Gold),
                    Mathf.Sin(Mathf.Clamp01(age / life) * Mathf.PI),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.4f
                );
            }
        }

        // 虹：溜める間、画面の端から中央のカードへ、細い光の線が次々に吸い込まれる。
        private void ShowConverge(float t, Vector2 at)
        {
            int used =
                plan.Look == Look.Rainbow ? Converge.Length
                : plan.Look == Look.Gold ? Converge.Length * 3 / 5
                : 0;
            float from = beats.SpinEnd;
            float window = beats.Turn + 0.06f - from;
            for (int i = 0; i < Converge.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.53f);
                float b = Hash(i + 31, plan.Seed + 0.53f);
                const float life = 0.16f;
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
                float radius = Mathf.Lerp(1100f, 260f, u * u);
                float length = 180f + 120f * b;
                ShowBoard(
                    Converge[i],
                    true,
                    at + outward * (radius + length * 0.38f),
                    new Vector2(24f, length),
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

        // 表を向いた瞬間：普通は小さな輪が1本だけ。金は金の光線と金・白の輪、虹は光線を重ねて白と淡い虹の輪。
        private void ShowBurst(float t, Vector2 at)
        {
            float burst = beats.Burst;
            bool rich = plan.Look != Look.Plain;
            float rays = Ramp(t, burst, burst + 0.45f);
            var rayTint = plan.Look == Look.Rainbow ? WarmWhite : DeepGold;
            float raySize = plan.Look == Look.Rainbow ? 1900f : 1500f;
            ShowBoard(
                Rays,
                rich && t >= burst && rays < 1f,
                at,
                Vector2.one * raySize,
                0f,
                rayTint,
                1f - rays * rays,
                plan.Seed,
                rays,
                rays,
                1.8f
            );
            ShowBoard(
                MoreRays,
                plan.Look == Look.Rainbow && t >= burst && rays < 1f,
                at,
                Vector2.one * raySize * 0.85f,
                22.5f,
                Pastel(plan.Seed),
                0.85f * (1f - rays * rays),
                plan.Seed + 0.41f,
                rays,
                rays,
                1.3f
            );

            // 輪はカードのまわりに居座らず、すぐ外へ抜ける。普通も大きく広がり、少し遅れて2本目が続く。
            float ringTime = rich ? 0.38f : 0.36f;
            float ring = Ramp(t, burst, burst + ringTime);
            float spread = 1f - (1f - ring) * (1f - ring);
            // 輪はカードの外から広がり始める。
            Vector2 ringFrom = Vector2.one * plan.CardSize.y * CardScale * 1.05f;
            float ringTo = rich ? 1700f : 1450f;
            var ringTint = plan.Look switch
            {
                Look.Rainbow => Color.white,
                Look.Gold => DeepGold,
                _ => Color.Lerp(plan.StoneColor, Color.white, 0.35f),
            };
            ShowBoard(
                Ring,
                t >= burst && ring < 1f,
                at,
                Vector2.Lerp(ringFrom, Vector2.one * ringTo, spread),
                0f,
                ringTint,
                1f,
                plan.Seed + 0.25f,
                ring,
                ring,
                rich ? 1.2f : 1.15f
            );
            for (int i = 0; i < MoreRings.Length; i++)
            {
                bool used = plan.Look == Look.Rainbow || i == 0;
                float delay = plan.Look == Look.Rainbow ? 0.03f + 0.02f * i : 0.05f;
                float r = Ramp(t, burst + delay, burst + delay + 0.4f);
                float s = 1f - (1f - r) * (1f - r);
                float size = plan.Look == Look.Rainbow ? 0.9f + 0.05f * i : 0.86f;
                ShowBoard(
                    MoreRings[i],
                    used && t >= burst + delay && r < 1f,
                    at,
                    Vector2.Lerp(ringFrom, Vector2.one * ringTo * size, s),
                    0f,
                    plan.Look switch
                    {
                        Look.Rainbow => Pastel(i / 3f + 0.05f),
                        Look.Gold => Color.white,
                        _ => Color.Lerp(plan.StoneColor, Color.white, 0.6f),
                    },
                    plan.Look == Look.Rainbow ? 0.85f : plan.Look == Look.Gold ? 1f : 0.8f,
                    plan.Seed + 0.6f + i * 0.1f,
                    r,
                    r,
                    1.1f
                );
            }
        }

        // 表を向いた瞬間、カードの縁から外へ火花が飛び散り、空気に押されて遅くなりながら消える。
        private void ShowBurstSparks(float t, Vector2 at, float scale)
        {
            int used =
                plan.Look == Look.Rainbow ? Bursts.Length
                : plan.Look == Look.Gold ? Bursts.Length * 3 / 4
                : Bursts.Length / 2;
            Vector2 half = plan.CardSize * (scale * 0.5f);
            for (int i = 0; i < Bursts.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.27f);
                float b = Hash(i + 70, plan.Seed + 0.27f);
                float age = t - beats.Burst - 0.01f * (i % 4);
                float life = 0.32f + 0.2f * b;
                if (i >= used || age < 0f || age > life)
                {
                    Hide(Bursts[i]);
                    continue;
                }
                float angle = (i + 0.6f * a) / Mathf.Max(1, used) * Mathf.PI * 2f;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                // カードの縁（長方形）の上から飛び立つ。
                float reach = Mathf.Min(
                    half.x / Mathf.Max(Mathf.Abs(dir.x), 1e-3f),
                    half.y / Mathf.Max(Mathf.Abs(dir.y), 1e-3f)
                );
                float speed = 1100f + 900f * a;
                float travel = speed * (1f - Mathf.Exp(-age * 5f)) / 5f;
                ShowBoard(
                    Bursts[i],
                    true,
                    at + dir * (reach + travel),
                    Vector2.one * (18f + 16f * b),
                    0f,
                    plan.Look switch
                    {
                        Look.Rainbow => Pastel(a),
                        Look.Gold => i % 3 == 0 ? WarmWhite : Gold,
                        _ => i % 3 == 0 ? WarmWhite : Color.Lerp(plan.StoneColor, Color.white, 0.35f),
                    },
                    1f - Smooth(Ramp(age, life * 0.5f, life)),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.5f
                );
            }
        }

        // 表を向いた瞬間、カードの四隅が白く瞬く（金・虹）。
        private void ShowCorners(float t, Vector2 at, float scale)
        {
            Vector2 half = plan.CardSize * (scale * 0.5f);
            for (int i = 0; i < Corners.Length; i++)
            {
                float born = beats.Burst + 0.02f + 0.03f * i;
                const float life = 0.32f;
                float age = t - born;
                if (plan.Look == Look.Plain || age < 0f || age > life)
                {
                    Hide(Corners[i]);
                    continue;
                }
                var corner = new Vector2(i % 2 == 0 ? -half.x : half.x, i < 2 ? half.y : -half.y);
                ShowBoard(
                    Corners[i],
                    true,
                    at + corner,
                    Vector2.one * (plan.Look == Look.Rainbow ? 130f : 110f),
                    0f,
                    plan.Look == Look.Rainbow ? Pastel(0.25f * i) : WarmWhite,
                    Mathf.Sin(age / life * Mathf.PI),
                    Hash(i, plan.Seed + 0.15f),
                    k: age,
                    eaten: 0f,
                    brightness: 1.5f
                );
            }
        }

        // カードのまわりで、ずれて瞬くきらめき（普通は3つ、金・虹は全部）。
        private void ShowTwinkles(float t, Vector2 at, float scale)
        {
            int used = plan.Look == Look.Plain ? Mathf.Min(3, Twinkles.Length) : Twinkles.Length;
            for (int i = 0; i < Twinkles.Length; i++)
            {
                float born = beats.Burst + 0.04f + 0.05f * i;
                const float life = 0.3f;
                float age = t - born;
                if (i >= used || age < 0f || age > life)
                {
                    Hide(Twinkles[i]);
                    continue;
                }
                float a = Hash(i, plan.Seed + 0.9f);
                Vector2 offset = Polar(
                    plan.CardSize.y * scale * (0.5f + 0.2f * a),
                    a * Mathf.PI * 2f
                );
                ShowBoard(
                    Twinkles[i],
                    true,
                    at + offset,
                    Vector2.one * (56f + 36f * a),
                    0f,
                    plan.Look switch
                    {
                        Look.Rainbow => Pastel(a),
                        Look.Gold => i % 2 == 0 ? Gold : WarmWhite,
                        _ => Color.Lerp(plan.StoneColor, WarmWhite, 0.6f),
                    },
                    Mathf.Sin(age / life * Mathf.PI),
                    a,
                    k: age,
                    eaten: 0f,
                    brightness: 1.3f
                );
            }
        }

        // 金・虹：表を向いたあと、光がカードの左下から右上へ走る。
        private void ShowSheen(float t, Vector2 at, float scale)
        {
            float sheen = Ramp(t, beats.Burst + 0.1f, beats.Burst + 0.36f);
            Vector2 diagonal = plan.CardSize * (scale * 0.42f);
            ShowBoard(
                Sheen,
                plan.Look != Look.Plain && sheen > 0f && sheen < 1f,
                at + Vector2.Lerp(-diagonal, diagonal, Smooth(sheen)),
                Vector2.one * 120f,
                0f,
                Color.white,
                Mathf.Sin(sheen * Mathf.PI),
                plan.Seed + 0.3f,
                k: sheen,
                eaten: 0f,
                brightness: 1.5f
            );
        }

        // 虹：表を向くと外へ飛び、空気に押されて止まってから舞い落ちる淡い小片と、画面に散って瞬くきらめき。
        private void ShowFlakes(float t, Vector2 at)
        {
            float burst = beats.Burst;
            bool rainbow = plan.Look == Look.Rainbow;
            float end = beats.ReturnStart + 0.2f;
            for (int i = 0; i < Flakes.Length; i++)
            {
                float a = Hash(i, plan.Seed + 0.71f);
                float b = Hash(i + 50, plan.Seed + 0.71f);
                float age = t - burst;
                float life = end - burst - 0.1f * b;
                if (!rainbow || age < 0f || age > life)
                {
                    Hide(Flakes[i]);
                    continue;
                }
                float angle = (i + a) / Mathf.Max(1, Flakes.Length) * Mathf.PI * 2f;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = 900f + 700f * b;
                // カードの文字に重ならないよう、カードの縁の外から飛び出す。
                Vector2 pos =
                    at
                    + dir * (plan.CardSize.y * CardScale * 0.55f)
                    + dir * (speed * (1f - Mathf.Exp(-age * 6f)) / 6f)
                    + Vector2.down * (90f * age * age);
                pos.x += Mathf.Sin(age * 7f + a * 6f) * 18f * Ramp(age, 0.15f, 0.4f);
                float flicker = 0.65f + 0.35f * Mathf.Sin(age * 22f + b * 9f);
                ShowBoard(
                    Flakes[i],
                    true,
                    pos,
                    new Vector2(28f, 58f) * (0.8f + 0.5f * a),
                    age * (200f + 300f * a) * (i % 2 == 0 ? 1f : -1f),
                    Pastel(a),
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
                if (!rainbow || age < 0f || age > life)
                {
                    Hide(Prisms[i]);
                    continue;
                }
                Vector2 pos = new((a - 0.5f) * 1700f, (b - 0.5f) * 900f);
                ShowBoard(
                    Prisms[i],
                    true,
                    pos,
                    Vector2.one * (60f + 56f * b),
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

        // 溜める光の色：金は金、虹はゆっくり色相が巡る淡い虹、普通は石の色。
        private Color LookColor(float t) =>
            plan.Look switch
            {
                Look.Rainbow => Pastel(t * 0.9f + plan.Seed),
                Look.Gold => Gold,
                _ => plan.StoneColor,
            };

        // 長方形の縁の上の1点（0から1で周を一周する）。中心からのずれで返す。
        private static Vector2 OnEdge(float u, Vector2 half)
        {
            float w = half.x * 2f,
                h = half.y * 2f;
            float d = u * (w + h) * 2f;
            if (d < w)
                return new Vector2(-half.x + d, half.y);
            d -= w;
            if (d < h)
                return new Vector2(half.x, half.y - d);
            d -= h;
            if (d < w)
                return new Vector2(half.x - d, -half.y);
            d -= w;
            return new Vector2(-half.x, -half.y + d);
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

        private static float Hash(int i, float seed) =>
            Mathf.Repeat(Mathf.Sin(i * 12.9898f + seed * 78.233f) * 43758.547f, 1f);

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
