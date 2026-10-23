using System;
using System.Collections;
using Baryonyx.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Baryonyx.UI.UiText;

namespace Baryonyx.Training
{
    /// <summary>
    /// The moment a bond deepens, over the whole page: the character's illustration under an arc
    /// of six stones joined by gold lines, where the new stage's stone drops in and lights with a
    /// burst of light, then the name, the stage and what it raises. A tap anywhere closes it.
    /// TrainingAssets bakes it into the formation prefab; in the editor (and while the page is
    /// not shown) it shows the end of the moment at once.
    /// </summary>
    public sealed class TrainingBondCelebration : MonoBehaviour
    {
        // 暗く落としてから石が灯り、名前と段が出るまで（秒）。
        private const float FadeIn = 0.3f;
        private const float LightAt = 0.45f;
        private const float DropTime = 0.4f;
        private const float BurstTime = 0.8f;
        private const float CardAt = 0.95f;
        private const float CardTime = 0.4f;

        // 灯る石が上から落ちてくるときの大きさと、名前と段の札が下からせり上がる量（設計座標）。
        private const float DropScale = 2.6f;
        private const float BurstScale = 3.4f;
        private const float CardRise = 36f;

        public CanvasGroup Group;
        public Button Close;
        public RawImage Illustration;
        public Image[] Stones = Array.Empty<Image>();

        // Links[i] は Stones[i] と Stones[i + 1] をつなぐ線。両端が灯ると明るくする。
        public Image[] Links = Array.Empty<Image>();
        public Sprite Lit;
        public Sprite Dark;
        public Color LinkLit = new(1f, 0.84f, 0.45f, 0.95f);
        public Color LinkDark = new(0.55f, 0.6f, 0.75f, 0.35f);

        // 灯る石から広がる光。
        public Image Burst;
        public CanvasGroup Card;
        public TMP_Text Name;
        public TMP_Text Stage;
        public TMP_Text Effect;

        private PartyMember member;
        private int stage;
        private Vector2 cardHome;
        private bool homed;

        public event Action Closed;

        private void Awake() => Home();

        private void OnEnable()
        {
            if (Close != null)
                Close.onClick.AddListener(OnClose);
        }

        private void OnDisable()
        {
            if (Close != null)
                Close.onClick.RemoveListener(OnClose);
            StopAllCoroutines();
        }

        private void OnClose() => Closed?.Invoke();

        /// <summary>
        /// Shows <paramref name="lit"/> stages lit for <paramref name="who"/>, the last one lighting
        /// up. Showing the same again keeps the moment where it is.
        /// </summary>
        public void Show(PartyMember who, int lit)
        {
            if (gameObject.activeSelf && member == who && stage == lit)
                return;
            member = who;
            stage = Mathf.Clamp(lit, 1, Stones.Length);
            Home();
            if (Illustration != null)
                PartyArt.Cover(Illustration, who != null ? who.Illustration : null);
            Set(Name, who != null ? who.Name : "");
            Set(Stage, stage.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Set(Effect, $"全ステータス +{TrainingBond.PercentAt(stage)}%");
            gameObject.SetActive(true);
            StopAllCoroutines();
            if (Application.isPlaying && isActiveAndEnabled)
                StartCoroutine(Play());
            else
                Pose(float.MaxValue);
        }

        public void Hide()
        {
            StopAllCoroutines();
            member = null;
            stage = 0;
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private IEnumerator Play()
        {
            float time = 0f;
            while (time < CardAt + CardTime)
            {
                Pose(time);
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            Pose(float.MaxValue);
        }

        // 演出の始めから time 秒の姿。
        private void Pose(float time)
        {
            int last = stage - 1;
            float light = Mathf.Clamp01((time - LightAt) / DropTime);
            if (Group != null)
                Group.alpha = Mathf.Clamp01(time / FadeIn);
            for (int i = 0; i < Stones.Length; i++)
            {
                var stone = Stones[i];
                if (stone == null)
                    continue;
                bool lit = i < last || (i == last && time >= LightAt);
                stone.sprite = lit ? Lit : Dark;
                // 灯る石は大きく薄い姿から、元の大きさへ少し行き過ぎて戻る。
                float scale =
                    i == last && time >= LightAt
                        ? Mathf.LerpUnclamped(DropScale, 1f, BackOut(light))
                        : 1f;
                stone.rectTransform.localScale = Vector3.one * scale;
                stone.color = new Color(
                    1f,
                    1f,
                    1f,
                    i == last && time >= LightAt ? Mathf.Clamp01(light * 2f) : 1f
                );
            }
            for (int i = 0; i < Links.Length; i++)
                if (Links[i] != null)
                    Links[i].color =
                        i + 1 < last || (i + 1 == last && time >= LightAt + DropTime)
                            ? LinkLit
                            : LinkDark;
            if (Burst != null)
            {
                float burst = Mathf.Clamp01((time - LightAt - DropTime * 0.6f) / BurstTime);
                bool on = last >= 0 && last < Stones.Length && burst > 0f && burst < 1f;
                Burst.gameObject.SetActive(on);
                if (on)
                {
                    Burst.rectTransform.anchoredPosition = Stones[last]
                        .rectTransform
                        .anchoredPosition;
                    Burst.rectTransform.localScale =
                        Vector3.one
                        * Mathf.Lerp(0.6f, BurstScale, 1f - (1f - burst) * (1f - burst));
                    Burst.color = new Color(1f, 0.9f, 0.6f, (1f - burst) * 0.9f);
                }
            }
            if (Card != null)
            {
                float card = Mathf.Clamp01((time - CardAt) / CardTime);
                Card.alpha = card;
                ((RectTransform)Card.transform).anchoredPosition =
                    cardHome + Vector2.down * (CardRise * (1f - card) * (1f - card));
            }
        }

        // 札の置き場所を覚える（演出でずらす前の位置）。
        private void Home()
        {
            if (homed || Card == null)
                return;
            cardHome = ((RectTransform)Card.transform).anchoredPosition;
            homed = true;
        }

        private static float BackOut(float t)
        {
            const float overshoot = 1.7f;
            float u = t - 1f;
            return 1f + u * u * ((overshoot + 1f) * u + overshoot);
        }
    }
}
