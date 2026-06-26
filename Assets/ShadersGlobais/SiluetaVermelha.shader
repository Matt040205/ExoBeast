Shader "Custom/SilhouetteShader"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+100" }

        Pass
        {
            Lighting Off
            ZTest Always // Desenha o objeto mesmo que ele esteja atrás de outros.
            ZWrite Off // Não permite que outros objetos fiquem atrás do objeto marcado.
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}