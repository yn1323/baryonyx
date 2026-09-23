using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// Places a RectTransform inside a supplied Safe Area or the device Safe Area.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFollower : MonoBehaviour
    {
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

            return ApplyAnchors(
                new Vector2(
                    Mathf.Clamp01(safe.xMin / screen.x),
                    Mathf.Clamp01(safe.yMin / screen.y)
                ),
                new Vector2(
                    Mathf.Clamp01(safe.xMax / screen.x),
                    Mathf.Clamp01(safe.yMax / screen.y)
                )
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
