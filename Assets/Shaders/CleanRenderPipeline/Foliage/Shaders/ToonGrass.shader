Shader "CleanRender/ToonGrass"
{
    Properties
    {
        [MainTexture] _BaseMap("Grass Texture", 2D) = "white"{}
        _BaseColor("Base Color (Bottom)", Color) = (0.2, 0.4, 0.1, 1)
        _TipColor("Tip Color (Top)", Color) = (0.5, 0.85, 0.2, 1)
        [Header(Wind)]
        _WindTex("Wind Noise Tex", 2D) = "gray"{}
        _WindScale("Wind Scale", Float) = 0.08
        _WindSpeed("Wind Speed", Float) = 1.5
        _WindStrength("Wind Strength", Range(0, 1.5)) = 0.4[Header(Interaction)]
        [Toggle(_INTERACTIVE)] _Interactive("Enable Player Interaction", Float) = 1
        _InteractRadius("Interact Radius", Range(0, 5)) = 1.5
        _InteractStrength("Interact Bend Strength", Range(0, 2)) = 1.0
        [Header(Geometry)]
        _GrassHeight("Grass Height", Range(0.1, 3)) = 0.8
        _GrassWidth("Grass Width", Range(0.01, 0.5)) = 0.1
        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.1, 0.2, 0.05, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.4
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/NoiseLib.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TipColor;
            float _WindScale;
            float _WindSpeed;
            float _WindStrength;
            float _InteractRadius;
            float _InteractStrength;
            float _GrassHeight;
            float _GrassWidth;
            half4 _ShadowColor;
            float _Threshold;
            float _Cutoff;
        CBUFFER_END
        float4 _InteractorPositions[8];
        int _InteractorCount;
        TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
        TEXTURE2D(_WindTex);    SAMPLER(sampler_WindTex);
        ENDHLSL
        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex GrassVert
            #pragma fragment GrassFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _INTERACTIVE
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD0;
                float heightGradient : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings GrassVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float heightMask = saturate(input.positionOS.y * rcp(max(_GrassHeight, 1e-4)));
                o.heightGradient = heightMask;
                float3 wind = SampleWind(TEXTURE2D_ARGS(_WindTex, sampler_WindTex),
                    posWS, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                posWS += wind * heightMask;
                #ifdef _INTERACTIVE
                for (int i = 0; i < _InteractorCount; i++)
                {
                    float3 interactPos = _InteractorPositions[i].xyz;
                    float radius = _InteractorPositions[i].w;
                    if (radius <= 0.0) continue;
                    float3 diff = posWS - interactPos;
                    float dist = length(diff.xz);
                    float influence = saturate(1.0 - dist * rcp(max(radius, 1e-4))) * heightMask * _InteractStrength;
                    float2 bendDir = normalize(diff.xz + 1e-4);
                    posWS.xz += bendDir * influence * 0.5;
                    posWS.y -= influence * 0.3;
                }
                #endif
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }
            half4 GrassFrag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);
                half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, input.heightGradient) * tex.rgb;
                float3 N = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = dot(N, mainLight.direction);
                float intensity = smoothstep(_Threshold - 0.05, _Threshold + 0.05,
                                             NdotL * mainLight.shadowAttenuation);
                color *= lerp(_ShadowColor.rgb, mainLight.color, intensity);
                color += SampleSH(N) * color * 0.3;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex SV
            #pragma fragment SF
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _INTERACTIVE
            float3 _LightDirection;
            struct A { float4 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V SV(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                float3 ws = TransformObjectToWorld(i.p.xyz);
                float heightMask = saturate(i.p.y * rcp(max(_GrassHeight, 1e-4)));
                float3 wind = SampleWind(TEXTURE2D_ARGS(_WindTex, sampler_WindTex),
                    ws, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                ws += wind * heightMask;
                #ifdef _INTERACTIVE
                for (int idx = 0; idx < _InteractorCount; idx++)
                {
                    float3 interactPos = _InteractorPositions[idx].xyz;
                    float radius = _InteractorPositions[idx].w;
                    if (radius <= 0.0) continue;
                    float3 diff = ws - interactPos;
                    float dist = length(diff.xz);
                    float influence = saturate(1.0 - dist * rcp(max(radius, 1e-4))) * heightMask * _InteractStrength;
                    float2 bendDir = normalize(diff.xz + 1e-4);
                    ws.xz += bendDir * influence * 0.5;
                    ws.y -= influence * 0.3;
                }
                #endif
                float3 wn = TransformObjectToWorldNormal(i.n);
                o.p = TransformWorldToHClip(ApplyShadowBias(ws, wn, _LightDirection));
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 SF(V i):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DOV
            #pragma fragment DOF
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _INTERACTIVE
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V DOV(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                float3 ws = TransformObjectToWorld(i.p.xyz);
                float heightMask = saturate(i.p.y * rcp(max(_GrassHeight, 1e-4)));
                float3 wind = SampleWind(TEXTURE2D_ARGS(_WindTex, sampler_WindTex),
                    ws, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                ws += wind * heightMask;
                #ifdef _INTERACTIVE
                for (int idx = 0; idx < _InteractorCount; idx++)
                {
                    float3 interactPos = _InteractorPositions[idx].xyz;
                    float radius = _InteractorPositions[idx].w;
                    if (radius <= 0.0) continue;
                    float3 diff = ws - interactPos;
                    float dist = length(diff.xz);
                    float influence = saturate(1.0 - dist * rcp(max(radius, 1e-4))) * heightMask * _InteractStrength;
                    float2 bendDir = normalize(diff.xz + 1e-4);
                    ws.xz += bendDir * influence * 0.5;
                    ws.y -= influence * 0.3;
                }
                #endif
                o.p = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 DOF(V i):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex DNV
            #pragma fragment DNF
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _INTERACTIVE
            struct A { float4 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p:SV_POSITION; float3 n:TEXCOORD0; float2 uv:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V DNV(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = TransformObjectToWorld(i.p.xyz);
                float heightMask = saturate(i.p.y * rcp(max(_GrassHeight, 1e-4)));
                float3 wind = SampleWind(TEXTURE2D_ARGS(_WindTex, sampler_WindTex), ws, _WindScale, _WindSpeed, _WindStrength, _Time.y);
                ws += wind * heightMask;
                #ifdef _INTERACTIVE
                for (int idx = 0; idx < _InteractorCount; idx++)
                {
                    float3 interactPos = _InteractorPositions[idx].xyz;
                    float radius = _InteractorPositions[idx].w;
                    if (radius <= 0.0) continue;
                    float3 diff = ws - interactPos;
                    float dist = length(diff.xz);
                    float influence = saturate(1.0 - dist * rcp(max(radius, 1e-4))) * heightMask * _InteractStrength;
                    float2 bendDir = normalize(diff.xz + 1e-4);
                    ws.xz += bendDir * influence * 0.5;
                    ws.y -= influence * 0.3;
                }
                #endif
                o.p = TransformWorldToHClip(ws);
                o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }
            half4 DNF(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return half4(normalize(i.n), 0.0);
            }
            ENDHLSL
        }
    }
}