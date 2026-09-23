using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Pseudo tilt-shift for flat pixel-art backgrounds. It has no depth information, so it
    /// blurs by screen height instead: a sharp horizontal band and softer top and bottom edges.
    /// Rendered by <see cref="Hd2dTiltShiftRendererFeature"/> only while this override is active.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("Baryonyx/HD-2D Tilt Shift")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class Hd2dTiltShift : VolumeComponent, IPostProcessComponent
    {
        public const float ReferenceScreenHeight = 1080f;

        [Tooltip("ぼかしの強さ。0で効果を止め、描画処理も行いません。")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("くっきり見せる帯の中心の高さ。0が画面の下端、1が上端です。")]
        public ClampedFloatParameter focusCenter = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("くっきり見せる帯の半分の高さ。画面の高さに対する割合です。")]
        public ClampedFloatParameter focusHalfHeight = new ClampedFloatParameter(0.25f, 0f, 1f);

        [Tooltip("帯の外で最大のぼかしに届くまでの高さ。大きいほどなだらかにぼけます。")]
        public ClampedFloatParameter falloff = new ClampedFloatParameter(0.25f, 0.01f, 1f);

        [Tooltip(
            "最大のぼかし半径（画面の高さ1080px基準のpx）。ドット絵の1粒より大きくしないと効果が見えません。6〜10が目安です。"
        )]
        public ClampedFloatParameter maxRadius = new ClampedFloatParameter(8f, 0f, 16f);

        public bool IsActive()
        {
            return intensity.value > 0f && maxRadius.value > 0f;
        }

        /// <summary>
        /// Blur strength from 0 to 1 at a normalized screen height. Mirrors the shader.
        /// </summary>
        public static float EvaluateBlurAmount(
            float height01,
            float center,
            float halfHeight,
            float falloff,
            float intensity
        )
        {
            var distance = Mathf.Abs(height01 - center) - halfHeight;
            return Mathf.Clamp01(distance / Mathf.Max(falloff, 1e-4f)) * Mathf.Clamp01(intensity);
        }
    }
}
