Shader "CleanRender/Horror/PBR_Backroom"
{
    Properties
    {
        [Header(Base Triplanar)]
        _BaseMap("Wall Texture", 2D) = "white"{}
        [MainColor]_BaseColor("Color", Color) = (0.72, 0.68, 0.5, 1)
        _BumpMap("Normal Map", 2D) = "bump"{}
        _BumpScale("Normal Scale", Range(0, 2)) = 1.0
        _BaseScale("Triplanar Scale", Range(0.01, 5)) = 0.5
        _TriSharpness("Tri Sharpness", Range(1, 10)) = 4

        [Header(PBR)]
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _SmoothnessBase("Smoothness", Range(0, 1)) = 0.25
        _OcclusionMap("Occlusion(R)", 2D) = "white"{}
        _OcclusionStrength("Occ Str", Range(0, 1)) = 1.0

        [Header(Noise)]
        _NoiseTex("Noise Texture", 2D) = "grey"{}

        [Header(Dirt)][Toggle(_DIRT_ON)]_EnableDirt("Enable Dirt", Float) = 1
        _DirtColor("Color", Color) = (0.25, 0.2, 0.15, 1)
        _DirtScale("Scale", Range(0.05, 3)) = 0.4
        _DirtAmount("Amount", Range(0, 1)) = 0.4
        _DirtRoughness("Dirt Roughness", Range(0, 1)) = 0.85[Header(Water Stain)]
        [Toggle(_WATER_STAIN_ON)]_EnableWaterStain("Wall Stains", Float) = 1
        _StainColor("Color", Color) = (0.35, 0.32, 0.25, 1)
        _StainHeight("Height", Range(0, 1)) = 0.3
        _StainSoftness("Softness", Range(0.01, 0.5)) = 0.15
        _StainNoiseScale("Noise Scale", Range(0.1, 3)) = 0.6
        _StainSmoothness("Stain Smooth", Range(0, 1)) = 0.7[Toggle(_CEILING_STAIN_ON)]_EnableCeilingStain("Ceiling", Float) = 0
        _CeilingStainThreshold("Threshold", Range(0, 1)) = 0.6

        [Header(Blood)]
        [Toggle(_BLOOD_ON)]_EnableBlood("Blood", Float) = 0
        _BloodColor("Color", Color) = (0.4, 0.02, 0.02, 1)
        _BloodScale("Scale", Range(0.05, 3)) = 0.35
        _BloodThreshold("Threshold", Range(0, 1)) = 0.65
        _BloodEdge("Edge", Range(0.01, 0.3)) = 0.08
        _BloodSmoothness("Blood Smooth", Range(0, 1)) = 0.9

        [Header(Flicker)][Toggle(_FLICKER_ON)]_EnableFlicker("Flicker", Float) = 0[HDR]_FlickerColor("Color", Color) = (2, 1.8, 1.5, 1)
        _FlickerSpeed("Speed", Range(0.1, 10)) = 4
        _FlickerMask("Mask(R)", 2D) = "black"{}

        [Header(Emission)]
        [Toggle(_EMISSION)]_EnableEmission("Emission", Float) = 0
        [HDR]_EmissionColor("Color", Color) = (0, 0, 0, 1)

        [Header(Options)][Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 300
        Cull[_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/NoiseLib.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Horror/Shaders/Includes/HorrorLib.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        float _BumpScale;
        float _BaseScale;
        float _TriSharpness;
        float _Metallic;
        float _SmoothnessBase;
        float _OcclusionStrength;
        half4 _DirtColor;
        float _DirtScale;
        float _DirtAmount;
        float _DirtRoughness;
        half4 _StainColor;
        float _StainHeight;
        float _StainSoftness;
        float _StainNoiseScale;
        float _StainSmoothness;
        float _CeilingStainThreshold;
        half4 _BloodColor;
        float _BloodScale;
        float _BloodThreshold;
        float _BloodEdge;
        float _BloodSmoothness;
        half4 _FlickerColor;
        float _FlickerSpeed;
        half4 _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_FlickerMask); SAMPLER(sampler_FlickerMask);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

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

            #pragma shader_feature_local _DIRT_ON
            #pragma shader_feature_local _WATER_STAIN_ON
            #pragma shader_feature_local _CEILING_STAIN_ON
            #pragma shader_feature_local _BLOOD_ON
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

            Varyings Vert(Attributes input)
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
                output.uv.xy = input.uv;

                #if defined(LIGHTMAP_ON)
                output.uv.zw = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #else
                output.uv.zw = 0;
                #endif

                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3 normalWS = normalize(input.normalWS);
                float2 uv = input.uv.xy;

                TriplanarUV tp = ComputeTriplanarUV(positionWS, normalWS, _BaseScale, _TriSharpness);
                half3 albedo = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tp).rgb * _BaseColor.rgb;

                float smoothness = _SmoothnessBase;
                float metallic = _Metallic;
                float occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r, _OcclusionStrength);

                #ifdef _DIRT_ON
                half dirt = DirtOverlay(positionWS, normalWS, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _DirtScale, _DirtAmount);
                albedo = lerp(albedo, _DirtColor.rgb, dirt * 0.6);
                smoothness = lerp(smoothness, _DirtRoughness, dirt);
                #endif

                #ifdef _WATER_STAIN_ON
                half stain = WaterDamageStain(positionWS, normalWS, _StainHeight, _StainSoftness, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _StainNoiseScale);
                albedo = lerp(albedo, _StainColor.rgb, stain * 0.7);
                smoothness = lerp(smoothness, _StainSmoothness, stain);
                #endif

                #ifdef _CEILING_STAIN_ON
                half cStain = CeilingStain(positionWS, normalWS, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _StainNoiseScale * 0.7, _CeilingStainThreshold);
                albedo = lerp(albedo, _StainColor.rgb * 0.8, cStain * 0.5);
                #endif

                #ifdef _BLOOD_ON
                half blood = BloodSplatTriplanar(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _BloodScale, _TriSharpness, _BloodThreshold, _BloodEdge);
                albedo = lerp(albedo, _BloodColor.rgb, blood);
                smoothness = lerp(smoothness, _BloodSmoothness, blood);
                #endif

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 bitangent = cross(normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 N = normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * normalWS);

                float3 V = normalize(GetCameraPositionWS() - positionWS);
                half pR = 1.0h - (half)smoothness;
                half r = max(pR * pR, HALF_MIN_SQRT);
                half r2 = max(r * r, HALF_MIN);
                half3 F0 = lerp(half3(0.04, 0.04, 0.04), albedo, (half)metallic);
                half omr = (1.0h - (half)metallic) * 0.96h;
                half3 diffAlbedo = albedo * omr;

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                float4 shadowCoord = ComputeScreenPos(input.positionCS);
                #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                #else
                float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half3 L = (half3)mainLight.direction;
                half3 H = normalize(L + (half3)V);

                half NdotL = saturate(dot(N, L));
                half NdotV = saturate(dot(N, (half3)V));
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
                    Light light = GetAdditionalLight(j, positionWS);
                    half atten = (half)(light.distanceAttenuation * light.shadowAttenuation);
                    additionalLightsColor += (half3)light.color * atten * saturate(dot(N, (half3)light.direction)) * diffAlbedo;
                }
                #endif

                half3 finalColor = directColor + indirectColor + additionalLightsColor;

                #ifdef _EMISSION
                finalColor += _EmissionColor.rgb;
                #endif

                #ifdef _FLICKER_ON
                finalColor += _FlickerColor.rgb * SAMPLE_TEXTURE2D(_FlickerMask, sampler_FlickerMask, uv).r * FlickerIntensity(_Time.y, _FlickerSpeed);
                #endif

                return half4(MixFog(finalColor, input.fogFactor), 1.0);
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

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
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
                output.uv = input.uv;

                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 lightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings MetaVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.lightmapUV, input.dynamicLightmapUV);
                return output;
            }

            half4 MetaFrag(Varyings input) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = _BaseColor.rgb;
                metaInput.Emission = half3(0, 0, 0);

                #ifdef _EMISSION
                metaInput.Emission = _EmissionColor.rgb;
                #endif

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "PBRBackroomGUI"
}