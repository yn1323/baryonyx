using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Baryonyx.Vfx.Hd2d;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// Runes gained flying into the balance (doc/features/screens/top-home.md ルーン獲得の演出):
    /// they burst out from where they came from, are drawn all together into the balance icon,
    /// and the balance counts up as each lands, swelling and turning gold. The home plays it from
    /// the step panel, the trading house from the rows it sold. It sits on its own layer over the
    /// screen; the owner keeps the balance's text and puts it back when <see cref="Finished"/> runs.
    /// </summary>
    public sealed class RuneGainEffect : MonoBehaviour
    {
        public static readonly Color PeakColor = new(0.95f, 0.76f, 0.31f);

        // 所持ルーンの数とアイコン。演出の間だけ、付与前の値から数え上げて大きくする。
        public TMP_Text RunesLabel;
        public RectTransform RuneTarget;

        // 粒子・キラキラ・光を置く層。座標はこの層の中で数える。
        public RectTransform Layer;

        // 複製して使う粒子の見本。非表示のまま置く。
        public Image RuneParticle;
        public CanvasGroup GainGroup;
        public TMP_Text GainLabel;

        // ルーン獲得のキラキラ。発生源は、0番が1粒着くごと、1番が最後の1粒、2番が弾けた瞬間、
        // 3番が飛んでいるルーンの通り道で使う。
        public Hd2dEmberEmitter RuneSparkles;

        [Min(0)]
        public int RuneArrivalSparkles = 3;

        [Min(0)]
        public int RuneFinalSparkles = 16;

        [Min(0)]
        public int RuneBurstSparkles = 24;

        // 飛んでいる1粒が通り道にキラキラを1つ残す間隔。
        [Min(0.02f)]
        public float RuneTrailSeconds = 0.07f;

        // キラキラの後ろに漂う、ぼかした光のモヤ。発生源の順番はキラキラと同じ。
        public Hd2dEmberEmitter RuneHaze;

        [Min(0)]
        public int RuneArrivalHaze = 1;

        [Min(0)]
        public int RuneFinalHaze = 4;

        [Min(0)]
        public int RuneBurstHaze = 6;

        // 通り道のキラキラをこの数だけ残すごとに、モヤを1つ残す。
        [Min(1)]
        public int RuneTrailHazeEvery = 2;

        // 所持ルーンのアイコンに重ねる加算の光。1粒ごとに小さく、最後の1粒で大きく光る。
        public Image RuneGlow;

        [Range(0f, 1f)]
        public float RuneGlowArrivalAlpha = 0.3f;

        [Range(0f, 1f)]
        public float RuneGlowFinalAlpha = 0.75f;

        // 粒子の数は獲得量の桁数に応じて増やし、この数を上限にする。
        [Range(1, 64)]
        public int RuneParticleMax = 40;

        // 粒子が出どころから四方へ広がる時間。
        [Min(0.01f)]
        public float RuneBurstSeconds = 0.25f;

        // 広がる距離の上限。少ない獲得では近く、10万ルーン以上でこの距離まで広がる。
        [Min(0f)]
        public float RuneBurstNearDistance = 140f;

        [Min(0f)]
        public float RuneBurstFarDistance = 520f;

        // 広がってから吸い込まれ始めるまで。
        [Min(0f)]
        public float RuneHoverSeconds = 0.08f;

        // 全部の粒子をまとめて吸い込む。到着がこの幅でばらけ、所持数が数え上がって見える。
        [Min(0f)]
        public float RuneArrivalSpreadSeconds = 0.15f;

        // 吸い込まれる時間の平均。粒子ごとに ±RuneFlySecondsJitter の割合でばらつかせる。
        [Min(0.01f)]
        public float RuneFlySeconds = 0.55f;

        [Range(0f, 0.9f)]
        public float RuneFlySecondsJitter = 0.4f;

        // 所持数の文字は、吸い込んだ量に比例してこの倍率まで大きくなる。
        [Min(1f)]
        public float RunePeakScale = 1.45f;

        // 粒子が着いてから「+N」を消し始めるまでと、消える時間。
        [Min(0f)]
        public float GainHoldSeconds = 0.5f;

        [Min(0f)]
        public float GainFadeSeconds = 0.3f;

        private const float BumpSeconds = 0.16f;
        private const float BumpScale = 0.35f;

        // 拡大が粒子の到着に追いつく速さ（1秒あたり）と、全部が着いてから戻すまで。
        private const float GrowRate = 25f;
        private const float PeakHoldSeconds = 0.15f;
        private const float ShrinkSeconds = 0.3f;
        private const float DriftSpeed = 40f;
        private const int ArrivalSource = 0;
        private const int FinalSource = 1;
        private const int BurstSource = 2;
        private const int TrailSource = 3;
        private const float FinalGlowSeconds = 0.5f;
        private const float FinalGlowGrowth = 0.5f;

        private readonly List<Image> particles = new();
        private Coroutine routine;

        // 最後の波で金色にした所持数を、演出の終わりに戻す色。
        private Color? runesLabelColor;

        // 演出で動かした所持ルーンのアイコンの拡大の軸（横）を、演出の終わりに戻す値。
        private float? runeIconPivot;

        /// <summary>Runs whenever it ends or is cut short, for the owner to put its balance text back.</summary>
        public event Action Finished;

        // 演出の間は、所持ルーンを付与前の値から数え上げる。終わると持ち主が最新の表示に戻す。
        public bool Playing => routine != null;

        // 直前の演出でルーンが弾けた最初の出どころ（層の座標）。テストで確かめる。
        public Vector3 BurstOrigin { get; private set; }

        /// <summary>The balance and gained amount as written: "12,345".</summary>
        public static string Text(long value) =>
            Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);

        // The runes that fly are made before the first gain, so it does not start with a stall.
        private void Start()
        {
            if (Application.isPlaying)
                MakeParticles(RuneParticleMax);
        }

        private void OnDisable() => Finish();

        private void MakeParticles(int count)
        {
            if (RuneParticle == null || Layer == null)
                return;
            while (particles.Count < count)
            {
                var copy = Instantiate(RuneParticle, Layer);
                copy.name = RuneParticle.name;
                copy.gameObject.SetActive(false);
                particles.Add(copy);
            }
        }

        /// <summary>Counts the balance from <paramref name="from"/> to <paramref name="to"/>, the runes bursting from <paramref name="origin"/> (in the layer).</summary>
        public void Play(long from, long to, Vector3 origin) => Play(from, to, new[] { origin });

        /// <summary>
        /// Counts the balance from <paramref name="from"/> to <paramref name="to"/> as runes land,
        /// the runes shared out among <paramref name="origins"/> (in the layer) and bursting from them.
        /// </summary>
        public void Play(long from, long to, IReadOnlyList<Vector3> origins)
        {
            Finish();
            if (
                to <= from
                || origins == null
                || origins.Count == 0
                || !isActiveAndEnabled
                || RuneTarget == null
                || Layer == null
                || RuneParticle == null
            )
                return;
            routine = StartCoroutine(Run(from, to, origins));
        }

        // 1,000で25個、1万で31個のように桁が増えるほど粒子を増やす。獲得量より多くはしない。
        public static int ParticleCountFor(long granted, int max)
        {
            if (granted <= 0)
                return 0;
            int count = Mathf.RoundToInt(7f + 6f * Mathf.Log10(granted));
            return (int)Math.Min(granted, Mathf.Clamp(count, 1, Math.Max(1, max)));
        }

        // 獲得量の桁数に応じて、広がる距離を近い値から遠い値へ伸ばす（10万ルーン以上で最大）。
        public static float BurstDistanceFor(long granted, float near, float far)
        {
            float digits = granted <= 1 ? 0f : Mathf.Log10(granted);
            return Mathf.Lerp(near, Mathf.Max(near, far), Mathf.Clamp01(digits / 5f));
        }

        private IEnumerator Run(long from, long to, IReadOnlyList<Vector3> origins)
        {
            long granted = to - from;
            int count = ParticleCountFor(granted, RuneParticleMax);
            BurstOrigin = origins[0];
            runeIconPivot ??= RuneTarget.pivot.x;
            Vector3 target = PointOf(RuneTarget);
            Vector3 targetWorld = Layer.TransformPoint(target);
            if (RuneGlow != null)
            {
                RuneGlow.rectTransform.localPosition = target;
                RuneGlow.rectTransform.localScale = Vector3.one;
                SetAlpha(RuneGlow, 0f);
                RuneGlow.gameObject.SetActive(true);
            }
            float reach = BurstDistanceFor(granted, RuneBurstNearDistance, RuneBurstFarDistance);
            var origin = new Vector3[count];
            var direction = new Vector3[count];
            var distance = new float[count];
            var size = new float[count];
            var converge = new float[count];
            var fly = new float[count];
            var nextTrail = new float[count];
            var trails = new int[count];
            float jitter = Mathf.Clamp(RuneFlySecondsJitter, 0f, 0.9f);
            for (int i = 0; i < count; i++)
            {
                origin[i] = origins[i % origins.Count];
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                direction[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
                distance[i] = UnityEngine.Random.Range(reach * 0.4f, reach);
                size[i] = UnityEngine.Random.Range(0.8f, 1.5f);
                converge[i] =
                    RuneBurstSeconds
                    + RuneHoverSeconds
                    + UnityEngine.Random.Range(0f, RuneArrivalSpreadSeconds);
                fly[i] = RuneFlySeconds * UnityEngine.Random.Range(1f - jitter, 1f + jitter);
                nextTrail[i] = UnityEngine.Random.Range(0f, RuneTrailSeconds);
            }
            MakeParticles(count);
            // キラキラはルーンの手前に描く。後ろでは96pxのルーンに隠れて見えない。
            if (RuneSparkles != null)
                RuneSparkles.transform.SetAsLastSibling();

            runesLabelColor ??= RunesLabel != null ? RunesLabel.color : Color.white;
            Color labelColor = runesLabelColor.Value;
            Set(RunesLabel, Text(from));
            if (GainLabel != null)
            {
                GainLabel.text = "+" + Text(granted);
                GainLabel.rectTransform.localScale = Vector3.one;
            }

            // 弾けるキラキラとモヤは、出どころの数で分け合う。
            int burstSparkles = Mathf.CeilToInt(RuneBurstSparkles / (float)origins.Count);
            int burstHaze = Mathf.CeilToInt(RuneBurstHaze / (float)origins.Count);
            foreach (var point in origins)
                Sparkle(BurstSource, burstSparkles, burstHaze, Layer.TransformPoint(point));

            var landed = new bool[count];
            int arrived = 0;
            float elapsed = 0f;
            float firstArrival = float.MaxValue;
            float bumpElapsed = BumpSeconds;
            float grown = 1f;
            float lastArrival = float.MaxValue;
            while (true)
            {
                for (int i = 0; i < count; i++)
                {
                    if (landed[i])
                        continue;
                    var particle = particles[i];
                    float flight = (elapsed - converge[i]) / fly[i];
                    if (flight >= 1f)
                    {
                        // フレームが飛んでも、着いた粒子を必ず1回ずつ数える。
                        landed[i] = true;
                        particle.gameObject.SetActive(false);
                        arrived++;
                        Set(RunesLabel, Text(from + granted * arrived / count));
                        bumpElapsed = 0f;
                        firstArrival = Mathf.Min(firstArrival, elapsed);
                        if (arrived == count)
                            lastArrival = elapsed;
                        if (arrived == count)
                            Sparkle(FinalSource, RuneFinalSparkles, RuneFinalHaze, targetWorld);
                        else
                            Sparkle(
                                ArrivalSource,
                                RuneArrivalSparkles,
                                RuneArrivalHaze,
                                targetWorld
                            );
                        continue;
                    }
                    if (!particle.gameObject.activeSelf)
                        particle.gameObject.SetActive(true);
                    var rect = particle.rectTransform;
                    Vector3 spread = origin[i] + direction[i] * distance[i];
                    if (elapsed < RuneBurstSeconds)
                    {
                        // ボワッと一気に広がり、外側でゆっくり止まる。
                        float p = 1f - Mathf.Pow(1f - elapsed / RuneBurstSeconds, 3f);
                        rect.localPosition = Vector3.Lerp(origin[i], spread, p);
                        rect.localScale = Vector3.one * Mathf.Lerp(0.3f, size[i], p);
                    }
                    else if (flight < 0f)
                    {
                        // 吸い込まれるまで、少しずつ外へ漂う。
                        float drift = elapsed - RuneBurstSeconds;
                        rect.localPosition = spread + direction[i] * (DriftSpeed * drift);
                        rect.localScale = Vector3.one * size[i];
                    }
                    else
                    {
                        // 加速しながら所持数へ吸い込まれ、小さくなる。
                        float drift = converge[i] - RuneBurstSeconds;
                        Vector3 start = spread + direction[i] * (DriftSpeed * drift);
                        float e = flight * flight;
                        rect.localPosition = Bezier(
                            start,
                            start + direction[i] * (reach * 0.15f),
                            target,
                            e
                        );
                        rect.localScale = Vector3.one * Mathf.Lerp(size[i], 0.4f, e);
                    }
                    particle.color = RuneParticle.color;

                    // 広がるときも吸い込まれるときも、通り道にキラキラとモヤを残す。
                    if (elapsed >= nextTrail[i])
                    {
                        nextTrail[i] = elapsed + RuneTrailSeconds;
                        trails[i]++;
                        int haze = trails[i] % Mathf.Max(1, RuneTrailHazeEvery) == 0 ? 1 : 0;
                        Sparkle(TrailSource, 1, haze, rect.position);
                    }
                }

                bumpElapsed += Time.unscaledDeltaTime;
                float bump = Mathf.Sin(Mathf.PI * Mathf.Clamp01(bumpElapsed / BumpSeconds));

                // 吸い込んだ量に比例して所持数の文字とアイコンを大きくし、全部が着いたら少し止めて戻す。
                float peak = Mathf.Max(1f, RunePeakScale);
                float grow = Mathf.Lerp(1f, peak, (float)arrived / count);
                grown = Mathf.Lerp(grown, grow, 1f - Mathf.Exp(-GrowRate * Time.unscaledDeltaTime));
                float settled = elapsed - lastArrival - PeakHoldSeconds;
                float shrink = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(settled / ShrinkSeconds));
                float scale = Mathf.Lerp(grown, 1f, shrink);
                float glow = peak > 1f ? (scale - 1f) / (peak - 1f) : 0f;
                if (RunesLabel != null)
                {
                    RunesLabel.rectTransform.localScale = Vector3.one * scale;
                    RunesLabel.color = Color.Lerp(labelColor, PeakColor, glow);
                }
                ScaleRuneIcon(scale, 1f + BumpScale * bump);

                // 大きくなって動いたアイコンへ、粒子・光・キラキラを向け直す。
                target = CenterOf(RuneTarget);
                targetWorld = Layer.TransformPoint(target);

                // 1粒ごとの小さな光と、最後の1粒で広がりながら消える大きな光。
                if (RuneGlow != null)
                {
                    float final = Mathf.Clamp01((elapsed - lastArrival) / FinalGlowSeconds);
                    float flash = elapsed >= lastArrival ? (1f - final) * (1f - final) : 0f;
                    SetAlpha(
                        RuneGlow,
                        Mathf.Max(RuneGlowArrivalAlpha * bump, RuneGlowFinalAlpha * flash)
                    );
                    RuneGlow.rectTransform.localPosition = target;
                    RuneGlow.rectTransform.localScale =
                        Vector3.one
                        * scale
                        * (1f + FinalGlowGrowth * (elapsed >= lastArrival ? final : 0f));
                }

                bool shrinking = arrived < count || settled < ShrinkSeconds;
                if (GainGroup != null)
                {
                    // 「+N」は最初の粒子が着くと出し、全部が着いてから消す。
                    float shown = Mathf.Clamp01((elapsed - firstArrival) / 0.15f);
                    float hidden = Mathf.Clamp01(
                        (elapsed - lastArrival - GainHoldSeconds)
                            / Mathf.Max(1e-4f, GainFadeSeconds)
                    );
                    GainGroup.alpha = shown * (1f - hidden);
                    if (GainLabel != null)
                        GainLabel.rectTransform.localScale =
                            Vector3.one * Mathf.Lerp(1f, 1.15f, glow);
                    if (hidden >= 1f && !shrinking)
                        break;
                }
                else if (!shrinking && bumpElapsed >= BumpSeconds)
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            routine = null;
            Finish();
        }

        /// <summary>Ends it at once and puts everything back; the owner then writes its balance again.</summary>
        public void Finish()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            foreach (var particle in particles)
                if (particle != null)
                    particle.gameObject.SetActive(false);
            if (RuneTarget != null)
            {
                RuneTarget.localScale = Vector3.one;
                if (runeIconPivot.HasValue)
                    SetPivotX(RuneTarget, runeIconPivot.Value);
            }
            if (RuneGlow != null)
                RuneGlow.gameObject.SetActive(false);
            if (RunesLabel != null)
            {
                RunesLabel.rectTransform.localScale = Vector3.one;
                if (runesLabelColor.HasValue)
                    RunesLabel.color = runesLabelColor.Value;
            }
            if (GainGroup != null)
                GainGroup.alpha = 0f;
            if (GainLabel != null)
                GainLabel.rectTransform.localScale = Vector3.one;
            Finished?.Invoke();
        }

        /// <summary>The middle of <paramref name="rect"/> in the layer, after the layout settles.</summary>
        public Vector3 PointOf(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            return CenterOf(rect);
        }

        /// <summary>The screen point <paramref name="screen"/> in the layer, or null when it misses.</summary>
        public Vector3? LayerPointOf(Vector2 screen)
        {
            if (
                Layer == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    Layer,
                    screen,
                    EventCamera(),
                    out var local
                )
            )
                return null;
            return new Vector3(local.x, local.y, 0f);
        }

        // Overlayのキャンバスではカメラを使わず、展示室のカメラ描画ではそのカメラで座標を変換する。
        public Camera EventCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas = canvas.rootCanvas;
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        // 層の座標で、拡大を含めた対象の今の中心を返す。
        private Vector3 CenterOf(RectTransform rect)
        {
            var point = Layer.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            point.z = 0f;
            return point;
        }

        // 所持ルーンのアイコンを、所持数の文字の中心から文字と同じ grow 倍に広げた位置へ置き、
        // 粒が着いた跳ね bump を重ねて大きくする。文字と並んだまま重ならない。
        private void ScaleRuneIcon(float grow, float bump)
        {
            float pivot = runeIconPivot ?? 0.5f;
            if (RunesLabel != null && RunesLabel.rectTransform.parent == RuneTarget.parent)
            {
                var label = RunesLabel.rectTransform;
                float width = RuneTarget.rect.width;
                pivot = IconPivotFor(
                    RuneTarget.localPosition.x - RuneTarget.pivot.x * width,
                    width,
                    label.localPosition.x + (0.5f - label.pivot.x) * label.rect.width,
                    grow,
                    grow * bump
                );
            }
            SetPivotX(RuneTarget, pivot);
            RuneTarget.localScale = Vector3.one * (grow * bump);
        }

        // 左端 left・幅 width（親の座標）の対象を、中心が anchor から grow 倍の位置へ移るように
        // scale 倍へ広げる拡大の軸を、幅に対する割合で返す。
        private static float IconPivotFor(
            float left,
            float width,
            float anchor,
            float grow,
            float scale
        )
        {
            if (width <= 0f || Mathf.Abs(scale - 1f) < 1e-4f)
                return 0.5f;
            float center = left + width * 0.5f;
            float moved = anchor + grow * (center - anchor);
            return ((moved - scale * center) / (1f - scale) - left) / width;
        }

        // 拡大の軸（横）だけを変え、並べた位置（拡大前の矩形）は動かさない。
        // 横並びのレイアウトも軸に合わせて同じ位置へ置くため、組み直されても跳ばない。
        private static void SetPivotX(RectTransform rect, float x)
        {
            var pivot = rect.pivot;
            rect.anchoredPosition += new Vector2((x - pivot.x) * rect.rect.width, 0f);
            rect.pivot = new Vector2(x, pivot.y);
        }

        // キラキラとモヤを、同じ番号の発生源から同じ位置に出す。
        private void Sparkle(int source, int sparkles, int haze, Vector3 worldPosition)
        {
            if (RuneSparkles != null)
                RuneSparkles.BurstAt(source, sparkles, worldPosition);
            if (RuneHaze != null)
                RuneHaze.BurstAt(source, haze, worldPosition);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text;
        }

        private static Vector3 Bezier(Vector3 start, Vector3 control, Vector3 end, float t) =>
            Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, end, t), t);
    }
}
