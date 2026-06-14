Shader "Custom/AnimeOutline"
{
    Properties
    {
        _OutlineColor ("Cor da Linha", Color) = (0.1, 0.1, 0.1, 1)
        _OutlineWidth ("Espessura da Linha", Range(0.0001, 0.05)) = 0.002
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Outline"
            // O SEGREDO DO ESTILO ANIME: Renderiza apenas a parte de trás/dentro do modelo
            Cull Front 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Expande o modelo na direção das normais para criar a borda
                float3 pos = input.positionOS.xyz + (input.normalOS * _OutlineWidth);
                output.positionCS = TransformObjectToHClip(pos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Pinta a borda com a cor escolhida
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}