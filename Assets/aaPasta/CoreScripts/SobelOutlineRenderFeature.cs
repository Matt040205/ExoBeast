using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// ── SobelOutlineRenderFeature ────────────────────────────────
/// Render Feature que aplica efeito de contorno Sobel via material customizado.
///
///  ▸ RecordRenderGraph: implementação nativa do RenderGraph (URP 17 / Unity 6)
///  ▸ Execute / OnCameraSetup: fallback para Compatibility Mode (RenderGraph desabilitado)
///  ▸ Dois passes: efeito (activeColor → temp) e cópia de volta (temp → activeColor)
/// ─────────────────────────────────────────────────────────────
/// </summary>
public class SobelOutlineRenderFeature : ScriptableRendererFeature
{
    class SobelOutlinePass : ScriptableRenderPass
    {
        private Material material;

        // ── Compatibility Mode ─────────────────────────────────
        private RTHandle source;
        private RTHandle tempTexture;

        public SobelOutlinePass(Material mat)
        {
            material = mat;
        }

        // ── RenderGraph (URP 17 / Unity 6) ────────────────────

        private class EffectPassData
        {
            public TextureHandle source;
            public Material material;
        }

        private class CopyBackPassData
        {
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            // Back buffer não suporta blit intermediário
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle activeColor = resourceData.activeColorTexture;

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle tempHandle = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "_SobelOutlineTemp", false);

            // Passo 1: activeColor → temp usando material Sobel
            using (var builder = renderGraph.AddRasterRenderPass<EffectPassData>(
                "Sobel Outline Effect", out var passData))
            {
                passData.source   = activeColor;
                passData.material = material;

                builder.UseTexture(activeColor, AccessFlags.Read);
                builder.SetRenderAttachment(tempHandle, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((EffectPassData data, RasterGraphContext ctx) =>
                {
                    // TextureHandle → RTHandle via conversão implícita (URP 17)
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                });
            }

            // Passo 2: temp → activeColor (cópia simples sem material)
            using (var builder = renderGraph.AddRasterRenderPass<CopyBackPassData>(
                "Sobel Outline CopyBack", out var passData))
            {
                passData.source = tempHandle;

                builder.UseTexture(tempHandle, AccessFlags.Read);
                builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);

                builder.SetRenderFunc((CopyBackPassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                });
            }
        }

        // ── Compatibility Mode fallback ────────────────────────

#pragma warning disable CS0618, CS0672
        // URP 17 keeps these APIs only for Compatibility Mode. RenderGraph path above is the primary path.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Sobel Outline Pass");

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, desc, name: "_TempSobelOutlineTexture");

            Blitter.BlitCameraTexture(cmd, source, tempTexture, material, 0);
            Blitter.BlitCameraTexture(cmd, tempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618, CS0672

        public void Dispose()
        {
            tempTexture?.Release();
        }
    }

    [System.Serializable]
    public class Settings
    {
        public Material material = null;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    SobelOutlinePass outlinePass;

    public override void Create()
    {
        outlinePass = new SobelOutlinePass(settings.material);
        outlinePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
        {
            Debug.LogWarning("Material de Outline Sobel não foi atribuído no Render Feature.");
            return;
        }
        renderer.EnqueuePass(outlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        outlinePass?.Dispose();
    }
}
