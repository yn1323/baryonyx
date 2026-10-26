using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Baryonyx.UI
{
    /// <summary>
    /// A frosted-glass card that the player taps or holds: a translucent rounded plate with a rim
    /// lit from the top left, a pattern in the top-left corner and a soft shadow under it
    /// (GlassCardAssets builds the parts). Pressing sinks the plate a little so it reads as
    /// pressable; a tap raises <see cref="Clicked"/>, and holding for <see cref="HoldSeconds"/>
    /// raises <see cref="Held"/> instead. Dragging (scrolling the list it sits in) cancels both.
    /// The chosen card shows its gold rim and glow (<see cref="Selection"/>).
    /// </summary>
    public sealed class GlassCard
        : MonoBehaviour,
            IPointerDownHandler,
            IPointerUpHandler,
            IPointerExitHandler,
            IPointerClickHandler
    {
        public const float DefaultHoldSeconds = 0.5f;

        // 押している間に沈める板と、小さくする影。
        public RectTransform Body;
        public RectTransform Shadow;

        // 選んだときの金の縁と光（中身の上）。板の金色の色づけ（中身の下）は GlassCardSelection が一緒に出す。
        public GameObject Selection;

        // 押せないとき（保存中など）は薄くする。
        public CanvasGroup Group;
        public float HoldSeconds = DefaultHoldSeconds;

        private const float SinkDistance = 3f;
        private const float DisabledAlpha = 0.55f;

        // 押せるが、選んでも決められない物（材料の足りない合成など）の薄さ。
        private const float DimmedAlpha = 0.5f;

        private PointerEventData press;
        private float pressedAt;
        private bool held;
        private bool sunk;
        private bool interactable = true;
        private bool dimmed;

        public event Action Clicked;
        public event Action Held;

        public bool Selected
        {
            get => Selection != null && Selection.activeSelf;
            set
            {
                if (Selection != null)
                    Selection.SetActive(value);
            }
        }

        /// <summary>The card's opacity when face up: faded while it takes no taps, or is dimmed.</summary>
        public float RestingAlpha =>
            !interactable ? DisabledAlpha
            : dimmed ? DimmedAlpha
            : 1f;

        /// <summary>Whether the card takes taps; a card that does not is drawn faded.</summary>
        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                ShowRestingAlpha();
                if (!value)
                    Cancel();
            }
        }

        /// <summary>Whether the card is drawn faded while still taking taps.</summary>
        public bool Dimmed
        {
            get => dimmed;
            set
            {
                dimmed = value;
                ShowRestingAlpha();
            }
        }

        private void ShowRestingAlpha()
        {
            if (Group == null)
                return;
            // 伏せている間は裏を透かさない。表になるときに GlassCardBack が戻す。
            if (TryGetComponent(out GlassCardBack back) && (back.FaceDown || back.Leaving))
                return;
            Group.alpha = RestingAlpha;
        }

        /// <summary>Taps the card, as a finger lifting on it would.</summary>
        public void Click()
        {
            if (interactable)
                Clicked?.Invoke();
        }

        /// <summary>Holds the card, as a finger resting on it would.</summary>
        public void Hold()
        {
            if (interactable)
                Held?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Buttonと組み合わせたカード（GlassCardAssets.Choice）は、Buttonが押せないときは沈めない。
            if (
                !interactable
                || (TryGetComponent(out UnityEngine.UI.Button button) && !button.interactable)
            )
                return;
            press = eventData;
            pressedAt = Time.unscaledTime;
            held = false;
            Sink(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            press = null;
            Sink(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (press != null && !held)
                Cancel();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 長押しのあとに指を離したときは、押したことにしない。
            if (held)
            {
                held = false;
                return;
            }
            if (!eventData.dragging)
            {
                Audio.UiSounds.Play(Pressable() ? Audio.UiSound.Press : Audio.UiSound.Denied);
                Click();
            }
        }

        // Buttonと組み合わせたカードは、Buttonが押せないときも押せない。
        private bool Pressable() =>
            interactable
            && !(TryGetComponent(out UnityEngine.UI.Button button) && !button.IsInteractable());

        private void Update()
        {
            if (press == null || held)
                return;
            // 一覧を動かし始めたら、長押しにしない。
            if (press.dragging)
            {
                Cancel();
                return;
            }
            if (Time.unscaledTime - pressedAt >= HoldSeconds)
            {
                held = true;
                Sink(false);
                Hold();
            }
        }

        private void OnDisable() => Cancel();

        private void Cancel()
        {
            press = null;
            Sink(false);
        }

        private void Sink(bool down)
        {
            if (sunk == down)
                return;
            sunk = down;
            float offset = down ? -SinkDistance : SinkDistance;
            if (Body != null)
                Body.anchoredPosition += new Vector2(0f, offset);
            if (Shadow != null)
                Shadow.anchoredPosition += new Vector2(0f, -offset * 2f);
        }
    }
}
