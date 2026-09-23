using System;
using System.Collections;
using UnityEngine;

namespace Baryonyx.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(UnityEngine.UI.GraphicRaycaster))]
    public sealed class SceneTransitionController : MonoBehaviour
    {
        [Header("Exit")]
        [SerializeField]
        private SceneTransitionSettings defaultSettings = new();

        [Header("Enter")]
        [SerializeField]
        private SceneTransitionSettings enterSettings = new();

        [SerializeField]
        private bool startCovered;

        [SerializeField]
        private bool revealOnStart;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private UnityEngine.UI.Image blocker;

        [SerializeField]
        private UnityEngine.UI.Image fadePanel;

        [SerializeField]
        private UnityEngine.UI.Image wipePanel;

        [SerializeField]
        private UnityEngine.UI.Image shutterFirst;

        [SerializeField]
        private UnityEngine.UI.Image shutterSecond;

        private bool playing;
        private bool covered;

        public SceneTransitionSettings DefaultSettings => defaultSettings;
        public SceneTransitionSettings EnterSettings => enterSettings;
        public bool IsPlaying => playing;
        public bool IsCovered => covered;

        private void Awake()
        {
            EnsureSettings();
            if (!ResolveReferences())
                return;
            Configure(defaultSettings);
            ApplyProgress(defaultSettings, 1f, false);
            SetInputBlocked(false);
        }

        private void Start()
        {
            if (!ResolveReferences())
                return;
            var initialSettings = startCovered ? enterSettings : defaultSettings;
            Configure(initialSettings);
            covered = startCovered;
            ApplyProgress(initialSettings, 1f, startCovered);
            SetInputBlocked(startCovered);
            if (startCovered && revealOnStart)
                PlayIn(enterSettings);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            playing = false;
            covered = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                SetInputBlocked(false);
            }
        }

        private void OnValidate()
        {
            defaultSettings?.Normalize();
            enterSettings?.Normalize();
        }

        public bool PlayOut(Action onCovered = null) => PlayOut(defaultSettings, onCovered);

        public bool PlayOut(SceneTransitionSettings settings, Action onCovered = null)
        {
            if (!TryPrepare(settings, defaultSettings, out var prepared))
                return false;
            playing = true;
            StartCoroutine(PlayRoutine(prepared, true, onCovered));
            return true;
        }

        public bool PlayIn(Action onCompleted = null) => PlayIn(enterSettings, onCompleted);

        public bool PlayIn(SceneTransitionSettings settings, Action onCompleted = null)
        {
            if (!TryPrepare(settings, enterSettings, out var prepared))
                return false;
            playing = true;
            StartCoroutine(PlayRoutine(prepared, false, onCompleted));
            return true;
        }

        private IEnumerator PlayRoutine(
            SceneTransitionSettings settings,
            bool closing,
            Action callback
        )
        {
            yield return Animate(settings, closing);
            playing = false;
            callback?.Invoke();
        }

        private IEnumerator Animate(SceneTransitionSettings settings, bool closing)
        {
            Configure(settings);
            SetInputBlocked(true);
            covered = !closing;
            ApplyProgress(settings, 0f, closing);
            float duration = closing ? settings.CoverDuration : settings.RevealDuration;
            float elapsed = 0f;
            // 初期状態も1フレーム描画し、短いdurationでも開始位置が飛ばないようにする。
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                ApplyProgress(
                    settings,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)),
                    closing
                );
            }
            ApplyProgress(settings, 1f, closing);
            covered = closing;
            SetInputBlocked(closing);
        }

        private bool TryPrepare(
            SceneTransitionSettings settings,
            SceneTransitionSettings fallback,
            out SceneTransitionSettings prepared
        )
        {
            prepared = null;
            if (playing || !isActiveAndEnabled || !ResolveReferences())
                return false;
            prepared = (settings ?? fallback ?? new SceneTransitionSettings()).Clone();
            prepared.Normalize();
            return true;
        }

        private void EnsureSettings()
        {
            defaultSettings ??= new SceneTransitionSettings();
            enterSettings ??= defaultSettings.Clone();
            defaultSettings.Normalize();
            enterSettings.Normalize();
        }

        private bool ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (blocker == null)
                blocker = FindImage("TransitionBlocker");
            if (fadePanel == null)
                fadePanel = FindImage("FadePanel");
            if (wipePanel == null)
                wipePanel = FindImage("WipePanel");
            if (shutterFirst == null)
                shutterFirst = FindImage("ShutterFirst");
            if (shutterSecond == null)
                shutterSecond = FindImage("ShutterSecond");
            bool ready =
                canvasGroup != null
                && blocker != null
                && fadePanel != null
                && wipePanel != null
                && shutterFirst != null
                && shutterSecond != null;
            if (!ready)
                Debug.LogError(
                    "SceneTransitionController requires its complete overlay hierarchy.",
                    this
                );
            return ready;
        }

        private UnityEngine.UI.Image FindImage(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<UnityEngine.UI.Image>() : null;
        }

        private void Configure(SceneTransitionSettings settings)
        {
            blocker.color = Color.clear;
            blocker.raycastTarget = true;
            SetRect(blocker.rectTransform, Vector2.zero, Vector2.one);
            SetRect(fadePanel.rectTransform, Vector2.zero, Vector2.one);
            foreach (var panel in new[] { fadePanel, wipePanel, shutterFirst, shutterSecond })
            {
                panel.color = settings.Color;
                panel.raycastTarget = false;
            }
            fadePanel.gameObject.SetActive(settings.Type == SceneTransitionType.Fade);
            wipePanel.gameObject.SetActive(settings.Type == SceneTransitionType.Wipe);
            shutterFirst.gameObject.SetActive(settings.Type == SceneTransitionType.Shutter);
            shutterSecond.gameObject.SetActive(settings.Type == SceneTransitionType.Shutter);
            canvasGroup.alpha = 1f;
        }

        private void ApplyProgress(SceneTransitionSettings settings, float progress, bool closing)
        {
            float openness = closing ? 1f - progress : progress;
            switch (settings.Type)
            {
                case SceneTransitionType.Fade:
                    canvasGroup.alpha = 1f - openness;
                    break;
                case SceneTransitionType.Wipe:
                    float direction =
                        settings.WipeDirection == SceneTransitionWipeDirection.LeftToRight
                            ? 1f
                            : -1f;
                    float x = (closing ? -direction : direction) * openness;
                    SetRect(wipePanel.rectTransform, new Vector2(x, 0f), new Vector2(x + 1f, 1f));
                    break;
                case SceneTransitionType.Shutter:
                    float offset = openness * 0.5f;
                    if (settings.ShutterAxis == SceneTransitionShutterAxis.Vertical)
                    {
                        SetRect(
                            shutterFirst.rectTransform,
                            new Vector2(0f, 0.5f + offset),
                            new Vector2(1f, 1f + offset)
                        );
                        SetRect(
                            shutterSecond.rectTransform,
                            new Vector2(0f, -offset),
                            new Vector2(1f, 0.5f - offset)
                        );
                    }
                    else
                    {
                        SetRect(
                            shutterFirst.rectTransform,
                            new Vector2(-offset, 0f),
                            new Vector2(0.5f - offset, 1f)
                        );
                        SetRect(
                            shutterSecond.rectTransform,
                            new Vector2(0.5f + offset, 0f),
                            new Vector2(1f + offset, 1f)
                        );
                    }
                    break;
            }
        }

        private void SetInputBlocked(bool blocked)
        {
            canvasGroup.blocksRaycasts = blocked;
            canvasGroup.interactable = false;
            if (blocked && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
