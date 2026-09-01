Shader "CleanRender/Horror/ToonFlesh"
{
    Properties
    {
        [Header(Base Triplanar)]
        _BaseMap("Base Map", 2D) = "white"{}
        [MainColor]_BaseColor("Flesh Color", Color) = (0.65, 0.35, 0.3, 1)
        _BaseScale("Triplanar Scale", Range(0.01, 5)) = 0.5
        _TriSharpness("Triplanar Sharpness", Range(1, 10)) = 4

        [Header(Noise)]
        _NoiseTex("Noise (Normal)", 2D) = "grey"{}
        _NormalStrength("Normal Str", Range(0, 5)) = 2.5

        [Header(Veins)]
        [Toggle(_VEINS_ON)]_EnableVeins("Veins", Float) = 1
        _VeinColor("Vein Color", Color) = (0.3, 0.05, 0.1, 1)
        _VeinScale("Scale", Range(0.1, 3)) = 0.5
        _VeinThickness("Thickness", Range(0.01, 0.3)) = 0.08

        [Header(SSS)]
        [Toggle(_SSS_ON)]_EnableSSS("Fake SSS", Float) = 1
        _SSSColor("Color", Color) = (0.8, 0.15, 0.05, 1)
        _SSSStrength("Strength", Range(0, 3)) = 1.2
        _SSSDistortion("Distortion", Range(0, 1)) = 0.5

        [Header(Pulse)][Toggle(_PULSE_ON)]_EnablePulse("Pulsation", Float) = 1
        _PulseSpeed("Speed", Range(0.1, 5)) = 1.0
        _PulseScale("Noise Scale", Range(0.1, 5)) = 1.0
        _PulseAmplitude("Displacement", Range(0, 0.1)) = 0.015
        [HDR]_PulseEmission("Emission", Color) = (0.6, 0.05, 0.02, 1)
        _PulseEmissionStrength("Em Str", Range(0, 3)) = 0.5

        [Header(Cel)]
        _ShadowColor("Shadow", Color) = (0.15, 0.05, 0.08, 1)
        _Threshold("Threshold", Range(0, 1)) = 0.4
        _Smoothness("Smoothness", Range(0.001, 0.5)) = 0.06
        _RimColor("Rim", Color) = (0.7, 0.2, 0.15, 0.5)
        _RimPower("Power", Range(0.1, 10)) = 3[Header(Options)]
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 200
        Cull[_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/NoiseLib.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Horror/Shaders/Includes/HorrorLib.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        float _BaseScale;
        float _TriSharpness;
        float _NormalStrength;
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
        half4 _ShadowColor;
        half4 _RimColor;
        float _Threshold;
        float _Smoothness;
        float _RimPower;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
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

            #pragma shader_feature_local _VEINS_ON
            #pragma shader_feature_local _SSS_ON
            #pragma shader_feature_local _PULSE_ON

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
                float2 lightmapUV : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half pulseValue : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
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
                output.lightmapUV = ToonTransformLightmapUV(input.lightmapUV);
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3 normalWS = normalize(input.normalWS);

                TriplanarUV tp = ComputeTriplanarUV(positionWS, normalWS, _BaseScale, _TriSharpness);
                half3 albedo = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tp).rgb * _BaseColor.rgb;

                #ifdef _VEINS_ON
                half vein = VeinPatternTriplanar(positionWS, normalWS, _VeinScale, _TriSharpness, _VeinThickness, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex));
                albedo = lerp(albedo, _VeinColor.rgb, vein * 0.7);
                #endif

                half3 N = TriplanarNormalFromNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _BaseScale * 2.0, _TriSharpness, _NormalStrength);
                float3 V = normalize(GetCameraPositionWS() - positionWS);

                float4 shadowCoord = ToonGetShadowCoordSimple(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = mainLight.direction;

                half NdotL = dot(N, (half3)L);
                half NdotV = dot(N, (half3)V);
                half shadowAtten = (half)mainLight.shadowAttenuation;

                half intensity = ToonCelRamp(NdotL * shadowAtten, _Threshold, _Smoothness);
                half3 diffuseColor = albedo * lerp(_ShadowColor.rgb, (half3)mainLight.color, intensity);

                diffuseColor += _RimColor.rgb * ToonRimFactor(NdotV, _RimPower, intensity) * _RimColor.a;

                #ifdef _SSS_ON
                diffuseColor += FakeSSS(NdotL, NdotV, _SSSColor.rgb, _SSSStrength, _SSSDistortion, (float3)N, L, V);
                #endif

                diffuseColor += ToonSampleBakedGI(input.lightmapUV, N) * albedo;

                #ifdef _ADDITIONAL_LIGHTS
                diffuseColor += ComputeToonAdditionalLights(positionWS, N, albedo, _Threshold, _Smoothness);
                #endif

                half3 emissionColor = half3(0, 0, 0);

                #ifdef _PULSE_ON
                half beat = BloodPulse(_Time.y, _PulseSpeed * 0.8, 1.0);
                emissionColor += _PulseEmission.rgb * beat * _PulseEmissionStrength * saturate(input.pulseValue / max(_PulseAmplitude, 1e-4));
                #endif

                return half4(MixFog(diffuseColor + emissionColor, input.fogFactor), 1.0);
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
            #pragma shader_feature_local _PULSE_ON

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

                #ifdef _PULSE_ON
                positionWS += normalWS * OrganicPulse(positionWS, _Time.y, _PulseSpeed, _PulseScale, _PulseAmplitude);
                #endif

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
            #pragma shader_feature_local _PULSE_ON

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
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
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
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
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "ToonFleshGUI"
}