Shader "CleanRender/Horror/PBR_HorrorMetal"
{
    Properties
    {
        [MainTexture]_BaseMap("Base Map", 2D) = "white"{}[MainColor]_BaseColor("Metal Color", Color) = (0.4, 0.38, 0.35, 1)
        _BumpMap("Normal Map", 2D) = "bump"{}
        _BumpScale("Normal Scale", Range(0, 2)) = 1.0
        _MetallicGlossMap("Metallic(R) Smooth(A)", 2D) = "white"{}

        [Header(PBR)]
        _Metallic("Metallic", Range(0, 1)) = 0.85
        _SmoothnessBase("Smoothness", Range(0, 1)) = 0.55
        _OcclusionMap("Occlusion (R)", 2D) = "white"{}
        _OcclusionStrength("Occlusion Str", Range(0, 1)) = 1.0

        [Header(Rust)]
        [Toggle(_RUST_ON)]_EnableRust("Enable Rust", Float) = 1
        _NoiseTex("Noise Texture", 2D) = "grey"{}
        _RustColor("Rust Color", Color) = (0.5, 0.2, 0.06, 1)
        _RustColor2("Deep Rust", Color) = (0.28, 0.1, 0.03, 1)
        _RustThreshold("Rust Amount", Range(0, 1)) = 0.5
        _RustScale("Rust Scale", Range(0.1, 10)) = 2
        _RustSmoothness("Rust Smoothness", Range(0, 1)) = 0.15
        _RustMetallic("Rust Metallic", Range(0, 1)) = 0.1
        _EdgeWear("Edge Corrosion", Range(0, 1)) = 0.35
        _EdgeSharpness("Edge Sharpness", Range(0.01, 0.5)) = 0.12

        [Header(Cracks)][Toggle(_CRACKS_ON)]_EnableCracks("Enable Cracks", Float) = 1
        _CrackScale("Scale", Range(0.5, 20)) = 6
        _CrackSharpness("Width", Range(0.01, 0.3)) = 0.06
        [HDR]_CrackGlow("Glow (HDR)", Color) = (5, 1.5, 0.2, 1)
        _CrackPulseRate("Pulse Rate", Range(0.1, 5)) = 0.8

        [Header(Flicker)]
        [Toggle(_FLICKER_ON)]_EnableFlicker("Enable Flicker", Float) = 0
        [HDR]_FlickerColor("Color (HDR)", Color) = (8, 0.3, 0.0, 1)
        _FlickerSpeed("Speed", Range(0.1, 10)) = 3
        _FlickerMask("Mask (R)", 2D) = "black"{}[Header(Emission)]
        [Toggle(_EMISSION)]_EnableEmission("Emission", Float) = 0[HDR]_EmissionColor("Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Map", 2D) = "white"{}

        [Header(Options)]
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2
        [Toggle(_ALPHATEST_ON)]_AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Cutoff", Range(0, 1)) = 0.5
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
        half4 _RustColor;
        half4 _RustColor2;
        float _RustThreshold;
        float _RustScale;
        float _RustSmoothness;
        float _RustMetallic;
        float _EdgeWear;
        float _EdgeSharpness;
        float _CrackScale;
        float _CrackSharpness;
        half4 _CrackGlow;
        float _CrackPulseRate;
        half4 _FlickerColor;
        float _FlickerSpeed;
        half4 _EmissionColor;
        float _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_FlickerMask); SAMPLER(sampler_FlickerMask);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex PBRMetalVert
            #pragma fragment PBRMetalFrag

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
            #pragma shader_feature_local _RUST_ON
            #pragma shader_feature_local _CRACKS_ON
            #pragma shader_feature_local _FLICKER_ON
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

            Varyings PBRMetalVert(Attributes input)
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

            half4 PBRMetalFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv.xy;
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

                #ifdef _ALPHATEST_ON
                clip(baseTex.a * _BaseColor.a - _Cutoff);
                #endif

                half3 albedo = baseTex.rgb * _BaseColor.rgb;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 N = normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS);

                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                half NdotV = saturate(dot(N, (half3)V));

                half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                float metallic = mg.r * _Metallic;
                float smoothness = mg.a * _SmoothnessBase;
                float occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r, _OcclusionStrength);

                half rustMask = 0;
                #ifdef _RUST_ON
                half tr = RustMask(uv, _RustScale, _RustThreshold, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex));
                half er = EdgeCorrosion(NdotV, _EdgeWear, _EdgeSharpness);
                rustMask = saturate(tr + er);
                albedo = ApplyRust(albedo, lerp(_RustColor.rgb, _RustColor2.rgb, tr), rustMask);
                smoothness = lerp(smoothness, _RustSmoothness, rustMask);
                metallic = lerp(metallic, _RustMetallic, rustMask);
                occlusion = lerp(occlusion, occlusion * 0.6, rustMask);
                #endif

                half pR = 1.0h - (half)smoothness;
                half r = max(pR * pR, HALF_MIN_SQRT);
                half r2 = max(r * r, HALF_MIN);
                half3 F0 = lerp(half3(0.04, 0.04, 0.04), albedo, (half)metallic);
                half omr = (1.0h - (half)metallic) * 0.96h;
                half3 diffAlbedo = albedo * omr;

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                float4 shadowCoord = ComputeScreenPos(input.positionCS);
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half3 L = (half3)mainLight.direction;
                half3 H = normalize(L + (half3)V);

                half NdotL = saturate(dot(N, L));
                half NdotH = saturate(dot(N, H));
                half VdotH = saturate(dot((half3)V, H));

                half3 F = F0 + (1.0h - F0) * pow(1.0h - VdotH, 5.0h);
                half dn = NdotH * NdotH * (r2 - 1.0h) + 1.0h;
                half D = r2 / (PI * dn * dn + 1e-7h);
                half k = r * 0.5h;
                half visL = NdotL / (NdotL * (1.0h - k) + k + 1e-5h);
                half visV = NdotV / (NdotV * (1.0h - k) + k + 1e-5h);

                half3 specular = D * visL * visV * F;
                half attenuation = (half)(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 radiance = (half3)mainLight.color * attenuation * NdotL;
                half3 directColor = (diffAlbedo + specular) * radiance;

                half3 indirectDiffuse;
                #if defined(LIGHTMAP_ON)
                indirectDiffuse = SampleLightmap(input.uv.zw, 0.0, N);
                #else
                indirectDiffuse = SampleSH(N);
                #endif

                half3 indirectColor = indirectDiffuse * diffAlbedo * occlusion;
                half3 additionalLightsColor = half3(0, 0, 0);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                UNITY_LOOP
                for (uint j = 0u; j < lightCount; j + + )
                {
                    Light light = GetAdditionalLight(j, input.positionWS);
                    half atten = (half)(light.distanceAttenuation * light.shadowAttenuation);
                    additionalLightsColor += (half3)light.color * atten * saturate(dot(N, (half3)light.direction)) * diffAlbedo;
                }
                #endif

                half3 finalColor = directColor + indirectColor + additionalLightsColor;
                half3 emission = half3(0, 0, 0);

                #ifdef _CRACKS_ON
                emission += GlowingCracks(uv, _CrackScale, _CrackSharpness, _CrackGlow.rgb, _Time.y, _CrackPulseRate, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex));
                #endif

                #ifdef _FLICKER_ON
                emission += _FlickerColor.rgb * SAMPLE_TEXTURE2D(_FlickerMask, sampler_FlickerMask, uv).r * FlickerIntensity(_Time.y, _FlickerSpeed);
                #endif

                #ifdef _EMISSION
                emission += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
                #endif

                finalColor += emission;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, baseTex.a * _BaseColor.a);
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
                return half4(normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS), 0.0h);
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
                float2 lightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings MetaVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.lightmapUV, input.dynamicLightmapUV);
                output.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return output;
            }

            half4 MetaFrag(Varyings input) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                #ifdef _ALPHATEST_ON
                clip(baseTex.a * _BaseColor.a - _Cutoff);
                #endif

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = baseTex.rgb * _BaseColor.rgb;
                metaInput.Emission = half3(0, 0, 0);

                #ifdef _EMISSION
                metaInput.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "PBRHorrorMetalGUI"
}