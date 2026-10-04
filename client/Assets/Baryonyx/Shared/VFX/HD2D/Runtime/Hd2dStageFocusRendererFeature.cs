using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Baryonyx.Vfx.Hd2d
{
    /// <summary>
    /// Renders <see cref="Hd2dStageFocus"/>: a blur of the opaque stage whose radius follows each
    /// pixel's distance from the focus, drawn before the transparent queue (the characters'
    /// boards, the effects and the stage's UI). The blur runs at half size, across and then
    /// down, and is laid over the sharp picture where the stage is out of focus. Cameras
    /// without an active override skip the pass entirely.
    /// </summary>
    [DisallowMultipleRendererFeature("HD-2D Stage Focus")]
    public sealed class Hd2dStageFocusRendererFeature : ScriptableRendererFeature
    {
        [Tooltip(
            "Hidden/Baryonyx/HD2D/StageFocus シェーダー。ビルドに含めるためここで参照します。"
        )]
        public Shader Shader;

        private Material material;
        private StageFocusPass pass;

        public override void Create()
        {
            pass = new StageFocusPass();
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
            if (!EnsureMaterial() || !renderingData.cameraData.postProcessEnabled)
                return;
            // Only a perspective camera looks into a stage with depth.
            if (renderingData.cameraData.camera.orthographic)
                return;

            var settings = VolumeManager.instance.stack.GetComponent<Hd2dStageFocus>();
            if (settings == null || !settings.IsActive())
                return;

            pass.Setup(material, settings);
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

        private sealed class StageFocusPass : ScriptableRenderPass
        {
            private static readonly int ParamsId = Shader.PropertyToID("_Hd2dStageFocusParams");
            private static readonly int FalloffId = Shader.PropertyToID("_Hd2dStageFocusFalloff");
            private static readonly int TexelSizeId = Shader.PropertyToID(
                "_Hd2dStageFocusTexelSize"
            );
            private static readonly int BlurId = Shader.PropertyToID("_Hd2dStageFocusBlur");
            private const int HorizontalPass = 0;
            private const int VerticalPass = 1;
            private const int CompositePass = 2;

            private Material material;
            private Hd2dStageFocus settings;

            public StageFocusPass()
            {
                profilingSampler = new ProfilingSampler("HD-2D Stage Focus");
                // After the opaque stage and before the transparent queue.
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(Material passMaterial, Hd2dStageFocus focus)
            {
                material = passMaterial;
                settings = focus;
            }

            private sealed class PassData
            {
                internal TextureHandle Source;
                internal TextureHandle Blur;
                internal Material Material;
                internal int Pass;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData
            )
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid())
                    return;

                // The radii are authored for a 1080 px tall screen and scale with the target.
                float scale =
                    Mathf.Max(1, cameraData.cameraTargetDescriptor.height)
                    / Hd2dStageFocus.ReferenceScreenHeight;
                material.SetVector(
                    ParamsId,
                    new Vector4(
                        settings.focusDistance.value,
                        settings.focusRange.value,
                        settings.intensity.value,
                        0f
                    )
                );
                material.SetVector(
                    FalloffId,
                    new Vector4(
                        settings.nearFalloff.value,
                        settings.farFalloff.value,
                        settings.nearMaxRadius.value * scale,
                        settings.maxRadius.value * scale
                    )
                );

                var source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
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
                description.name = "_Hd2dStageFocus";
                var focused = renderGraph.CreateTexture(description);
                description.width = Mathf.Max(1, description.width / 2);
                description.height = Mathf.Max(1, description.height / 2);
                description.filterMode = FilterMode.Bilinear;
                description.name = "_Hd2dStageFocusHorizontal";
                var horizontal = renderGraph.CreateTexture(description);
                description.name = "_Hd2dStageFocusVertical";
                var vertical = renderGraph.CreateTexture(description);

                AddPass(
                    renderGraph,
                    resources,
                    source,
                    TextureHandle.nullHandle,
                    horizontal,
                    HorizontalPass,
                    "HD-2D Stage Focus Horizontal"
                );
                AddPass(
                    renderGraph,
                    resources,
                    horizontal,
                    TextureHandle.nullHandle,
                    vertical,
                    VerticalPass,
                    "HD-2D Stage Focus Vertical"
                );
                AddPass(
                    renderGraph,
                    resources,
                    source,
                    vertical,
                    focused,
                    CompositePass,
                    "HD-2D Stage Focus Composite"
                );
                // The characters, the effects and the stage's UI are drawn on the focused stage.
                resources.cameraColor = focused;
            }

            private void AddPass(
                RenderGraph renderGraph,
                UniversalResourceData resources,
                TextureHandle source,
                TextureHandle blur,
                TextureHandle destination,
                int shaderPass,
                string name
            )
            {
                using var builder = renderGraph.AddRasterRenderPass<PassData>(
                    name,
                    out var data,
                    profilingSampler
                );
                data.Source = source;
                data.Blur = blur;
                data.Material = material;
                data.Pass = shaderPass;
                builder.UseTexture(source, AccessFlags.Read);
                if (blur.IsValid())
                    builder.UseTexture(blur, AccessFlags.Read);
                // Bound globally as _CameraDepthTexture by the copy of the opaque depth.
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(
                    static (PassData pass, RasterGraphContext context) =>
                    {
                        if (pass.Blur.IsValid())
                            pass.Material.SetTexture(BlurId, pass.Blur);
                        Blitter.BlitTexture(
                            context.cmd,
                            pass.Source,
                            new Vector4(1f, 1f, 0f, 0f),
                            pass.Material,
                            pass.Pass
                        );
                    }
                );
            }
        }
    }
}
