using UnityEngine;

namespace Baryonyx.Health
{
    [DisallowMultipleComponent]
    public sealed class HealthScreenLayout : MonoBehaviour
    {
        public RectTransform SafeArea;
        public RectTransform DetailsSafeArea;
        public RectTransform MainPanel;
        public RectTransform DetailsPanel;
        public const float PageMargin = 40;
        public const float MainMaximumWidth = 860;
        public const float DetailsMaximumWidth = 980;

        private Vector2 previousScreen;
        private Vector2 previousCanvas;
        private Rect previousSafeArea;
        private bool applied;

        private void OnEnable() => applied = false;

        private void LateUpdate()
        {
            ApplyViewport(
                new Vector2(Screen.width, Screen.height),
                Screen.safeArea,
                ((RectTransform)transform).rect.size
            );
        }

        // Explicit dimensions also let layout tests use the production prefab without OS state.
        public bool ApplyViewport(Vector2 screenSize, Rect safeArea, Vector2 canvasSize)
        {
            if (
                screenSize.x <= 0
                || screenSize.y <= 0
                || canvasSize.x <= 0
                || canvasSize.y <= 0
                || safeArea.width <= 0
                || safeArea.height <= 0
                || SafeArea == null
                || DetailsSafeArea == null
                || MainPanel == null
                || DetailsPanel == null
            )
                return false;

            if (
                applied
                && previousScreen == screenSize
                && previousCanvas == canvasSize
                && previousSafeArea == safeArea
            )
                return false;

            var minimum = new Vector2(
                Mathf.Clamp01(safeArea.xMin / screenSize.x),
                Mathf.Clamp01(safeArea.yMin / screenSize.y)
            );
            var maximum = new Vector2(
                Mathf.Clamp01(safeArea.xMax / screenSize.x),
                Mathf.Clamp01(safeArea.yMax / screenSize.y)
            );
            if (maximum.x <= minimum.x || maximum.y <= minimum.y)
                return false;

            FitSafeArea(SafeArea, minimum, maximum);
            FitSafeArea(DetailsSafeArea, minimum, maximum);
            float availableWidth = canvasSize.x * (maximum.x - minimum.x);
            FitPanel(MainPanel, availableWidth, MainMaximumWidth);
            FitPanel(DetailsPanel, availableWidth, DetailsMaximumWidth);
            previousScreen = screenSize;
            previousCanvas = canvasSize;
            previousSafeArea = safeArea;
            applied = true;
            return true;
        }

        private static void FitSafeArea(RectTransform target, Vector2 minimum, Vector2 maximum)
        {
            target.anchorMin = minimum;
            target.anchorMax = maximum;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }

        private static void FitPanel(RectTransform target, float availableWidth, float maximumWidth)
        {
            target.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(0, Mathf.Min(maximumWidth, availableWidth - PageMargin * 2))
            );
        }
    }
}
