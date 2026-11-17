using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Uses framebuffer blending, never replacing the scene with a sampled color texture.
public sealed class UnifiedLookFeature : ScriptableRendererFeature
{
    [SerializeField] private Material _material;
    [SerializeField] private bool _enableOutline = true;
    private LookPass _pass;

    public void Configure(Material material) => _material = material;

    public override void Create() => _pass = new LookPass();

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _material.passCount < 2 ||
            renderingData.cameraData.renderType == CameraRenderType.Overlay ||
            renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection) return;
        _pass.Material = _material;
        _pass.EnableOutline = _enableOutline;
        _pass.ConfigureInput(_enableOutline ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
        renderer.EnqueuePass(_pass);
    }

    private sealed class LookPass : ScriptableRenderPass
    {
        public Material Material;
        public bool EnableOutline;
        private sealed class Data { public Material Material; public bool Outline; }

        public LookPass() => renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.activeColorTexture.IsValid()) return;
            using var builder = renderGraph.AddRasterRenderPass<Data>("Unified Look Safe", out var data);
            data.Material = Material;
            data.Outline = EnableOutline && resources.cameraDepthTexture.IsValid();
            // Preserve and load existing pixels for hardware blending.
            builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
            if (data.Outline) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
            builder.SetRenderFunc(static (Data passData, RasterGraphContext context) =>
            {
                context.cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 0, MeshTopology.Triangles, 3, 1);
                if (passData.Outline)
                    context.cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 1, MeshTopology.Triangles, 3, 1);
            });
        }
    }
}
