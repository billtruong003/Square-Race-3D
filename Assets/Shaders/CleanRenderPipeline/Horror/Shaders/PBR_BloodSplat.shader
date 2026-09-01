Shader "CleanRender/Horror/PBR_BloodSplat"
{
    Properties
    {
        [MainTexture]_BaseMap("Base Map", 2D) = "white"{}
        [MainColor]_BaseColor("Base Color", Color) = (0.7, 0.7, 0.7, 1)
        _BumpMap("Normal Map", 2D) = "bump"{}
        _BumpScale("Normal Scale", Range(0, 2)) = 1.0

        [Header(PBR)]
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _SmoothnessBase("Smoothness", Range(0, 1)) = 0.3
        _OcclusionMap("Occlusion (R)", 2D) = "white"{}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1.0

        [Header(Blood)]
        _BloodColor("Fresh Blood Color", Color) = (0.5, 0.02, 0.02, 1)
        _DriedBloodColor("Dried Blood Color", Color) = (0.2, 0.02, 0.0, 1)
        _BloodMask("Blood Mask (R)", 2D) = "black"{}
        _NoiseTex("Noise Texture", 2D) = "grey"{}
        _NoiseScale("Noise Tiling", Range(0.1, 20)) = 4
        _BloodWetness("Wetness", Range(0, 1)) = 0.85
        _BloodSmoothness("Blood Smoothness", Range(0, 1)) = 0.9
        _BloodEdgeNoise("Edge Distortion", Range(0, 1)) = 0.3
        _BloodNormalStrength("Blood Normal Str", Range(0, 3)) = 1.0
        _BloodMetallic("Blood Metallic", Range(0, 1)) = 0.0

        [Header(Drip)][Toggle(_DRIP_ON)]_EnableDrip("Enable Drips", Float) = 1
        _DripSpeed("Drip Speed", Range(0, 2)) = 0.3
        _DripScale("Drip Scale", Range(1, 20)) = 8

        [Header(Pulse)]
        [Toggle(_PULSE_ON)]_EnablePulse("Enable Pulse", Float) = 0
        _PulseRate("Pulse Rate", Range(0.5, 3)) = 1.2
        _PulseStrength("Pulse Strength", Range(0, 1)) = 0.3
        [HDR]_PulseEmission("Pulse Emission", Color) = (2, 0.1, 0.1, 1)

        [Header(Emission)]
        [Toggle(_EMISSION)]_EnableEmission("Emission", Float) = 0
        [HDR]_EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white"{}

        [Header(Options)]
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)]_AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 300
        Cull[_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Horror/Shaders/Includes/HorrorLib.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        float _BumpScale;
        float _Metallic;
        float _SmoothnessBase;
        float _OcclusionStrength;
        half4 _BloodColor;
        half4 _DriedBloodColor;
        float _NoiseScale;
        float _BloodWetness;
        float _BloodSmoothness;
        float _BloodEdgeNoise;
        float _BloodNormalStrength;
        float _BloodMetallic;
        float _DripSpeed;
        float _DripScale;
        float _PulseRate;
        float _PulseStrength;
        half4 _PulseEmission;
        half4 _EmissionColor;
        float _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
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
            #pragma vertex PBRBloodVert
            #pragma fragment PBRBloodFrag

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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                float4 uv : TEXCOORD0;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings PBRBloodVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4((half3)TransformObjectToWorldDir(input.tangentOS.xyz), (half)sign);
                output.uv.xy = TRANSFORM_TEX(input.uv, _BaseMap);

                #if defined(LIGHTMAP_ON)
                output.uv.zw = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #else
                output.uv.zw = 0;
                #endif

                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 PBRBloodFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv.xy;
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                #ifdef _ALPHATEST_ON
                clip(baseTex.a - _Cutoff);
                #endif

                half bloodMask = SAMPLE_TEXTURE2D(_BloodMask, sampler_BloodMask, uv).r;
                float edgeNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * _NoiseScale * 0.5).r;
                bloodMask = saturate(bloodMask + (edgeNoise - 0.5) * _BloodEdgeNoise);

                #ifdef _DRIP_ON
                bloodMask = saturate(bloodMask + BloodDripMask(uv, _DripSpeed, _DripScale, _Time.y) * 0.6);
                #endif

                half3 bloodCol = BloodColorGradient(_BloodWetness, _BloodColor.rgb, _DriedBloodColor.rgb);
                half3 albedo = lerp(baseTex.rgb, bloodCol, bloodMask);
                float smoothness = lerp(_SmoothnessBase, _BloodSmoothness, bloodMask * _BloodWetness);
                float metallic = lerp(_Metallic, _BloodMetallic, bloodMask);
                float occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r, _OcclusionStrength);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 bloodNTS = NormalFromNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), uv * _NoiseScale, 1.0 / 256.0, _BloodNormalStrength);
                normalTS = normalize(lerp(normalTS, bloodNTS, bloodMask));
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS);

                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                half perceptualRoughness = 1.0h - (half)smoothness;
                half roughness = max(perceptualRoughness * perceptualRoughness, HALF_MIN_SQRT);
                half roughness2 = max(roughness * roughness, HALF_MIN);
                half3 F0 = lerp(half3(0.04, 0.04, 0.04), albedo, (half)metallic);
                half oneMinusRefl = (1.0h - (half)metallic) * 0.96h;
                half3 diffAlbedo = albedo * oneMinusRefl;

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                float4 sc = ComputeScreenPos(input.positionCS);
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float4 sc = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 sc = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(sc);
                half3 L = (half3)mainLight.direction;
                half3 H = normalize(L + (half3)V);
                half NdotL = saturate(dot(normalWS, L));
                half NdotV = saturate(dot(normalWS, (half3)V));
                half NdotH = saturate(dot(normalWS, H));
                half VdotH = saturate(dot((half3)V, H));

                half3 F = F0 + (1.0h - F0) * pow(1.0h - VdotH, 5.0h);
                half denom = NdotH * NdotH * (roughness2 - 1.0h) + 1.0h;
                half D = roughness2 / (PI * denom * denom + 1e-7h);
                half k = roughness * 0.5h;
                half visL = NdotL / (NdotL * (1.0h - k) + k + 1e-5h);
                half visV = NdotV / (NdotV * (1.0h - k) + k + 1e-5h);
                half3 specular = D * visL * visV * F;
                half atten = (half)(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 radiance = (half3)mainLight.color * atten * NdotL;
                half3 directColor = (diffAlbedo + specular) * radiance;

                half3 indirectDiffuse;
                #if defined(LIGHTMAP_ON)
                indirectDiffuse = SampleLightmap(input.uv.zw, 0.0, normalWS);
                #else
                indirectDiffuse = SampleSH(normalWS);
                #endif

                half3 indirectColor = indirectDiffuse * diffAlbedo * occlusion;
                half3 addLights = half3(0, 0, 0);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lc = GetAdditionalLightsCount();
                UNITY_LOOP
                for (uint i = 0u; i < lc; i + + )
                {
                    Light al = GetAdditionalLight(i, input.positionWS);
                    half aa = (half)(al.distanceAttenuation * al.shadowAttenuation);
                    half an = saturate(dot(normalWS, (half3)al.direction));
                    addLights += (half3)al.color * aa * an * diffAlbedo;
                }
                #endif

                half3 finalColor = directColor + indirectColor + addLights;

                #ifdef _EMISSION
                finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
                #endif

                #ifdef _PULSE_ON
                half pulse = BloodPulse(_Time.y, _PulseRate, _PulseStrength);
                finalColor += _PulseEmission.rgb * bloodMask * (pulse - 1.0h);
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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 tangentWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
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
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4((half3)TransformObjectToWorldDir(input.tangentOS.xyz), (half)sign);

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

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS);

                return half4(normalWS, 0.0h);
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

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = lerp(baseTex.rgb * _BaseColor.rgb, bloodCol, bloodMask);
                metaInput.Emission = half3(0, 0, 0);

                #ifdef _EMISSION
                metaInput.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "PBRBloodSplatGUI"
}