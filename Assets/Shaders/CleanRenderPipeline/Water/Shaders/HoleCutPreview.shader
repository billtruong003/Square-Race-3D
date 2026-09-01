Shader "Hidden/HoleCutPreview"
{
    Properties
    {
        _HoleTex("Hole Texture", 2D) = "white"{}
        _Threshold("Threshold", Range(0, 1)) = 0.5
        _Padding("Padding Texels", Float) = 0
        _HoleColor("Hole Color", Color) = (1, 0.15, 0.1, 0.4)
        _EdgeColor("Edge Color", Color) = (1, 0.85, 0.1, 0.35)
        _EdgeWidth("Edge Width", Range(0, 0.15)) = 0.05
        _UseUV1("Use UV1", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _HoleTex_ST;
                float  _Threshold;
                float  _Padding;
                half4  _HoleColor;
                half4  _EdgeColor;
                half   _EdgeWidth;
                float  _UseUV1;
            CBUFFER_END
            TEXTURE2D(_HoleTex); SAMPLER(sampler_HoleTex);
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float2 baseUV = lerp(input.uv0, input.uv1, _UseUV1);
                o.uv = baseUV * _HoleTex_ST.xy + _HoleTex_ST.zw;
                return o;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half val = SAMPLE_TEXTURE2D(_HoleTex, sampler_HoleTex, uv).r;
                if (_Padding > 0)
                {
                    float2 dx = ddx(uv);
                    float2 dy = ddy(uv);
                    float2 texel = float2(length(dx), length(dy)) * _Padding;
                    half v1 = SAMPLE_TEXTURE2D(_HoleTex, sampler_HoleTex, uv + float2(texel.x, 0)).r;
                    half v2 = SAMPLE_TEXTURE2D(_HoleTex, sampler_HoleTex, uv - float2(texel.x, 0)).r;
                    half v3 = SAMPLE_TEXTURE2D(_HoleTex, sampler_HoleTex, uv + float2(0, texel.y)).r;
                    half v4 = SAMPLE_TEXTURE2D(_HoleTex, sampler_HoleTex, uv - float2(0, texel.y)).r;
                    val = min(val, min(min(v1, v2), min(v3, v4)));
                }
                half dist = val - _Threshold;
                half isHole = step(val, _Threshold);
                half isEdge = step(abs(dist), _EdgeWidth) * (1.0h - isHole);
                half4 col = _HoleColor * isHole + _EdgeColor * isEdge;
                col.a *= saturate(isHole + isEdge);
                return col;
            }
            ENDHLSL
        }
    }
}
