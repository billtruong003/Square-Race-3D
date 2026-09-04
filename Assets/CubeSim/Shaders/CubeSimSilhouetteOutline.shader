// CubeSimSilhouetteOutline.shader - pass two of the armed silhouette outline. Re-draws the mesh
// pushed outward from its bounds centre (per axis, so hard-edged cubes and box-built knives stay
// closed at the corners) with depth testing off, and lets the fragment through only where the
// stencil was NOT stamped by CubeSim/SilhouetteMask. The result is a flat, even-width rim hugging
// the object's on-screen silhouette: never cut by the floor, never seamed between parts, visible
// even where a wall overlaps the racer. The HDR colour glows under the scene bloom.
Shader "CubeSim/SilhouetteOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Colour", Color) = (1.8, 0.03, 0.03, 1)
        _HullCenter        ("Bounds Centre (object space)", Vector) = (0, 0, 0, 0)
        _HullFactor        ("Expansion Factor per axis", Vector) = (1.1, 1.1, 1.1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+6" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SilhouetteOutline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float4 _HullCenter;
                float4 _HullFactor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = _HullCenter.xyz + (v.positionOS.xyz - _HullCenter.xyz) * _HullFactor.xyz;
                o.positionHCS = TransformObjectToHClip(p);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }
    }
    FallBack Off
}
