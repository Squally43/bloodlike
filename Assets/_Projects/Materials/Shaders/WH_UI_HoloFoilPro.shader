Shader "WH/UI/HoloFoilPro"
{
    Properties
    {
        // UGUI baseline
        [PerRendererData]_MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Foil inputs
        _FoilTex  ("Foil Ramp (RGB)", 2D) = "gray" {}
        _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _Iridescence ("Iridescence", Range(0,2)) = 1.0
        _FoilScale  ("Foil Scale", Range(0,12)) = 3.5
        _FoilSpeed  ("Foil Speed", Range(0,6)) = 1.2
        _HueShift   ("Hue Shift", Range(-1,1)) = 0.0

        // Optional animated sweep highlight (trigger via material property)
        _SweepAmount ("Sweep Amount", Range(0,1)) = 0.0
        _SweepWidth  ("Sweep Width", Range(0.02,0.6)) = 0.18
        _SweepAngle  ("Sweep Angle (deg)", Range(-90,90)) = 24
        _SweepBoost  ("Sweep Boost", Range(0,3)) = 1.2

        // --- Unity UI Stencil/Mask props (match UI/Default) ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZTest [unity_GUIZTestMode]
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "HoloFoil"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0; // sprite uv
                float2 maskUV   : TEXCOORD1; // rect mask uv
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float2 maskUV   : TEXCOORD1;
                float4 worldPos : TEXCOORD2;
            };

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _FoilTex; float4 _FoilTex_ST;
            sampler2D _NoiseTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _Iridescence, _FoilScale, _FoilSpeed, _HueShift;
            float _SweepAmount, _SweepWidth, _SweepAngle, _SweepBoost;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldPos = v.vertex;
                o.maskUV = v.maskUV;
                o.color = v.color * _Color;
                return o;
            }

            fixed3 hueShift(fixed3 c, float s) // s in [-1,1]
            {
                // rotate around RGB space; lightweight approximate hue shift
                float3x3 m = float3x3(
                    0.299 + 0.701*cos(s) + 0.168*sin(s), 0.587 - 0.587*cos(s) + 0.330*sin(s), 0.114 - 0.114*cos(s) - 0.497*sin(s),
                    0.299 - 0.299*cos(s) - 0.328*sin(s), 0.587 + 0.413*cos(s) + 0.035*sin(s), 0.114 - 0.114*cos(s) + 0.292*sin(s),
                    0.299 - 0.3*cos(s)   + 1.25*sin(s),  0.587 - 0.588*cos(s) - 1.05*sin(s),  0.114 + 0.886*cos(s) - 0.203*sin(s)
                );
                return saturate(mul(c, m));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                // Rect mask / clipping
                #ifdef UNITY_UI_CLIP_RECT
                baseCol.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip (baseCol.a - 0.001);
                #endif

                // Foil UVs (card-space) with time scroll
                float2 uv = i.uv * _FoilScale;
                float t = _Time.y * _FoilSpeed;
                uv += float2(t * 0.17, t * 0.23);

                fixed3 ramp  = tex2D(_FoilTex, uv).rgb;
                fixed  noise = tex2D(_NoiseTex, uv * 1.73).r * 0.5 + 0.5;

                ramp = hueShift(ramp, _HueShift * 3.14159); // radians

                // Animated sweep: angle & width in card UV space
                float ang = radians(_SweepAngle);
                float2 dir = float2(cos(ang), sin(ang));
                float sweep = 0.0;
                if (_SweepAmount > 0.001)
                {
                    // project uv to sweep axis (centered); [-0.5..0.5]
                    float2 uv01 = i.uv - 0.5;
                    float p = dot(uv01, dir);
                    float center = lerp(-0.6, 0.6, _SweepAmount);
                    float band = smoothstep(_SweepWidth, 0.0, abs(p - center));
                    sweep = band * _SweepBoost;
                }

                // Iridescent term modulated by base alpha
                fixed holo = saturate(noise * _Iridescence + sweep) * baseCol.a;

                // Screen-like blend over base
                fixed3 resultRGB = 1 - (1 - baseCol.rgb) * (1 - ramp * holo);

                return fixed4(resultRGB, baseCol.a);
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}

