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

        [Tooltip("範囲の縁をぼかす幅（Canvas基準のpx）。Xは左右、Yは上下です。縁は霧の形に沿って崩れます。")]
        public Vector2Int Softness = new Vector2Int(220, 80);

        [Tooltip("大きな模様1つ分の大きさ（Canvas基準のpx）。横長にすると流れる霧らしくなります。")]
        public Vector2 TileSize = new Vector2(760f, 200f);

        [Tooltip("霧が流れる速さ（px/秒）。符号で向きが変わります。")]
        public Vector2 ScrollSpeed = new Vector2(12f, 0f);

        [Range(0f, 1f)]
        [Tooltip("逆向きに流れる細かい切れ端の濃さ。0で大きな模様だけになり、動きが単調になります。")]
        public float DetailOpacity = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("床に沈む度合い。上げるほど下が濃く、上が切れ端に崩れます。")]
        public float Settle = 0.5f;

        [Min(0f)]
        [Tooltip("霧が渦を巻いて形を変える速さです。0で形を保ったまま流れます。")]
        public float Churn = 0.25f;

        [Range(0f, 1f)]
        [Tooltip("濃さがゆっくり増減する量です。")]
        public float BreathAmount = 0.15f;

        [Min(0f)]
        public float BreathSpeed = 0.2f;
    }

    /// <summary>
    /// UI-based layered fog over a painted background. Each layer is a soft-edged bank that the
    /// fog shader (<c>Baryonyx/HD2D/UI Fog</c>) fills with five octaves of noise read through two
    /// rounds of domain warping: drifting shapes with lit rims, dark folds and torn wisps instead
    /// of a sliding picture. No image is used.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Hd2dFog : MonoBehaviour
    {
        [Header("レイヤーと素材")]
        public RectTransform FogLayer;

        [Tooltip("霧のシェーダー（Baryonyx/HD2D/UI Fog）のマテリアル。霧の範囲ごとに複製して使います。")]
        public Material FogMaterial;

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

        // The torn wisps drift against the wind, a little slower, and rise a little.
        private const float DetailSpeedScale = -0.6f;
        private const float DetailVerticalDrift = 0.35f;

        private static readonly int TilesId = Shader.PropertyToID("_Tiles");
        private static readonly int FlowId = Shader.PropertyToID("_Flow");
        private static readonly int EvolveId = Shader.PropertyToID("_Evolve");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int EdgeId = Shader.PropertyToID("_Edge");
        private static readonly int DetailId = Shader.PropertyToID("_Detail");
        private static readonly int SettleId = Shader.PropertyToID("_Settle");

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
            // The pattern scale depends on the layer size, so refresh it after a resize.
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
                    layer.Settle = Mathf.Clamp01(layer.Settle);
                    layer.Churn = Mathf.Max(0f, layer.Churn);
                    layer.BreathAmount = Mathf.Clamp01(layer.BreathAmount);
                    layer.BreathSpeed = Mathf.Max(0f, layer.BreathSpeed);
                }
            }
            rebuildRequested = true;
        }

        /// <summary>
        /// Recreates the fog banks using the current Inspector values.
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
            if (FogLayer == null || Layers == null || FogMaterial == null)
                return;

            var random = new System.Random(RandomSeed);
            for (var index = 0; index < Layers.Count; index++)
            {
                var settings = Layers[index];
                if (settings != null)
                    states.Add(CreateLayer(index, settings, random));
            }

            initialized = true;
            ApplyAllVisuals(Application.IsPlaying(gameObject) ? CurrentTime() : 0f);
        }

        /// <summary>
        /// Shows the fog as it is <paramref name="time"/> seconds into play, without playing:
        /// for captures and previews.
        /// </summary>
        public void ShowAt(float time)
        {
            if (!initialized)
                RebuildLayers();
            ApplyAllVisuals(time);
            Hd2dAmbientClock.Set(time);
        }

        /// <summary>
        /// The pattern offset of a bank after <paramref name="time"/> seconds: the pattern moves
        /// with <paramref name="scrollSpeed"/> (px/s), measured in tiles of
        /// <paramref name="tileSize"/> px.
        /// </summary>
        public static Vector2 EvaluateFlow(Vector2 scrollSpeed, Vector2 tileSize, float time)
        {
            // Reading the noise further back makes the visible pattern move forward.
            return new Vector2(
                -scrollSpeed.x / Mathf.Max(1f, tileSize.x) * time,
                -scrollSpeed.y / Mathf.Max(1f, tileSize.y) * time
            );
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

        private LayerState CreateLayer(int index, Hd2dFogLayer settings, System.Random random)
        {
            var name = string.IsNullOrWhiteSpace(settings.Name) ? "Fog" : settings.Name;
            var bankObject = new GameObject(
                "Fog_" + index.ToString("00") + "_" + name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.RawImage)
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

            // Each bank reads its own part of the noise and moves on its own, so it needs its
            // own copy of the material. The copy is never saved.
            var material = new Material(FogMaterial)
            {
                name = FogMaterial.name + " (" + name + ")",
                hideFlags = HideFlags.DontSave,
            };
            var image = bankObject.GetComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            image.material = material;

            return new LayerState
            {
                Settings = settings,
                Bank = bank,
                Image = image,
                Material = material,
                Seed = new Vector2(RandomRange(random) * 50f, RandomRange(random) * 50f),
                BreathPhase = RandomRange(random) * 100f,
            };
        }

        private void ApplyAllVisuals(float time)
        {
            foreach (var state in states)
                ApplyVisual(state, time);
        }

        private void ApplyVisual(LayerState state, float time)
        {
            if (state.Bank == null || state.Image == null || state.Material == null)
                return;

            var settings = state.Settings;
            var size = state.Bank.rect.size;
            var tiles = new Vector2(
                Mathf.Max(0.01f, size.x / settings.TileSize.x),
                Mathf.Max(0.01f, size.y / settings.TileSize.y)
            );
            var flow = EvaluateFlow(settings.ScrollSpeed, settings.TileSize, time);
            // The wisps ride the wind with the large shapes; this is their drift against them.
            var detail = new Vector2(
                flow.x * (DetailSpeedScale - 1f),
                flow.y * (DetailSpeedScale - 1f) - Mathf.Abs(flow.x) * DetailVerticalDrift
            );
            var material = state.Material;
            material.SetVector(TilesId, tiles);
            material.SetVector(FlowId, new Vector4(flow.x, flow.y, detail.x, detail.y));
            material.SetFloat(EvolveId, time * settings.Churn);
            material.SetVector(SeedId, state.Seed);
            material.SetVector(
                EdgeId,
                new Vector4(
                    size.x > 1f ? settings.Softness.x / size.x : 0.1f,
                    size.y > 1f ? settings.Softness.y / size.y : 0.1f,
                    0.6f,
                    0f
                )
            );
            material.SetFloat(DetailId, settings.DetailOpacity);
            material.SetFloat(SettleId, settings.Settle);

            var color = settings.Color;
            color.a = Mathf.Clamp01(color.a * EvaluateBreath(state, time) * Intensity);
            state.Image.color = color;
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
            foreach (var state in states)
            {
                if (state.Bank != null)
                {
                    state.Bank.gameObject.SetActive(false);
                    if (Application.IsPlaying(gameObject))
                        Destroy(state.Bank.gameObject);
                    else
                        DestroyImmediate(state.Bank.gameObject);
                }
                if (state.Material != null)
                {
                    if (Application.IsPlaying(gameObject))
                        Destroy(state.Material);
                    else
                        DestroyImmediate(state.Material);
                }
            }
            states.Clear();
        }

        private static float RandomRange(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private sealed class LayerState
        {
            public Hd2dFogLayer Settings;
            public RectTransform Bank;
            public UnityEngine.UI.RawImage Image;
            public Material Material;
            public Vector2 Seed;
            public float BreathPhase;
        }
    }
}
