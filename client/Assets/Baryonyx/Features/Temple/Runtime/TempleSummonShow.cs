using System;
using System.Collections;
using System.Collections.Generic;
using Baryonyx.Combat.Presentation;
using Baryonyx.Library;
using Baryonyx.UI.EditModePreview;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Temple.TempleSummonFx;

namespace Baryonyx.Temple
{
    /// <summary>How bright a summoning's light is: the best of what it gave (doc/features/screens.md).</summary>
    public enum TempleSummonTier
    {
        // ☆3以下
        White,

        // ☆4と品質S
        Gold,

        // ☆5とキャラ
        Rainbow,
    }

    /// <summary>
    /// The summoning once: the cathedral's rose window lights from behind, cracks from its middle
    /// and shatters toward the camera, and the card comes down in the light that pours through
    /// (doc/features/screens.md, the summoning's presentation). Every layer is set from the time
    /// alone (<see cref="Evaluate"/>), so a moment can be drawn again without running time.
    /// </summary>
    public sealed class TempleSummonShow : MonoBehaviour, IEditModePreview
    {
        public const float Duration = 3.6f;
        public const float BreakAt = 1.9f;

        // 砕けるまでの時間。
        private const float LightUpEnd = 1.2f;
        private const float RadialEnd = 1.45f;
        private const float RingsEnd = 1.65f;
        private const float TintAt = 0.8f;
        private const float UpgradeAt = 1.1f;
        private const float CardFrom = 2.6f;
        private const float CardTo = 3.4f;

        // 揺れと寄りを掛ける、舞台全体。
        public RectTransform Stage;
        public Image Dim;
        public BattleVfxImage FloorPool;
        public BattleVfxImage Rays;
        public BattleVfxImage BackGlow;
        public BattleVfxImage Window;
        public BattleVfxImage Hole;
        public TempleGlassShards Shards;
        public BattleVfxImage Glitter;
        public BattleVfxImage Flash;
        public BattleVfxImage Pillar;
        public BattleVfxImage CardGlow;
        public RectTransform Card;
        public CanvasGroup CardGroup;

        // 画面全体の見えないボタン。押すと終わりまで飛ばす。
        public Button TapArea;

        // 降りてくる札の表と、札に描く物の絵。
        public TempleSummonCard CardFace;
        public LibraryData Data;
        public float CardArtBox = 128f;

        // 止まったときの札の大きさ（結果の窓の札に対する倍率）。
        public float CardScale = 1.35f;

        // 札が降り始める高さと、止まる高さ（Stageの中の位置）。
        public Vector2 CardTop;
        public Vector2 CardRest;

        // ひびと破片の分け方の種。
        public int Seed = 7;

        private TempleSummonTier tier;
        private TempleSummonTier from;
        private TempleGlassPattern pattern;
        private Material[] tierMaterials = Array.Empty<Material>();
        private Coroutine playing;

        public bool Playing => playing != null;

        public event Action Finished;

        /// <summary>
        /// Gets ready for one summoning. <paramref name="startTier"/> below <paramref name="tier"/>
        /// lights the window in it first and raises it just before it cracks.
        /// </summary>
        public void Setup(TempleSummonTier result, TempleSummonTier startTier, int seed)
        {
            tier = result;
            from = startTier > result ? result : startTier;
            Seed = seed;
            pattern = new TempleGlassPattern(seed);
            OwnMaterials();
            foreach (var material in tierMaterials)
                pattern.Apply(material);
            if (Shards != null)
                Shards.Setup(pattern, seed * 31 + 5, 1.7f);
            Evaluate(0f);
        }

        /// <summary>Gets ready for <paramref name="result"/>: its light, and its card coming down.</summary>
        public void Setup(SummonResult result, int seed) =>
            Setup(result, TempleSummonFx.TierOf(result), seed);

        /// <summary>
        /// Gets ready for <paramref name="result"/>, first lit in <paramref name="startTier"/> and
        /// raised to the result's own light before the window cracks.
        /// </summary>
        public void Setup(SummonResult result, TempleSummonTier startTier, int seed)
        {
            if (CardFace != null)
                TempleSummonResults.Paint(CardFace, result, Data, CardArtBox);
            Setup(TempleSummonFx.TierOf(result), startTier, seed);
        }

        /// <summary>Plays from the start. <see cref="Skip"/> jumps to the end.</summary>
        public void Play()
        {
            Stop();
            playing = StartCoroutine(Run());
        }

