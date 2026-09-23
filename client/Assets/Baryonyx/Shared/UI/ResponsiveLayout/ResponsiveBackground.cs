using UnityEngine;

namespace Baryonyx.UI
{
    /// <summary>
    /// Keeps a background image proportional while covering its RectTransform parent.
    /// The background may extend outside the parent so a different aspect ratio never
    /// stretches the source artwork.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsiveBackground : MonoBehaviour
    {
        public const float LandscapeAspect = 16f / 9f;

        [Min(0.01f)]
        public float AspectRatio = LandscapeAspect;

        private RectTransform rect;
        private RectTransform parent;
        private Vector2 lastParentSize;
        private float lastAspect;

        private void Awake() => Cache();

        private void OnEnable()
        {
            lastParentSize = Vector2.zero;
            Cache();
            Apply();
        }

        private void OnTransformParentChanged()
        {
            lastParentSize = Vector2.zero;
            Cache();
            Apply();
        }

        private void LateUpdate() => Apply();

        public bool Apply()
        {
            Cache();
            if (parent == null || rect == null)
                return false;

            var parentSize = parent.rect.size;
            var aspect = AspectRatio > 0 ? AspectRatio : LandscapeAspect;
            if (
                parentSize.x <= 0
                || parentSize.y <= 0
                || (parentSize == lastParentSize && Mathf.Approximately(aspect, lastAspect))
            )
                return false;

            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = CoverSize(parentSize, aspect);
            lastParentSize = parentSize;
            lastAspect = aspect;
            return true;
        }

        public static Vector2 CoverSize(Vector2 parentSize, float aspect)
        {
            if (parentSize.x <= 0 || parentSize.y <= 0 || aspect <= 0)
                return Vector2.zero;

            float width = Mathf.Max(parentSize.x, parentSize.y * aspect);
            float height = Mathf.Max(parentSize.y, parentSize.x / aspect);
            return new Vector2(width, height);
        }

        private void Cache()
        {
            rect ??= transform as RectTransform;
            var nextParent = rect != null ? rect.parent as RectTransform : null;
            if (parent != nextParent)
            {
                parent = nextParent;
                lastParentSize = Vector2.zero;
            }
        }
    }
}
