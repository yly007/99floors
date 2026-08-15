Shader "Hidden/NinetyNine/AnalogHorror"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 1)) = 0.5
        _TimeSeed ("Time Seed", Float) = 0
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Intensity;
            float _TimeSeed;
            float _Brightness;

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 centered = uv - 0.5;
                float radius = dot(centered, centered);
                uv += centered * radius * 0.045 * _Intensity;

                float glitchBand = step(0.992 - _Intensity * 0.008,
                    Hash(float2(floor(uv.y * 90.0), floor(_TimeSeed * 13.0))));
                uv.x += glitchBand * (Hash(float2(uv.y, _TimeSeed)) - 0.5) * 0.022 * _Intensity;

                float aberration = (0.00075 + radius * 0.003) * _Intensity;
                float red = tex2D(_MainTex, uv + float2(aberration, 0)).r;
                float green = tex2D(_MainTex, uv).g;
                float blue = tex2D(_MainTex, uv - float2(aberration, 0)).b;
                float3 color = float3(red, green, blue);

                float scan = sin((uv.y + _TimeSeed * 0.025) * _ScreenParams.y * 1.6) * 0.5 + 0.5;
                color *= 1.0 - scan * 0.035 * _Intensity;
                float grain = Hash(uv * _ScreenParams.xy + _TimeSeed * 197.0) - 0.5;
                color += grain * 0.052 * _Intensity;

                float vignette = smoothstep(0.72, 0.18, radius);
                color *= lerp(1.0, vignette, 0.62 * _Intensity);
                color = lerp(color, color * float3(0.88, 1.04, 1.02), 0.24);
                color *= _Brightness;
                return fixed4(saturate(color), 1.0);
            }
            ENDCG
        }
    }
}
