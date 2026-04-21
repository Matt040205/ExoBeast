Shader "Custom/ChromaKey_Foliage_URP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _KeyColor ("Cor do Fundo para Remover", Color) = (0,0,0,1) // Preto por padrão
        _Cutoff ("Sensibilidade das Bordas", Range(0, 1.5)) = 0.1
    }
    SubShader
    {
        Tags { 
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Cull Off // Faz a folha ser visível de ambos os lados

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _KeyColor;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Pega a cor exata da textura
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                
                // Calcula a diferença matemática entre a cor atual do pixel e a cor do fundo (KeyColor)
                float colorDistance = distance(c.rgb, _KeyColor.rgb);
                
                // Se a cor do pixel for muito parecida com a cor do fundo (distância menor que o Cutoff), recorta e apaga
                clip(colorDistance - _Cutoff);
                
                // Retorna a cor da folha multiplicada pela cor base
                half3 finalColor = c.rgb * _BaseColor.rgb;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}