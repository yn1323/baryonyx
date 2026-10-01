using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// Carries the battlefield very slowly round a circle, as if the camera's centre circled the
    /// stage (as in Octopath Traveler's battles), so the still scene keeps a little life without
    /// the player noticing it move. It moves by fractions of a screen pixel, so it glides instead
    /// of stepping; the pixel art on the stage is drawn with the "Baryonyx/UI Pixel Art" shader so
    /// it stays sharp between pixels. The shake of a blow moves the stage inside this, so the two
    /// add up, and it goes on in real time through hit stops; the controls stay still on their own
    /// canvas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BattleStageDrift : MonoBehaviour
    {
        /// <summary>
        /// The widest circle (px): the background is laid this much further past the screen than
        /// the shake needs, so its edge never shows.
        /// </summary>
        public const float MaxRadius = 16f;

        [Tooltip("カメラの中心が描く円の半径（px、1920×1080の設計座標）。0で止める。")]
        [Range(0f, MaxRadius)]
        public float Radius = 12f;

        [Tooltip("円を1周する秒数。大きいほどゆっくり回る。")]
        [Min(1f)]
        public float Period = 40f;

        private RectTransform rect;

        /// <summary>
        /// The stage's offset at <paramref name="time"/> seconds: a point going once round a
        /// circle of <paramref name="radius"/> every <paramref name="period"/> seconds.
        /// </summary>
        public static Vector2 Offset(float time, float radius, float period)
        {
            if (radius <= 0f || period <= 0f)
                return Vector2.zero;
            float angle = Mathf.Repeat(time, period) / period * (Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void Awake() => rect = (RectTransform)transform;

        private void OnDisable()
        {
            if (rect != null)
                rect.anchoredPosition = Vector2.zero;
        }

        // Real time, like the shake: a hit stop freezes the battle, not the camera. Wrapped in
        // double first, so a long session keeps the motion smooth.
        private void LateUpdate()
        {
            float time = Period > 0f ? (float)(Time.unscaledTimeAsDouble % Period) : 0f;
            rect.anchoredPosition = Offset(time, Radius, Period);
        }
    }
}
