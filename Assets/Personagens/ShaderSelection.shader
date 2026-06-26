Shader "Custom/HighlightOverlay"
{
    Properties
    {
        // Propriedade para controlar a cor e a opacidade no Inspector
        _Color ("Color", Color) = (0, 1, 0, 0.5) 
    }
    SubShader
    {
        // Tags para URP e transparência
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalRenderPipeline" }
        LOD 100

        Pass
        {
            // Configura o "Alpha Blending" (para permitir opacidade)
            Blend SrcAlpha OneMinusSrcAlpha
            
            // --- ESTA É A PARTE IMPORTANTE ---
            // ZWrite Off: Não "escreve" na memória de profundidade.
            // ZTest Always: Desenha SEMPRE, ignorando se algo está na frente.
            ZWrite Off
            ZTest Always 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            half4 _Color; // A nossa variável de cor

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Simplesmente retorna a cor que definimos no Inspector
                return _Color;
            }
            ENDHLSL
        }
    }
}
