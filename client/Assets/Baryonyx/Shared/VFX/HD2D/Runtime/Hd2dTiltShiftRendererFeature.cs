using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Renders <see cref="Hd2dTiltShift"/> as a separable blur whose radius grows toward the
    /// top and bottom of the screen. Cameras without an active override skip the pass entirely.
    /// </summary>
    [DisallowMultipleRendererFeature("HD-2D Tilt Shift")]
    public sealed class Hd2dTiltShiftRendererFeature : ScriptableRendererFeature
    {
        [Tooltip("Hidden/Baryonyx/HD2D/TiltShift シェーダー。ビルドに含めるためここで参照します。")]
        public Shader Shader;

        // Before the built-in post-processing, so Bloom and Vignette apply on top of the blur.
        public RenderPassEvent InjectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material material;
        private TiltShiftPass pass;

        public override void Create()
        {
            pass = new TiltShiftPass();
            EnsureMaterial();
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData
        )
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;
            // The shader can be assigned after Create, for example by an editor setup script.
            if (!EnsureMaterial() || !renderingData.cameraData.postProcessEnabled)
                return;

            var settings = VolumeManager.instance.stack.GetComponent<Hd2dTiltShift>();
            if (settings == null || !settings.IsActive())
                return;

            pass.Setup(material, settings, InjectionPoint);
            renderer.EnqueuePass(pass);
        }

        private bool EnsureMaterial()
        {
            if (material == null && Shader != null)
                material = CoreUtils.CreateEngineMaterial(Shader);
            return material != null;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class TiltShiftPass : ScriptableRenderPass
        {
            private static readonly int ParamsId = Shader.PropertyToID("_Hd2dTiltShiftParams");
            private static readonly int RadiusId = Shader.PropertyToID("_Hd2dTiltShiftRadius");
            private static readonly int TexelSizeId = Shader.PropertyToID(
                "_Hd2dTiltShiftTexelSize"
            );
            private const int HorizontalPass = 0;
            private const int VerticalPass = 1;

            private Material material;
            private Hd2dTiltShift settings;

            public TiltShiftPass()
            {
                profilingSampler = new ProfilingSampler("HD-2D Tilt Shift");
                // The blur reads the camera colour, so it needs an intermediate texture.
                requiresIntermediateTexture = true;
            }

            public void Setup(Material passMaterial, Hd2dTiltShift tiltShift, RenderPassEvent evt)
            {
                material = passMaterial;
                settings = tiltShift;
                renderPassEvent = evt;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData
            )
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer)
                    return;

                material.SetVector(
                    ParamsId,
                    new Vector4(
                        settings.focusCenter.value,
                        settings.focusHalfHeight.value,
                        settings.falloff.value,
                        settings.intensity.value
                    )
                );
                // The radius is authored for a 1080 px tall screen and scales with the target.
                var height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height);
                material.SetFloat(
                    RadiusId,
                    settings.maxRadius.value * height / Hd2dTiltShift.ReferenceScreenHeight
                );

                var source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
                // The blitter does not bind _BlitTexture_TexelSize, so pass the size explicitly.
                material.SetVector(
                    TexelSizeId,
                    new Vector4(
                        1f / Mathf.Max(1, description.width),
                        1f / Mathf.Max(1, description.height),
                        description.width,
                        description.height
                    )
                );
                description.clearBuffer = false;
                description.name = "_Hd2dTiltShiftHorizontal";
                var horizontal = renderGraph.CreateTexture(description);
                description.name = "_Hd2dTiltShiftVertical";
                var vertical = renderGraph.CreateTexture(description);

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(
                        source,
                        horizontal,
                        material,
                        HorizontalPass
                    ),
                    "HD-2D Tilt Shift Horizontal"
                );
                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(
                        horizontal,
                        vertical,
                        material,
                        VerticalPass
                    ),
                    "HD-2D Tilt Shift Vertical"
                );
                resources.cameraColor = vertical;
            }
        }
    }
}
