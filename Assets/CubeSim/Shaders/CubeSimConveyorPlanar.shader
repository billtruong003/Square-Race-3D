// CubeSimConveyorPlanar.shader - the belt texture projected from world space, not from the
// plate's UVs. Every belt shows the pattern at the same physical size no matter how the plate
// is scaled, the pattern can be turned by any angle, and it slides along the belt's own drag
// direction (fed per renderer by the device system). Triplanar blend so the plate's thin
// sides pick the pattern up too instead of streaking.
Shader "CubeSim/ConveyorPlanar"
{
    Properties
    {
        _BaseMap         ("Pattern", 2D) = "white" {}
        [HDR] _Color     ("Tint", Color) = (1.2, 2.2, 2.4, 1)
        [HDR] _Emission  ("Emission", Color) = (1.6, 6.5, 7.5, 1)
        _MetresPerRepeat ("Metres per repeat", Float) = 5.6
        _Angle           ("Pattern rotation (deg)", Range(-180, 180)) = 0
        _AngleOffset     ("Extra rotation for taste (deg)", Range(-180, 180)) = 0
        _ScrollDir       ("Scroll direction (world x, z)", Vector) = (1, 0, 0, 0)
        _ScrollDist      ("Scroll distance (m)", Float) = 0
        _Sharpness       ("Triplanar sharpness", Range(1, 8)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ConveyorPlanar"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float4 _Emission;
                float  _MetresPerRepeat;
                float  _Angle;
                float  _AngleOffset;
                float4 _ScrollDir;
                float  _ScrollDist;
                float  _Sharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // Slide along the belt in world space, then turn the pattern, then scale to repeats.
            float2 PlaneUV(float2 p)
            {
                float2 dir = normalize(_ScrollDir.xy + float2(1e-5, 0));
                p -= dir * _ScrollDist;
                float a = radians(_Angle + _AngleOffset);
                float c = cos(a), s = sin(a);
                p = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                return p / max(0.01, _MetresPerRepeat);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = abs(normalize(i.normalWS));
                float3 w = pow(n, _Sharpness);
                w /= (w.x + w.y + w.z);

                half3 top   = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, PlaneUV(i.positionWS.xz)).rgb;
                half3 sideX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, PlaneUV(i.positionWS.zy)).rgb;
                half3 sideZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, PlaneUV(i.positionWS.xy)).rgb;
                half3 tex = top * w.y + sideX * w.x + sideZ * w.z;

                half3 col = tex * _Color.rgb + tex * _Emission.rgb;
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };
            Varyings vert(Attributes v) { Varyings o; o.positionHCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
