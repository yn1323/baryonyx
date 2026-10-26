using UnityEngine;
using UnityEngine.UI;

namespace Baryonyx.UI
{
    /// <summary>
    /// Makes the wooden pawn of the exploration's map and the highway's world map hop from square
    /// to square (doc/features/stage-progression.md): it takes off at once, stretching a little,
    /// leans the way it goes in the air and squashes when it lands, with the shadow shrinking and
    /// fading while it is high. Only the picture and the shadow move; the owner moves the pawn
    /// itself along the road, straight and at a steady speed over <see cref="AirSeconds"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapPawnHop : MonoBehaviour
    {
        // 1回の跳躍：空中にいる時間と、着地でつぶれて戻る時間（秒）、跳ぶ高さ（設計座標）。
        public const float AirSeconds = 0.4f;
        public const float LandSeconds = 0.2f;
        public const float Height = 70f;

        // 形の変わり方：跳び上がりで縦に伸びる割合、着地で縦につぶれる割合、空中で傾く角度（度）。
        private const float Stretch = 0.15f;
        private const float Squash = 0.25f;
        private const float Lean = 12f;

        // 跳び上がりで伸びきるまでの、空中の時間に対する割合。
        private const float StretchRise = 0.12f;

        // 影：この高さで最も小さく薄くなり、そのときの縮む割合と薄れる割合。
        private const float ShadowLift = 120f;
        private const float ShadowShrink = 0.45f;
        private const float ShadowFade = 0.5f;

        [Tooltip("駒の絵。足元を軸にする。")]
        public RectTransform Picture;

        [Tooltip("足元の影。")]
        public RectTransform Shadow;

        [Tooltip("ゲームの時間の速さによらず進める（時間を止めても動く画面）。")]
        public bool UnscaledTime;

        private float sinceTakeoff = float.PositiveInfinity;
        private float sinceLanding = float.PositiveInfinity;
        private float direction = 1f;
        private bool captured;
        private Vector2 picturePosition;
        private Vector3 shadowScale;
        private Graphic shadowGraphic;
        private float shadowAlpha;

        public bool Hopping => sinceTakeoff < AirSeconds || sinceLanding < LandSeconds;

        /// <summary>The shape of the pawn at a moment of a hop.</summary>
        public readonly struct Pose
        {
            public readonly float Height;
            public readonly Vector2 Scale;

            // 度。正で進む向きへ傾く。
            public readonly float Lean;

            // 影の浮き具合（0で地面、1で最も高い）。
            public readonly float Lift;

            public Pose(float height, Vector2 scale, float lean, float lift)
            {
                Height = height;
                Scale = scale;
                Lean = lean;
                Lift = lift;
            }
        }

        /// <summary>
        /// Takes off now. <paramref name="towards"/> is the sign of the horizontal step (positive to
        /// the right), so the pawn leans the way it goes. A landing still squashing goes on.
        /// </summary>
        public void Play(float towards = 1f)
        {
            Capture();
            direction =
                towards < 0f ? -1f
                : towards > 0f ? 1f
                : 0f;
            sinceTakeoff = 0f;
            Apply();
        }

        private void Update()
        {
            if (!Hopping)
                return;
            float dt = UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            bool airborne = sinceTakeoff < AirSeconds;
            sinceTakeoff += dt;
            sinceLanding += dt;
            if (airborne && sinceTakeoff >= AirSeconds)
                sinceLanding = sinceTakeoff - AirSeconds;
            Apply();
        }

        /// <summary>
        /// The pose <paramref name="sinceTakeoff"/> seconds after taking off and
        /// <paramref name="sinceLanding"/> seconds after the last landing (infinite for none).
        /// </summary>
        public static Pose At(float sinceTakeoff, float sinceLanding)
        {
            float height = 0f;
            float squash = 0f;
            float lean = 0f;
            if (sinceTakeoff >= 0f && sinceTakeoff < AirSeconds)
            {
                float x = sinceTakeoff / AirSeconds;
                height = 4f * x * (1f - x) * Height;
                squash = -Stretch * (1f - x) * Mathf.Min(1f, x / StretchRise);
                lean = Lean * Mathf.Sin(Mathf.PI * x);
            }
            if (sinceLanding >= 0f && sinceLanding < LandSeconds)
            {
                // つぶれてから戻る：着地の直後に最もつぶれ、時間とともに弱まる。
                float s = sinceLanding / LandSeconds;
                squash += Squash * Mathf.Sin(Mathf.PI * s) * (1f - s);
            }
            // 体積を保つ：縦につぶれた分だけ横に広がる。
            float sy = 1f - squash;
            var scale = new Vector2(1f / Mathf.Max(0.5f, sy), sy);
            return new Pose(height, scale, lean, Mathf.Clamp01(height / ShadowLift));
        }

        private void Capture()
        {
            if (captured)
                return;
            captured = true;
            if (Picture != null)
                picturePosition = Picture.anchoredPosition;
            if (Shadow != null)
            {
                shadowScale = Shadow.localScale;
                shadowGraphic = Shadow.GetComponent<Graphic>();
                shadowAlpha = shadowGraphic != null ? shadowGraphic.color.a : 1f;
            }
        }

        private void Apply()
        {
            var pose = At(sinceTakeoff, sinceLanding);
            if (Picture != null)
            {
                Picture.anchoredPosition =
                    picturePosition + new Vector2(0f, Mathf.Round(pose.Height));
                Picture.localScale = new Vector3(pose.Scale.x, pose.Scale.y, 1f);
                Picture.localRotation = Quaternion.Euler(0f, 0f, -pose.Lean * direction);
            }
            if (Shadow != null)
            {
                Shadow.localScale = shadowScale * (1f - ShadowShrink * pose.Lift);
                if (shadowGraphic != null)
                {
                    var color = shadowGraphic.color;
                    color.a = shadowAlpha * (1f - ShadowFade * pose.Lift);
                    shadowGraphic.color = color;
                }
            }
        }
    }
}
