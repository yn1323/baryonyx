using UnityEngine;

namespace Baryonyx.Wireframe
{
    public sealed class WireframeLayout : MonoBehaviour
    {
        public RectTransform SafeArea;
        public RectTransform Panel;
        public RectTransform Viewport;
        public RectTransform PopupPanel;
        public RectTransform DebugPanel;
        public RectTransform HealthDetailsPanel;
        public UnityEngine.UI.GridLayoutGroup HpGrid;
        public UnityEngine.UI.LayoutElement HpSize;
        public UnityEngine.UI.LayoutElement[] PageSizes;
        public GameObject Navigation;
        private Vector2 lastScreen;
        private Vector2 lastCanvas;
        private Rect lastSafe;
        private bool lastNavigation;
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

        public void ApplyViewport(Vector2 screen, Rect safe, Vector2 canvas)
        {
            if (
                screen.x <= 0
                || screen.y <= 0
                || safe.width <= 0
                || safe.height <= 0
                || canvas.x <= 0
                || canvas.y <= 0
                || SafeArea == null
            )
                return;
            bool navigation = Navigation.activeSelf;
            if (
                applied
                && screen == lastScreen
                && safe == lastSafe
                && canvas == lastCanvas
                && navigation == lastNavigation
            )
                return;
            var minimum = new Vector2(
                Mathf.Clamp01(safe.xMin / screen.x),
                Mathf.Clamp01(safe.yMin / screen.y)
            );
            var maximum = new Vector2(
                Mathf.Clamp01(safe.xMax / screen.x),
                Mathf.Clamp01(safe.yMax / screen.y)
            );
            if (maximum.x <= minimum.x || maximum.y <= minimum.y)
                return;
            SafeArea.anchorMin = minimum;
            SafeArea.anchorMax = maximum;
            SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
            float width = Mathf.Min(560, canvas.x * (maximum.x - minimum.x) - 16);
            float height = canvas.y * (maximum.y - minimum.y);
            Panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0, width));
            PopupPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(0, Mathf.Min(420, width))
            );
            DebugPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(0, Mathf.Min(420, width))
            );
            if (HealthDetailsPanel != null)
                HealthDetailsPanel.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    Mathf.Max(0, Mathf.Min(420, width))
                );
            Viewport.offsetMin = new Vector2(0, navigation ? 86 : 24);
            Viewport.offsetMax = new Vector2(0, -62);
            foreach (var page in PageSizes)
                page.minHeight = Mathf.Max(0, height - (navigation ? 148 : 86));
            int columns = 4;
            HpGrid.constraintCount = columns;
            HpGrid.cellSize = new Vector2(Mathf.Max(0, (width - (columns - 1) * 8) / columns), 52);
            HpSize.minHeight = HpSize.preferredHeight = 52;
            lastScreen = screen;
            lastCanvas = canvas;
            lastSafe = safe;
            lastNavigation = navigation;
            applied = true;
        }
    }
}
