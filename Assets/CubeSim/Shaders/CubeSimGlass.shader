// =============================================================================
//  CubeSimGlass.shader - stylized toon glass for the rainbow gates.
//
//  Self-contained on purpose: the StylizedToonWorldKit source (URPCompat /
//  StylizedLighting / StylizedNoise) is not in this project, so the exact toon
//  math the gates need - toon step specular, fresnel rim, gradient noise for
//  the frost jitter - is inlined below (STW_* functions, same formulas as the
//  kit's StylizedLighting.hlsl) instead of being included. Everything else maps
//  to plain URP 17 calls.
//
//  Requires the URP asset's Opaque Texture (already on) for refraction/frost.
// =============================================================================
Shader "CubeSim/Glass"
{
    Properties
    {
        [Header(Body)][Space(4)]
        [HDR] _TintColor   ("Tint Color", Color) = (0.85, 0.95, 1, 0.45)

        [Header(Refraction)][Space(4)]
        _RefractStrength   ("Refraction Strength", Range(0, 1)) = 0.12

        [Header(Frosted)][Space(4)]
        _FrostAmount       ("Frost Blur Amount", Range(0, 0.05)) = 0.012
        _FrostJitter       ("Frost Jitter", Range(0, 1)) = 0.3
        _FrostNoiseScale   ("Frost Noise Scale", Range(1, 80)) = 30

        [Header(Fresnel and Specular)][Space(4)]
        [HDR] _FresnelColor ("Fresnel Color", Color) = (1, 1, 1, 1)
        _FresnelPower      ("Fresnel Power", Range(0.2, 8)) = 3
        _FresnelStrength   ("Fresnel Strength", Range(0, 3)) = 1
        [HDR] _SpecColor2  ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecStrength      ("Specular Strength", Range(0, 4)) = 1
        _SpecSize          ("Specular Size", Range(0, 1)) = 0.08

        [Header(Render State)][Space(4)]
        _Alpha             ("Overall Alpha", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define STW_EPSILON 1e-4h

            CBUFFER_START(UnityPerMaterial)
                half4  _TintColor;
                half   _RefractStrength;
                half   _FrostAmount;
                half   _FrostJitter;
                half   _FrostNoiseScale;
                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelStrength;
                half4  _SpecColor2;
                half   _SpecStrength;
                half   _SpecSize;
                half   _Alpha;
                half   _Cull;
            CBUFFER_END

            // ---- STW toon math, inlined (formulas from StylizedLighting.hlsl) ----

            half3 STW_SafeNormalizeH3(half3 v)
            {
                return v * rsqrt(max(STW_EPSILON, dot(v, v)));
            }

            // Toon specular: Blinn half-vector raised by size, stepped hard for the
            // flat anime highlight.
            half STW_ToonSpecular(half3 normalWS, half3 lightDirWS, half3 viewDirWS, half size)
            {
                half3 h = STW_SafeNormalizeH3(lightDirWS + viewDirWS);
                half ndh = saturate(dot(normalWS, h));
                half spec = pow(ndh, lerp(256.0h, 8.0h, saturate(size)));
                return smoothstep(0.5h - 0.05h, 0.5h + 0.05h, spec);
            }

            // Fresnel rim.
            half STW_Fresnel(half3 normalWS, half3 viewDirWS, half power)
            {
                half f = 1.0h - saturate(dot(normalWS, viewDirWS));
                return pow(f, max(STW_EPSILON, power));
            }

            // Gradient-style hash noise (stand-in for StylizedNoise's
            // STW_GradientNoise) - only feeds the frost jitter.
            half STW_GradientNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);

                return (half)lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
                half   fogCoord   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.uv         = IN.uv;
                OUT.screenPos  = ComputeScreenPos(pos.positionCS);
                OUT.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half3 normalWS  = STW_SafeNormalizeH3(IN.normalWS);
                half3 viewDirWS = STW_SafeNormalizeH3((half3)GetWorldSpaceViewDir(IN.positionWS));

                // The scene-colour refraction path is deliberately gone: this project's renderer
                // (CleanRender outline stack) never hands transparents a usable opaque texture, so
                // sampling it painted the gates black. Alpha blending already shows what is behind
                // the pane; a frost-noise shimmer keeps the surface reading as glass, not plastic.
                half frost = STW_GradientNoise(IN.uv * _FrostNoiseScale);
                half3 color = _TintColor.rgb * (0.85h + frost * _FrostJitter * 0.3h);

                // --- toon specular + fresnel, lit by the main light ---
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half spec = STW_ToonSpecular(normalWS, (half3)mainLight.direction, viewDirWS, _SpecSize);
                color += spec * _SpecStrength * _SpecColor2.rgb * mainLight.color;

                half fres = STW_Fresnel(normalWS, viewDirWS, _FresnelPower);
                color += fres * _FresnelStrength * _FresnelColor.rgb;

                half alpha = saturate(_TintColor.a + fres * _FresnelStrength) * _Alpha;

                color = MixFog(color, IN.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
