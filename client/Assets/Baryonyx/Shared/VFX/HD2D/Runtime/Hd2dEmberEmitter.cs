using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// One particle source such as torch embers, magic motes or a reward burst.
    /// The anchor is normalized to the parent rect; the other lengths are canvas pixels.
    /// </summary>
    [Serializable]
    public sealed class Hd2dEmberSource
    {
        public string Name = "Embers";

        [Tooltip("発生位置の中心。親の正規化座標です。")]
        public Vector2 Anchor = new Vector2(0.5f, 0.5f);

        [Tooltip("発生位置の広がり（Canvas基準のpx）。この範囲の中からランダムに出ます。")]
        public Vector2 SpawnArea = new Vector2(24f, 8f);

        [Min(0)]
        [Tooltip("同時に表示する粒子の数です。")]
        public int Count = 10;

        [Tooltip(
            "オンのときは消えた粒子がすぐ出直します。オフのときはBurstを呼んだときだけ出ます。"
        )]
        public bool Loop = true;

        public Vector2 LifetimeRange = new Vector2(1.2f, 2.4f);

        [Tooltip("出る向き（度）。90で真上、270で真下です。")]
        public float Direction = 90f;

        [Range(0f, 360f)]
        [Tooltip("向きのばらつき（度）。360で全方向に散ります。")]
        public float Spread = 30f;

        [Tooltip("初速の範囲（px/秒）。")]
        public Vector2 SpeedRange = new Vector2(30f, 70f);

        [Tooltip("上向きの加速度（px/秒²）。負の値で落ちます。")]
        public float Buoyancy = 12f;

        [Min(0f)]
        [Tooltip("左右にゆれる幅（px）。")]
        public float Sway = 8f;

        [Min(0f)]
        public float SwaySpeed = 1.6f;

        [Tooltip("大きさの範囲（px）。ドット絵に合わせて整数に丸めます。")]
        public Vector2 SizeRange = new Vector2(3f, 5f);

        [Tooltip("出たときの色です。")]
        public Color StartColor = new Color(1f, 0.82f, 0.42f, 1f);

        [Tooltip("消える直前の色です。Aを0にすると冷めながら消えます。")]
        public Color EndColor = new Color(1f, 0.32f, 0.08f, 0f);

        [Range(0f, 1f)]
        [Tooltip("ちらつきの量です。")]
        public float Twinkle = 0.35f;
    }

    /// <summary>
    /// UI-based particles emitted from points, drawn above a ScreenSpaceOverlay background.
    /// Positions are evaluated from each particle's age, so an Editor preview matches Play Mode.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Hd2dEmberEmitter : MonoBehaviour
    {
        [Header("レイヤーと素材")]
        public RectTransform ParticleLayer;

        [Tooltip("未指定の場合は、ドット絵に合わせた四角い点で描きます。")]
        public Sprite ParticleSprite;

        [Tooltip("加算合成のマテリアル。未指定の場合は通常の半透明で描きます。")]
        public Material AdditiveMaterial;

        [Header("再生")]
        public bool PlayOnEnable = true;
        public bool Animate = true;
        public bool UseUnscaledTime = true;
        public int RandomSeed = 7070;

        [Header("Editorプレビュー")]
        [Tooltip("再生しなくてもScene/Gameビューに現在の設定を表示します。")]
        public bool PreviewInEditor = true;

        [Header("全体")]
        [Range(0f, 2f)]
        [Tooltip("全粒子の濃さに掛ける倍率です。")]
        public float Intensity = 1f;

        [Header("発生源")]
        public List<Hd2dEmberSource> Sources = new List<Hd2dEmberSource>();

        private const float FadeInPortion = 0.12f;

        private readonly List<SourceState> states = new List<SourceState>();
        private System.Random random;
        private bool initialized;
        private bool rebuildRequested;

        private void OnEnable()
        {
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            ClearParticles();
        }

        private void Update()
        {
            var playing = Application.IsPlaying(gameObject);
            if (!playing && !PreviewInEditor)
            {
                if (initialized)
                    ClearParticles();
                return;
            }

            if (rebuildRequested && (!playing || PlayOnEnable || initialized))
                RebuildParticles();

            if (!initialized || !Animate || !playing)
                return;

            var deltaTime = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (deltaTime > 0f)
                Step(deltaTime);
        }

        private void OnValidate()
        {
            Intensity = Mathf.Clamp(Intensity, 0f, 2f);
            if (Sources != null)
            {
                foreach (var source in Sources)
                {
                    if (source == null)
                        continue;
                    source.Count = Mathf.Max(0, source.Count);
                    source.SpawnArea = new Vector2(
                        Mathf.Max(0f, source.SpawnArea.x),
                        Mathf.Max(0f, source.SpawnArea.y)
                    );
                    source.LifetimeRange = NormalizeRange(source.LifetimeRange, 0.05f);
                    source.SpeedRange = NormalizeRange(source.SpeedRange, 0f);
                    source.SizeRange = NormalizeRange(source.SizeRange, 1f);
                    source.Spread = Mathf.Clamp(source.Spread, 0f, 360f);
                    source.Sway = Mathf.Max(0f, source.Sway);
                    source.SwaySpeed = Mathf.Max(0f, source.SwaySpeed);
                    source.Twinkle = Mathf.Clamp01(source.Twinkle);
                }
            }
            rebuildRequested = true;
        }

        /// <summary>
        /// Recreates the particle pool using the current Inspector values. Looping sources
        /// start with spread-out ages so the stream already looks steady on the first frame.
        /// </summary>
        public void RebuildParticles()
        {
            // Prefab assets must never receive generated children. Prefab Mode has a scene.
            if (!gameObject.scene.IsValid() || !isActiveAndEnabled)
                return;

            ClearParticles();
            rebuildRequested = false;
            if (!Application.IsPlaying(gameObject) && !PreviewInEditor)
                return;

            CacheParticleLayer();
            if (ParticleLayer == null || Sources == null)
                return;

            random = new System.Random(RandomSeed);
            for (var index = 0; index < Sources.Count; index++)
            {
                var source = Sources[index];
                if (source != null)
                    states.Add(CreateSource(index, source));
            }

            initialized = true;
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                    ApplyParticle(state.Settings, particle);
            }
        }

        /// <summary>
        /// Emits up to <paramref name="count"/> particles from a source at once, reusing the
        /// oldest ones. Use it with a non-looping source for rewards or transitions.
        /// </summary>
        public void Burst(int sourceIndex, int count)
        {
            if (!initialized || sourceIndex < 0 || sourceIndex >= states.Count || count <= 0)
                return;

            var state = states[sourceIndex];
            var ordered = new List<ParticleState>(state.Particles);
            // Dead particles first, then the oldest live ones.
            ordered.Sort((a, b) => Remaining(a).CompareTo(Remaining(b)));
            for (var index = 0; index < Mathf.Min(count, ordered.Count); index++)
            {
                Respawn(state.Settings, ordered[index], 0f);
                ApplyParticle(state.Settings, ordered[index]);
            }
        }

        /// <summary>
        /// Offset of a particle from its spawn point after <paramref name="age"/> seconds.
        /// </summary>
        public static Vector2 EvaluateOffset(
            Vector2 velocity,
            float buoyancy,
            float sway,
            float swaySpeed,
            float swayPhase,
            float age
        )
        {
            var drift = velocity * age + new Vector2(0f, 0.5f * buoyancy * age * age);
            var swing =
                sway > 0f ? Mathf.Sin(swayPhase + age * swaySpeed) - Mathf.Sin(swayPhase) : 0f;
            return drift + new Vector2(swing * sway, 0f);
        }

        /// <summary>
        /// Opacity over the particle life: a quick fade-in, then an eased change from the
        /// start alpha to the end alpha. An end alpha of 0 makes the particle fade out.
        /// </summary>
        public static float EvaluateLifeAlpha(float life01, float startAlpha, float endAlpha)
        {
            life01 = Mathf.Clamp01(life01);
            var fadeIn = Mathf.Clamp01(life01 / FadeInPortion);
            return Mathf.Lerp(startAlpha, endAlpha, SmoothUnit(life01)) * fadeIn;
        }

        private void Step(float deltaTime)
        {
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                {
                    particle.Age += deltaTime;
                    if (particle.Age >= particle.Lifetime && state.Settings.Loop)
                        Respawn(state.Settings, particle, particle.Age - particle.Lifetime);
                    ApplyParticle(state.Settings, particle);
                }
            }
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

        private SourceState CreateSource(int index, Hd2dEmberSource source)
        {
            var state = new SourceState { Settings = source };
            var name = string.IsNullOrWhiteSpace(source.Name) ? "Embers" : source.Name;
            for (var particleIndex = 0; particleIndex < source.Count; particleIndex++)
            {
                var imageObject = new GameObject(
                    "Ember_"
                        + index.ToString("00")
                        + "_"
                        + name
                        + "_"
                        + particleIndex.ToString("00"),
                    typeof(RectTransform),
                    typeof(UnityEngine.UI.Image)
                );
                imageObject.hideFlags = HideFlags.DontSave;
                imageObject.layer = ParticleLayer.gameObject.layer;
                imageObject.transform.SetParent(ParticleLayer, false);

                var rect = (RectTransform)imageObject.transform;
                rect.anchorMin = rect.anchorMax = source.Anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);

                var image = imageObject.GetComponent<UnityEngine.UI.Image>();
                image.sprite = ParticleSprite;
                image.material = AdditiveMaterial;
                image.preserveAspect = true;
                image.raycastTarget = false;

                var particle = new ParticleState { Rect = rect, Image = image };
                Respawn(source, particle, 0f);
                // Looping streams start mid-flow; one-shot sources wait for a Burst.
                if (source.Loop)
                    particle.Age = RandomRange(0f, particle.Lifetime);
                else
                    particle.Age = particle.Lifetime;
                state.Particles.Add(particle);
            }
            return state;
        }

        private void Respawn(Hd2dEmberSource source, ParticleState particle, float age)
        {
            var angle =
                (source.Direction + RandomRange(-source.Spread, source.Spread) * 0.5f)
                * Mathf.Deg2Rad;
            var speed = RandomRange(source.SpeedRange.x, source.SpeedRange.y);
            particle.Origin = new Vector2(
                RandomRange(-source.SpawnArea.x, source.SpawnArea.x) * 0.5f,
                RandomRange(-source.SpawnArea.y, source.SpawnArea.y) * 0.5f
            );
            particle.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            particle.Lifetime = RandomRange(source.LifetimeRange.x, source.LifetimeRange.y);
            particle.Age = Mathf.Clamp(age, 0f, particle.Lifetime);
            particle.SwayPhase = RandomRange(0f, Mathf.PI * 2f);
            particle.TwinklePhase = RandomRange(0f, 100f);
            // Whole pixels keep the square embers crisp next to the pixel art.
            var size = Mathf.Round(RandomRange(source.SizeRange.x, source.SizeRange.y));
            particle.Rect.sizeDelta = new Vector2(size, size);
        }

        private void ApplyParticle(Hd2dEmberSource source, ParticleState particle)
        {
            if (particle.Rect == null || particle.Image == null)
                return;

            var alive = particle.Age < particle.Lifetime;
            if (particle.Image.enabled != alive)
                particle.Image.enabled = alive;
            if (!alive)
                return;

            var life01 = particle.Lifetime > 0f ? particle.Age / particle.Lifetime : 1f;
            particle.Rect.anchoredPosition =
                particle.Origin
                + EvaluateOffset(
                    particle.Velocity,
                    source.Buoyancy,
                    source.Sway,
                    source.SwaySpeed,
                    particle.SwayPhase,
                    particle.Age
                );

            var twinkle =
                source.Twinkle > 0f
                    ? Mathf.Lerp(
                        1f - source.Twinkle,
                        1f,
                        Mathf.PerlinNoise(particle.TwinklePhase + particle.Age * 6f, 0.37f)
                    )
                    : 1f;
            var color = Color.Lerp(source.StartColor, source.EndColor, life01);
            color.a = Mathf.Clamp01(
                EvaluateLifeAlpha(life01, source.StartColor.a, source.EndColor.a)
                    * twinkle
                    * Intensity
            );
            particle.Image.color = color;
        }

        private static float Remaining(ParticleState particle)
        {
            return particle.Lifetime - particle.Age;
        }

        private void ClearParticles()
        {
            initialized = false;
            foreach (var state in states)
            {
                foreach (var particle in state.Particles)
                {
                    if (particle.Rect == null)
                        continue;
                    particle.Rect.gameObject.SetActive(false);
                    if (Application.IsPlaying(gameObject))
                        Destroy(particle.Rect.gameObject);
                    else
                        DestroyImmediate(particle.Rect.gameObject);
                }
            }
            states.Clear();
        }

        private float RandomRange(float min, float max)
        {
            random ??= new System.Random(RandomSeed);
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private static float SmoothUnit(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Vector2 NormalizeRange(Vector2 value, float minimum)
        {
            var min = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
            var max = Mathf.Max(min, Mathf.Max(value.x, value.y));
            return new Vector2(min, max);
        }

        private sealed class SourceState
        {
            public Hd2dEmberSource Settings;
            public readonly List<ParticleState> Particles = new List<ParticleState>();
        }

        private sealed class ParticleState
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public Vector2 Origin;
            public Vector2 Velocity;
            public float Lifetime;
            public float Age;
            public float SwayPhase;
            public float TwinklePhase;
        }
    }
}
