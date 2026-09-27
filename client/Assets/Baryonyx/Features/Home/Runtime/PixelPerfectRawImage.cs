using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.Home
{
    /// <summary>
    /// Keeps a pixel-art <see cref="RawImage"/> on a Screen Space - Overlay canvas pixel perfect:
    /// one texel always covers a whole number of screen pixels and the image starts on a pixel
    /// boundary. At the 1920x1080 design size a texel is <see cref="DotSize"/> pixels; on other
    /// screens it is rounded to the nearest whole number, so the size changes slightly instead of
    /// the dots turning uneven. Set the pivot to the feet (bottom centre) so they stay in place.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class PixelPerfectRawImage : MonoBehaviour
    {
        [Min(1f)]
        public float DotSize = 4f;

        private RectTransform rect;
        private RawImage image;
        private Vector2 designPosition;
        private float lastScale = -1f;
        private Vector3 lastParentPosition;

        private void Awake()
        {
            rect = (RectTransform)transform;
            image = GetComponent<RawImage>();
            designPosition = rect.anchoredPosition;
        }

        private void OnEnable() => lastScale = -1f;

        private void LateUpdate() => Apply();

        public void Apply()
        {
            var parent = rect.parent;
            var texture = image.texture;
            if (parent == null || texture == null)
                return;

            // Only an overlay canvas measures world units in screen pixels. On other canvases
            // (the showcase previews prefabs on a camera canvas) keep the design size.
            var canvas = image.canvas;
            if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                rect.sizeDelta = TexelSize(texture, image.uvRect) * DotSize;
                rect.anchoredPosition = designPosition;
                return;
            }

            // Screen pixels per local unit (on an overlay canvas, world units are screen pixels).
            float scale = Mathf.Abs(parent.lossyScale.y * rect.localScale.y);
            if (scale <= 0f)
                return;
            if (Mathf.Approximately(scale, lastScale) && parent.position == lastParentPosition)
                return;
            lastScale = scale;
            lastParentPosition = parent.position;

            float unit = DotPixels(DotSize, scale) / scale;
            rect.sizeDelta = TexelSize(texture, image.uvRect) * unit;
            rect.anchoredPosition = designPosition;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var corner = corners[0];
            rect.position += new Vector3(
                Mathf.Round(corner.x) - corner.x,
                Mathf.Round(corner.y) - corner.y,
                0f
            );
        }

        private static Vector2 TexelSize(Texture texture, Rect uv) =>
            new(
                Mathf.Round(texture.width * Mathf.Abs(uv.width)),
                Mathf.Round(texture.height * Mathf.Abs(uv.height))
            );

        /// <summary>Whole screen pixels per texel for a design dot size at the given scale.</summary>
        public static int DotPixels(float dotSize, float scale) =>
            Mathf.Max(1, Mathf.RoundToInt(dotSize * scale));
    }
}
