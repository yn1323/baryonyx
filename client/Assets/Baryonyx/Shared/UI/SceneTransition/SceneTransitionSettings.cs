using System;
using UnityEngine;

namespace Baryonyx.UI
{
    public enum SceneTransitionType
    {
        Fade,
        Wipe,
        Shutter,
    }

    public enum SceneTransitionWipeDirection
    {
        LeftToRight,
        RightToLeft,
    }

    public enum SceneTransitionShutterAxis
    {
        Vertical,
        Horizontal,
    }

    [Serializable]
    public sealed class SceneTransitionSettings
    {
        public static readonly Color DefaultColor = new Color(0.16f, 0.16f, 0.18f, 1f);
        public const int DefaultSteppedFrameRate = 25;

        [SerializeField]
        private SceneTransitionType type = SceneTransitionType.Fade;

        [SerializeField]
        private SceneTransitionWipeDirection wipeDirection =
            SceneTransitionWipeDirection.LeftToRight;

        [SerializeField]
        private SceneTransitionShutterAxis shutterAxis = SceneTransitionShutterAxis.Vertical;

        [Min(0.01f)]
        [SerializeField]
        private float coverDuration = 0.25f;

        [Min(0.01f)]
        [SerializeField]
        private float revealDuration = 0.25f;

        // WipeとShutterを1秒あたり何回動かすか。0で毎フレーム滑らかに動かす。
        [Min(0)]
        [SerializeField]
        private int steppedFrameRate = DefaultSteppedFrameRate;

        [ColorUsage(false)]
        [SerializeField]
        private Color color = DefaultColor;

        public SceneTransitionType Type
        {
            get => type;
            set => type = value;
        }

        public SceneTransitionWipeDirection WipeDirection
        {
            get => wipeDirection;
            set => wipeDirection = value;
        }

        public SceneTransitionShutterAxis ShutterAxis
        {
            get => shutterAxis;
            set => shutterAxis = value;
        }

        public float CoverDuration
        {
            get => coverDuration;
            set => coverDuration = value;
        }

        public float RevealDuration
        {
            get => revealDuration;
            set => revealDuration = value;
        }

        public int SteppedFrameRate
        {
            get => steppedFrameRate;
            set => steppedFrameRate = value;
        }

        public Color Color
        {
            get => color;
            set => color = value;
        }

        public SceneTransitionSettings Clone()
        {
            return new SceneTransitionSettings
            {
                type = type,
                wipeDirection = wipeDirection,
                shutterAxis = shutterAxis,
                coverDuration = coverDuration,
                revealDuration = revealDuration,
                steppedFrameRate = steppedFrameRate,
                color = color,
            };
        }

        public void Normalize()
        {
            coverDuration = NormalizeDuration(coverDuration);
            revealDuration = NormalizeDuration(revealDuration);
            steppedFrameRate = Mathf.Max(0, steppedFrameRate);
        }

        // 経過時間から0〜1の進行度を返す。WipeとShutterはドット絵に合わせてコマ送りにする。
        public float EvaluateProgress(float elapsed, float duration)
        {
            if (duration <= 0f)
                return 1f;
            if (type != SceneTransitionType.Fade && steppedFrameRate > 0)
                // 切り上げて押下直後から1コマ目を表示し、操作への反応を遅らせない。
                elapsed = Mathf.Ceil(elapsed * steppedFrameRate) / steppedFrameRate;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
        }

        private static float NormalizeDuration(float duration) =>
            float.IsNaN(duration) || float.IsInfinity(duration)
                ? 0.25f
                : Mathf.Max(0.01f, duration);
    }
}
