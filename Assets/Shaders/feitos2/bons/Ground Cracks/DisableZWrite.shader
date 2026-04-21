Shader "Custom/DisableZWrite"
{
    SubShader
    {
        // Define como um objeto opaco para a fila de renderização
        Tags { "RenderType"="Opaque" }

        Pass
        {
            // Desliga a escrita no buffer de profundidade (Depth Buffer)
            ZWrite Off
            
            // Impede que o shader desenhe qualquer cor (RGB ou Alpha) na tela. 
            // É isso que fará a malha ficar 100% invisível.
            ColorMask 0 
        }
    }
}