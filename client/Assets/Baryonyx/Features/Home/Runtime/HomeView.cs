using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Baryonyx.Vfx.Hd2d;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Baryonyx.Home
{
    /// <summary>
    /// Draws a <see cref="HomeViewState"/> into the generated prefab and reports button
    /// presses as <see cref="HomeAction"/> values. It owns no game rules. Without a
    /// listener (the showcase preview), tapping the step panel plays sample feedback.
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        public static readonly Color GaugeFilled = new(0.37f, 0.83f, 0.78f);
        public static readonly Color GaugeAchieved = new(0.95f, 0.76f, 0.31f);
        public static readonly Color GaugeEmpty = new(0.95f, 0.91f, 0.82f, 0.14f);
        public static readonly Color TextMain = new(0.95f, 0.91f, 0.82f);
        public static readonly Color TextSub = new(0.79f, 0.75f, 0.66f);

        [Header("歩数")]
        public Button StepButton;
        public TMP_Text DateLabel;
        public GameObject StepDetails;
        public GameObject UnlinkedDetails;
        public TMP_Text StepsLabel;
        public Image[] Segments = Array.Empty<Image>();
        public TMP_Text RemainingLabel;
        public TMP_Text ClaimLabel;

        // 案内の行（アイコンと文言）。TopのTAP TO STARTと同じ周期と濃さで点滅させる。
        public CanvasGroup ClaimGroup;

        [Min(0.1f)]
        public float ClaimPulseSeconds = 2.4f;

        [Range(0f, 1f)]
        public float ClaimPulseMinimumAlpha = 0.35f;

        [Header("右上")]
        public TMP_Text RunesLabel;
        public Button SettingsButton;

        [Header("世界")]
        public Button PartyWorldButton;

        [Header("左下")]
        [FormerlySerializedAs("PartyButton")]
        public Button TavernButton;

        [FormerlySerializedAs("EquipmentButton")]
        public Button WorkshopButton;

        [FormerlySerializedAs("SummonButton")]
        public Button TempleButton;

        [FormerlySerializedAs("WorldMapButton")]
        public Button TravelOfficeButton;

        [Header("右下")]
        public Button ResumeButton;
        public TMP_Text DestinationNameLabel;
        public TMP_Text DestinationFloorLabel;

        [Header("通知")]
        public CanvasGroup Toast;
        public TMP_Text ToastLabel;

        [Min(0.1f)]
        public float ToastSeconds = 1.6f;

        [Min(0f)]
        public float ToastFadeSeconds = 0.3f;

        // 画面中央に出す結果の表示（「獲得ルーンはありません」）。
        public CanvasGroup Notice;
        public TMP_Text NoticeLabel;

        [Min(0.1f)]
        public float NoticeSeconds = 1.4f;

        [Header("ルーン獲得")]
        // 粒子はUPTパネルを押した位置（分からなければ案内の文字）から出て、右上の所持ルーンのアイコンへ飛ぶ。
        public RectTransform RuneOrigin;
        public RectTransform RuneTarget;
        public RectTransform RuneEffectLayer;

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

        // 粒子がUPTパネルから四方へ広がる時間。
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

        // 粒子が着いてから「+N」を消し始めるまで。
        [Min(0f)]
        public float GainHoldSeconds = 0.5f;

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

        private readonly List<(Button button, UnityEngine.Events.UnityAction listener)> bindings =
            new();
        private readonly List<Image> particles = new();
        private Coroutine toastRoutine;
        private Coroutine noticeRoutine;
        private Coroutine runeRoutine;
        private bool claimPulses;

        // 最後に描画した所持ルーン。描画前（Prefabのプレビュー）は焼き込んだ表示を残すためnull。
        private string runesText;
        private bool previewShowsNotice;

        // 最後の波で金色にした所持数を、演出の終わりに戻す色。
        private Color? runesLabelColor;

        // 演出で動かした所持ルーンのアイコンの拡大の軸（横）を、演出の終わりに戻す値。
        private float? runeIconPivot;

        // UPTパネルを押した画面上の位置。ルーンはここから弾ける。取れなければパネルのアイコンから出す。
        private Vector2? tapScreenPoint;

        public event Action<HomeAction> ActionRequested;

        public string CurrentToast => Toast != null && Toast.alpha > 0 ? ToastLabel.text : "";
        public string CurrentNotice => Notice != null && Notice.alpha > 0 ? NoticeLabel.text : "";

        // 演出の間は、所持ルーンを付与前の値から数え上げる。終わると最新の表示に戻す。
        public bool RuneGainPlaying => runeRoutine != null;

        // 直前の演出でルーンが弾けた中心（演出の層の座標）。テストで確かめる。
        internal Vector3 BurstOrigin { get; private set; }

        private void OnEnable()
        {
            Bind(StepButton, HomeAction.SyncSteps);
            Bind(SettingsButton, HomeAction.Settings);
            // The party standing in the world opens the tavern, where the party is formed.
            Bind(PartyWorldButton, HomeAction.Tavern);
            Bind(TavernButton, HomeAction.Tavern);
            Bind(WorkshopButton, HomeAction.Workshop);
            Bind(TempleButton, HomeAction.Temple);
            Bind(TravelOfficeButton, HomeAction.TravelOffice);
            Bind(ResumeButton, HomeAction.Resume);
            HideToast();
            HideNotice();
            FinishRuneGain();
        }

        private void OnDisable()
        {
            foreach (var (button, listener) in bindings)
                if (button != null)
                    button.onClick.RemoveListener(listener);
            bindings.Clear();
            toastRoutine = null;
            noticeRoutine = null;
            FinishRuneGain();
        }

        public void Render(HomeViewState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            Set(DateLabel, state.DateText);
            if (StepDetails != null)
                StepDetails.SetActive(state.ShowSteps);
            if (UnlinkedDetails != null)
                UnlinkedDetails.SetActive(!state.ShowSteps);
            Set(StepsLabel, state.UptText);
            if (StepsLabel != null)
                StepsLabel.color = state.DailyAchieved ? GaugeAchieved : TextMain;
            for (int i = 0; i < Segments.Length; i++)
            {
                if (Segments[i] == null)
                    continue;
                Segments[i].color =
                    i >= state.FilledSegments ? GaugeEmpty
                    : state.DailyAchieved ? GaugeAchieved
                    : GaugeFilled;
            }
            Set(RemainingLabel, state.RemainingText);
            if (RemainingLabel != null)
                RemainingLabel.color = state.DailyAchieved ? GaugeAchieved : TextSub;
            Set(ClaimLabel, state.ClaimText);
            claimPulses = state.ClaimPulses;
            if (!claimPulses && ClaimGroup != null)
                ClaimGroup.alpha = 1f;
            runesText = state.RunesText ?? "";
            if (!RuneGainPlaying)
                Set(RunesLabel, runesText);
            Set(DestinationNameLabel, state.DestinationNameText);
            Set(DestinationFloorLabel, state.DestinationFloorText);
        }

        private void Update()
        {
            if (claimPulses && ClaimGroup != null)
                ClaimGroup.alpha = PulseAlpha(
                    Time.unscaledTime,
                    ClaimPulseSeconds,
                    ClaimPulseMinimumAlpha
                );
        }

        // 周期の始めと終わりで1、半周期で最小値になる。
        public static float PulseAlpha(float time, float periodSeconds, float minimumAlpha)
        {
            float period = Mathf.Max(0.1f, periodSeconds);
            float wave = (Mathf.Cos(time / period * Mathf.PI * 2f) + 1f) * 0.5f;
            return Mathf.Lerp(Mathf.Clamp01(minimumAlpha), 1f, wave);
        }

        public void ShowToast(string message)
        {
            if (Toast == null || ToastLabel == null || string.IsNullOrEmpty(message))
                return;
            ToastLabel.text = message;
            Toast.alpha = 1f;
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = isActiveAndEnabled ? StartCoroutine(FadeToast()) : null;
        }

        public void HideToast()
        {
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = null;
            if (Toast != null)
                Toast.alpha = 0f;
        }

        private IEnumerator FadeToast()
        {
            yield return new WaitForSecondsRealtime(ToastSeconds);
            yield return FadeOut(Toast);
            toastRoutine = null;
        }

        public void ShowNotice(string message)
        {
            if (Notice == null || NoticeLabel == null || string.IsNullOrEmpty(message))
                return;
            NoticeLabel.text = message;
            Notice.alpha = 1f;
            if (noticeRoutine != null)
                StopCoroutine(noticeRoutine);
            noticeRoutine = isActiveAndEnabled ? StartCoroutine(FadeNotice()) : null;
        }

        public void HideNotice()
        {
            if (noticeRoutine != null)
                StopCoroutine(noticeRoutine);
            noticeRoutine = null;
            if (Notice != null)
                Notice.alpha = 0f;
        }

        private IEnumerator FadeNotice()
        {
            yield return new WaitForSecondsRealtime(NoticeSeconds);
            yield return FadeOut(Notice);
            noticeRoutine = null;
        }

        private IEnumerator FadeOut(CanvasGroup group)
        {
            float elapsed = 0f;
            while (elapsed < ToastFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / ToastFadeSeconds);
                yield return null;
            }
            group.alpha = 0f;
        }

        // 所持ルーンを from から to まで、粒子が着くたびに増やして見せる。
        public void PlayRuneGain(long from, long to)
        {
            FinishRuneGain();
            if (
                to <= from
                || !isActiveAndEnabled
                || RuneOrigin == null
                || RuneTarget == null
                || RuneEffectLayer == null
                || RuneParticle == null
            )
                return;
            runeRoutine = StartCoroutine(RuneGain(from, to));
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

        private IEnumerator RuneGain(long from, long to)
        {
            long granted = to - from;
            int count = ParticleCountFor(granted, RuneParticleMax);
            Vector3 origin = TapPoint() ?? PointOf(RuneOrigin);
            BurstOrigin = origin;
            runeIconPivot ??= RuneTarget.pivot.x;
            Vector3 target = PointOf(RuneTarget);
            Vector3 targetWorld = RuneEffectLayer.TransformPoint(target);
            if (RuneGlow != null)
            {
                RuneGlow.rectTransform.localPosition = target;
                RuneGlow.rectTransform.localScale = Vector3.one;
                SetAlpha(RuneGlow, 0f);
                RuneGlow.gameObject.SetActive(true);
            }
            float reach = BurstDistanceFor(granted, RuneBurstNearDistance, RuneBurstFarDistance);
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
            while (particles.Count < count)
            {
                var copy = Instantiate(RuneParticle, RuneEffectLayer);
                copy.name = RuneParticle.name;
                particles.Add(copy);
            }
            // キラキラはルーンの手前に描く。後ろでは96pxのルーンに隠れて見えない。
            if (RuneSparkles != null)
                RuneSparkles.transform.SetAsLastSibling();

            runesLabelColor ??= RunesLabel != null ? RunesLabel.color : Color.white;
            Color labelColor = runesLabelColor.Value;
            Set(RunesLabel, HomeViewState.Runes(from));
            if (GainLabel != null)
            {
                GainLabel.text = "+" + HomeViewState.Runes(granted);
                GainLabel.rectTransform.localScale = Vector3.one;
            }

            Sparkle(
                BurstSource,
                RuneBurstSparkles,
                RuneBurstHaze,
                RuneEffectLayer.TransformPoint(origin)
            );

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
                        Set(RunesLabel, HomeViewState.Runes(from + granted * arrived / count));
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
                    Vector3 spread = origin + direction[i] * distance[i];
                    if (elapsed < RuneBurstSeconds)
                    {
                        // ボワッと一気に広がり、外側でゆっくり止まる。
                        float p = 1f - Mathf.Pow(1f - elapsed / RuneBurstSeconds, 3f);
                        rect.localPosition = Vector3.Lerp(origin, spread, p);
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
                    RunesLabel.color = Color.Lerp(labelColor, GaugeAchieved, glow);
                }
                ScaleRuneIcon(scale, 1f + BumpScale * bump);

                // 大きくなって動いたアイコンへ、粒子・光・キラキラを向け直す。
                target = CenterOf(RuneTarget);
                targetWorld = RuneEffectLayer.TransformPoint(target);

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
                        (elapsed - lastArrival - GainHoldSeconds) / ToastFadeSeconds
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
            runeRoutine = null;
            FinishRuneGain();
        }

        private void FinishRuneGain()
        {
            if (runeRoutine != null)
                StopCoroutine(runeRoutine);
            runeRoutine = null;
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
            if (runesText != null)
                Set(RunesLabel, runesText);
        }

        // 押した位置を覚える。直接呼び出した場合など、ポインターがパネルの外にあれば使わない。
        private void RememberTap(Button button)
        {
            tapScreenPoint = null;
            var pointer = Pointer.current;
            if (pointer == null || button == null)
                return;
            Vector2 point = pointer.position.ReadValue();
            if (
                RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)button.transform,
                    point,
                    EventCamera()
                )
            )
                tapScreenPoint = point;
        }

        // 覚えた位置を演出の層の座標にして返す。1回使ったら忘れる。
        private Vector3? TapPoint()
        {
            var point = tapScreenPoint;
            tapScreenPoint = null;
            if (
                !point.HasValue
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    RuneEffectLayer,
                    point.Value,
                    EventCamera(),
                    out var local
                )
            )
                return null;
            return new Vector3(local.x, local.y, 0f);
        }

        // Overlayのキャンバスではカメラを使わず、展示室のカメラ描画ではそのカメラで座標を変換する。
        private Camera EventCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        // 演出の層の座標で、対象の中心を返す。レイアウト後の位置を使う。
        private Vector3 PointOf(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            return CenterOf(rect);
        }

        // 演出の層の座標で、拡大を含めた対象の今の中心を返す。
        private Vector3 CenterOf(RectTransform rect)
        {
            var point = RuneEffectLayer.InverseTransformPoint(
                rect.TransformPoint(rect.rect.center)
            );
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

        private static Vector3 Bezier(Vector3 start, Vector3 control, Vector3 end, float t) =>
            Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, end, t), t);

        // 展示室のプレビューには受け手がいないため、UPTパネルで獲得と獲得なしを交互に見せる。
        private void PlayPreview(HomeAction action)
        {
            if (action != HomeAction.SyncSteps)
                return;
            previewShowsNotice = !previewShowsNotice;
            if (!previewShowsNotice)
            {
                ShowNotice(HomePresenter.NoRunesMessage);
                return;
            }
            long.TryParse(
                runesText ?? (RunesLabel != null ? RunesLabel.text : ""),
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out long from
            );
            long to = from + 1340;
            runesText = HomeViewState.Runes(to);
            PlayRuneGain(from, to);
        }

        private void Bind(Button button, HomeAction action)
        {
            if (button == null)
                return;
            UnityEngine.Events.UnityAction listener = () =>
            {
                if (action == HomeAction.SyncSteps)
                    RememberTap(button);
                if (ActionRequested != null)
                    ActionRequested.Invoke(action);
                else
                    PlayPreview(action);
            };
            button.onClick.AddListener(listener);
            bindings.Add((button, listener));
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null)
                label.text = text ?? "";
        }
    }
}
