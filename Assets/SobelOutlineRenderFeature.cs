using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SobelOutlineRenderFeature : ScriptableRendererFeature
{
    class SobelOutlinePass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle source;
        private RTHandle tempTexture;

        public SobelOutlinePass(Material material)
        {
            this.material = material;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Pega a textura da câmera atual usando a nova API RTHandle
            source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Sobel Outline Pass");

            // Configura a textura temporária
            RenderTextureDescriptor cameraTextureDesc = renderingData.cameraData.cameraTargetDescriptor;
            cameraTextureDesc.depthBufferBits = 0; // Não precisamos do depth buffer na textura temporária

            // Aloca a textura temporária de forma segura na nova API
            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, cameraTextureDesc, name: "_TempSobelOutlineTexture");

            // Aplica o efeito usando o material do Fullscreen Shader Graph
            Blitter.BlitCameraTexture(cmd, source, tempTexture, material, 0);

            // Copia o resultado de volta para a câmera
            Blitter.BlitCameraTexture(cmd, tempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Limpa a memória na nova API
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
        // Cria o passe de renderização e define quando ele vai acontecer na câmera
        outlinePass = new SobelOutlinePass(settings.material);
        outlinePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Se o material não estiver configurado no painel, ele não roda para não dar erro
        if (settings.material == null)
        {
            Debug.LogWarning("Material de Outline Sobel não foi atribuído no Render Feature.");
            return;
        }
        renderer.EnqueuePass(outlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        // Garante que a memória seja limpa quando o jogo fechar
        outlinePass?.Dispose();
    }
}