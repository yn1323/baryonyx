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
        [SerializeField]
        private SceneTransitionType type = SceneTransitionType.Fade;

        [SerializeField]
        private SceneTransitionWipeDirection wipeDirection = SceneTransitionWipeDirection.LeftToRight;

        [SerializeField]
        private SceneTransitionShutterAxis shutterAxis = SceneTransitionShutterAxis.Vertical;

        [Min(0.01f)]
        [SerializeField]
        private float coverDuration = 0.25f;

        [Min(0.01f)]
        [SerializeField]
        private float revealDuration = 0.25f;

        [ColorUsage(false)]
        [SerializeField]
        private Color color = Color.black;

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
                color = color,
            };
        }

        public void Normalize()
        {
            coverDuration = NormalizeDuration(coverDuration);
            revealDuration = NormalizeDuration(revealDuration);
        }

        private static float NormalizeDuration(float duration) =>
            float.IsNaN(duration) || float.IsInfinity(duration)
                ? 0.25f
                : Mathf.Max(0.01f, duration);
    }
}
