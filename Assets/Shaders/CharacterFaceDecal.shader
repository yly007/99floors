Shader "NinetyNine/CharacterFaceDecal"
{
    Properties
    {
        _MainTex ("Character Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 atlasUv : TEXCOORD0;
                float2 localUv : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.atlasUv = TRANSFORM_TEX(input.uv, _MainTex);
                output.localUv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.atlasUv);
                float foreground = smoothstep(0.08, 0.25,
                    max(color.r, max(color.g, color.b)));
                float2 centered = (input.localUv - 0.5) * float2(1.45, 1.55);
                float edgeFade = 1.0 - smoothstep(0.7, 1.0, length(centered));
                color.a *= foreground * edgeFade;
                clip(color.a - 0.035);
                return color;
            }
            ENDCG
        }
    }
}
