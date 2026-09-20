using UnityEngine;

namespace Baryonyx.Wireframe
{
    public sealed class WireframeLayout : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new(1920, 1080);
        public const float ReferenceAspect = 16f / 9f;
        public RectTransform CoreArea;
        public GameObject BattlefieldAmbient;
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
                || CoreArea == null
            )
                return;

            bool navigation = Navigation != null && Navigation.activeSelf;
            if (
                applied
                && screen == lastScreen
                && safe == lastSafe
                && canvas == lastCanvas
                && navigation == lastNavigation
            )
                return;

            // Keep a true 16:9 coordinate space in the middle of every landscape
            // viewport. The CanvasScaler matches height, so the width left over on
            // 19.5:9 and 20:9 displays remains a margin for backgrounds and effects.
            float coreHeight = Mathf.Min(canvas.y, canvas.x / ReferenceAspect);
            float coreWidth = coreHeight * ReferenceAspect;
            var core = new Vector2(coreWidth, coreHeight);
            if (CoreArea.sizeDelta != core || CoreArea.anchorMin != Vector2.one * .5f)
            {
                CoreArea.anchorMin = CoreArea.anchorMax = Vector2.one * .5f;
                CoreArea.pivot = Vector2.one * .5f;
                CoreArea.sizeDelta = core;
                CoreArea.anchoredPosition = Vector2.zero;
            }
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
            float safeWidth = canvas.x * (maximum.x - minimum.x);
            float width = Mathf.Min(560, safeWidth - 16, coreWidth - 16);
            float safeMinX = safe.xMin / screen.x * canvas.x;
            float safeMaxX = safe.xMax / screen.x * canvas.x;
            float coreBottom = (canvas.y - coreHeight) * .5f;
            float safeMinY = Mathf.Max(0, safe.yMin / screen.y * canvas.y - coreBottom);
            float safeMaxY = Mathf.Min(coreHeight, safe.yMax / screen.y * canvas.y - coreBottom);
            float panelHeight = Mathf.Max(0, safeMaxY - safeMinY - 16);
            Panel.anchorMin = new Vector2(.5f, 0);
            Panel.anchorMax = new Vector2(.5f, 1);
            Panel.pivot = new Vector2(.5f, .5f);
            Panel.sizeDelta = new Vector2(Mathf.Max(0, width), -Mathf.Max(0, coreHeight - panelHeight));
            Panel.anchoredPosition = new Vector2(
                (safeMinX + safeMaxX - canvas.x) * .5f,
                (safeMinY + safeMaxY - coreHeight) * .5f
            );
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
                page.minHeight = Mathf.Max(0, panelHeight - (navigation ? 148 : 86));
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
