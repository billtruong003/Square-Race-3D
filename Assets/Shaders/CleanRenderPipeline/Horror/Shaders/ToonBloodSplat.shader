Shader "CleanRender/Horror/ToonBloodSplat"
{
    Properties
    {
        [MainTexture]_BaseMap("Base Map", 2D) = "white"{}
        [MainColor]_BaseColor("Base Color", Color) = (0.6, 0.6, 0.6, 1)

        [Header(Blood)]
        _BloodColor("Fresh Blood Color", Color) = (0.5, 0.02, 0.02, 1)
        _DriedBloodColor("Dried Blood Color", Color) = (0.2, 0.02, 0.0, 1)
        _BloodMask("Blood Mask (R)", 2D) = "black"{}
        _NoiseTex("Noise Texture", 2D) = "grey"{}
        _NoiseScale("Noise Tiling", Range(0.1, 20)) = 4
        _NormalStrength("Normal-from-Noise Strength", Range(0, 5)) = 1.5
        _BloodWetness("Wetness", Range(0, 1)) = 0.8
        _BloodEdgeNoise("Edge Distortion", Range(0, 1)) = 0.3

        [Header(Drip)][Toggle(_DRIP_ON)]_EnableDrip("Enable Drips", Float) = 1
        _DripSpeed("Drip Speed", Range(0, 2)) = 0.3
        _DripScale("Drip Scale", Range(1, 20)) = 8
        _DripColor("Drip Color", Color) = (0.45, 0.01, 0.01, 1)

        [Header(Pulse)]
        [Toggle(_PULSE_ON)]_EnablePulse("Enable Heartbeat Pulse", Float) = 0
        _PulseRate("Pulse Rate", Range(0.5, 3)) = 1.2
        _PulseStrength("Pulse Strength", Range(0, 0.5)) = 0.15
        [HDR]_PulseEmission("Pulse Emission Color", Color) = (1, 0.05, 0.05, 1)

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.15, 0.05, 0.1, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.04
        _RimColor("Rim Color", Color) = (0.8, 0.1, 0.1, 0.5)
        _RimPower("Rim Power", Range(0.1, 10)) = 3

        [Header(Emission)]
        [Toggle(_EMISSION)]_Emission("Emission", Float) = 0
        [HDR]_EmissionColor("Emission Color", Color) = (0, 0, 0)
        _EmissionMap("Emission Map", 2D) = "white"{}[Header(Options)]
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)]_AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 200
        Cull[_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Horror/Shaders/Includes/HorrorLib.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _BloodColor;
        half4 _DriedBloodColor;
        float4 _NoiseTex_ST;
        float _NoiseScale;
        float _NormalStrength;
        float _BloodWetness;
        float _BloodEdgeNoise;
        float _DripSpeed;
        float _DripScale;
        half4 _DripColor;
        float _PulseRate;
        float _PulseStrength;
        half4 _PulseEmission;
        half4 _ShadowColor;
        half4 _RimColor;
        float _Threshold;
        float _Smoothness;
        float _RimPower;
        half4 _EmissionColor;
        float _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BloodMask); SAMPLER(sampler_BloodMask);
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            AlphaToMask[_ALPHATEST_ON]

            HLSLPROGRAM
            #pragma vertex BloodVert
            #pragma fragment BloodFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _DRIP_ON
            #pragma shader_feature_local _PULSE_ON
            #pragma shader_feature_local _EMISSION

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 uv : TEXCOORD0;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings BloodVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv.xy = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uv.zw = ToonTransformLightmapUV(input.lightmapUV);
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 BloodFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUV = input.uv.xy;
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV) * _BaseColor;

                #ifdef _ALPHATEST_ON
                clip(baseTex.a - _Cutoff);
                #endif

                half bloodMask = SAMPLE_TEXTURE2D(_BloodMask, sampler_BloodMask, baseUV).r;
                float edgeNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, baseUV * _NoiseScale * 0.5).r;
                bloodMask = saturate(bloodMask + (edgeNoise - 0.5) * _BloodEdgeNoise);

                #ifdef _DRIP_ON
                half drip = BloodDripMask(baseUV, _DripSpeed, _DripScale, _Time.y);
                bloodMask = saturate(bloodMask + drip * 0.6);
                #endif

                half3 bloodCol = BloodColorGradient(_BloodWetness, _BloodColor.rgb, _DriedBloodColor.rgb);
                half3 albedo = lerp(baseTex.rgb, bloodCol, bloodMask);

                half3 normalWS = normalize(input.normalWS);
                half3 perturbedN = PerturbNormalNoTBN(normalWS, input.positionWS, baseUV, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _NoiseScale, _NormalStrength * bloodMask);
                normalWS = normalize(lerp(normalWS, perturbedN, bloodMask));

                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                ToonLightResult lit = ComputeToonMainLight(input.positionWS, normalWS, viewDirWS, albedo, input.uv.zw, _Threshold, _Smoothness, _ShadowColor.rgb, _RimColor.rgb, _RimPower, _RimColor.a, 1.0);

                half3 finalColor = lit.diffuse + lit.rim + lit.globalIllumination;

                #ifdef _ADDITIONAL_LIGHTS
                finalColor += ComputeToonAdditionalLights(input.positionWS, normalWS, albedo, _Threshold, _Smoothness);
                #endif

                #ifdef _EMISSION
                finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, baseUV).rgb * _EmissionColor.rgb;
                #endif

                #ifdef _PULSE_ON
                half pulse = BloodPulse(_Time.y, _PulseRate, _PulseStrength);
                finalColor += _PulseEmission.rgb * bloodMask * (pulse - 1.0);
                #endif

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, baseTex.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _ALPHATEST_ON

            float3 _LightDirection;

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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                #ifdef _ALPHATEST_ON
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #else
                output.uv = 0;
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));

                #ifdef _ALPHATEST_ON
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #else
                output.uv = 0;
                #endif

                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _ALPHATEST_ON

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
                half3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);

                #ifdef _ALPHATEST_ON
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #else
                output.uv = 0;
                #endif

                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif

                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings MetaVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return output;
            }

            half4 MetaFrag(Varyings input) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                #ifdef _ALPHATEST_ON
                clip(baseTex.a * _BaseColor.a - _Cutoff);
                #endif

                half bloodMask = SAMPLE_TEXTURE2D(_BloodMask, sampler_BloodMask, input.uv).r;
                half3 bloodCol = BloodColorGradient(_BloodWetness, _BloodColor.rgb, _DriedBloodColor.rgb);
                half3 albedo = lerp(baseTex.rgb * _BaseColor.rgb, bloodCol, bloodMask);
                half3 emission = half3(0, 0, 0);

                #ifdef _EMISSION
                emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = albedo;
                metaInput.Emission = emission;
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "ToonBloodSplatGUI"
}