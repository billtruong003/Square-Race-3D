Shader "Shmackle/UVEyeAnimatedStylize"
{
    Properties
    {
        [Header(Base Sclera)]
        _BaseColor ("Sclera Color", Color) = (1, 1, 1, 1)

        [Header(Veins)]
        _VeinsMap ("Veins Texture (Black=Vein)", 2D) = "white"{}
        _VeinsColor ("Veins Tint", Color) = (0.8, 0, 0, 1)
        _VeinsCutoff ("Veins Cutoff", Range(0, 1)) = 0.5
        _VeinsSmoothness ("Veins Smoothness", Range(0.001, 0.5)) = 0.05
        _VeinsIntensity ("Veins Intensity", Range(0, 5)) = 1.0

        [Header(Iris and Pupil)]
        _IrisMap ("Iris Texture", 2D) = "black"{}
        _IrisColor ("Iris Tint", Color) = (1, 1, 1, 1)
        _IrisSize ("Iris Size", Range(0.1, 2.0)) = 1.0
        _ParallaxStrength ("Pupil Depth", Range(0, 0.1)) = 0.02

        [Header(Highlight)]
        _HighlightMap ("Highlight Texture", 2D) = "black"{}
        _HighlightColor ("Highlight Tint", Color) = (1, 1, 1, 1)
        _HighlightScale ("Highlight Scale", Range(0.1, 5.0)) = 1.0
        _HighlightPosX ("Highlight Position X", Range(-0.5, 0.5)) = 0.0
        _HighlightPosY ("Highlight Position Y", Range(-0.5, 0.5)) = 0.0

        [Header(Driven By Script)]
        _PupilOffset ("Pupil Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirTS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _VeinsColor;
            float4 _IrisColor;
            float4 _HighlightColor;
            float2 _PupilOffset;
            float _VeinsCutoff;
            float _VeinsSmoothness;
            float _VeinsIntensity;
            float _IrisSize;
            float _ParallaxStrength;
            float _HighlightScale;
            float _HighlightPosX;
            float _HighlightPosY;
            CBUFFER_END

            TEXTURE2D(_VeinsMap);
            SAMPLER(sampler_VeinsMap);
            TEXTURE2D(_IrisMap);
            SAMPLER(sampler_IrisMap);
            TEXTURE2D(_HighlightMap);
            SAMPLER(sampler_HighlightMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv; // Use raw UVs

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                float3 bitangent = cross(normalInput.normalWS, normalInput.tangentWS) * input.tangentOS.w * unity_WorldTransformParams.w;
                output.viewDirTS = float3(
                dot(viewDirWS, normalInput.tangentWS),
                dot(viewDirWS, bitangent),
                dot(viewDirWS, normalInput.normalWS)
                );

                return output;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 1. Base Sclera (Pure Color)
                half3 finalColor = _BaseColor.rgb;

                // 2. Veins Logic (Black = Vein, White = Cutoff)
                half4 veinSample = SAMPLE_TEXTURE2D(_VeinsMap, sampler_VeinsMap, input.uv);
                float veinVal = veinSample.r; // Grayscale

                // Invert logic: 1 when black (vein), 0 when white (cutoff)
                float veinMask = 1.0 - smoothstep(_VeinsCutoff, _VeinsCutoff + _VeinsSmoothness, veinVal);
                veinMask = saturate(veinMask * _VeinsIntensity);

                finalColor = lerp(finalColor, _VeinsColor.rgb, veinMask * _VeinsColor.a);

                // 3. Iris with Parallax
                float2 parallaxOffset = input.viewDirTS.xy * _ParallaxStrength;
                float2 irisCenter = float2(0.5, 0.5);

                // Scale from center -> Apply Offset -> Apply Parallax
                float2 irisUV = (input.uv - irisCenter) * (1.0 / _IrisSize) + irisCenter;
                irisUV += _PupilOffset - parallaxOffset;

                half4 irisCol = SAMPLE_TEXTURE2D(_IrisMap, sampler_IrisMap, irisUV) * _IrisColor;
                finalColor = lerp(finalColor, irisCol.rgb, irisCol.a);

                // 4. Highlight (Moves with Pupil)
                float2 highlightCenter = float2(0.5, 0.5);

                // Scale from center
                float2 highlightUV = (input.uv - highlightCenter) * (1.0 / _HighlightScale) + highlightCenter;

                // Apply Static Position Offset (Manual placement)
                highlightUV -= float2(_HighlightPosX, _HighlightPosY);

                // Apply Dynamic Pupil Movement (Follows eye)
                highlightUV += _PupilOffset;

                half4 highlightCol = SAMPLE_TEXTURE2D(_HighlightMap, sampler_HighlightMap, highlightUV) * _HighlightColor;
                finalColor = lerp(finalColor, highlightCol.rgb, highlightCol.a);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
