// CubeSimPortal.shader - a flat, unlit, self-animating portal disc for the teleporter pads.
// Procedural: concentric rings pulled into a slow swirl, brightest at the rim, fading to nothing
// past the pad edge. No textures, no lighting, additive over the floor so it glows under bloom.
Shader "CubeSim/Portal"
{
    Properties
    {
        [HDR] _Color     ("Colour", Color) = (0.55, 0.25, 1.0, 1)
        [HDR] _RimColor  ("Rim Colour", Color) = (0.9, 0.7, 1.0, 1)
        _Speed           ("Swirl Speed", Range(0, 8)) = 2.5
        _Rings           ("Ring Count", Range(2, 40)) = 14
        _Arms            ("Swirl Arms", Range(1, 12)) = 5
        _Intensity       ("Intensity", Range(0, 4)) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Portal"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RimColor;
                float  _Speed;
                float  _Rings;
                float  _Arms;
                float  _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float r = length(p) * 2.0;          // 0 centre .. 1 rim
                float a = atan2(p.y, p.x);
                float t = _Time.y * _Speed;

                // Rings drifting inward, twisted by the swirl arms.
                float swirl = sin(a * _Arms + r * 6.0 - t);
                float rings = 0.5 + 0.5 * sin(r * _Rings - t * 2.0 + swirl * 1.5);
                rings = pow(rings, 3.0);

                float edge = 1.0 - smoothstep(0.86, 1.0, r);        // hard fade past the pad
                float core = 1.0 - smoothstep(0.0, 0.35, r);         // bright eye in the middle
                float rim  = smoothstep(0.55, 0.95, r) * edge;

                float3 col = _Color.rgb * (rings * 1.2 + core * 0.8) + _RimColor.rgb * rim * (0.5 + 0.5 * swirl);
                float alpha = saturate((rings * 0.8 + core + rim * 0.7) * edge) * _Intensity;
                return half4(col * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
