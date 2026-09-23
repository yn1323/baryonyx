using UnityEngine;

namespace Baryonyx.Home
{
    /// <summary>
    /// Shrinks the centred world layer when the screen is narrower than the 16:9 design
    /// width (for example 4:3 tablets), so the party never slides under the corner buttons.
    /// Wider screens keep the design size and only reveal more background.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HomeWorldFit : MonoBehaviour
    {
        public Vector2 DesignSize = new(1920f, 1080f);

        private RectTransform rect;
        private float lastWidth = -1f;

        private void OnEnable()
        {
            lastWidth = -1f;
            Apply();
        }

        private void LateUpdate() => Apply();

        public bool Apply()
        {
            rect ??= transform as RectTransform;
            var parent = rect != null ? rect.parent as RectTransform : null;
            if (parent == null || DesignSize.x <= 0f)
                return false;

            float width = parent.rect.width;
            if (width <= 0f || Mathf.Approximately(width, lastWidth))
                return false;

            lastWidth = width;
            float scale = ScaleFor(width, DesignSize.x);
            rect.localScale = new Vector3(scale, scale, 1f);
            return true;
        }

        public static float ScaleFor(float width, float designWidth) =>
            width <= 0f || designWidth <= 0f ? 1f : Mathf.Min(1f, width / designWidth);
    }
}
