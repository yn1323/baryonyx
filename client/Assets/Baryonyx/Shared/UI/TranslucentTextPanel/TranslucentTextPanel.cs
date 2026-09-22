using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// Reusable text panel with a soft gray translucent backdrop.
    /// </summary>
    public sealed class TranslucentTextPanel : MonoBehaviour
    {
        public RawImage Panel;
        public RawImage Backdrop;
        public RectTransform BackdropCanvas;
        public TextMeshProUGUI Label;
        public CanvasGroup LabelGroup;

        [Header("表示設定")]
        [Min(0.1f)]
        [SerializeField]
        private float fontSize = 64f;

        [SerializeField]
        private Vector2 backdropSize = new Vector2(1320f, 260f);

        [Range(0f, 1f)]
        [SerializeField]
        private float backdropAlpha = 0.42f;

        [Header("点滅設定")]
        [SerializeField]
        private bool pulseEnabled;

        [Min(0.1f)]
        [SerializeField]
        private float pulseDurationSeconds = 2.4f;

        [Range(0f, 1f)]
        [SerializeField]
        private float pulseMinimumAlpha = 0.35f;

        private Coroutine pulseRoutine;

        public float FontSize
        {
            get => fontSize;
            set
            {
                fontSize = value;
                ApplyVisualSettings();
            }
        }

        public Vector2 BackdropSize
        {
            get => backdropSize;
            set
            {
                backdropSize = value;
                ApplyVisualSettings();
            }
        }

        public float BackdropAlpha
        {
            get => backdropAlpha;
            set
            {
                backdropAlpha = value;
                ApplyVisualSettings();
            }
        }

        public bool PulseEnabled
        {
            get => pulseEnabled;
            set => SetPulseEnabled(value);
        }

        public float PulseDurationSeconds
        {
            get => pulseDurationSeconds;
            set => pulseDurationSeconds = Mathf.Max(0.1f, value);
        }

        public float PulseMinimumAlpha
        {
            get => pulseMinimumAlpha;
            set => pulseMinimumAlpha = Mathf.Clamp01(value);
        }

        private void OnEnable()
        {
            ApplyVisualSettings();
            StartPulseIfNeeded();
        }

        private void OnDisable()
        {
            StopPulse();
        }

        public void SetText(string value)
        {
            if (Label != null)
                Label.text = value;
        }

        public void SetFontSize(float value)
        {
            FontSize = value;
        }

        public void SetBackdropSize(Vector2 size)
        {
            BackdropSize = size;
        }

        public void SetBackdropAlpha(float value)
        {
            BackdropAlpha = value;
        }

        public void SetPulseEnabled(bool value)
        {
            pulseEnabled = value;
            if (!isActiveAndEnabled)
                return;

            if (pulseEnabled)
                StartPulseIfNeeded();
            else
                StopPulse();
        }

        private void OnValidate()
        {
            ApplyVisualSettings();
            if (!Application.isPlaying)
                return;

            if (pulseEnabled)
                StartPulseIfNeeded();
            else
                StopPulse();
        }

        private void ApplyVisualSettings()
        {
            fontSize = Mathf.Max(0.1f, fontSize);
            backdropSize = new Vector2(
                Mathf.Max(0f, backdropSize.x),
                Mathf.Max(0f, backdropSize.y)
            );
            backdropAlpha = Mathf.Clamp01(backdropAlpha);
            pulseDurationSeconds = Mathf.Max(0.1f, pulseDurationSeconds);
            pulseMinimumAlpha = Mathf.Clamp01(pulseMinimumAlpha);

            if (Label != null)
                Label.fontSize = fontSize;
            if (BackdropCanvas != null)
                BackdropCanvas.sizeDelta = backdropSize;
            if (Backdrop != null)
            {
                var color = Backdrop.color;
                color.a = backdropAlpha;
                Backdrop.color = color;
            }
        }

        private void StartPulseIfNeeded()
        {
            if (!Application.isPlaying || !pulseEnabled || LabelGroup == null)
                return;

            StopPulse();
            pulseRoutine = StartCoroutine(PulseLabel());
        }

        private void StopPulse()
        {
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            if (LabelGroup != null)
                LabelGroup.alpha = 1f;
        }

        private IEnumerator PulseLabel()
        {
            var elapsed = 0f;
            while (true)
            {
                var duration = Mathf.Max(0.1f, pulseDurationSeconds);
                var normalizedTime = elapsed / duration;
                var wave = (Mathf.Cos(normalizedTime * Mathf.PI * 2f) + 1f) * 0.5f;
                var minimumAlpha = Mathf.Clamp01(pulseMinimumAlpha);
                LabelGroup.alpha = Mathf.Lerp(minimumAlpha, 1f, wave);

                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= duration)
                    elapsed -= duration;

                yield return null;
            }
        }
    }
}
