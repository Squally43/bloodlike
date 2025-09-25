Shader "WH/FX/Posterize_Fullscreen"
{
    Properties{
        _Levels    ("Levels", Float) = 5
        _DitherTex ("Dither (Bayer)", 2D) = "gray" {}
        _DitherAmp ("Dither Amp", Float) = 0.03
        _Contrast  ("Contrast", Float) = 1.0
        _Saturation("Saturation", Float) = 1.0
        _Gamma     ("Gamma", Float) = 1.0
        _CellSize  ("Dither Cell Size (px)", Float) = 4
    }
    SubShader{
        Tags{ "RenderType"="Opaque" "Queue"="Overlay" }
        Pass{
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex FS_Vert
            #pragma fragment FS_Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // NOTE: Do NOT redeclare _BlitTexture; Blit.hlsl does that.

            // Dither
            TEXTURE2D(_DitherTex);
            SAMPLER(sampler_DitherTex);

            float _Levels, _DitherAmp, _Contrast, _Saturation, _Gamma;
            float _CellSize;

            struct FS_Attributes { uint vertexID : SV_VertexID; };
            struct FS_Varyings   { float4 positionHCS: SV_POSITION; float2 uv : TEXCOORD0; float2 uvD: TEXCOORD1; };

            FS_Varyings FS_Vert (FS_Attributes v)
            {
                FS_Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv          = GetFullScreenTriangleTexCoord(v.vertexID);
                // Tile Bayer in N-pixel cells across screen
                float cell = max(_CellSize, 1.0);
                o.uvD = o.uv * (_ScreenParams.xy / cell);
                return o;
            }

            float3 Posterize(float3 c, float levels)
            {
                levels = max(levels, 2.0);
                return round(c * (levels - 1.0)) / (levels - 1.0);
            }

            float3 AdjustCSG(float3 c, float contrast, float saturation, float gamma)
            {
                c = (c - 0.5) * contrast + 0.5;
                float l = dot(c, float3(0.299,0.587,0.114));
                c = lerp(l.xxx, c, saturation);
                c = pow(saturate(c), 1.0 / max(0.0001, gamma));
                return c;
            }

            half4 FS_Frag (FS_Varyings i) : SV_Target
            {
                // Source bound by Full Screen Pass/Blitter
                float3 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.uv).rgb;

                col = AdjustCSG(col, _Contrast, _Saturation, 1.0);

                float d = SAMPLE_TEXTURE2D(_DitherTex, sampler_DitherTex, i.uvD).r - 0.5;
                col = saturate(col + d * _DitherAmp);

                col = Posterize(col, _Levels);
                col = pow(saturate(col), 1.0 / max(0.0001, _Gamma));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}



