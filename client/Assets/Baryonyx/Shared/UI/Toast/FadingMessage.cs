using System.Collections;
using TMPro;
using UnityEngine;

namespace Baryonyx.UI
{
    // 知らせの文字をしばらく表示してから消す（トースト）。表示中にもう一度出すと、表示時間を数え直す。
    // 見えているかどうかは CanvasGroup の透明度で表す。
    public sealed class FadingMessage
    {
        private readonly MonoBehaviour host;
        private Coroutine routine;

        public FadingMessage(MonoBehaviour host) => this.host = host;

        // 表示が止まっている画面では文字だけを入れ替え、見せない。
        public void Show(
            CanvasGroup group,
            TMP_Text label,
            string message,
            float holdSeconds,
            float fadeSeconds
        )
        {
            if (group == null || label == null)
                return;
            label.text = message ?? "";
            Stop();
            if (!host.isActiveAndEnabled)
                return;
            group.alpha = 1f;
            routine = host.StartCoroutine(HoldAndFade(group, holdSeconds, fadeSeconds));
        }

        public void Hide(CanvasGroup group)
        {
            Stop();
            if (group != null)
                group.alpha = 0f;
        }

        private void Stop()
        {
            if (routine != null)
                host.StopCoroutine(routine);
            routine = null;
        }

        private IEnumerator HoldAndFade(CanvasGroup group, float holdSeconds, float fadeSeconds)
        {
            yield return new WaitForSecondsRealtime(holdSeconds);
            for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }
            group.alpha = 0f;
            routine = null;
        }
    }
}
