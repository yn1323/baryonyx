using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Lightweight UI particles for atmosphere around a 2D scene.
    /// The component owns its small image pool and exposes all tuning values in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Hd2dLightingVfx : MonoBehaviour
    {
        [Header("再生")]
        public bool PlayOnEnable = true;
        public bool AnimateParticles = true;
        public bool UseUnscaledTime = true;
        public int RandomSeed = 2048;

        [Header("レイヤーと素材")]
        public RectTransform ParticleLayer;
        public Sprite DustSprite;
        public Sprite SparkleSprite;

        [Header("発生範囲")]
        public Vector2 ReferenceAreaSize = new Vector2(1920f, 1080f);
        public Vector2 ParticleAreaPadding = new Vector2(80f, 100f);

        [Header("塵")]
        [Min(0)]
        public int DustCount = 34;
        public Color DustColor = new Color(0.76f, 0.84f, 0.84f, 0.2f);
        public Vector2 DustSizeRange = new Vector2(6f, 14f);
        public Vector2 DustLifetimeRange = new Vector2(5f, 10f);
        public Vector2 DustVerticalSpeedRange = new Vector2(2f, 8f);

        [Min(0f)]
        public float DustHorizontalDrift = 9f;

        [Header("きらめき")]
        [Min(0)]
        public int SparkleCount = 8;
        public Color SparkleColor = new Color(1f, 0.82f, 0.48f, 0.58f);
        public Vector2 SparkleSizeRange = new Vector2(10f, 22f);
        public Vector2 SparkleLifetimeRange = new Vector2(2f, 4.8f);
        public Vector2 SparkleVerticalSpeedRange = new Vector2(1f, 5f);

        [Min(0f)]
        public float SparkleHorizontalDrift = 5f;

        [Range(0f, 2f)]
        public float ParticleAlphaMultiplier = 1f;

        private readonly List<ParticleState> particles = new List<ParticleState>();
        private System.Random random;
        private bool initialized;

        private void Awake()
        {
            CacheParticleLayer();
        }

        private void OnEnable()
        {
            CacheParticleLayer();
            if (PlayOnEnable)
                RebuildParticles();
        }

        private void OnDisable()
        {
            initialized = false;
            foreach (var particle in particles)
            {
                if (particle.Image != null)
                    particle.Image.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!initialized || !AnimateParticles)
                return;

            var deltaTime = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            var area = GetAreaSize();
            foreach (var particle in particles)
            {
                particle.Age += deltaTime;
                if (particle.Age >= particle.Lifetime)
                    ResetParticle(particle, area, false);

                particle.Position += particle.Velocity * deltaTime;
                if (IsOutsideArea(particle.Position, area))
                    ResetParticle(particle, area, false);

                particle.Rect.anchoredPosition = particle.Position;
                ApplyParticleVisual(particle);
            }
        }

        private void OnValidate()
        {
            DustCount = Mathf.Max(0, DustCount);
            SparkleCount = Mathf.Max(0, SparkleCount);
            ReferenceAreaSize = ClampSize(ReferenceAreaSize);
            ParticleAreaPadding = ClampSize(ParticleAreaPadding);
            DustSizeRange = NormalizeRange(DustSizeRange, 0f);
            SparkleSizeRange = NormalizeRange(SparkleSizeRange, 0f);
            DustLifetimeRange = NormalizeRange(DustLifetimeRange, 0.1f);
            SparkleLifetimeRange = NormalizeRange(SparkleLifetimeRange, 0.1f);
            DustVerticalSpeedRange = NormalizeRange(DustVerticalSpeedRange, 0f);
            SparkleVerticalSpeedRange = NormalizeRange(SparkleVerticalSpeedRange, 0f);
            DustHorizontalDrift = Mathf.Max(0f, DustHorizontalDrift);
            SparkleHorizontalDrift = Mathf.Max(0f, SparkleHorizontalDrift);
            ParticleAlphaMultiplier = Mathf.Clamp(ParticleAlphaMultiplier, 0f, 2f);
            CacheParticleLayer();
        }

        /// <summary>
        /// Recreates the small image pool using the current public settings.
        /// </summary>
        public void RebuildParticles()
        {
            CacheParticleLayer();
            if (ParticleLayer == null)
                return;

            ClearParticles();
            random = new System.Random(RandomSeed);
            var area = GetAreaSize();
            for (var index = 0; index < DustCount; index++)
                AddParticle("Dust_" + index.ToString("00"), DustSprite, DustColor, false, area);
            for (var index = 0; index < SparkleCount; index++)
                AddParticle("Sparkle_" + index.ToString("00"), SparkleSprite, SparkleColor, true, area);

            initialized = true;
        }

        private void CacheParticleLayer()
        {
            if (ParticleLayer == null)
            {
                var candidate = transform.Find("ParticleLayer");
                if (candidate != null)
                    ParticleLayer = candidate as RectTransform;
            }

            if (ParticleLayer != null)
            {
                ParticleLayer.anchorMin = Vector2.zero;
                ParticleLayer.anchorMax = Vector2.one;
                ParticleLayer.offsetMin = Vector2.zero;
                ParticleLayer.offsetMax = Vector2.zero;
            }
        }

        private void AddParticle(
            string name,
            Sprite sprite,
            Color color,
            bool sparkle,
            Vector2 area
        )
        {
            if (sprite == null)
                return;

            var particleObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            particleObject.transform.SetParent(ParticleLayer, false);
            var rect = particleObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;

            var image = particleObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var particle = new ParticleState
            {
                Rect = rect,
                Image = image,
                Color = color,
                Sparkle = sparkle,
            };
            particles.Add(particle);
            ResetParticle(particle, area, true);
        }

        private void ResetParticle(ParticleState particle, Vector2 area, bool initial)
        {
            var sizeRange = particle.Sparkle ? SparkleSizeRange : DustSizeRange;
            var lifetimeRange = particle.Sparkle ? SparkleLifetimeRange : DustLifetimeRange;
            var verticalRange = particle.Sparkle
                ? SparkleVerticalSpeedRange
                : DustVerticalSpeedRange;
            var horizontalDrift = particle.Sparkle
                ? SparkleHorizontalDrift
                : DustHorizontalDrift;

            particle.Position = new Vector2(
                RandomRange(-area.x * 0.5f + ParticleAreaPadding.x, area.x * 0.5f - ParticleAreaPadding.x),
                RandomRange(-area.y * 0.5f + ParticleAreaPadding.y, area.y * 0.5f - ParticleAreaPadding.y)
            );
            particle.Velocity = new Vector2(
                RandomRange(-horizontalDrift, horizontalDrift),
                RandomRange(verticalRange.x, verticalRange.y)
            );
            particle.Size = Vector2.one * RandomRange(sizeRange.x, sizeRange.y);
            particle.Lifetime = RandomRange(lifetimeRange.x, lifetimeRange.y);
            particle.Age = initial ? RandomRange(0f, particle.Lifetime) : 0f;
            particle.Phase = RandomRange(0f, Mathf.PI * 2f);
            particle.Rect.sizeDelta = particle.Size;
            particle.Rect.anchoredPosition = particle.Position;
            particle.Image.gameObject.SetActive(true);
            ApplyParticleVisual(particle);
        }

        private void ApplyParticleVisual(ParticleState particle)
        {
            var fadeIn = Mathf.Clamp01(particle.Age / 0.6f);
            var fadeOut = Mathf.Clamp01((particle.Lifetime - particle.Age) / 0.9f);
            var lifeAlpha = Mathf.Min(fadeIn, fadeOut);
            var pulse = particle.Sparkle
                ? Mathf.Lerp(0.55f, 1f, (Mathf.Sin(particle.Age * 4f + particle.Phase) + 1f) * 0.5f)
                : 1f;
            var color = particle.Color;
            color.a = Mathf.Clamp01(particle.Color.a * lifeAlpha * pulse * ParticleAlphaMultiplier);
            particle.Image.color = color;
        }

        private Vector2 GetAreaSize()
        {
            if (ParticleLayer != null && ParticleLayer.rect.width > 1f && ParticleLayer.rect.height > 1f)
                return ParticleLayer.rect.size;
            return ReferenceAreaSize;
        }

        private bool IsOutsideArea(Vector2 position, Vector2 area)
        {
            var halfWidth = area.x * 0.5f + ParticleAreaPadding.x;
            var halfHeight = area.y * 0.5f + ParticleAreaPadding.y;
            return Mathf.Abs(position.x) > halfWidth || Mathf.Abs(position.y) > halfHeight;
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (random == null)
                random = new System.Random(RandomSeed);
            if (maximum < minimum)
            {
                var swap = minimum;
                minimum = maximum;
                maximum = swap;
            }
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private void ClearParticles()
        {
            foreach (var particle in particles)
            {
                if (particle.Rect == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(particle.Rect.gameObject);
                else
                    DestroyImmediate(particle.Rect.gameObject);
            }
            particles.Clear();
        }

        private static Vector2 ClampSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        private static Vector2 NormalizeRange(Vector2 value, float minimum)
        {
            var low = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
            var high = Mathf.Max(low, Mathf.Max(value.x, value.y));
            return new Vector2(low, high);
        }

        private sealed class ParticleState
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public Color Color;
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 Size;
            public float Lifetime;
            public float Age;
            public float Phase;
            public bool Sparkle;
        }
    }
}
