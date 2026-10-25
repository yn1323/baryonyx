using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baryonyx.Combat.Presentation;
using Baryonyx.Library;
using Baryonyx.UI.EditModePreview;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.Temple.TempleSummonFx;

namespace Baryonyx.Temple
{
    /// <summary>One of the ten windows on the wall, and the card that comes out of it.</summary>
    [Serializable]
    public sealed class TempleSummonPane
    {
        public BattleVfxImage Glow;
        public BattleVfxImage Glass;
        public BattleVfxImage Hole;
        public TempleGlassShards Shards;
        public BattleVfxImage CardGlow;
        public RectTransform Card;
        public CanvasGroup CardGroup;
        public TempleSummonCard CardFace;
    }

    /// <summary>
    /// The summoning ten times (doc/features/screens/temple.md, the summoning's presentation): ten small
    /// windows on the cathedral's wall light in their results' lights; the white ones break one
    /// after another from the top left, and the gold and rainbow ones are kept to the end and
    /// break after gathering light, a rainbow one raised from gold. Each window leaves its card
    /// face up in its place, and the presentation ends by itself a moment after the last one; a
    /// tap jumps to the cards all out, and another ends at once. Every layer is set from the time
    /// alone (<see cref="Evaluate"/>), so a moment can be drawn again without running time.
    /// </summary>
    public sealed class TempleSummonTenShow : MonoBehaviour, IEditModePreview
    {
        // 白の窓が砕け始める時刻と、その間隔。金と虹の窓は、そのあとに光をためてから砕ける。
        private const float FirstBreak = 1.0f;
        private const float WhiteGap = 0.18f;
        private const float RareGap = 0.65f;
        private const float RareCharge = 0.45f;
        private const float AfterLast = 1.0f;

        // 札が並んでから、ひとりでに終わるまで。
        private const float Linger = 1.2f;

        public RectTransform Stage;
        public Image Dim;
        public BattleVfxImage Glitter;
        public BattleVfxImage Flash;
        public TempleSummonPane[] Panes = Array.Empty<TempleSummonPane>();
        public LibraryData Data;
        public float CardArtBox = 128f;

        // 画面全体の見えないボタン。押すと、札が並んだところまで飛ばす・終える。
        public Button TapArea;

        // 窓の前に止まったときの札の大きさ（結果の窓の札に対する倍率）。
        public float CardScale = 0.72f;
        public int Seed = 11;

        private readonly TempleSummonTier[] tiers = new TempleSummonTier[TempleSummon.TenCount];
        private readonly float[] breaks = new float[TempleSummon.TenCount];
        private Material[] materials = Array.Empty<Material>();
        private float duration = 4f;
        private float firstBreak = FirstBreak;
        private bool ready;
        private Coroutine playing;

        public bool Playing => playing != null;

        /// <summary>When the last window has broken and every card lies face up.</summary>
        public float Duration => duration;

        /// <summary>When the presentation ends by itself, a moment after the cards are all out.</summary>
        public float End => duration + Linger;

        public event Action Finished;

        /// <summary>Gets ready for ten <paramref name="results"/>, each window in its own light.</summary>
        public void Setup(IReadOnlyList<SummonResult> results, int seed)
        {
            Seed = seed;
            for (int i = 0; i < Panes.Length && i < tiers.Length; i++)
            {
                var pane = Panes[i];
                bool shown = results != null && i < results.Count;
                tiers[i] = shown ? TierOf(results[i]) : TempleSummonTier.White;
                if (shown && pane.CardFace != null)
                    TempleSummonResults.Paint(pane.CardFace, results[i], Data, CardArtBox);
            }
            Prepare();
        }

        /// <summary>Gets ready with the given lights, for the previews and the lab.</summary>
        public void Setup(IReadOnlyList<TempleSummonTier> lights, int seed)
        {
            Seed = seed;
            for (int i = 0; i < tiers.Length; i++)
                tiers[i] = lights != null && i < lights.Count ? lights[i] : TempleSummonTier.White;
            Prepare();
        }

