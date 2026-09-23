using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// UI-based soft light shafts for a ScreenSpaceOverlay background.
    /// The shafts travel from outside the upper-right edge toward outside the lower-left.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Hd2dLightShaft : MonoBehaviour
    {
        [Header("レイヤーと素材")]
        public RectTransform ShaftLayer;
        public Sprite ShaftSprite;

        [Header("再生")]
        public bool PlayOnEnable = true;
        public bool Animate = true;
        public bool UseUnscaledTime = true;
        public int RandomSeed = 518;

        [Header("Editorプレビュー")]
        [Tooltip("再生しなくてもScene/Gameビューに現在の設定を表示します。")]
        public bool PreviewInEditor = true;

        [Header("発生位置")]
        [Tooltip("画面外を含む正規化座標。1を超えると画面の外側から開始します。")]
        public Vector2 SourceAnchor = new Vector2(0.99f, 1.04f);
        public Vector2 SourceJitter = new Vector2(0.015f, 0.015f);

        [Header("光芒")]
        [Min(0)]
        public int ShaftCount = 2;
        public Color ShaftColor = new Color(1f, 0.92f, 0.74f, 0.035f);
        public Vector2 LengthRange = new Vector2(2050f, 2450f);
        public Vector2 WidthRange = new Vector2(270f, 360f);
        public Vector2 RotationRange = new Vector2(-142f, -136f);

        [Header("本数ごとの太さ")]
        [Tooltip("複数本を同じ画像で描くときの細い線から太い線への倍率です。")]
        public Vector2 WidthScaleRange = new Vector2(0.72f, 1.2f);

        [Header("ゆらぎ")]
        [Range(0f, 1f)]
        public float FlickerAmount = 0.1f;

        [Min(0f)]
        public float FlickerSpeed = 0.55f;

        [Min(0f)]
        public float MotionAmplitude = 22f;

        [Min(0f)]
        public float MotionSpeed = 0.14f;

        private readonly List<ShaftState> shafts = new List<ShaftState>();
        private System.Random random;
        private bool initialized;
        private bool rebuildRequested;

        private void OnEnable()
        {
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            ClearShafts();
        }

        private void Update()
        {
            var playing = Application.IsPlaying(gameObject);
            if (!playing && !PreviewInEditor)
            {
                if (initialized)
                    ClearShafts();
                return;
            }

            if (rebuildRequested && (!playing || PlayOnEnable || initialized))
                RebuildShafts();

            if (!initialized || !Animate || !playing)
                return;

            var time = UseUnscaledTime ? Time.unscaledTime : Time.time;
            foreach (var shaft in shafts)
                ApplyVisual(shaft, time);
        }

        private void OnValidate()
        {
            ShaftCount = Mathf.Max(0, ShaftCount);
            SourceAnchor = new Vector2(
                Mathf.Clamp(SourceAnchor.x, -1f, 2f),
                Mathf.Clamp(SourceAnchor.y, -1f, 2f)
            );
            SourceJitter = ClampSize(SourceJitter);
            LengthRange = NormalizeRange(LengthRange, 1f);
            WidthRange = NormalizeRange(WidthRange, 1f);
            WidthScaleRange = NormalizeRange(WidthScaleRange, 0.01f);
            FlickerAmount = Mathf.Clamp01(FlickerAmount);
            FlickerSpeed = Mathf.Max(0f, FlickerSpeed);
            MotionAmplitude = Mathf.Max(0f, MotionAmplitude);
            MotionSpeed = Mathf.Max(0f, MotionSpeed);
            rebuildRequested = true;
        }

        /// <summary>
        /// Recreates the small image pool using the current Inspector values.
        /// </summary>
        public void RebuildShafts()
        {
            // Prefab assets must never receive generated children. Prefab Mode has a scene.
            if (!gameObject.scene.IsValid() || !isActiveAndEnabled)
                return;

            ClearShafts();
            rebuildRequested = false;
            if (!Application.IsPlaying(gameObject) && !PreviewInEditor)
                return;

            CacheShaftLayer();
            if (ShaftLayer == null || ShaftSprite == null)
                return;

            random = new System.Random(RandomSeed);
            for (var index = 0; index < ShaftCount; index++)
                AddShaft("Shaft_" + index.ToString("00"), index);

            initialized = true;
            var time = Application.IsPlaying(gameObject)
                ? (UseUnscaledTime ? Time.unscaledTime : Time.time)
                : 0f;
            foreach (var shaft in shafts)
                ApplyVisual(shaft, time);
        }

        private void CacheShaftLayer()
        {
            if (ShaftLayer == null)
            {
                var candidate = transform.Find("ShaftLayer");
                if (candidate != null)
                    ShaftLayer = candidate as RectTransform;
            }

            if (ShaftLayer != null)
            {
                ShaftLayer.anchorMin = Vector2.zero;
                ShaftLayer.anchorMax = Vector2.one;
                ShaftLayer.offsetMin = Vector2.zero;
                ShaftLayer.offsetMax = Vector2.zero;
            }
        }

        private void AddShaft(string name, int index)
        {
            var shaftObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            shaftObject.hideFlags = HideFlags.DontSave;
            shaftObject.layer = ShaftLayer.gameObject.layer;
            shaftObject.transform.SetParent(ShaftLayer, false);

            var rect = shaftObject.GetComponent<RectTransform>();
            var source = new Vector2(
                SourceAnchor.x + RandomRange(-SourceJitter.x, SourceJitter.x),
                SourceAnchor.y + RandomRange(-SourceJitter.y, SourceJitter.y)
            );
            rect.anchorMin = rect.anchorMax = source;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var image = shaftObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = ShaftSprite;
            image.preserveAspect = false;
            image.raycastTarget = false;

            var widthScale =
                ShaftCount <= 1
                    ? Mathf.Lerp(WidthScaleRange.x, WidthScaleRange.y, 0.5f)
                    : Mathf.Lerp(
                        WidthScaleRange.x,
                        WidthScaleRange.y,
                        index / (float)(ShaftCount - 1)
                    );
            var shaft = new ShaftState
            {
                Rect = rect,
                Image = image,
                Length = RandomRange(LengthRange.x, LengthRange.y),
                Width = RandomRange(WidthRange.x, WidthRange.y) * widthScale,
                Rotation = RandomRange(RotationRange.x, RotationRange.y),
                BaseOffset = new Vector2(
                    RandomRange(-WidthRange.y * 0.22f, WidthRange.y * 0.22f),
                    RandomRange(-WidthRange.y * 0.12f, WidthRange.y * 0.12f)
                ),
                Phase = RandomRange(0f, Mathf.PI * 2f),
                FlickerPhase = RandomRange(0f, 100f),
                Opacity = RandomRange(0.78f, 1.1f),
            };
            shafts.Add(shaft);
        }

        private void ApplyVisual(ShaftState shaft, float time)
        {
            if (shaft.Rect == null || shaft.Image == null)
                return;

            var motion =
                MotionAmplitude > 0f && MotionSpeed > 0f
                    ? new Vector2(
                        Mathf.Sin(shaft.Phase + time * MotionSpeed) * MotionAmplitude,
                        Mathf.Cos(shaft.Phase * 0.71f + time * MotionSpeed * 0.83f)
                            * MotionAmplitude
                            * 0.45f
                    )
                    : Vector2.zero;
            var flicker = 1f;
            if (FlickerAmount > 0f && FlickerSpeed > 0f)
            {
                var noise = Mathf.PerlinNoise(
                    shaft.FlickerPhase + time * FlickerSpeed,
                    shaft.FlickerPhase * 0.37f + 0.13f
                );
                flicker = Mathf.Lerp(1f - FlickerAmount, 1f + FlickerAmount, noise);
            }

            shaft.Rect.sizeDelta = new Vector2(shaft.Length, shaft.Width);
            shaft.Rect.anchoredPosition = shaft.BaseOffset + motion;
            shaft.Rect.localEulerAngles = new Vector3(0f, 0f, shaft.Rotation);

            var color = ShaftColor;
            color.a = Mathf.Clamp01(ShaftColor.a * shaft.Opacity * flicker);
            shaft.Image.color = color;
            shaft.Image.raycastTarget = false;
        }

        private void ClearShafts()
        {
            initialized = false;
            foreach (var shaft in shafts)
            {
                if (shaft.Image != null)
                {
                    shaft.Image.gameObject.SetActive(false);
                    if (Application.IsPlaying(gameObject))
                        Destroy(shaft.Image.gameObject);
                    else
                        DestroyImmediate(shaft.Image.gameObject);
                }
            }
            shafts.Clear();
        }

        private float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private static Vector2 ClampSize(Vector2 value)
        {
            return new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
        }

        private static Vector2 NormalizeRange(Vector2 value, float minimum)
        {
            var min = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
            var max = Mathf.Max(min, Mathf.Max(value.x, value.y));
            return new Vector2(min, max);
        }

        private sealed class ShaftState
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public float Length;
            public float Width;
            public float Rotation;
            public Vector2 BaseOffset;
            public float Phase;
            public float FlickerPhase;
            public float Opacity;
        }
    }
}
