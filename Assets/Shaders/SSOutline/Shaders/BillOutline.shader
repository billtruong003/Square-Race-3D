Shader "Hidden/BillDev/SSOutline"
{
    Properties
    {
        _BlitTexture ("Source", 2D) = "white" {}
        _SelectionMaskTexture ("Selection Mask", 2D) = "black" {}
        _OcclusionMaskTexture ("Occlusion Mask", 2D) = "black" {}
        _Thickness ("Thickness", Float) = 1.5
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _DepthThreshold ("Depth Threshold", Float) = 1.5
        _NormalThreshold ("Normal Threshold", Float) = 0.35
        _DepthViewBias ("Depth View Bias", Float) = 0.85
        _NormalViewBias ("Normal View Bias", Float) = 0.5
        _OutlineIntensity ("Outline Intensity", Float) = 1.0
        _DebugMode ("Debug Mode", Int) = 0
        _FadeStart ("Fade Start", Float) = 80
        _FadeEnd ("Fade End", Float) = 150
        _VRPeripheryFade ("VR Periphery Fade", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "BillOutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ OUTLINE_FULL OUTLINE_SELECTION OUTLINE_MIXED
            #pragma shader_feature_local _ USE_DEPTH
            #pragma shader_feature_local _ USE_NORMALS

            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #if defined(USE_NORMALS)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #endif

            TEXTURE2D_X(_SelectionMaskTexture);
            TEXTURE2D_X(_OcclusionMaskTexture);

            CBUFFER_START(UnityPerMaterial)
                half  _Thickness;
                half4 _OutlineColor;
                half  _DepthThreshold;
                half  _NormalThreshold;
                half  _DepthViewBias;
                half  _NormalViewBias;
                half  _OutlineIntensity;
                int   _DebugMode;
                half  _FadeStart;
                half  _FadeEnd;
                half  _VRPeripheryFade;
            CBUFFER_END

            half SampleLinearEye(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw < 1e-6) return 30000.0h;
                #else
                    if (raw > 0.999999) return 30000.0h;
                #endif
                return (half)LinearEyeDepth(raw, _ZBufferParams);
            }

            half CalcDepthEdge(float2 uv, float2 d, half thresh)
            {
                half d0 = SampleLinearEye(uv + float2(-d.x, -d.y));
                half d1 = SampleLinearEye(uv + float2( d.x,  d.y));
                half d2 = SampleLinearEye(uv + float2(-d.x,  d.y));
                half d3 = SampleLinearEye(uv + float2( d.x, -d.y));

                half avg = max((d0 + d1 + d2 + d3) * 0.25h, 0.01h);
                half diff = (abs(d0 - d1) + abs(d2 - d3)) * rcp(avg);

                return saturate((diff - thresh) * 25.0h);
            }

            #if defined(USE_NORMALS)
            half CalcNormalEdge(float2 uv, float2 d, half thresh)
            {
                half3 n0 = (half3)SampleSceneNormals(uv + float2(-d.x, -d.y));
                half3 n1 = (half3)SampleSceneNormals(uv + float2( d.x,  d.y));
                half3 n2 = (half3)SampleSceneNormals(uv + float2(-d.x,  d.y));
                half3 n3 = (half3)SampleSceneNormals(uv + float2( d.x, -d.y));

                half diff = (1.0h - dot(n0, n1)) + (1.0h - dot(n2, n3));

                return saturate((diff - thresh) * 5.0h);
            }
            #endif

            half CalcSelectionEdge(float2 uv, float2 d, half center)
            {
                half s0 = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv + float2( d.x,  0)).r;
                half s1 = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv + float2(-d.x,  0)).r;
                half s2 = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv + float2( 0,  d.y)).r;
                half s3 = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv + float2( 0, -d.y)).r;
                half diff = abs(center - s0) + abs(center - s1) + abs(center - s2) + abs(center - s3);
                return saturate((diff - 0.05h) * 4.0h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                #if defined(UNITY_EDITOR)
                    if (_DebugMode == 1) return half4((half3)Linear01Depth(SampleSceneDepth(uv), _ZBufferParams).xxx, 1);
                    #if defined(USE_NORMALS)
                        if (_DebugMode == 2) return half4((half3)SampleSceneNormals(uv) * 0.5h + 0.5h, 1);
                    #endif
                    if (_DebugMode == 3) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                    if (_DebugMode == 5) return half4(SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv).rrr, 1);
                    if (_DebugMode == 6) return half4(SAMPLE_TEXTURE2D_X(_OcclusionMaskTexture, sampler_LinearClamp, uv).rrr, 1);
                #endif

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth < 1e-6) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                #else
                    if (rawDepth > 0.999999) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                #endif

                half occl = SAMPLE_TEXTURE2D_X(_OcclusionMaskTexture, sampler_LinearClamp, uv).r;
                if (occl > 0.5h) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                half linearEye = (half)LinearEyeDepth(rawDepth, _ZBufferParams);

                half distFade = 1.0h - saturate((linearEye - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001h));
                if (distFade < 0.001h) return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                half viewPitch = saturate(abs((half)UNITY_MATRIX_V[2].y));

                half dynDepthThresh = max(0.001h, _DepthThreshold * 0.03h * (1.0h - viewPitch * _DepthViewBias));
                half dynNormThresh  = max(0.001h, _NormalThreshold * (1.0h - viewPitch * _NormalViewBias));

                half thickScale = lerp(1.0h, 0.5h, saturate(linearEye * 0.002h));
                float2 delta = _BlitTexture_TexelSize.xy * _Thickness * thickScale;

                half edge = 0.0h;

                #if defined(OUTLINE_FULL) || defined(OUTLINE_MIXED)
                    #if defined(USE_DEPTH)
                        edge = max(edge, CalcDepthEdge(uv, delta, dynDepthThresh));
                    #endif
                    #if defined(USE_NORMALS)
                        edge = max(edge, CalcNormalEdge(uv, delta, dynNormThresh));
                    #endif
                #endif

                #if defined(OUTLINE_SELECTION) || defined(OUTLINE_MIXED)
                    half centerMask = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv).r;
                    half selEdge = CalcSelectionEdge(uv, delta, centerMask) * (1.0h - step(0.01h, centerMask));
                    edge = max(edge, selEdge);
                #endif

                edge *= distFade;

                if (_VRPeripheryFade > 0.001h)
                {
                    half2 centered = (half2)(uv * 2.0 - 1.0);
                    edge *= 1.0h - saturate(dot(centered, centered) * _VRPeripheryFade);
                }

                #if defined(UNITY_EDITOR)
                    if (_DebugMode == 4) return half4(edge, edge, edge, 1);
                #endif

                edge = saturate(edge * _OutlineIntensity);
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return lerp(scene, half4(_OutlineColor.rgb, 1.0h), edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