        // 砕ける順と時刻を決め、窓ごとにひびの分け方と破片の動きを作る。
        private void Prepare()
        {
            if (materials.Length == 0)
                materials = OwnMaterials(
                    transform,
                    Panes
                        .SelectMany(pane =>
                            new Graphic[]
                            {
                                pane.Glow,
                                pane.Glass,
                                pane.Hole,
                                pane.Shards,
                                pane.CardGlow,
                            }
                        )
                        .Append(Glitter)
                        .Append(Flash)
                );
            int count = Mathf.Min(Panes.Length, tiers.Length);
            var whites = Enumerable.Range(0, count).Where(i => tiers[i] == TempleSummonTier.White);
            var rares = Enumerable
                .Range(0, count)
                .Where(i => tiers[i] != TempleSummonTier.White)
                .OrderBy(i => tiers[i])
                .ThenBy(i => i);
            float at = FirstBreak;
            firstBreak = float.PositiveInfinity;
            foreach (int i in whites)
            {
                breaks[i] = at;
                firstBreak = Mathf.Min(firstBreak, at);
                at += WhiteGap;
            }
            at += RareCharge;
            foreach (int i in rares)
            {
                breaks[i] = at;
                firstBreak = Mathf.Min(firstBreak, at);
                at += RareGap;
            }
            duration = at - (rares.Any() ? RareGap : WhiteGap) + AfterLast;
            for (int i = 0; i < count; i++)
            {
                var pane = Panes[i];
                var pattern = new TempleGlassPattern(Seed * 13 + i * 7, 10);
                pattern.Apply(pane.Glass != null ? pane.Glass.material : null);
                if (pane.Shards != null)
                    pane.Shards.Setup(pattern, Seed * 31 + i, 1.3f);
            }
            ready = true;
            Evaluate(0f);
        }

        private void Awake()
        {
            if (TapArea != null)
                TapArea.onClick.AddListener(Tap);
        }

        /// <summary>Plays from the start to the end, the cards face up.</summary>
        public void Play()
        {
            Stop();
            playing = StartCoroutine(Run());
        }

        /// <summary>
        /// A tap on the screen: before every card is out, jumps to the cards all out; then ends
        /// at once.
        /// </summary>
        public void Tap()
        {
            if (!Playing)
                return;
            current = current < duration ? duration : End;
        }

        /// <summary>Jumps to the end, the cards face up.</summary>
        public void Skip()
        {
            Stop();
            Evaluate(End);
            Finished?.Invoke();
        }

        private float current;

        private void Stop()
        {
            if (playing == null)
                return;
            StopCoroutine(playing);
            playing = null;
        }

        private IEnumerator Run()
        {
            current = 0f;
            while (current < End)
            {
                Evaluate(current);
                yield return null;
                current += Time.unscaledDeltaTime;
            }
            Evaluate(End);
            playing = null;
            Finished?.Invoke();
        }

