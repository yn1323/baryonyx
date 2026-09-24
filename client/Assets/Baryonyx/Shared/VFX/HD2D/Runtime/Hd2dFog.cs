using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// One rectangular fog bank. Coordinates are normalized to the parent rect and may
    /// extend outside it, so the same settings work for any screen that stretches the layer.
    /// </summary>
    [Serializable]
    public sealed class Hd2dFogLayer
    {
        public string Name = "Fog";

        [Tooltip("霧の範囲の左下。親の正規化座標で、0未満や1超えで画面外まで広げられます。")]
        public Vector2 AnchorMin = new Vector2(-0.05f, -0.05f);

        [Tooltip("霧の範囲の右上。親の正規化座標です。")]
        public Vector2 AnchorMax = new Vector2(1.05f, 0.25f);

        public Color Color = new Color(0.62f, 0.7f, 0.82f, 0.18f);

        [Tooltip("範囲の縁をぼかす幅（Canvas基準のpx）。Xは左右、Yは上下です。")]
        public Vector2Int Softness = new Vector2Int(220, 80);

        [Tooltip("ノイズ模様1枚分の大きさ（Canvas基準のpx）。横長にすると流れる霧らしくなります。")]
        public Vector2 TileSize = new Vector2(760f, 200f);

        [Tooltip("霧が流れる速さ（px/秒）。符号で向きが変わります。")]
        public Vector2 ScrollSpeed = new Vector2(12f, 0f);

        [Range(0f, 1f)]
        [Tooltip("逆向きに流れる細かい模様の濃さ。0で1枚だけになり、動きが単調になります。")]
        public float DetailOpacity = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("濃さがゆっくり増減する量です。")]
        public float BreathAmount = 0.15f;

        [Min(0f)]
        public float BreathSpeed = 0.2f;

        [Tooltip("未指定の場合は、コンポーネントのノイズ画像を使います。")]
        public Texture Texture;
    }

    /// <summary>
    /// UI-based layered fog for a ScreenSpaceOverlay background. Each layer is a soft-edged
    /// rect filled with two tileable noise sheets that drift in opposite directions.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Hd2dFog : MonoBehaviour
    {
        [Header("レイヤーと素材")]
        public RectTransform FogLayer;

        [Tooltip("継ぎ目なく並べられる白いノイズ画像。Wrap ModeをRepeatにします。")]
        public Texture NoiseTexture;

        [Header("再生")]
        public bool PlayOnEnable = true;
        public bool Animate = true;
        public bool UseUnscaledTime = true;
        public int RandomSeed = 1234;

        [Header("Editorプレビュー")]
        [Tooltip("再生しなくてもScene/Gameビューに現在の設定を表示します。")]
        public bool PreviewInEditor = true;

        [Header("全体")]
        [Range(0f, 2f)]
        [Tooltip("全レイヤーの濃さに掛ける倍率です。")]
        public float Intensity = 1f;

        [Header("霧のレイヤー")]
        public List<Hd2dFogLayer> Layers = new List<Hd2dFogLayer>();

        private const float DetailTileScale = 0.55f;
        private const float DetailSpeedScale = -0.6f;
        private const float DetailVerticalDrift = 0.35f;
        private const int NoiseBaseCells = 4;
        private const int NoiseOctaves = 4;

        private readonly List<LayerState> states = new List<LayerState>();
        private bool initialized;
        private bool rebuildRequested;

        private void OnEnable()
        {
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            ClearLayers();
        }

        private void Update()
        {
            var playing = Application.IsPlaying(gameObject);
            if (!playing && !PreviewInEditor)
            {
                if (initialized)
                    ClearLayers();
                return;
            }

            if (rebuildRequested && (!playing || PlayOnEnable || initialized))
                RebuildLayers();

            if (!initialized || !Animate || !playing)
                return;

            ApplyAllVisuals(CurrentTime());
        }

        private void OnRectTransformDimensionsChange()
        {
            // The noise tiling depends on the layer size, so refresh it after a resize.
            if (initialized)
                ApplyAllVisuals(CurrentTime());
        }

        private void OnValidate()
        {
            Intensity = Mathf.Clamp(Intensity, 0f, 2f);
            if (Layers != null)
            {
                foreach (var layer in Layers)
                {
                    if (layer == null)
                        continue;
                    layer.Softness = new Vector2Int(
                        Mathf.Max(0, layer.Softness.x),
                        Mathf.Max(0, layer.Softness.y)
                    );
                    layer.TileSize = new Vector2(
                        Mathf.Max(1f, layer.TileSize.x),
                        Mathf.Max(1f, layer.TileSize.y)
                    );
                    layer.DetailOpacity = Mathf.Clamp01(layer.DetailOpacity);
                    layer.BreathAmount = Mathf.Clamp01(layer.BreathAmount);
                    layer.BreathSpeed = Mathf.Max(0f, layer.BreathSpeed);
                }
            }
            rebuildRequested = true;
        }

        /// <summary>
        /// Recreates the fog images using the current Inspector values.
        /// </summary>
        public void RebuildLayers()
        {
            // Prefab assets must never receive generated children. Prefab Mode has a scene.
            if (!gameObject.scene.IsValid() || !isActiveAndEnabled)
                return;

            ClearLayers();
            rebuildRequested = false;
            if (!Application.IsPlaying(gameObject) && !PreviewInEditor)
                return;

            CacheFogLayer();
            if (FogLayer == null || Layers == null)
                return;

            var random = new System.Random(RandomSeed);
            for (var index = 0; index < Layers.Count; index++)
            {
                var settings = Layers[index];
                if (settings == null)
                    continue;
                var texture = settings.Texture != null ? settings.Texture : NoiseTexture;
                if (texture == null)
                    continue;
                states.Add(CreateLayer(index, settings, texture, random));
            }

            initialized = true;
            ApplyAllVisuals(Application.IsPlaying(gameObject) ? CurrentTime() : 0f);
        }

        /// <summary>
        /// Tileable fog density for the generated noise texture. The pattern repeats every
        /// 1 in both <paramref name="u"/> and <paramref name="v"/>.
        /// </summary>
        public static float EvaluateNoiseDensity(float u, float v, int seed)
        {
            var sum = 0f;
            var weight = 0f;
            var amplitude = 1f;
            for (var octave = 0; octave < NoiseOctaves; octave++)
            {
                var cells = NoiseBaseCells << octave;
                sum +=
                    PeriodicValueNoise(u * cells, v * cells, cells, seed + octave * 131)
                    * amplitude;
                weight += amplitude;
                amplitude *= 0.5f;
            }
            return sum / weight;
        }

        /// <summary>
        /// Alpha of the generated noise texture: a thin veil everywhere with denser wisps.
        /// </summary>
        public static float EvaluateNoiseAlpha(float u, float v, int seed)
        {
            var density = EvaluateNoiseDensity(u, v, seed);
            return Mathf.Clamp01(0.2f + 0.8f * SmoothUnit((density - 0.3f) / 0.45f));
        }

        private void CacheFogLayer()
        {
            if (FogLayer == null)
            {
                var candidate = transform.Find("FogLayer");
                if (candidate != null)
                    FogLayer = candidate as RectTransform;
            }

            if (FogLayer != null)
            {
                FogLayer.anchorMin = Vector2.zero;
                FogLayer.anchorMax = Vector2.one;
                FogLayer.offsetMin = Vector2.zero;
                FogLayer.offsetMax = Vector2.zero;
            }
        }

        private LayerState CreateLayer(
            int index,
            Hd2dFogLayer settings,
            Texture texture,
            System.Random random
        )
        {
            var name = string.IsNullOrWhiteSpace(settings.Name) ? "Fog" : settings.Name;
            var bankObject = new GameObject(
                "Fog_" + index.ToString("00") + "_" + name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.RectMask2D)
            );
            bankObject.hideFlags = HideFlags.DontSave;
            bankObject.layer = FogLayer.gameObject.layer;
            bankObject.transform.SetParent(FogLayer, false);

            var bank = (RectTransform)bankObject.transform;
            bank.anchorMin = settings.AnchorMin;
            bank.anchorMax = settings.AnchorMax;
            bank.offsetMin = Vector2.zero;
            bank.offsetMax = Vector2.zero;
            bank.pivot = new Vector2(0.5f, 0.5f);

            var mask = bankObject.GetComponent<UnityEngine.UI.RectMask2D>();
            mask.softness = settings.Softness;

            var state = new LayerState
            {
                Settings = settings,
                Bank = bank,
                Base = CreateSheet("Base", bank, texture),
                Detail = settings.DetailOpacity > 0f ? CreateSheet("Detail", bank, texture) : null,
                BaseOffset = new Vector2(RandomRange(random), RandomRange(random)),
                DetailOffset = new Vector2(RandomRange(random), RandomRange(random)),
                BreathPhase = RandomRange(random) * 100f,
            };
            return state;
        }

        private static UnityEngine.UI.RawImage CreateSheet(
            string name,
            RectTransform parent,
            Texture texture
        )
        {
            var sheetObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.RawImage)
            );
            sheetObject.hideFlags = HideFlags.DontSave;
            sheetObject.layer = parent.gameObject.layer;
            sheetObject.transform.SetParent(parent, false);

            var rect = (RectTransform)sheetObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = sheetObject.GetComponent<UnityEngine.UI.RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private void ApplyAllVisuals(float time)
        {
            foreach (var state in states)
                ApplyVisual(state, time);
        }

        private void ApplyVisual(LayerState state, float time)
        {
            if (state.Bank == null || state.Base == null)
                return;

            var settings = state.Settings;
            var size = state.Bank.rect.size;
            var tiles = new Vector2(size.x / settings.TileSize.x, size.y / settings.TileSize.y);
            var scroll = new Vector2(
                settings.ScrollSpeed.x / settings.TileSize.x,
                settings.ScrollSpeed.y / settings.TileSize.y
            );
            var breath = EvaluateBreath(state, time);

            // The base sheet moves with the wind; texture offsets wrap to keep float precision.
            // Moving the UVs toward negative X makes the visible pattern flow toward positive X.
            var baseOffset = Wrap(state.BaseOffset - scroll * time);
            state.Base.uvRect = new Rect(baseOffset, tiles);
            state.Base.color = WithAlpha(
                settings.Color,
                1f - settings.DetailOpacity * 0.5f,
                breath
            );

            if (state.Detail == null)
                return;

            // A finer sheet drifting against the base reads as churning fog, not a sliding image.
            var detailScroll = new Vector2(
                scroll.x * DetailSpeedScale,
                scroll.y * DetailSpeedScale + Mathf.Abs(scroll.x) * DetailVerticalDrift
            );
            var detailOffset = Wrap(state.DetailOffset - detailScroll * time);
            state.Detail.uvRect = new Rect(detailOffset, tiles / DetailTileScale);
            state.Detail.color = WithAlpha(settings.Color, settings.DetailOpacity * 0.5f, breath);
        }

        private Color WithAlpha(Color color, float share, float breath)
        {
            color.a = Mathf.Clamp01(color.a * share * breath * Intensity);
            return color;
        }

        private static float EvaluateBreath(LayerState state, float time)
        {
            var amount = state.Settings.BreathAmount;
            if (amount <= 0f || state.Settings.BreathSpeed <= 0f)
                return 1f;

            var noise = Mathf.PerlinNoise(
                state.BreathPhase + time * state.Settings.BreathSpeed,
                state.BreathPhase * 0.37f + 0.21f
            );
            return Mathf.Lerp(1f - amount, 1f + amount, noise);
        }

        private float CurrentTime()
        {
            if (!Application.IsPlaying(gameObject))
                return 0f;
            return UseUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private void ClearLayers()
        {
            initialized = false;
            // Destroying a bank also destroys its noise sheets.
            foreach (var state in states)
            {
                if (state.Bank == null)
                    continue;
                state.Bank.gameObject.SetActive(false);
                if (Application.IsPlaying(gameObject))
                    Destroy(state.Bank.gameObject);
                else
                    DestroyImmediate(state.Bank.gameObject);
            }
            states.Clear();
        }

        private static float PeriodicValueNoise(float x, float y, int period, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = SmoothUnit(x - x0);
            var fy = SmoothUnit(y - y0);
            var a = Hash(x0, y0, period, seed);
            var b = Hash(x0 + 1, y0, period, seed);
            var c = Hash(x0, y0 + 1, period, seed);
            var d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int period, int seed)
        {
            // Wrapping the lattice makes the noise tile seamlessly.
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                var h = x * 374761393 + y * 668265263 + seed * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xffff) / 65535f;
            }
        }

        private static Vector2 Wrap(Vector2 value)
        {
            return new Vector2(Mathf.Repeat(value.x, 1f), Mathf.Repeat(value.y, 1f));
        }

        private static float RandomRange(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float SmoothUnit(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private sealed class LayerState
        {
            public Hd2dFogLayer Settings;
            public RectTransform Bank;
            public UnityEngine.UI.RawImage Base;
            public UnityEngine.UI.RawImage Detail;
            public Vector2 BaseOffset;
            public Vector2 DetailOffset;
            public float BreathPhase;
        }
    }
}
