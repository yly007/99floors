Shader "NinetyNine/AdditiveParticle"
{
    Properties
    {
        _MainTex ("Particle Atlas", 2D) = "black" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Color (0,0,0,0) }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;

            v2f vert(appdata value)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(value.vertex);
                output.uv = TRANSFORM_TEX(value.uv, _MainTex);
                output.color = value.color * _TintColor;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return tex2D(_MainTex, input.uv) * input.color;
            }
            ENDCG
        }
    }
}