        /// <summary>Sets every layer to <paramref name="t"/> seconds into the summoning.</summary>
        public void Evaluate(float t)
        {
            if (!ready)
                Setup(Array.Empty<TempleSummonTier>(), Seed);
            if (Dim != null)
                Dim.color = new Color(Night.r, Night.g, Night.b, 0.55f * Smooth(Ramp(t, 0f, 0.5f)));
            float shake = 0f;
            float flash = 0f;
            int flashPane = -1;
            for (int i = 0; i < Panes.Length && i < tiers.Length; i++)
            {
                var pane = Panes[i];
                var tier = tiers[i];
                float b = breaks[i];
                bool rare = tier != TempleSummonTier.White;
                // 虹の窓は金で灯り、ためる間に虹へ上がる。
                var from = tier == TempleSummonTier.Rainbow ? TempleSummonTier.Gold : tier;
                float raiseAt = b - RareCharge * 0.8f;
                bool raised = tier == TempleSummonTier.Rainbow && t >= raiseAt;
                float start = 0.15f + 0.05f * i;
                var light = Color.Lerp(
                    Moonlight,
                    ColorOf(from),
                    Smooth(Ramp(t, start + 0.3f, start + 0.6f))
                );
                if (tier == TempleSummonTier.Rainbow)
                    light = Color.Lerp(
                        light,
                        Color.white,
                        Smooth(Ramp(t, raiseAt, raiseAt + 0.1f))
                    );
                SetTier(pane, raised);
                float charge = rare ? Ramp(t, b - RareCharge, b) : 0f;
                float upgrade =
                    tier == TempleSummonTier.Rainbow ? Pulse(t, raiseAt + 0.05f, 0.08f) : 0f;
                float lit = 0.1f + 0.8f * EaseIn(Ramp(t, start, start + 0.6f)) + 0.5f * charge;
                float broken = t >= b ? t - b : -1f;
                float crackFrom = rare ? b - RareCharge : b - 0.3f;
                float crack =
                    EaseOut(Ramp(t, crackFrom, Mathf.Lerp(crackFrom, b, 0.55f)))
                    + EaseOut(Ramp(t, Mathf.Lerp(crackFrom, b, 0.55f), b - 0.04f));
                Show(
                    pane.Glass,
                    broken < 0f,
                    light,
                    1f,
                    (i * 0.17f) % 1f,
                    crack,
                    lit,
                    1.4f + 1.2f * charge + 2f * upgrade
                );
                Show(
                    pane.Hole,
                    broken >= 0f,
                    light,
                    1f,
                    0.3f,
                    broken >= 0f ? 0.15f + (rare ? 1.1f : 0.8f) * Mathf.Exp(-broken * 3f) : 0f,
                    0f,
                    rare ? 1.8f : 1.4f
                );
                if (pane.Shards != null)
                {
                    pane.Shards.gameObject.SetActive(broken >= 0f && broken < 1.4f);
                    pane.Shards.color = light;
                    pane.Shards.Pose(Mathf.Max(0f, broken), 1.4f);
                }
                float glow =
                    (0.25f * lit + 0.7f * charge) * (broken < 0f ? 1f : 0f)
                    + 0.6f * upgrade
                    + (broken >= 0f ? (rare ? 1.4f : 0.8f) * Mathf.Exp(-broken * 3.5f) : 0f);
                Show(pane.Glow, glow > 0.001f, light, Mathf.Clamp01(glow), 0f, 0f, 0f, 0.7f + glow);

                // 窓から札が表を向いて出て、窓の前に止まる。
                float card = Ramp(t, b + 0.05f, b + 0.4f);
                if (pane.Card != null)
                {
                    pane.Card.gameObject.SetActive(broken >= 0.05f);
                    float ease = EaseOut(card);
                    var home = HomeOf(pane);
                    pane.Card.anchoredPosition = home + new Vector2(0f, 40f * (1f - ease));
                    float scale = Mathf.Lerp(0.45f, CardScale, ease);
                    pane.Card.localScale = new Vector3(scale, scale, 1f);
                    if (pane.CardGroup != null)
                        pane.CardGroup.alpha = Ramp(t, b + 0.05f, b + 0.15f);
                }
                Show(
                    pane.CardGlow,
                    broken >= 0.05f,
                    light,
                    Ramp(t, b + 0.05f, b + 0.3f) * (rare ? 1f : 0.3f),
                    0f,
                    0f,
                    0f,
                    rare ? 1.3f : 0.8f
                );
                if (pane.CardGlow != null && pane.Card != null)
                    pane.CardGlow.rectTransform.anchoredPosition = pane.Card.anchoredPosition;

                if (broken >= 0f)
                {
                    shake += (rare ? 10f : 3f) * (1f - Ramp(broken, 0f, 0.22f));
                    if (rare)
                    {
                        float f = Mathf.Exp(-broken / 0.13f);
                        if (f > flash)
                        {
                            flash = f;
                            flashPane = i;
                        }
                    }
                }
                else
                    shake += 2.5f * charge;
            }

            var flashLight = flashPane >= 0 ? ColorOf(tiers[flashPane]) : Color.white;
            if (Flash != null && flashPane >= 0)
                Flash.rectTransform.anchoredPosition = HomeOf(Panes[flashPane]);
            Show(Flash, flash > 0.01f, flashLight, flash, 0f, 0f, 0f, 3f);
            bool glitter = t >= firstBreak;
            Show(
                Glitter,
                glitter,
                Color.white,
                1f,
                0.5f,
                Mathf.Max(0f, t - firstBreak) / 4f,
                0.75f,
                2.5f
            );

            if (Stage != null)
            {
                Stage.anchoredPosition =
                    new Vector2(Noise(t * 37f, 1.3f), Noise(t * 41f, 7.9f)) * shake;
            }
        }

        // 窓の中心（札が止まる位置）。
        private static Vector2 HomeOf(TempleSummonPane pane) =>
            pane.Glass != null ? pane.Glass.rectTransform.anchoredPosition : Vector2.zero;

        private void SetTier(TempleSummonPane pane, bool rainbow)
        {
            foreach (
                var graphic in new Graphic[]
                {
                    pane.Glow,
                    pane.Glass,
                    pane.Hole,
                    pane.Shards,
                    pane.CardGlow,
                }
            )
                if (graphic != null && graphic.material != null)
                    graphic.material.SetFloat("_Tier", rainbow ? 2f : 0f);
        }

        private void OnDestroy() => DestroyMaterials(materials);

        // 停止中の表示の切り替え：白7・金2・虹1の結果の各拍の1コマ。
        private static readonly TempleSummonTier[] SampleLights =
        {
            TempleSummonTier.White,
            TempleSummonTier.White,
            TempleSummonTier.Gold,
            TempleSummonTier.White,
            TempleSummonTier.White,
            TempleSummonTier.White,
            TempleSummonTier.Rainbow,
            TempleSummonTier.White,
            TempleSummonTier.Gold,
            TempleSummonTier.White,
        };

        private static readonly string[] MomentNames =
        {
            "灯る",
            "白の窓が砕ける",
            "虹の窓が砕ける",
            "並んだ札",
        };

        public IReadOnlyList<string> PreviewStates => MomentNames;

        public void ShowPreviewState(int index)
        {
            Setup(SampleLights, Seed);
            float rainbow = breaks[Array.IndexOf(SampleLights, TempleSummonTier.Rainbow)];
            switch (index)
            {
                case 0:
                    Evaluate(0.9f);
                    break;
                case 1:
                    Evaluate(FirstBreak + WhiteGap * 3.5f);
                    break;
                case 2:
                    Evaluate(rainbow + 0.08f);
                    break;
                default:
                    Evaluate(duration);
                    break;
            }
        }
    }
}
