Shader "CleanRender/Horror/ToonBloodTriplanar"
{
    Properties
    {
        [Header(Base Surface Triplanar)]
        _BaseMap("Base Texture", 2D) = "white"{}
        [MainColor]_BaseColor("Base Color", Color) = (0.6, 0.58, 0.52, 1)
        _BaseScale("Base Triplanar Scale", Range(0.01, 5)) = 0.5
        _TriSharpness("Triplanar Sharpness", Range(1, 10)) = 4[Header(Blood Triplanar)]
        _BloodColor("Fresh Blood", Color) = (0.5, 0.02, 0.02, 1)
        _DriedBloodColor("Dried Blood", Color) = (0.2, 0.02, 0.0, 1)
        _NoiseTex("Noise Texture", 2D) = "grey"{}
        _BloodScale("Blood Noise Scale", Range(0, 5)) = 0.3
        _BloodThreshold("Splat Threshold", Range(0, 1)) = 0.55
        _BloodEdge("Splat Edge Softness", Range(0.01, 0.3)) = 0.08
        _BloodWetness("Wetness", Range(0, 1)) = 0.8
        _NormalStrength("Normal-from-Noise Str", Range(0, 5)) = 2.0

        [Header(Drip Walls Only)]
        [Toggle(_DRIP_ON)]_EnableDrip("Enable Drips", Float) = 1
        _DripSpeed("Drip Speed", Range(0, 2)) = 0.25
        _DripScale("Drip Scale", Range(0.1, 3)) = 0.8

        [Header(Pulse)]
        [Toggle(_PULSE_ON)]_EnablePulse("Enable Pulse", Float) = 0
        _PulseRate("Pulse Rate", Range(0.5, 3)) = 1.2
        _PulseStrength("Pulse Strength", Range(0, 0.5)) = 0.15
        [HDR]_PulseEmission("Pulse Emission", Color) = (1, 0.05, 0.05, 1)

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.15, 0.05, 0.1, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.04
        _RimColor("Rim Color", Color) = (0.8, 0.1, 0.1, 0.5)
        _RimPower("Rim Power", Range(0.1, 10)) = 3

        [Header(Emission)]
        [Toggle(_EMISSION)]_Emission("Emission", Float) = 0
        [HDR]_EmissionColor("Emission Color", Color) = (0, 0, 0)
        _EmissionMap("Emission Map", 2D) = "white"{}

        [Header(Options)][Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2[Toggle(_ALPHATEST_ON)]_AlphaClip("Alpha Clip", Float) = 0
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
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/NoiseLib.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Horror/Shaders/Includes/HorrorLib.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        float _BaseScale;
        float _TriSharpness;
        half4 _BloodColor;
        half4 _DriedBloodColor;
        float _BloodScale;
        float _BloodThreshold;
        float _BloodEdge;
        float _BloodWetness;
        float _NormalStrength;
        float _DripSpeed;
        float _DripScale;
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
        TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
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

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                output.uv.xy = input.uv;
                output.uv.zw = ToonTransformLightmapUV(input.lightmapUV);
                output.fogFactor = (half)ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3 normalWS = normalize(input.normalWS);

                TriplanarUV tpBase = ComputeTriplanarUV(positionWS, normalWS, _BaseScale, _TriSharpness);
                half3 baseTex = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tpBase).rgb;
                half3 baseAlbedo = baseTex * _BaseColor.rgb;

                half bloodMask = BloodSplatTriplanar(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _BloodScale, _TriSharpness, _BloodThreshold, _BloodEdge);

                #ifdef _DRIP_ON
                half drip = BloodDripTriplanar(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _DripScale, _TriSharpness, _DripSpeed, _Time.y);
                bloodMask = saturate(bloodMask + drip * 0.6);
                #endif

                half3 bloodCol = BloodColorGradient(_BloodWetness, _BloodColor.rgb, _DriedBloodColor.rgb);
                half3 albedo = lerp(baseAlbedo, bloodCol, bloodMask);

                half3 perturbedN = TriplanarNormalFromNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _BloodScale * 2.0, _TriSharpness, _NormalStrength);
                half3 finalNormalWS = normalize(lerp(normalWS, perturbedN, bloodMask));

                float3 viewDirWS = normalize(GetCameraPositionWS() - positionWS);
                ToonLightResult lit = ComputeToonMainLight(positionWS, finalNormalWS, viewDirWS, albedo, input.uv.zw, _Threshold, _Smoothness, _ShadowColor.rgb, _RimColor.rgb, _RimPower, _RimColor.a, 1.0);
                half3 finalColor = lit.diffuse + lit.rim + lit.globalIllumination;

                #ifdef _ADDITIONAL_LIGHTS
                finalColor += ComputeToonAdditionalLights(positionWS, finalNormalWS, albedo, _Threshold, _Smoothness);
                #endif

                #ifdef _EMISSION
                finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv.xy).rgb * _EmissionColor.rgb;
                #endif

                #ifdef _PULSE_ON
                half pulse = BloodPulse(_Time.y, _PulseRate, _PulseStrength);
                finalColor += _PulseEmission.rgb * bloodMask * (pulse - 1.0);
                #endif

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0);
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
            #pragma shader_feature_local _EMISSION
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
                output.uv = input.uv0;
                return output;
            }

            half4 MetaFrag(Varyings input) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = _BaseColor.rgb;
                metaInput.Emission = half3(0, 0, 0);

                #ifdef _EMISSION
                metaInput.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
    CustomEditor "ToonBloodTriplanarGUI"
}