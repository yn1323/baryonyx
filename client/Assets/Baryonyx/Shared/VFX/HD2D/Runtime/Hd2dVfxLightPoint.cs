using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// A reusable warm light made from a soft glow and a thin light ray.
    /// This is UI based so it can sit above a ScreenSpaceOverlay background.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hd2dVfxLightPoint : MonoBehaviour
    {
        [Header("画像")]
        public UnityEngine.UI.Image Glow;
        public UnityEngine.UI.Image Ray;

        [Header("色と強さ")]
        public Color LightColor = new Color(1f, 0.52f, 0.18f, 1f);

        [Range(0f, 1f)]
        public float GlowAlpha = 0.24f;

        [Range(0f, 1f)]
        public float RayAlpha = 0.1f;

        [Header("サイズ")]
        public Vector2 GlowSize = new Vector2(260f, 260f);
        public Vector2 RaySize = new Vector2(420f, 170f);
        public float RayRotation;

        [Header("ゆらぎ")]
        public bool Animate = true;

        [Range(0f, 1f)]
        public float FlickerAmount = 0.12f;

        [Min(0f)]
        public float FlickerSpeed = 1.2f;

        public float FlickerSeed;

        private RectTransform glowRect;
        private RectTransform rayRect;

        private void Awake()
        {
            CacheImages();
            ApplyVisual(0f);
        }

        private void OnEnable()
        {
            CacheImages();
            ApplyVisual(Application.isPlaying ? Time.unscaledTime : 0f);
        }

        private void Update()
        {
            if (Animate)
                ApplyVisual(Time.unscaledTime);
        }

        private void OnValidate()
        {
            GlowAlpha = Mathf.Clamp01(GlowAlpha);
            RayAlpha = Mathf.Clamp01(RayAlpha);
            FlickerAmount = Mathf.Clamp01(FlickerAmount);
            FlickerSpeed = Mathf.Max(0f, FlickerSpeed);
            GlowSize = ClampSize(GlowSize);
            RaySize = ClampSize(RaySize);
            CacheImages();
            ApplyVisual(Application.isPlaying ? Time.unscaledTime : 0f);
        }

        public void ApplyVisual(float time)
        {
            CacheImages();

            var flicker = 1f;
            if (Animate && FlickerAmount > 0f && FlickerSpeed > 0f)
            {
                var noise = Mathf.PerlinNoise(
                    FlickerSeed + time * FlickerSpeed,
                    FlickerSeed * 0.37f + 0.17f
                );
                flicker = Mathf.Lerp(1f - FlickerAmount, 1f + FlickerAmount, noise);
            }

            ApplyImage(Glow, glowRect, GlowSize, GlowAlpha * flicker, 0f);
            ApplyImage(Ray, rayRect, RaySize, RayAlpha * flicker, RayRotation);
        }

        private void CacheImages()
        {
            if (Glow == null || Ray == null)
            {
                var images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var image in images)
                {
                    if (image.name == "Glow" && Glow == null)
                        Glow = image;
                    else if (image.name == "Ray" && Ray == null)
                        Ray = image;
                }
            }

            glowRect = Glow != null ? Glow.rectTransform : null;
            rayRect = Ray != null ? Ray.rectTransform : null;
            if (Glow != null)
                Glow.raycastTarget = false;
            if (Ray != null)
                Ray.raycastTarget = false;
        }

        private void ApplyImage(
            UnityEngine.UI.Image image,
            RectTransform rect,
            Vector2 size,
            float alpha,
            float rotation
        )
        {
            if (image == null || rect == null)
                return;

            rect.sizeDelta = size;
            rect.localEulerAngles = new Vector3(0f, 0f, rotation);
            var color = LightColor;
            color.a = Mathf.Clamp01(LightColor.a * alpha);
            image.color = color;
            image.raycastTarget = false;
        }

        private static Vector2 ClampSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }
    }
}
