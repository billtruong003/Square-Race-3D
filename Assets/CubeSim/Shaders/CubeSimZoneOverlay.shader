// CubeSimZoneOverlay.shader - the closing zone drawn as a floor decal from a per-cell mask.
// R = closed, G = closing soon. The mask is bilinear filtered, so cell edges become a soft
// contour that the shader turns into a crisp glowing rim. Closed floor is a dark tint with slow
// diagonal hazard stripes (style 0) or an open pit (style 1, the collapsing floor); the warning
// band pulses. It never glows over the whole court the way the old emissive slabs did.
Shader "CubeSim/ZoneOverlay"
{
    Properties
    {
        _Mask         ("Cell mask (R closed, G warn)", 2D) = "black" {}
        _ClosedColor  ("Closed tint", Color) = (0.07, 0.05, 0.09, 0.78)
        _StripeColor  ("Stripe colour", Color) = (0.85, 0.12, 0.16, 0.55)
        [HDR] _RimColor ("Rim colour", Color) = (2.2, 0.35, 0.25, 1)
        _WarnColor    ("Warning colour", Color) = (1.0, 0.82, 0.18, 0.42)
        _StripeMetres ("Stripe period (m)", Float) = 1.2
        _RimWidth     ("Rim width", Range(0.01, 0.4)) = 0.12
        _Style        ("Style 0 zone, 1 pit", Float) = 0
        _Pulse        ("Pulse 0..1 (fed per frame)", Float) = 0
        _MaskRect     ("Mask world rect (x, z, w, h)", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ZoneOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _Mask_ST;
                float4 _ClosedColor;
                float4 _StripeColor;
                float4 _RimColor;
                float4 _WarnColor;
                float  _StripeMetres;
                float  _RimWidth;
                float  _Style;
                float  _Pulse;
                float4 _MaskRect;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = (i.positionWS.xz - _MaskRect.xy) / max(_MaskRect.zw, 1e-3);
                half4 m = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, uv);

                float closed = smoothstep(0.5 - _RimWidth, 0.5 + _RimWidth, m.r);
                float rim = saturate(1.0 - abs(m.r - 0.5) / max(_RimWidth, 1e-3)) * step(0.02, m.r);

                float diag = (i.positionWS.x + i.positionWS.z) / max(_StripeMetres, 0.05);
                float stripe = step(0.5, frac(diag));

                half4 col = 0;
                if (_Style < 0.5)
                {
                    col = lerp(_ClosedColor, _StripeColor, stripe * _StripeColor.a);
                    col.a = _ClosedColor.a * closed;
                }
                else
                {
                    // A pit: almost black, with a faint depth gradient toward the rim.
                    col = half4(0.02, 0.02, 0.03, closed);
                }

                // Warning band: pulsing amber stripes on floor that is about to go.
                float warn = saturate(m.g) * (1.0 - closed);
                half4 w = _WarnColor;
                w.a *= warn * lerp(0.45, 1.0, _Pulse) * lerp(0.6, 1.0, stripe);
                col.rgb = lerp(col.rgb, w.rgb, w.a / max(col.a + w.a, 1e-3));
                col.a = saturate(col.a + w.a);

                // Glowing rim along the closing edge.
                col.rgb = lerp(col.rgb, _RimColor.rgb, rim);
                col.a = max(col.a, rim * _RimColor.a);
                return col;
            }
            ENDHLSL
        }
    }
}
