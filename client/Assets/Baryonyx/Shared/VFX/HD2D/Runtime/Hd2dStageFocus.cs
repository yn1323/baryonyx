using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Depth of field for a 3D stage (HD-2D): the ground and scenery nearer or farther than the
    /// characters blur by their distance from the camera, so the stage reads as a diorama seen
    /// through a lens. It is drawn right after the stage's opaque scenery, before the characters'
    /// boards, the effects and the UI of the stage, so those always stay sharp (a usual depth of
    /// field would blur the UI, which writes no depth, as if it were the ground behind it).
    /// Rendered by <see cref="Hd2dStageFocusRendererFeature"/> only while this override is active.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Baryonyx/HD-2D Stage Focus")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class Hd2dStageFocus : VolumeComponent, IPostProcessComponent
    {
        public const float ReferenceScreenHeight = 1080f;

        [Tooltip("ぼかしの強さ。0で効果を止め、描画処理も行いません。")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("ピントの合う奥行き（カメラからの距離、m）。キャラが立つあたりにする。")]
        public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0.1f);

        [Tooltip("ピントの前後で、くっきり見せる幅の半分（m）。")]
        public MinFloatParameter focusRange = new MinFloatParameter(2f, 0f);

        [Tooltip("くっきりした幅より手前で、最大のぼかしに届くまでの距離（m）。")]
        public MinFloatParameter nearFalloff = new MinFloatParameter(3f, 0.01f);

        [Tooltip("くっきりした幅より奥で、最大のぼかしに届くまでの距離（m）。")]
        public MinFloatParameter farFalloff = new MinFloatParameter(12f, 0.01f);

        [Tooltip(
            "奥の最大のぼかし半径（画面の高さ1080px基準のpx）。ドット絵の1粒より大きくしないと効果が見えません。"
        )]
        public ClampedFloatParameter maxRadius = new ClampedFloatParameter(6f, 0f, 16f);

        [Tooltip(
            "手前の最大のぼかし半径（画面の高さ1080px基準のpx）。手前の物はドットが大きく写るため、奥より大きくする。"
        )]
        public ClampedFloatParameter nearMaxRadius = new ClampedFloatParameter(12f, 0f, 24f);

        public bool IsActive()
        {
            return intensity.value > 0f && (maxRadius.value > 0f || nearMaxRadius.value > 0f);
        }

        /// <summary>
        /// The blur radius (in the same units as the two radii) at a distance from the camera
        /// along its view: none within the focus range, growing to <paramref name="nearRadius"/>
        /// in front of it and to <paramref name="farRadius"/> behind it. It grows along a smooth
        /// step, so the blur creeps in from the sharp range and settles into its largest radius
        /// without a visible edge at either end. Mirrors the shader.
        /// </summary>
        public static float EvaluateBlurRadius(
            float depth,
            float focusDistance,
            float focusRange,
            float nearFalloff,
            float farFalloff,
            float nearRadius,
            float farRadius,
            float intensity
        )
        {
            float offset = depth - focusDistance;
            float far = Ease((offset - focusRange) / Mathf.Max(farFalloff, 1e-4f));
            float near = Ease((-offset - focusRange) / Mathf.Max(nearFalloff, 1e-4f));
            return Mathf.Max(far * farRadius, near * nearRadius) * Mathf.Clamp01(intensity);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
