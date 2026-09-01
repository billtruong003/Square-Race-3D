// Stylised rolling cloud sea for the floating arena.
//
// Technique adapted from MinionsArt's "Rolling Local Clouds Volume":
// https://www.patreon.com/minionsart/posts/rolling-local-166282612
// The shared ideas are a local bounded volume, world-space noise sampled while raymarching, density
// accumulation with vertical and edge fades, scrolling/distorted coordinates and a per-pixel jitter
// to break up banding.
//
// This is a clean rewrite rather than a port, and it differs in three deliberate ways:
//   * it targets URP 17 and stops the march against _CameraDepthTexture, so islands and monsters
//     correctly occlude the cloud instead of being washed over;
//   * the density field is layered 2D noise shaped by a vertical profile rather than true 3D noise,
//     because this is a flat cloud SEA seen from above and the cheaper field is indistinguishable
//     at these angles while costing a fraction of the samples;
//   * step count and the secondary noise layer are driven by a quality tier, so recording sessions
//     can pay for quality that iteration does not.
Shader "CleanRender/CloudSea"
{
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (RG = two octaves)", 2D) = "white" {}
        _Color        ("Lit Colour", Color) = (1, 1, 1, 1)
        _ShadowColor  ("Shadow Colour", Color) = (0.55, 0.62, 0.78, 1)
        _Density      ("Density", Range(0, 8)) = 2.2
        _Coverage     ("Coverage", Range(0, 1)) = 0.55
        _NoiseScale   ("Noise Scale", Float) = 0.012
        _NoiseScale2  ("Secondary Noise Scale", Float) = 0.045
        _ScrollSpeed  ("Scroll Speed", Float) = 0.35
        _Distortion   ("Distortion", Range(0, 1)) = 0.35
        _EdgeFade     ("Edge Fade", Range(0.01, 0.5)) = 0.18
        _VerticalFade ("Vertical Fade", Range(0.01, 0.6)) = 0.28
        _StepCount    ("Step Count", Range(4, 64)) = 24
        _Jitter       ("Jitter", Range(0, 1)) = 0.8
        _LightInfluence ("Light Influence", Range(0, 1)) = 0.7
        _MaxMarchDistance ("Max March Distance", Float) = 260
        [Toggle(_SECONDARY_NOISE)] _UseSecondary ("Secondary Noise Layer", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CloudSea"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Front faces are culled so the volume still draws when the camera is inside it; the
            // entry point is found analytically, so which face rasterises does not matter.
            Cull Front
            // Depth is resolved manually against the scene depth texture instead of by the depth
            // test, otherwise the box's back faces would be rejected by the very geometry the cloud
            // is meant to wrap around.
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _SECONDARY_NOISE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShadowColor;
                float _Density;
                float _Coverage;
                float _NoiseScale;
                float _NoiseScale2;
                float _ScrollSpeed;
                float _Distortion;
                float _EdgeFade;
                float _VerticalFade;
                float _StepCount;
                float _Jitter;
                float _LightInfluence;
                float _MaxMarchDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            // Slab test against the volume's object-space unit cube.
            bool RayBox (float3 roOS, float3 rdOS, out float tNear, out float tFar)
            {
                float3 invD = 1.0 / (rdOS + 1e-6);
                float3 t0 = (-0.5 - roOS) * invD;
                float3 t1 = ( 0.5 - roOS) * invD;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                tNear = max(max(tmin.x, tmin.y), tmin.z);
                tFar  = min(min(tmax.x, tmax.y), tmax.z);
                return tFar > max(tNear, 0.0);
            }

            // Density of the cloud field at a world position, already shaped by the volume fades.
            //
            // Modelled as a SEA rather than a cloud field: the noise drives the height of the top
            // surface and everything beneath it is filled. Sampling a 2D field and extruding it
            // produced hard vertical streaks, and shearing the lookup by altitude to hide that just
            // traded them for diagonal ones. A noise-driven surface gives billowing tops and a
            // solid body from one texture fetch, which is what a cloud sea actually looks like.
            float SampleDensity (float3 posWS, float3 boundsMin, float3 boundsSize, float time)
            {
                float3 local = (posWS - boundsMin) / max(boundsSize, 1e-4);
                float h = saturate(local.y);

                // Edge fade so the slab never shows a hard rectangular boundary.
                float2 edge = smoothstep(0.0, _EdgeFade, local.xz) *
                              (1.0 - smoothstep(1.0 - _EdgeFade, 1.0, local.xz));
                float edgeFade = edge.x * edge.y;
                if (edgeFade <= 0.001) return 0;

                float2 scroll = float2(time * _ScrollSpeed, time * _ScrollSpeed * 0.6) * 0.01;
                float2 uv1 = posWS.xz * _NoiseScale + scroll;
                float n1 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv1, 0).r;

                float surface = n1;
            #ifdef _SECONDARY_NOISE
                // Warp the second octave by the first: cheap curl-ish distortion that keeps the
                // silhouette from looking like scrolling wallpaper.
                float2 warp = (n1 - 0.5) * _Distortion * 0.5;
                float2 uv2 = posWS.xz * _NoiseScale2 + warp - float2(time * _ScrollSpeed * 0.4, 0) * 0.01;
                float n2 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_NoiseTex, uv2, 0).g;
                surface = n1 * 0.7 + n2 * 0.3;
            #endif

                // Coverage raises or lowers the whole sea inside its slab.
                float topH = saturate(surface * 0.75 + _Coverage - 0.42);

                // Filled below the surface, feathered across it. The feather is what turns a hard
                // waterline into billows.
                const float feather = 0.16;
                float body = smoothstep(topH + feather, topH - feather, h);

                // Soft underside so the sea has no visible floor.
                float bottom = smoothstep(0.0, _VerticalFade, h);

                return body * bottom * edgeFade * _Density * 0.25;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 camWS = GetCameraPositionWS();
                float3 rayDir = normalize(i.positionWS - camWS);

                float3 roOS = TransformWorldToObject(camWS);
                float3 rdOS = mul((float3x3)GetWorldToObjectMatrix(), rayDir);

                float tNear, tFar;
                if (!RayBox(roOS, rdOS, tNear, tFar)) return half4(0, 0, 0, 0);

                // These t values are already world distances. rdOS is the object-space image of a
                // unit world direction, so objectToWorld * rdOS == rayDir and a step of t in object
                // space is a step of t metres in world space. Rescaling by |rdOS| here inflated the
                // near distance by roughly the box's scale and made every ray miss.
                float wNear = max(tNear, 0.0);
                float wFar  = tFar;

                // Stop at scene geometry so islands, monsters and the arm occlude the cloud.
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                // GetViewForwardDir() already points along the camera's forward axis. Negating it
                // made this dot product negative, the clamp pinned it near zero, and sceneDist blew
                // up - so the depth clamp never fired and cloud drew straight over the arena.
                float cosAngle = max(dot(rayDir, GetViewForwardDir()), 1e-4);
                float sceneDist = sceneEye / cosAngle;
                wFar = min(wFar, sceneDist);
                // Cap how far any single ray marches. A near-horizontal ray can otherwise traverse
                // the full width of the volume, which costs the same as a short ray but adds almost
                // nothing visually because the far end is already fully occluded or faded out.
                wFar = min(wFar, wNear + _MaxMarchDistance);
                if (wFar <= wNear) return half4(0, 0, 0, 0);

                // Volume bounds in world space, for the fades.
                float3 c0 = TransformObjectToWorld(float3(-0.5, -0.5, -0.5));
                float3 c1 = TransformObjectToWorld(float3( 0.5,  0.5,  0.5));
                float3 boundsMin = min(c0, c1);
                float3 boundsSize = abs(c1 - c0);

                int steps = (int)_StepCount;
                float march = wFar - wNear;
                float stepSize = march / steps;

                // Per-pixel offset breaks the concentric banding a fixed step start produces.
                float dither = frac(sin(dot(screenUV * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453);
                float t = wNear + stepSize * dither * _Jitter;

                float time = _Time.y;
                Light sun = GetMainLight();
                float sunUp = saturate(sun.direction.y * 0.5 + 0.5);

                float transmittance = 1.0;
                float3 accum = 0;

                UNITY_LOOP
                for (int s = 0; s < steps; s++)
                {
                    if (transmittance < 0.03 || t > wFar) break;

                    float3 p = camWS + rayDir * t;
                    float d = SampleDensity(p, boundsMin, boundsSize, time);

                    if (d > 0.001)
                    {
                        // Cheap shading: higher and denser samples read as lit tops, the underside
                        // stays in shadow. A second march toward the sun is not worth the cost at
                        // this stylisation.
                        float hLocal = saturate((p.y - boundsMin.y) / max(boundsSize.y, 1e-4));
                        float lit = saturate(hLocal * 1.35 + d * 0.25);
                        lit = lerp(0.5, lit, _LightInfluence) * lerp(0.75, 1.0, sunUp);

                        float3 col = lerp(_ShadowColor.rgb, _Color.rgb, lit) * sun.color;
                        float a = saturate(d * stepSize * 0.11);

                        accum += col * a * transmittance;
                        transmittance *= (1.0 - a);
                    }
                    t += stepSize;
                }

                float alpha = saturate(1.0 - transmittance) * _Color.a;
                if (alpha <= 0.001) return half4(0, 0, 0, 0);
                return half4(accum / max(alpha, 1e-4) * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
