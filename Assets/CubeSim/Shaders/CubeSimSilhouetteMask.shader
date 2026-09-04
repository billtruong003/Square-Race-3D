// CubeSimSilhouetteMask.shader - pass one of the armed silhouette outline. Draws nothing to
// colour or depth; it only stamps the object's full screen silhouette into the stencil buffer
// (depth test off, so a racer half hidden behind a wall still masks its whole shape).
// CubeSimSilhouetteOutline draws afterwards and is refused wherever this stamp landed, so the
// rim shows only outside the union silhouette of everything that carries the mask - a knife
// built from several boxes gets one clean contour, not a red seam per box.
Shader "CubeSim/SilhouetteMask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+5" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SilhouetteMask"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
