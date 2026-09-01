Shader "CleanRender/Horror/ToonHorrorMetal"
{
    Properties
    {
        [Header(Base Triplanar)]
        _BaseMap("Base Map", 2D) = "white"{}
        [MainColor]_BaseColor("Metal Color", Color) = (0.35, 0.32, 0.3, 1)
        _BaseScale("Triplanar Scale", Range(0.01, 5)) = 0.5
        _TriSharpness("Triplanar Sharpness", Range(1, 10)) = 4

        [Header(Noise)]
        _NoiseTex("Noise Texture (Normal)", 2D) = "grey"{}
        _NormalStrength("Normal Strength", Range(0, 5)) = 2.0

        [Header(Rust Triplanar)]
        [Toggle(_RUST_ON)]_EnableRust("Enable Rust", Float) = 1
        _RustColor("Rust Color", Color) = (0.45, 0.18, 0.05, 1)
        _RustColor2("Deep Rust", Color) = (0.25, 0.08, 0.02, 1)
        _RustThreshold("Rust Amount", Range(0, 1)) = 0.5
        _RustScale("Rust Scale", Range(0.1, 5)) = 0.4
        _EdgeWear("Edge Corrosion", Range(0, 1)) = 0.4
        _EdgeSharpness("Edge Sharpness", Range(0.01, 0.5)) = 0.15

        [Header(Cracks)]
        [Toggle(_CRACKS_ON)]_EnableCracks("Enable Cracks", Float) = 1
        _CrackScale("Scale", Range(0.1, 5)) = 1.0
        _CrackSharpness("Width", Range(0.01, 0.3)) = 0.06
        [HDR]_CrackGlow("Glow (HDR)", Color) = (3, 0.5, 0.1, 1)
        _CrackPulseRate("Pulse Rate", Range(0.1, 5)) = 0.8[Header(Flicker)]
        [Toggle(_FLICKER_ON)]_EnableFlicker("Flicker", Float) = 0
        [HDR]_FlickerColor("Color", Color) = (5, 0.2, 0.0, 1)
        _FlickerSpeed("Speed", Range(0.1, 10)) = 3
        _FlickerMask("Mask (R)", 2D) = "black"{}

        [Header(Metal Highlight)]
        _MetalColor("Fresnel Color", Color) = (0.6, 0.55, 0.5, 1)
        _MetalCutoff("Cutoff", Range(0, 1)) = 0.5
        _MetalSmoothness("Smoothness", Range(0, 0.3)) = 0.05
        _SpecColor("Spec Color", Color) = (0.8, 0.75, 0.7, 1)
        _SpecCutoff("Spec Cutoff", Range(0, 1)) = 0.88
        _SpecSmoothness("Spec Smooth", Range(0, 0.2)) = 0.03

        [Header(Cel Shading)]
        _ShadowColor("Shadow", Color) = (0.1, 0.08, 0.12, 1)
        _Threshold("Threshold", Range(0, 1)) = 0.4
        _Smoothness("Smoothness", Range(0.001, 0.5)) = 0.04
        _RimColor("Rim", Color) = (0.5, 0.3, 0.2, 0.4)
        _RimPower("Rim Power", Range(0.1, 10)) = 4

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
        float _NormalStrength;
        half4 _RustColor;
        half4 _RustColor2;
        float _RustThreshold;
        float _RustScale;
        float _EdgeWear;
        float _EdgeSharpness;
        float _CrackScale;
        float _CrackSharpness;
        half4 _CrackGlow;
        float _CrackPulseRate;
        half4 _FlickerColor;
        float _FlickerSpeed;
        half4 _MetalColor;
        float _MetalCutoff;
        float _MetalSmoothness;
        half4 _SpecColor;
        float _SpecCutoff;
        float _SpecSmoothness;
        half4 _ShadowColor;
        half4 _RimColor;
        float _Threshold;
        float _Smoothness;
        float _RimPower;
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

            #pragma shader_feature_local _RUST_ON
            #pragma shader_feature_local _CRACKS_ON
            #pragma shader_feature_local _FLICKER_ON

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

                TriplanarUV tp = ComputeTriplanarUV(positionWS, normalWS, _BaseScale, _TriSharpness);
                half3 albedo = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), tp).rgb * _BaseColor.rgb;

                half3 N = TriplanarNormalFromNoise(TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex), positionWS, normalWS, _BaseScale * 2.0, _TriSharpness, _NormalStrength);
                float3 V = normalize(GetCameraPositionWS() - positionWS);
                float NdotV = saturate(dot(N, (half3)V));

                half rustMask = 0;

                #ifdef _RUST_ON
                half tr = RustMaskTriplanar(positionWS, normalWS, _RustScale, _TriSharpness, _RustThreshold, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex));
                half er = EdgeCorrosion(NdotV, _EdgeWear, _EdgeSharpness);
                rustMask = saturate(tr + er);
                albedo = ApplyRust(albedo, lerp(_RustColor.rgb, _RustColor2.rgb, tr), rustMask);
                #endif

                float4 shadowCoord = ToonGetShadowCoordSimple(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = mainLight.direction;
                half NdotL = dot(N, (half3)L);
                half shadowAtten = (half)mainLight.shadowAttenuation;

                half intensity = (half)smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, NdotL * shadowAtten);
                half3 diffuseColor = albedo * lerp(_ShadowColor.rgb, (half3)mainLight.color, intensity);

                half metalFactor = 1.0h - rustMask;
                half fresnel = (half)smoothstep(_MetalCutoff - _MetalSmoothness, _MetalCutoff + _MetalSmoothness, NdotV);
                half3 metalHighlight = lerp(albedo * _ShadowColor.rgb, _MetalColor.rgb, fresnel);
                diffuseColor = lerp(diffuseColor, metalHighlight * lerp(_ShadowColor.rgb, (half3)mainLight.color, intensity), metalFactor * 0.5);

                float3 H = normalize(L + V);
                half NdotH = saturate(dot(N, (half3)H));
                diffuseColor += _SpecColor.rgb * (half)smoothstep(_SpecCutoff - _SpecSmoothness, _SpecCutoff + _SpecSmoothness, NdotH) * shadowAtten * metalFactor;

                half rimV = 1.0h - (half)NdotV;
                diffuseColor += _RimColor.rgb * (half)smoothstep(1.0 - rcp(max(_RimPower, 1e-4)), 1.0, rimV * intensity) * _RimColor.a;

                diffuseColor += ToonSampleBakedGI(input.uv.zw, N) * albedo;

                #ifdef _ADDITIONAL_LIGHTS
                diffuseColor += ComputeToonAdditionalLights(positionWS, N, albedo, _Threshold, _Smoothness);
                #endif

                half3 emissionColor = half3(0, 0, 0);

                #ifdef _CRACKS_ON
                emissionColor += GlowingCracksTriplanar(positionWS, normalWS, _CrackScale, _TriSharpness, _CrackSharpness, _CrackGlow.rgb, _Time.y, _CrackPulseRate, TEXTURE2D_ARGS(_NoiseTex, sampler_NoiseTex));
                #endif

                #ifdef _FLICKER_ON
                emissionColor += _FlickerColor.rgb * SAMPLE_TEXTURE2D(_FlickerMask, sampler_FlickerMask, input.uv.xy).r * FlickerIntensity(_Time.y, _FlickerSpeed);
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
    CustomEditor "ToonHorrorMetalGUI"
}