        /// <summary>A tap on the screen: jumps to the end while playing.</summary>
        public void Tap()
        {
            if (Playing)
                Skip();
        }

        private void Awake()
        {
            if (TapArea != null)
                TapArea.onClick.AddListener(Tap);
        }

        public void Skip()
        {
            Stop();
            Evaluate(Duration);
            Finished?.Invoke();
        }

        private void Stop()
        {
            if (playing == null)
                return;
            StopCoroutine(playing);
            playing = null;
        }

        private IEnumerator Run()
        {
            float t = 0f;
            while (t < Duration)
            {
                Evaluate(t);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Evaluate(Duration);
            playing = null;
            Finished?.Invoke();
        }

        /// <summary>Sets every layer to <paramref name="t"/> seconds into the summoning.</summary>
        public void Evaluate(float t)
        {
            if (pattern == null)
                Setup(tier, from, Seed);
            var light = LightAt(t, out bool rainbow);
            foreach (var material in tierMaterials)
                material.SetFloat("_Tier", rainbow ? 2f : 0f);
            float broken = t >= BreakAt ? t - BreakAt : -1f;

            if (Dim != null)
            {
                float dim =
                    0.6f * Smooth(Ramp(t, 0f, 0.6f)) - 0.22f * Smooth(Ramp(t, BreakAt, 2.4f));
                Dim.color = new Color(Night.r, Night.g, Night.b, dim);
            }

            // 昇格の一瞬の脈。
            float upgrade = from != tier ? Pulse(t, UpgradeAt + 0.05f, 0.08f) : 0f;
            float charge = Ramp(t, LightUpEnd, BreakAt);
            float lit = 0.12f + 0.7f * EaseIn(Ramp(t, 0f, LightUpEnd)) + 0.5f * charge;
            Show(
                Window,
                broken < 0f,
                light,
                1f,
                Seed * 0.13f % 1f,
                CrackAt(t),
                lit,
                1.6f + 1.4f * charge + 2f * upgrade
            );
            // 抜けた直後だけ裏の光がまぶしく、すぐ落ち着いて、夜の光が残る。
            Show(
                Hole,
                broken >= 0f,
                light,
                1f,
                0.3f,
                broken >= 0f ? 0.18f + 0.9f * Mathf.Exp(-broken * 2.6f) : 0f,
                0f,
                1.6f
            );
            if (Shards != null)
            {
                Shards.gameObject.SetActive(broken >= 0f && broken < 1.8f);
                Shards.color = light;
                Shards.Pose(Mathf.Max(0f, broken), 1.6f);
            }

            // 窓の裏の光は砕けるまで強まり、抜けた直後にあふれて、すぐ引く。
            float before = broken < 0f ? 1f : 0f;
            float back =
                (0.15f * Smooth(Ramp(t, 0f, LightUpEnd)) + 0.25f * charge) * before
                + 0.6f * upgrade
                + (broken >= 0f ? 1.1f * Mathf.Exp(-broken * 3f) : 0f);
            Show(BackGlow, back > 0.001f, light, Mathf.Clamp01(back), 0f, 0f, 0f, 0.8f + back);

            float flash = broken >= 0f ? Mathf.Exp(-broken / 0.12f) : 0f;
            Show(Flash, flash > 0.01f, light, flash, 0f, 0f, 0f, 3.5f);

            float rays =
                0.85f * Smooth(Ramp(t, 0.2f, LightUpEnd))
                + 0.35f * charge
                + (broken >= 0f ? 0.9f * Mathf.Exp(-broken / 0.7f) : 0f);
            Show(
                Rays,
                rays > 0.001f,
                light,
                Mathf.Clamp01(rays),
                Seed * 0.07f % 1f,
                t / Duration,
                0f,
                1.2f + rays * 0.6f
            );
            Show(
                Glitter,
                broken >= 0f,
                light,
                1f,
                0.5f,
                Mathf.Max(0f, broken) / 4f,
                Glitter != null ? GlitterTop() : 0f,
                3f
            );

            float pillar = Smooth(Ramp(t, 2.25f, 2.6f));
            Show(Pillar, pillar > 0.001f, light, pillar, 0.71f, t / Duration, 0f, 1.6f);

            float pool =
                0.25f * Ramp(t, 0f, LightUpEnd)
                + (broken >= 0f ? 0.35f : 0f)
                + 0.4f * Smooth(Ramp(t, CardTo - 0.2f, CardTo + 0.1f));
            Show(FloorPool, pool > 0.001f, light, Mathf.Clamp01(pool), 0f, 0f, 0f, 0.7f + pool);

            float card = Ramp(t, CardFrom, CardTo);
            if (Card != null)
            {
                Card.gameObject.SetActive(t >= CardFrom);
                float ease = EaseOut(card);
                Card.anchoredPosition = Vector2.Lerp(CardTop, CardRest, ease);
                Card.localScale = Vector3.one * Mathf.Lerp(0.6f, CardScale, ease);
                if (CardGroup != null)
                    CardGroup.alpha = Ramp(t, CardFrom, CardFrom + 0.15f);
            }
            if (CardGlow != null)
            {
                Show(
                    CardGlow,
                    t >= CardFrom,
                    light,
                    Ramp(t, CardFrom, CardFrom + 0.2f),
                    0f,
                    0f,
                    0f,
                    1.1f
                );
                if (Card != null)
                    CardGlow.rectTransform.anchoredPosition = Card.anchoredPosition;
            }

            if (Stage != null)
            {
                float shake =
                    3f * charge * (t < BreakAt ? 1f : 0f)
                    + (broken >= 0f ? 12f * (1f - Ramp(broken, 0f, 0.25f)) : 0f);
                var jitter = new Vector2(Noise(t * 37f, 1.3f), Noise(t * 41f, 7.9f)) * shake;
                Stage.anchoredPosition = jitter;
                float zoom =
                    1f
                    + 0.035f * EaseOut(Ramp(t, BreakAt, BreakAt + 0.2f))
                    - 0.015f * Ramp(t, BreakAt + 0.2f, Duration);
                Stage.localScale = Vector3.one * zoom;
            }
        }

        // ひびの値：0〜1で放射状の線が伸び、1〜2で同心円の弧が入る。
        private static float CrackAt(float t) =>
            EaseOut(Ramp(t, LightUpEnd, RadialEnd)) + EaseOut(Ramp(t, RadialEnd, RingsEnd));

        private Color LightAt(float t, out bool rainbow)
        {
            var start = ColorOf(from);
            var end = ColorOf(tier);
            Color color;
            if (t < TintAt)
                color = Moonlight;
            else if (t < UpgradeAt)
                color = Color.Lerp(Moonlight, start, Smooth(Ramp(t, TintAt, TintAt + 0.2f)));
            else
                color = Color.Lerp(start, end, Smooth(Ramp(t, UpgradeAt, UpgradeAt + 0.1f)));
            var shown = t < UpgradeAt + 0.05f ? from : tier;
            rainbow = t >= TintAt + 0.1f && shown == TempleSummonTier.Rainbow;
            return color;
        }

        private float GlitterTop()
        {
            if (Window == null || Glitter == null)
                return 0.7f;
            var glitter = Glitter.rectTransform;
            var window = Window.rectTransform;
            var local = glitter.InverseTransformPoint(window.position);
            return Mathf.InverseLerp(glitter.rect.yMin, glitter.rect.yMax, local.y);
        }

        // 層ごとのマテリアルを複製し、段階の色とひびの分け方を、この演出だけに設定する。
        private void OwnMaterials()
        {
            if (tierMaterials.Length > 0)
                return;
            tierMaterials = TempleSummonFx.OwnMaterials(
                transform,
                new Graphic[]
                {
                    FloorPool,
                    Rays,
                    BackGlow,
                    Window,
                    Hole,
                    Shards,
                    Glitter,
                    Flash,
                    Pillar,
                    CardGlow,
                }
            );
        }

        private void OnDestroy() => DestroyMaterials(tierMaterials);

        // 停止中の表示の切り替え：虹の結果（キャラ）の各拍の1コマ。
        private static readonly (string Name, float Time)[] Moments =
        {
            ("予備動作（0.9秒）", 0.9f),
            ("溜め（1.6秒）", 1.6f),
            ("発生（2.0秒）", 2.0f),
            ("余韻（3.0秒）", 3.0f),
            ("終わり（3.6秒）", Duration),
        };

        public IReadOnlyList<string> PreviewStates => Array.ConvertAll(Moments, m => m.Name);

        public void ShowPreviewState(int index)
        {
            if (index < 0 || index >= Moments.Length)
                return;
            if (pattern == null)
                Setup(TempleSummonTier.Rainbow, TempleSummonTier.Gold, Seed);
            Evaluate(Moments[index].Time);
        }
    }
}
