using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// One flickering light such as a torch, a brazier or a magic crystal.
    /// Anchors are normalized to the parent rect, so aligning the parent with the background
    /// artwork keeps the light on the painted light source at any aspect ratio.
    /// </summary>
    [Serializable]
    public sealed class Hd2dFlickerLightSource
    {
        public string Name = "Light";

        [Tooltip("光源の中心。親の正規化座標です。")]
        public Vector2 Anchor = new Vector2(0.5f, 0.6f);

        public Color Color = new Color(1f, 0.58f, 0.24f, 1f);

        [Tooltip("光源のすぐ周りの明るい芯（Canvas基準のpx）。")]
        public Vector2 CoreSize = new Vector2(140f, 140f);

        [Range(0f, 1f)]
        public float CoreAlpha = 0.55f;

        [Tooltip("壁や柱を照らす広い光（Canvas基準のpx）。")]
        public Vector2 HaloSize = new Vector2(440f, 440f);

        [Range(0f, 1f)]
        public float HaloAlpha = 0.22f;

        [Tooltip("床の照り返しの中心。親の正規化座標です。")]
        public Vector2 ReflectionAnchor = new Vector2(0.5f, 0.3f);

        [Tooltip("床の照り返しの大きさ（Canvas基準のpx）。横長にすると床に落ちた光に見えます。")]
        public Vector2 ReflectionSize = new Vector2(380f, 96f);

        [Range(0f, 1f)]
        [Tooltip("0で照り返しを表示しません。")]
        public float ReflectionAlpha = 0.22f;

        [Range(0f, 1f)]
        [Tooltip("明るさの揺らぎの量です。")]
        public float FlickerAmount = 0.22f;

        [Min(0f)]
        [Tooltip("揺らぎの速さ。炎は2〜3、魔法の光は0.5前後が目安です。")]
        public float FlickerSpeed = 2.4f;

        [Range(0f, 0.3f)]
        [Tooltip("明るさに合わせて光の大きさが変わる割合です。")]
        public float SizeJitter = 0.05f;
    }

    /// <summary>
    /// UI-based flickering lights with a bright core, a wide halo and a floor reflection,
    /// drawn with additive blending over a ScreenSpaceOverlay background.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Hd2dFlickerLight : MonoBehaviour
    {
        [Header("レイヤーと素材")]
        public RectTransform LightLayer;
        public Sprite GlowSprite;

        [Tooltip("加算合成のマテリアル。未指定の場合は通常の半透明で描きます。")]
        public Material AdditiveMaterial;

        [Header("再生")]
        public bool PlayOnEnable = true;
        public bool Animate = true;
        public bool UseUnscaledTime = true;
        public int RandomSeed = 4242;

        [Header("Editorプレビュー")]
        [Tooltip("再生しなくてもScene/Gameビューに現在の設定を表示します。")]
        public bool PreviewInEditor = true;

        [Header("全体")]
        [Range(0f, 2f)]
        [Tooltip("全光源の明るさに掛ける倍率です。")]
        public float Intensity = 1f;

        [Header("光源")]
        public List<Hd2dFlickerLightSource> Sources = new List<Hd2dFlickerLightSource>();

        // The reflection follows the flame slightly later and more calmly, like bounced light.
        private const float ReflectionDelay = 0.08f;
        private const float ReflectionFlickerScale = 0.7f;

        private readonly List<SourceState> states = new List<SourceState>();
        private bool initialized;
        private bool rebuildRequested;

        private void OnEnable()
        {
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            ClearLights();
        }

        private void Update()
        {
            var playing = Application.IsPlaying(gameObject);
            if (!playing && !PreviewInEditor)
            {
                if (initialized)
                    ClearLights();
                return;
            }

            if (rebuildRequested && (!playing || PlayOnEnable || initialized))
                RebuildLights();

            if (!initialized || !Animate || !playing)
                return;

            ApplyAllVisuals(UseUnscaledTime ? Time.unscaledTime : Time.time);
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
                    source.CoreSize = ClampSize(source.CoreSize);
                    source.HaloSize = ClampSize(source.HaloSize);
                    source.ReflectionSize = ClampSize(source.ReflectionSize);
                    source.CoreAlpha = Mathf.Clamp01(source.CoreAlpha);
                    source.HaloAlpha = Mathf.Clamp01(source.HaloAlpha);
                    source.ReflectionAlpha = Mathf.Clamp01(source.ReflectionAlpha);
                    source.FlickerAmount = Mathf.Clamp01(source.FlickerAmount);
                    source.FlickerSpeed = Mathf.Max(0f, source.FlickerSpeed);
                    source.SizeJitter = Mathf.Clamp(source.SizeJitter, 0f, 0.3f);
                }
            }
            rebuildRequested = true;
        }

        /// <summary>
        /// Recreates the light images using the current Inspector values.
        /// </summary>
        public void RebuildLights()
        {
            // Prefab assets must never receive generated children. Prefab Mode has a scene.
            if (!gameObject.scene.IsValid() || !isActiveAndEnabled)
                return;

            ClearLights();
            rebuildRequested = false;
            if (!Application.IsPlaying(gameObject) && !PreviewInEditor)
                return;

            CacheLightLayer();
            if (LightLayer == null || GlowSprite == null || Sources == null)
                return;

            var random = new System.Random(RandomSeed);
            for (var index = 0; index < Sources.Count; index++)
            {
                var source = Sources[index];
                if (source != null)
                    states.Add(CreateSource(index, source, random));
            }

            initialized = true;
            var time = Application.IsPlaying(gameObject)
                ? (UseUnscaledTime ? Time.unscaledTime : Time.time)
                : 0f;
            ApplyAllVisuals(time);
        }

        /// <summary>
        /// Fire-like flicker between 0 and 1: a slow sway, a faster lick and a quick crackle.
        /// </summary>
        public static float EvaluateFlicker01(float phase, float time, float speed)
        {
            var t = time * speed;
            var sway = Mathf.PerlinNoise(phase + t, phase * 0.37f + 0.11f);
            var lick = Mathf.PerlinNoise(phase * 1.7f + t * 2.9f, phase * 0.53f + 0.71f);
            var crackle = Mathf.PerlinNoise(phase * 3.1f + t * 7.3f, phase * 0.19f + 0.37f);
            return Mathf.Clamp01(sway * 0.6f + lick * 0.3f + crackle * 0.1f);
        }

        private void CacheLightLayer()
        {
            if (LightLayer == null)
            {
                var candidate = transform.Find("LightLayer");
                if (candidate != null)
                    LightLayer = candidate as RectTransform;
            }

            if (LightLayer != null)
            {
                LightLayer.anchorMin = Vector2.zero;
                LightLayer.anchorMax = Vector2.one;
                LightLayer.offsetMin = Vector2.zero;
                LightLayer.offsetMax = Vector2.zero;
            }
        }

        private SourceState CreateSource(
            int index,
            Hd2dFlickerLightSource source,
            System.Random random
        )
        {
            var name = string.IsNullOrWhiteSpace(source.Name) ? "Light" : source.Name;
            var prefix = "Light_" + index.ToString("00") + "_" + name;
            // The reflection is created first so the wall light is drawn over it.
            return new SourceState
            {
                Settings = source,
                Reflection =
                    source.ReflectionAlpha > 0f
                        ? CreateImage(prefix + "_Reflection", source.ReflectionAnchor)
                        : null,
                Halo = CreateImage(prefix + "_Halo", source.Anchor),
                Core = CreateImage(prefix + "_Core", source.Anchor),
                Phase = (float)random.NextDouble() * 100f,
            };
        }

        private UnityEngine.UI.Image CreateImage(string name, Vector2 anchor)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            imageObject.hideFlags = HideFlags.DontSave;
            imageObject.layer = LightLayer.gameObject.layer;
            imageObject.transform.SetParent(LightLayer, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = GlowSprite;
            image.material = AdditiveMaterial;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private void ApplyAllVisuals(float time)
        {
            foreach (var state in states)
                ApplyVisual(state, time);
        }

        private void ApplyVisual(SourceState state, float time)
        {
            var source = state.Settings;
            var flame = EvaluateFlicker01(state.Phase, time, source.FlickerSpeed);
            var brightness = Mathf.Lerp(
                1f - source.FlickerAmount,
                1f + source.FlickerAmount,
                flame
            );
            var scale = 1f + (flame * 2f - 1f) * source.SizeJitter;

            ApplyImage(state.Core, source.CoreSize * scale, source.CoreAlpha * brightness);
            ApplyImage(state.Halo, source.HaloSize * scale, source.HaloAlpha * brightness);

            if (state.Reflection == null)
                return;
            var bounced = EvaluateFlicker01(
                state.Phase,
                time - ReflectionDelay,
                source.FlickerSpeed
            );
            var reflectionBrightness = Mathf.Lerp(
                1f - source.FlickerAmount * ReflectionFlickerScale,
                1f + source.FlickerAmount * ReflectionFlickerScale,
                bounced
            );
            ApplyImage(
                state.Reflection,
                source.ReflectionSize,
                source.ReflectionAlpha * reflectionBrightness
            );

            void ApplyImage(UnityEngine.UI.Image image, Vector2 size, float alpha)
            {
                if (image == null)
                    return;
                image.rectTransform.sizeDelta = size;
                var color = source.Color;
                color.a = Mathf.Clamp01(source.Color.a * alpha * Intensity);
                image.color = color;
            }
        }

        private void ClearLights()
        {
            initialized = false;
            foreach (var state in states)
            {
                DestroyGenerated(state.Reflection);
                DestroyGenerated(state.Halo);
                DestroyGenerated(state.Core);
            }
            states.Clear();
        }

        private void DestroyGenerated(UnityEngine.UI.Image image)
        {
            if (image == null)
                return;

            image.gameObject.SetActive(false);
            if (Application.IsPlaying(gameObject))
                Destroy(image.gameObject);
            else
                DestroyImmediate(image.gameObject);
        }

        private static Vector2 ClampSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        private sealed class SourceState
        {
            public Hd2dFlickerLightSource Settings;
            public UnityEngine.UI.Image Core;
            public UnityEngine.UI.Image Halo;
            public UnityEngine.UI.Image Reflection;
            public float Phase;
        }
    }
}
