Shader "CleanRender/Horror/ToonBackroom"
{
    Properties
    {
        [Header(Base Triplanar)]
        _BaseMap("Base Wall Texture", 2D) = "white"{}[MainColor]_BaseColor("Base Color (Walls)", Color) = (0.72, 0.68, 0.5, 1)
        _BaseScale("Triplanar Scale", Range(0.01, 5)) = 0.5
        _TriSharpness("Triplanar Sharpness", Range(1, 10)) = 4

        [Header(Noise Texture)]
        _NoiseTex("Noise Texture", 2D) = "grey"{}

        [Header(Dirt and Grime)]
        [Toggle(_DIRT_ON)]_EnableDirt("Enable Dirt", Float) = 1
        _DirtColor("Dirt Color", Color) = (0.25, 0.2, 0.15, 1)
        _DirtScale("Dirt Scale", Range(0.05, 3)) = 0.4
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.4
        _NormalStrength("Normal-from-Noise Str", Range(0, 5)) = 1.5

        [Header(Water Damage)]
        [Toggle(_WATER_STAIN_ON)]_EnableWaterStain("Enable Water Stains", Float) = 1
        _StainColor("Stain Color", Color) = (0.35, 0.32, 0.25, 1)
        _StainHeight("Stain Height", Range(0, 1)) = 0.3
        _StainSoftness("Stain Softness", Range(0.01, 0.5)) = 0.15
        _StainNoiseScale("Stain Noise Scale", Range(0.1, 3)) = 0.6
        [Toggle(_CEILING_STAIN_ON)]_EnableCeilingStain("Ceiling Stain", Float) = 0
        _CeilingStainThreshold("Ceiling Threshold", Range(0, 1)) = 0.6[Header(Blood Overlay)]
        [Toggle(_BLOOD_ON)]_EnableBlood("Enable Blood", Float) = 0
        _BloodColor("Blood Color", Color) = (0.4, 0.02, 0.02, 1)
        _BloodScale("Blood Scale", Range(0.05, 3)) = 0.35
        _BloodThreshold("Blood Threshold", Range(0, 1)) = 0.65
        _BloodEdge("Blood Edge", Range(0.01, 0.3)) = 0.08

        [Header(Flicker)][Toggle(_FLICKER_ON)]_EnableFlicker("Enable Flicker", Float) = 0
        [HDR]_FlickerColor("Flicker Color", Color) = (2, 1.8, 1.5, 1)
        _FlickerSpeed("Flicker Speed", Range(0.1, 10)) = 4
        _FlickerMask("Flicker Mask (R=emissive)", 2D) = "black"{}

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.18, 0.15, 0.12, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.45
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.05
        _RimColor("Rim Color", Color) = (0.4, 0.38, 0.3, 0.3)
        _RimPower("Rim Power", Range(0.1, 10)) = 4

        [Header(Emission)][Toggle(_EMISSION)]_Emission("Emission", Float) = 0
        [HDR]_EmissionColor("Emission Color", Color) = (0, 0, 0)

        [Header(Options)]
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
        half4 _DirtColor;
        float _DirtScale;
        float _DirtAmount;
        float _NormalStrength;
        half4 _StainColor;
        float _StainHeight;
        float _StainSoftness;
        float _StainNoiseScale;
        float _CeilingStainThreshold;
        half4 _BloodColor;
        float _BloodScale;
        float _BloodThreshold;
        float _BloodEdge;
        half4 _FlickerColor;
        float _FlickerSpeed;
        half4 _ShadowColor;
        half4 _RimColor;
        float _Threshold;
        float _Smoothness;
        float _RimPower;
        half4 _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
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
                half3 albedo = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tpBase).rgb * _BaseColor.rgb;

                #ifdef _DIRT_ON
                half dirt = DirtOverlay(positionWS, normalWS, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _DirtScale, _DirtAmount);
                albedo = lerp(albedo, _DirtColor.rgb, dirt * 0.6);
                #endif

                #ifdef _WATER_STAIN_ON
                half stain = WaterDamageStain(positionWS, normalWS, _StainHeight, _StainSoftness, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _StainNoiseScale);
                albedo = lerp(albedo, _StainColor.rgb, stain * 0.7);
                #endif

                #ifdef _CEILING_STAIN_ON
                half cStain = CeilingStain(positionWS, normalWS, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), _StainNoiseScale * 0.7, _CeilingStainThreshold);
                albedo = lerp(albedo, _StainColor.rgb * 0.8, cStain * 0.5);
                #endif

                #ifdef _BLOOD_ON
                half blood = BloodSplatTriplanar(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _BloodScale, _TriSharpness, _BloodThreshold, _BloodEdge);
                albedo = lerp(albedo, _BloodColor.rgb, blood);
                #endif

                half3 finalNormalWS = normalWS;
                #ifdef _DIRT_ON
                finalNormalWS = TriplanarNormalFromNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _DirtScale * 2.0, _TriSharpness, _NormalStrength);
                #endif

                float3 V = normalize(GetCameraPositionWS() - positionWS);
                ToonLightResult lit = ComputeToonMainLight(positionWS, finalNormalWS, V, albedo, input.uv.zw, _Threshold, _Smoothness, _ShadowColor.rgb, _RimColor.rgb, _RimPower, _RimColor.a, 1.0);
                half3 finalColor = lit.diffuse + lit.rim + lit.globalIllumination;

                #ifdef _ADDITIONAL_LIGHTS
                finalColor += ComputeToonAdditionalLights(positionWS, finalNormalWS, albedo, _Threshold, _Smoothness);
                #endif

                #ifdef _EMISSION
                finalColor += _EmissionColor.rgb;
                #endif

                #ifdef _FLICKER_ON
                half fMask = SAMPLE_TEXTURE2D(_FlickerMask, sampler_FlickerMask, input.uv.xy).r;
                half flicker = FlickerIntensity(_Time.y, _FlickerSpeed);
                finalColor += _FlickerColor.rgb * fMask * flicker;
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
    CustomEditor "ToonBackroomGUI"
}