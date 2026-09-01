Shader "CleanRender/ToonCrystal"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap("Surface Texture", 2D) = "white"{}
        _BaseColor("Surface Tint", Color) = (0.5, 0.7, 0.95, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [Header(Fake Interior)]
        _InteriorMap("Interior Texture (RGB)", 2D) = "black"{}
        _InteriorColor("Interior Tint", Color) = (0.2, 0.35, 0.7, 1)
        _InteriorDepth("Interior Depth", Range(0, 0.5)) = 0.15
        _InteriorBlend("Interior Blend", Range(0, 1)) = 0.6
        [Header(Matcap)][Toggle(_USE_MATCAP)] _UseMatcap("Use Matcap", Float) = 0
        _Matcap("Matcap Texture", 2D) = "white"{}
        _MatcapStrength("Matcap Strength", Range(0, 1)) = 0.5
        [Header(Fresnel)]
        _FresnelColor("Fresnel Color", Color) = (0.7, 0.85, 1, 1)
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 3
        [Header(Specular)]
        _SpecColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecCutoff("Specular Cutoff", Range(0, 1)) = 0.88
        _SpecSmoothness("Specular Smoothness", Range(0, 0.15)) = 0.03
        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.15, 0.15, 0.3, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.4
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.05
        [Header(Rim)]
        _RimColor("Rim Color", Color) = (0.6, 0.8, 1, 0.7)
        _RimPower("Rim Power", Range(0.1, 10)) = 4
        [Header(Emission)]
        [Toggle(_EMISSION)] _UseEmission("Enable Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            float4 _InteriorMap_ST;
            half4  _InteriorColor;
            half   _InteriorDepth;
            half   _InteriorBlend;
            half   _MatcapStrength;
            half4  _FresnelColor;
            half   _FresnelPower;
            half4  _SpecColor;
            half   _SpecCutoff;
            half   _SpecSmoothness;
            half4  _ShadowColor;
            half   _Threshold;
            half   _Smoothness;
            half4  _RimColor;
            half   _RimPower;
            half4  _EmissionColor;
        CBUFFER_END
        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_InteriorMap);  SAMPLER(sampler_InteriorMap);
        TEXTURE2D(_Matcap);       SAMPLER(sampler_Matcap);
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex CrystalVert
            #pragma fragment CrystalFrag
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
            #pragma shader_feature_local _USE_MATCAP
            #pragma shader_feature_local _EMISSION
            struct Attributes
            {
                float4 positionOS : POSITION;
                half3  normalOS   : NORMAL;
                half4  tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float2 lightmapUV  : TEXCOORD3;
                half   fogFactor   : TEXCOORD4;
                half3  viewDirTS   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings CrystalVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                o.lightmapUV = ToonTransformLightmapUV(input.lightmapUV);
                o.fogFactor  = (half)ComputeFogFactor(o.positionCS.z);
                half3 viewDirWS = (half3)normalize(GetCameraPositionWS() - posWS);
                half3 T = (half3)TransformObjectToWorldDir(input.tangentOS.xyz);
                half3 B = cross(o.normalWS, T) * input.tangentOS.w;
                o.viewDirTS = half3(dot(viewDirWS, T), dot(viewDirWS, B), dot(viewDirWS, o.normalWS));
                return o;
            }
            half4 CrystalFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 N = normalize(input.normalWS);
                half3 V = normalize(GetCameraPositionWS() - input.positionWS);
                half NdotV = saturate(dot(N, V));
                half3 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float2 interiorUV = input.uv + input.viewDirTS.xy * (_InteriorDepth / max(input.viewDirTS.z, 0.01h));
                interiorUV = TRANSFORM_TEX(interiorUV, _InteriorMap);
                half3 interior = SAMPLE_TEXTURE2D(_InteriorMap, sampler_InteriorMap, interiorUV).rgb * _InteriorColor.rgb;
                half edgeMask = pow(1.0h - NdotV, 1.5h);
                half3 crystalColor = lerp(surface, interior, _InteriorBlend * (1.0h - edgeMask * 0.5h));
                #ifdef _USE_MATCAP
                {
                    half3 viewNormal = mul((float3x3)UNITY_MATRIX_V, N);
                    float2 matcapUV = viewNormal.xy * 0.5h + 0.5h;
                    half3 matcapColor = SAMPLE_TEXTURE2D(_Matcap, sampler_Matcap, matcapUV).rgb;
                    crystalColor += matcapColor * _MatcapStrength;
                }
                #endif
                ToonLightResult lit = ComputeToonMainLight(
                    input.positionWS, N, V, crystalColor, input.lightmapUV,
                    _Threshold, _Smoothness, _ShadowColor.rgb,
                    _RimColor.rgb, _RimPower, _RimColor.a, 0.8h);
                half3 finalColor = lit.diffuse + lit.rim + lit.globalIllumination;
                float4 shadowCoord = ToonGetShadowCoordSimple(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 H = normalize((half3)mainLight.direction + V);
                half spec = smoothstep(_SpecCutoff - _SpecSmoothness,
                                       _SpecCutoff + _SpecSmoothness,
                                       saturate(dot(N, H)))
                          * (half)mainLight.shadowAttenuation;
                finalColor += _SpecColor.rgb * spec;
                half fresnel = smoothstep(0.3h, 0.6h, pow(1.0h - NdotV, _FresnelPower));
                finalColor += _FresnelColor.rgb * fresnel;
                finalColor += ComputeToonAdditionalLights(
                    input.positionWS, N, crystalColor, _Threshold, _Smoothness);
                #ifdef _EMISSION
                    finalColor += _EmissionColor.rgb;
                #endif
                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            float3 _LightDirection;
            struct ShadowAttr { float4 positionOS : POSITION; half3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVary { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            ShadowVary ShadowVert(ShadowAttr input)
            {
                ShadowVary o = (ShadowVary)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            half4 ShadowFrag(ShadowVary input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DOVert
            #pragma fragment DOFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            struct DOAttr { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DOVary { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            DOVary DOVert(DOAttr input)
            {
                DOVary o = (DOVary)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half4 DOFrag(DOVary input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            struct DNAttr { float4 positionOS : POSITION; half3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DNVary { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            DNVary DNVert(DNAttr input)
            {
                DNVary o = (DNVary)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 DNFrag(DNVary input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
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
            struct MetaAttr { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct MetaVary { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            MetaVary MetaVert(MetaAttr input)
            {
                MetaVary o = (MetaVary)0;
                o.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                o.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return o;
            }
            half4 MetaFrag(MetaVary input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                albedo = lerp(albedo, _InteriorColor.rgb, 0.3h);
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = albedo;
                #ifdef _EMISSION
                    metaInput.Emission = _EmissionColor.rgb;
                #endif
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
}