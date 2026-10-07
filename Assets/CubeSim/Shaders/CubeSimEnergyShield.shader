// Energy shield bubble: a sphere with a procedural hexagon lattice, scrolling noise and a
// fresnel rim, additive so the cube inside stays readable. Used for the Arena "Shield" pickup,
// which only absorbs hits - a bubble says that better than a wooden buckler.
Shader "CubeSim/EnergyShield"
{
    Properties
    {
        [HDR] _Color ("Tint", Color) = (0.3, 0.8, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.6
        _HexScale ("Hex Scale", Range(1, 40)) = 12
        _HexWidth ("Hex Line Width", Range(0.01, 0.4)) = 0.08
        _HexStrength ("Hex Strength", Range(0, 3)) = 0.9
        _NoiseScale ("Noise Scale", Range(0.5, 20)) = 4
        _ScrollSpeed ("Scroll Speed", Range(0, 4)) = 0.6
        _Alpha ("Body Alpha", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "EnergyShield"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RimPower, _RimStrength, _HexScale, _HexWidth, _HexStrength, _NoiseScale, _ScrollSpeed, _Alpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; float2 uv : TEXCOORD2; float3 posOS : TEXCOORD3; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.viewWS = normalize(GetWorldSpaceViewDir(posWS));
                o.uv = v.uv;
                o.posOS = v.positionOS.xyz;
                return o;
            }

            // Hexagon lattice distance field on a 2D coordinate.
            float hexDist(float2 p)
            {
                p = abs(p);
                return max(dot(p, normalize(float2(1.0, 1.7320508))), p.x);
            }

            float hexLine(float2 uv, float width)
            {
                const float2 r = float2(1.0, 1.7320508);
                float2 h = r * 0.5;
                float2 a = frac(uv / r) * r - h;
                float2 b = frac((uv - h) / r) * r - h;
                float2 gv = dot(a, a) < dot(b, b) ? a : b;
                float d = hexDist(gv);
                return smoothstep(0.5 - width, 0.5, d);
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float rim = pow(1.0 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS))), _RimPower) * _RimStrength;
                // Lattice on the sphere's UV (seam hidden by the rim on the far side).
                float2 huv = i.uv * float2(_HexScale * 2.0, _HexScale) + float2(_Time.y * _ScrollSpeed * 0.15, 0);
                float hex = hexLine(huv, _HexWidth) * _HexStrength;
                float n = noise(i.uv * _NoiseScale + float2(0, _Time.y * _ScrollSpeed));
                float pulse = 0.85 + 0.15 * sin(_Time.y * 3.0);
                float energy = (rim + hex * (0.5 + 0.5 * n) + _Alpha) * pulse;
                return half4(_Color.rgb * energy, 1);
            }
            ENDHLSL
        }
    }
}
