Shader "Hidden/BillDev/DistanceFog"
{
    Properties
    {
        _BlitTexture ("Source", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.6, 0.65, 0.7, 1)
        _FogDensity ("Fog Density", Float) = 1.0
        _FogStart ("Fog Start", Float) = 50
        _FogEnd ("Fog End", Float) = 500
        _SkyFogAmount ("Sky Fog Amount", Float) = 0.0
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
            Name "DistanceFogPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half  _FogDensity;
                half  _FogStart;
                half  _FogEnd;
                half  _SkyFogAmount;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    bool isSky = rawDepth < 1e-6;
                #else
                    bool isSky = rawDepth > 0.999999;
                #endif

                if (isSky)
                {
                    if (_SkyFogAmount < 0.001h) return scene;
                    return lerp(scene, half4(_FogColor.rgb, 1.0h), _SkyFogAmount);
                }

                half depth = (half)LinearEyeDepth(rawDepth, _ZBufferParams);
                half t = saturate((depth - _FogStart) / max(_FogEnd - _FogStart, 0.001h));
                half fog = saturate(t * t * _FogDensity);

                return lerp(scene, half4(_FogColor.rgb, 1.0h), fog);
            }
            ENDHLSL
        }
    }
}
