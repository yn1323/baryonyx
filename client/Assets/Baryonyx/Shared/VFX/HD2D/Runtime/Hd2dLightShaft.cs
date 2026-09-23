using System.Collections.Generic;
using UnityEngine;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// UI-based soft light shafts for a ScreenSpaceOverlay background.
    /// Each shaft starts outside the top edge, widens and fades toward the floor,
    /// and can carry dust motes and a floor light pool.
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
        public Vector2 SourceAnchor = new Vector2(0.64f, 1.18f);
        public Vector2 SourceJitter = new Vector2(0.01f, 0.005f);

        [Tooltip("複数本の開始位置を左右に並べる幅（片側・正規化座標）です。")]
        [Min(0f)]
        public float SourceSpread = 0.08f;

        [Header("光芒")]
        [Min(0)]
        public int ShaftCount = 4;
        public Color ShaftColor = new Color(0.59f, 0.75f, 1f, 0.34f);
        public Vector2 LengthRange = new Vector2(1300f, 1500f);
        public Vector2 WidthRange = new Vector2(150f, 250f);
        public Vector2 RotationRange = new Vector2(-110f, -106f);

        [Header("本数ごとの太さ")]
        [Tooltip("複数本を同じ画像で描くときの細い線から太い線への倍率です。")]
        public Vector2 WidthScaleRange = new Vector2(0.55f, 1.35f);

        [Tooltip("光芒ごとの濃さの倍率です。差を付けると筋が見分けやすくなります。")]
        public Vector2 OpacityRange = new Vector2(0.5f, 1f);

        [Header("床の光だまり")]
        public Sprite FloorPoolSprite;

        [Tooltip("光だまりの中心。画面の正規化座標です。")]
        public Vector2 FloorPoolAnchor = new Vector2(0.49f, 0.25f);
        public Vector2 FloorPoolSize = new Vector2(640f, 170f);

        [Range(0f, 1f)]
        [Tooltip("0で光だまりを表示しません。色は光芒の色を使います。")]
        public float FloorPoolAlpha = 0.4f;

        [Header("光の中の埃")]
        [Tooltip("未指定の場合は、ドット絵に合わせた四角い点で描きます。")]
        public Sprite MoteSprite;

        [Min(0)]
        public int MotesPerShaft = 18;
        public Color MoteColor = new Color(0.85f, 0.92f, 1f, 0.95f);
        public Vector2 MoteSizeRange = new Vector2(3f, 5f);

        [Tooltip("光芒に沿って流れる速さ（px/秒）です。")]
        public Vector2 MoteSpeedRange = new Vector2(6f, 16f);

        [Min(0f)]
        public float MoteSway = 10f;

        [Header("ゆらぎ")]
        [Range(0f, 1f)]
        public float FlickerAmount = 0.1f;

        [Min(0f)]
        public float FlickerSpeed = 0.55f;

        [Min(0f)]
        public float MotionAmplitude = 22f;

        [Min(0f)]
        public float MotionSpeed = 0.14f;

        private const float MoteAlongStart = 0.06f;
        private const float MoteAlongSpan = 0.86f;

        private readonly List<ShaftState> shafts = new List<ShaftState>();
        private UnityEngine.UI.Image floorPool;
        private float floorPoolPhase;
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
            ApplyAllVisuals(time);
        }

        private void OnValidate()
        {
            ShaftCount = Mathf.Max(0, ShaftCount);
            SourceAnchor = new Vector2(
                Mathf.Clamp(SourceAnchor.x, -1f, 2f),
                Mathf.Clamp(SourceAnchor.y, -1f, 2f)
            );
            SourceJitter = ClampSize(SourceJitter);
            SourceSpread = Mathf.Max(0f, SourceSpread);
            LengthRange = NormalizeRange(LengthRange, 1f);
            WidthRange = NormalizeRange(WidthRange, 1f);
            WidthScaleRange = NormalizeRange(WidthScaleRange, 0.01f);
            OpacityRange = NormalizeRange(OpacityRange, 0f);
            FloorPoolAnchor = new Vector2(
                Mathf.Clamp(FloorPoolAnchor.x, -1f, 2f),
                Mathf.Clamp(FloorPoolAnchor.y, -1f, 2f)
            );
            FloorPoolSize = ClampSize(FloorPoolSize);
            FloorPoolAlpha = Mathf.Clamp01(FloorPoolAlpha);
            MotesPerShaft = Mathf.Max(0, MotesPerShaft);
            MoteSizeRange = NormalizeRange(MoteSizeRange, 0.1f);
            MoteSpeedRange = NormalizeRange(MoteSpeedRange, 0f);
            MoteSway = Mathf.Max(0f, MoteSway);
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
            AddFloorPool();
            var slots = CreateShuffledSlots(ShaftCount);
            for (var index = 0; index < ShaftCount; index++)
                AddShaft("Shaft_" + index.ToString("00"), index, slots[index]);

            initialized = true;
            var time = Application.IsPlaying(gameObject)
                ? (UseUnscaledTime ? Time.unscaledTime : Time.time)
                : 0f;
            ApplyAllVisuals(time);
        }

        /// <summary>
        /// Relative brightness along a shaft: 0 is the source and 1 is the far end.
        /// The shaft texture and the dust motes share this profile.
        /// </summary>
        public static float EvaluateBeamIntensity(float along)
        {
            along = Mathf.Clamp01(along);
            var fadeIn = Mathf.Clamp01(along * 5f + 0.2f);
            return Mathf.Clamp01(Mathf.Pow(1f - along, 1.3f) * fadeIn / 0.8f);
        }

        /// <summary>
        /// Half width of the visible beam relative to the half height of the texture.
        /// The beam widens from the source toward the far end.
        /// </summary>
        public static float EvaluateBeamHalfWidth(float along)
        {
            return Mathf.Lerp(0.42f, 0.9f, Mathf.Clamp01(along));
        }

        /// <summary>
        /// Alpha of the shaft texture. <paramref name="across"/> is -1 to 1 from the center line.
        /// </summary>
        public static float EvaluateBeamAlpha(float along, float across)
        {
            var distance = Mathf.Abs(across);
            var ratio = distance / EvaluateBeamHalfWidth(along);
            var core = Mathf.Exp(-ratio * ratio * 2.2f);
            var edge = 1f - SmoothUnit((distance - 0.9f) / 0.1f);
            return Mathf.Clamp01(core * edge * EvaluateBeamIntensity(along));
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

        private void AddFloorPool()
        {
            if (FloorPoolSprite == null || FloorPoolAlpha <= 0f)
                return;

            floorPool = CreateImage("FloorPool", ShaftLayer, FloorPoolSprite);
            var rect = floorPool.rectTransform;
            rect.anchorMin = rect.anchorMax = FloorPoolAnchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = FloorPoolSize;
            floorPoolPhase = RandomRange(0f, 100f);
        }

        private int[] CreateShuffledSlots(int count)
        {
            // Mixing thin and thick shafts across the slots keeps neighbours distinct.
            var slots = new int[count];
            for (var index = 0; index < count; index++)
                slots[index] = index;
            for (var index = count - 1; index > 0; index--)
            {
                var swap = random.Next(index + 1);
                (slots[index], slots[swap]) = (slots[swap], slots[index]);
            }
            return slots;
        }

        private void AddShaft(string name, int index, int slot)
        {
            var image = CreateImage(name, ShaftLayer, ShaftSprite);
            var rect = image.rectTransform;
            var slotPosition = ShaftCount <= 1 ? 0.5f : slot / (float)(ShaftCount - 1);
            var source = new Vector2(
                SourceAnchor.x
                    + Mathf.Lerp(-SourceSpread, SourceSpread, slotPosition)
                    + RandomRange(-SourceJitter.x, SourceJitter.x),
                SourceAnchor.y + RandomRange(-SourceJitter.y, SourceJitter.y)
            );
            rect.anchorMin = rect.anchorMax = source;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

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
                Opacity = RandomRange(OpacityRange.x, OpacityRange.y),
            };
            AddMotes(shaft, index);
            shafts.Add(shaft);
        }

        private void AddMotes(ShaftState shaft, int shaftIndex)
        {
            if (MotesPerShaft <= 0)
                return;

            // A separate sequence keeps the shaft layout stable when only the dust count changes.
            var moteRandom = new System.Random(RandomSeed + 7919 * (shaftIndex + 1));
            for (var index = 0; index < MotesPerShaft; index++)
            {
                var image = CreateImage("Mote_" + index.ToString("00"), shaft.Rect, MoteSprite);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                // Whole pixels keep the square motes crisp next to the pixel art.
                var size = Mathf.Round(RandomRange(moteRandom, MoteSizeRange.x, MoteSizeRange.y));
                rect.sizeDelta = new Vector2(size, size);
                shaft.Motes.Add(
                    new MoteState
                    {
                        Rect = rect,
                        Image = image,
                        Along = (float)moteRandom.NextDouble(),
                        Across = RandomRange(moteRandom, -1f, 1f),
                        Speed = RandomRange(moteRandom, MoteSpeedRange.x, MoteSpeedRange.y),
                        SwayPhase = RandomRange(moteRandom, 0f, Mathf.PI * 2f),
                        TwinklePhase = RandomRange(moteRandom, 0f, 100f),
                    }
                );
            }
        }

        private UnityEngine.UI.Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            imageObject.hideFlags = HideFlags.DontSave;
            imageObject.layer = ShaftLayer.gameObject.layer;
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        private void ApplyAllVisuals(float time)
        {
            ApplyFloorPool(time);
            foreach (var shaft in shafts)
                ApplyVisual(shaft, time);
        }

        private void ApplyFloorPool(float time)
        {
            if (floorPool == null)
                return;

            var color = ShaftColor;
            color.a = Mathf.Clamp01(FloorPoolAlpha * EvaluateFlicker(floorPoolPhase, time, 0.5f));
            floorPool.color = color;
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
            var flicker = EvaluateFlicker(shaft.FlickerPhase, time, 1f);

            shaft.Rect.sizeDelta = new Vector2(shaft.Length, shaft.Width);
            shaft.Rect.anchoredPosition = shaft.BaseOffset + motion;
            shaft.Rect.localEulerAngles = new Vector3(0f, 0f, shaft.Rotation);

            var color = ShaftColor;
            color.a = Mathf.Clamp01(ShaftColor.a * shaft.Opacity * flicker);
            shaft.Image.color = color;
            shaft.Image.raycastTarget = false;

            foreach (var mote in shaft.Motes)
                ApplyMote(shaft, mote, time, flicker);
        }

        private void ApplyMote(ShaftState shaft, MoteState mote, float time, float flicker)
        {
            if (mote.Rect == null || mote.Image == null)
                return;

            // Motes drift from the source toward the floor and wrap inside the lit part.
            var travel = shaft.Length > 0f ? time * mote.Speed / shaft.Length : 0f;
            var along =
                MoteAlongStart + Mathf.Repeat(mote.Along * MoteAlongSpan + travel, MoteAlongSpan);
            var halfWidth = EvaluateBeamHalfWidth(along) * shaft.Width * 0.5f * 0.7f;
            var sway = Mathf.Sin(mote.SwayPhase + time * 0.6f) * MoteSway;
            mote.Rect.anchoredPosition = new Vector2(
                along * shaft.Length,
                mote.Across * halfWidth + sway
            );
            // Cancel the shaft rotation so the motes stay aligned with the screen pixels.
            mote.Rect.localEulerAngles = new Vector3(0f, 0f, -shaft.Rotation);

            var twinkle = Mathf.Lerp(
                0.45f,
                1f,
                Mathf.PerlinNoise(mote.TwinklePhase + time * 0.8f, mote.TwinklePhase * 0.53f)
            );
            var fadeAtEnds = SmoothUnit((along - MoteAlongStart) / 0.08f);
            var color = MoteColor;
            // The square root keeps motes visible in the dimmer lower part of the shaft.
            var brightness = Mathf.Sqrt(EvaluateBeamIntensity(along));
            color.a = Mathf.Clamp01(MoteColor.a * brightness * twinkle * fadeAtEnds * flicker);
            mote.Image.color = color;
        }

        private float EvaluateFlicker(float phase, float time, float amountScale)
        {
            var amount = FlickerAmount * amountScale;
            if (amount <= 0f || FlickerSpeed <= 0f)
                return 1f;

            var noise = Mathf.PerlinNoise(phase + time * FlickerSpeed, phase * 0.37f + 0.13f);
            return Mathf.Lerp(1f - amount, 1f + amount, noise);
        }

        private void ClearShafts()
        {
            initialized = false;
            // Destroying a shaft also destroys its child motes.
            foreach (var shaft in shafts)
                DestroyGenerated(shaft.Image);
            shafts.Clear();
            DestroyGenerated(floorPool);
            floorPool = null;
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

        private float RandomRange(float min, float max)
        {
            return RandomRange(random, min, max);
        }

        private static float RandomRange(System.Random source, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)source.NextDouble());
        }

        private static float SmoothUnit(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
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
            public readonly List<MoteState> Motes = new List<MoteState>();
        }

        private sealed class MoteState
        {
            public RectTransform Rect;
            public UnityEngine.UI.Image Image;
            public float Along;
            public float Across;
            public float Speed;
            public float SwayPhase;
            public float TwinklePhase;
        }
    }
}
