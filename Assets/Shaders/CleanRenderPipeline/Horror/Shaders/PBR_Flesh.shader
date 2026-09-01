Shader "CleanRender/Horror/PBR_Flesh"
{
    Properties
    {
        [MainTexture]_BaseMap("Base Map", 2D) = "white"{}
        [MainColor]_BaseColor("Flesh Color", Color) = (0.7, 0.4, 0.35, 1)
        _BumpMap("Normal Map", 2D) = "bump"{}
        _BumpScale("Normal Scale", Range(0, 2)) = 1.0[Header(PBR)]
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _SmoothnessBase("Smoothness", Range(0, 1)) = 0.55
        _OcclusionMap("Occlusion (R)", 2D) = "white"{}
        _OcclusionStrength("Occlusion Str", Range(0, 1)) = 1.0

        [Header(Noise)]
        _NoiseTex("Noise Texture", 2D) = "grey"{}
        _NoiseScale("Noise Tiling", Range(0.1, 20)) = 6[Header(Veins)]
        [Toggle(_VEINS_ON)]_EnableVeins("Enable Veins", Float) = 1
        _VeinColor("Vein Color", Color) = (0.3, 0.05, 0.1, 1)
        _VeinScale("Vein Scale", Range(1, 30)) = 10
        _VeinThickness("Vein Thickness", Range(0.01, 0.3)) = 0.08

        [Header(SSS)][Toggle(_SSS_ON)]_EnableSSS("Enable Fake SSS", Float) = 1
        _SSSColor("SSS Color", Color) = (0.85, 0.2, 0.08, 1)
        _SSSStrength("SSS Strength", Range(0, 3)) = 1.5
        _SSSDistortion("SSS Distortion", Range(0, 1)) = 0.5

        [Header(Pulse)]
        [Toggle(_PULSE_ON)]_EnablePulse("Enable Pulsation", Float) = 1
        _PulseSpeed("Speed", Range(0.1, 5)) = 1.0
        _PulseScale("Noise Scale", Range(0.1, 5)) = 1.0
        _PulseAmplitude("Displacement", Range(0, 0.1)) = 0.012
        [HDR]_PulseEmission("Emission", Color) = (1, 0.1, 0.05, 1)
        _PulseEmissionStrength("Emission Str", Range(0, 3)) = 0.5

        [Header(Emission)][Toggle(_EMISSION)]_EnableEmission("Emission", Float) = 0
        [HDR]_EmissionColor("Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Map", 2D) = "white"{}

        [Header(Options)][Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2[Toggle(_ALPHATEST_ON)]_AlphaClip("Alpha Clip", Float) = 0
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
        float _NoiseScale;
        half4 _VeinColor;
        float _VeinScale;
        float _VeinThickness;
        half4 _SSSColor;
        float _SSSStrength;
        float _SSSDistortion;
        float _PulseSpeed;
        float _PulseScale;
        float _PulseAmplitude;
        half4 _PulseEmission;
        float _PulseEmissionStrength;
        half4 _EmissionColor;
        float _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex FleshPBRVert
            #pragma fragment FleshPBRFrag

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
            #pragma shader_feature_local _VEINS_ON
            #pragma shader_feature_local _SSS_ON
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
                half pulseValue : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings FleshPBRVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.pulseValue = 0;

                #ifdef _PULSE_ON
                float pulse = OrganicPulse(positionWS, _Time.y, _PulseSpeed, _PulseScale, _PulseAmplitude);
                positionWS += normalWS * pulse;
                output.pulseValue = (half)pulse;
                #endif

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = (half3)normalWS;

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

            half4 FleshPBRFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv.xy;
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);

                #ifdef _ALPHATEST_ON
                clip(baseTex.a * _BaseColor.a - _Cutoff);
                #endif

                half3 albedo = baseTex.rgb * _BaseColor.rgb;

                #ifdef _VEINS_ON
                half vein = VeinPattern(uv, _VeinScale, _VeinThickness, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex));
                albedo = lerp(albedo, _VeinColor.rgb, vein * 0.7);
                #endif

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 N = normalize(normalTS.x * input.tangentWS.xyz + normalTS.y * bitangent + normalTS.z * input.normalWS);

                float3 V = normalize(GetCameraPositionWS() - input.positionWS);
                half NdotV = saturate(dot(N, (half3)V));

                float metallic = _Metallic;
                float smoothness = _SmoothnessBase;
                float occlusion = lerp(1.0, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r, _OcclusionStrength);

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

                #ifdef _SSS_ON
                directColor += FakeSSS(NdotL, NdotV, _SSSColor.rgb, _SSSStrength, _SSSDistortion, (float3)N, (float3)L, V) * attenuation;
                #endif

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
                #ifdef _PULSE_ON
                half beat = BloodPulse(_Time.y, _PulseSpeed * 0.8, 1.0);
                emission += _PulseEmission.rgb * beat * _PulseEmissionStrength * saturate(input.pulseValue / max(_PulseAmplitude, 1e-4));
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
            #pragma shader_feature_local _PULSE_ON

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

                #ifdef _PULSE_ON
                positionWS += normalWS * OrganicPulse(positionWS, _Time.y, _PulseSpeed, _PulseScale, _PulseAmplitude);
                #endif

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
            #pragma shader_feature_local _PULSE_ON

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

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                #ifdef _PULSE_ON
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * OrganicPulse(positionWS, _Time.y, _PulseSpeed, _PulseScale, _PulseAmplitude);
                #endif

                output.positionCS = TransformWorldToHClip(positionWS);

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
    CustomEditor "PBRFleshGUI"
}