using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// Places a RectTransform inside a supplied Safe Area or the device Safe Area. On a screen
    /// wider than 16:9 the device Safe Area is also cut to the 16:9 width at the centre, so the
    /// buttons stay where the thumbs reach (doc/rules/ui-design.md#画面端の部品と中央の層の部品).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFollower : MonoBehaviour
    {
        // 操作の部品を収める中央の幅（設計座標の1920×1080）。
        public const float CoreAspect = 16f / 9f;

        // Source and target must have parents with the same full-screen bounds.
        public RectTransform Source;

        private RectTransform target;
        private RectTransform source;

        private void Awake() => Cache();

        private void OnEnable()
        {
            Cache();
            Apply();
        }

        private void LateUpdate() => Apply();

        public bool Apply()
        {
            Cache();
            if (target == null)
                return false;

            if (source != null)
                return ApplyAnchors(source.anchorMin, source.anchorMax);

            return ApplyViewport(new Vector2(Screen.width, Screen.height), Screen.safeArea);
        }

        public bool ApplyViewport(Vector2 screen, Rect safe)
        {
            Cache();
            if (screen.x <= 0 || screen.y <= 0 || safe.width <= 0 || safe.height <= 0)
                return false;

            // 横長の画面では、中央の16:9の幅の外へ操作の部品を出さない（2026-11-04、ユーザーの指示）。
            float core = screen.y * CoreAspect;
            float coreLeft = Mathf.Max(0f, (screen.x - core) * 0.5f);
            float left = Mathf.Max(safe.xMin, coreLeft);
            float right = Mathf.Min(safe.xMax, screen.x - coreLeft);
            return ApplyAnchors(
                new Vector2(Mathf.Clamp01(left / screen.x), Mathf.Clamp01(safe.yMin / screen.y)),
                new Vector2(Mathf.Clamp01(right / screen.x), Mathf.Clamp01(safe.yMax / screen.y))
            );
        }

        private bool ApplyAnchors(Vector2 minimum, Vector2 maximum)
        {
            if (
                target == null
                || maximum.x <= minimum.x
                || maximum.y <= minimum.y
                || (
                    target.anchorMin == minimum
                    && target.anchorMax == maximum
                    && target.offsetMin == Vector2.zero
                    && target.offsetMax == Vector2.zero
                )
            )
                return false;

            target.anchorMin = minimum;
            target.anchorMax = maximum;
            target.offsetMin = target.offsetMax = Vector2.zero;
            return true;
        }

        private void Cache()
        {
            target ??= transform as RectTransform;
            source = Source;
        }
    }
}
