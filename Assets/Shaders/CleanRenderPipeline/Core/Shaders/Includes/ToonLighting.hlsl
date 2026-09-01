#ifndef TOON_LIGHTING_INCLUDED
#define TOON_LIGHTING_INCLUDED
float4 ToonGetShadowCoord(float3 positionWS, float4 positionCS)
{
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        return ComputeScreenPos(positionCS);
    #elif defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
        return TransformWorldToShadowCoord(positionWS);
    #else
        return float4(0.0, 0.0, 0.0, 0.0);
    #endif
}
float4 ToonGetShadowCoordSimple(float3 positionWS)
{
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        return TransformWorldToShadowCoord(positionWS);
    #else
        return float4(0.0, 0.0, 0.0, 0.0);
    #endif
}
half3 ToonSampleBakedGI(float2 lightmapUV, half3 normalWS)
{
    #if defined(LIGHTMAP_ON)
        return SampleLightmap(lightmapUV, 0.0, normalWS);
    #else
        return SampleSH(normalWS);
    #endif
}
float2 ToonTransformLightmapUV(float2 uv1)
{
    #if defined(LIGHTMAP_ON)
        return uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
    #else
        return float2(0.0, 0.0);
    #endif
}
half ToonCelRamp(half NdotL, half threshold, half smoothness)
{
    return smoothstep(threshold - smoothness, threshold + smoothness, NdotL);
}
half ToonRimFactor(half NdotV, half rimPower, half lightIntensity)
{
    half rim = 1.0h - saturate(NdotV);
    return smoothstep(1.0h - rcp(max(rimPower, 1e-4h)), 1.0h, rim * lightIntensity);
}
struct ToonLightResult
{
    half3 diffuse;
    half3 rim;
    half3 globalIllumination;
};
ToonLightResult ComputeToonMainLight(
    float3 posWS, float3 normalWS, float3 viewDirWS,
    half3 albedo, float2 lightmapUV,
    half threshold, half smoothness, half3 shadowColor,
    half3 rimColor, half rimPower, half rimIntensity, half ambientScale)
{
    ToonLightResult result = (ToonLightResult)0;
    float4 shadowCoord = ToonGetShadowCoordSimple(posWS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 N = (half3)normalWS;
    half3 V = (half3)viewDirWS;
    half3 L = (half3)mainLight.direction;
    half NdotL = dot(N, L);
    half NdotV = dot(N, V);
    half shadowAtten = (half)mainLight.shadowAttenuation;
    half intensity = ToonCelRamp(NdotL * shadowAtten, threshold, smoothness);
    result.diffuse = albedo * lerp(shadowColor, (half3)mainLight.color, intensity);
    half rim = ToonRimFactor(NdotV, rimPower, intensity);
    result.rim = rimColor * rim * rimIntensity;
    #if defined(LIGHTMAP_ON)
        result.globalIllumination = SampleLightmap(lightmapUV, 0.0, N) * albedo;
    #else
        result.globalIllumination = SampleSH(N) * albedo * ambientScale;
    #endif
    return result;
}
#define ComputeToonMainLightGlobal(posWS, normalWS, viewDirWS, albedo, lightmapUV) \
    ComputeToonMainLight(                                                           \
        (posWS), (normalWS), (viewDirWS), (albedo), (lightmapUV),                  \
        _Threshold, _Smoothness, _ShadowColor.rgb,                                 \
        _RimColor.rgb, _RimPower, _RimColor.a, 1.0h)
half3 ComputeToonAdditionalLights(float3 posWS, float3 normalWS, half3 albedo,
                                   half threshold, half smoothness)
{
    half3 addColor = half3(0.0h, 0.0h, 0.0h);
    #if defined(_ADDITIONAL_LIGHTS)
    {
        uint lightCount = GetAdditionalLightsCount();
        UNITY_LOOP
        for (uint i = 0u; i < lightCount; i++)
        {
            Light addLight = GetAdditionalLight(i, posWS);
            half addAtten = (half)(addLight.distanceAttenuation * addLight.shadowAttenuation);
            half addNdotL = saturate(dot((half3)normalWS, (half3)addLight.direction));
            half addIntensity = ToonCelRamp(addNdotL * addAtten, threshold, smoothness);
            addColor += albedo * (half3)addLight.color * addIntensity;
        }
    }
    #endif
    return addColor;
}
#define ComputeToonAdditionalLightsGlobal(posWS, normalWS, albedo) \
    ComputeToonAdditionalLights((posWS), (normalWS), (albedo), _Threshold, _Smoothness)
struct ToonLightingInput
{
    half3  albedo;
    half3  normalWS;
    float3 positionWS;
    float4 positionCS;
    float2 lightmapUV;
    half3  shadowColor;
    half   threshold;
    half   smoothness;
};
struct ToonLightingOutput
{
    half3 color;
};
ToonLightingOutput ComputeToonLighting(ToonLightingInput input)
{
    ToonLightingOutput output;
    float4 shadowCoord = ToonGetShadowCoord(input.positionWS, input.positionCS);
    Light mainLight = GetMainLight(shadowCoord);
    half3 L = (half3)mainLight.direction;
    half  NdotL = dot(input.normalWS, L);
    half  shadowAtt = (half)mainLight.shadowAttenuation;
    half  distAtt = (half)mainLight.distanceAttenuation;
    half celFactor = ToonCelRamp(NdotL, input.threshold, input.smoothness);
    half combinedLight = celFactor * shadowAtt * distAtt;
    half3 toonTint = lerp(input.shadowColor, half3(1.0h, 1.0h, 1.0h), combinedLight);
    half3 directDiffuse = toonTint * (half3)mainLight.color * input.albedo;
    half3 additionalDiffuse = half3(0.0h, 0.0h, 0.0h);
    #if defined(_ADDITIONAL_LIGHTS)
    {
        uint lightCount = GetAdditionalLightsCount();
        UNITY_LOOP
        for (uint i = 0u; i < lightCount; i++)
        {
            Light addLight = GetAdditionalLight(i, input.positionWS);
            half addNdotL = dot(input.normalWS, (half3)addLight.direction);
            half addCel = ToonCelRamp(addNdotL, input.threshold, input.smoothness);
            half addAtten = (half)(addLight.distanceAttenuation * addLight.shadowAttenuation);
            additionalDiffuse += addCel * addAtten * (half3)addLight.color * input.albedo;
        }
    }
    #endif
    half3 indirectDiffuse = ToonSampleBakedGI(input.lightmapUV, input.normalWS);
    half3 indirectContrib = indirectDiffuse * input.albedo;
    #if defined(LIGHTMAP_SHADOW_MIXING) && defined(LIGHTMAP_ON)
        directDiffuse *= shadowAtt;
    #endif
    output.color = directDiffuse + additionalDiffuse + indirectContrib;
    return output;
}
half3 ComputeToonLightingSimple(
    half3 albedo, half3 normalWS, float3 positionWS,
    half3 shadowColor, half threshold, half smoothness)
{
    float4 shadowCoord = ToonGetShadowCoordSimple(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half NdotL = dot(normalWS, (half3)mainLight.direction);
    half cel = ToonCelRamp(NdotL, threshold, smoothness);
    half shadow = cel * (half)mainLight.shadowAttenuation * (half)mainLight.distanceAttenuation;
    half3 tint = lerp(shadowColor, half3(1.0h, 1.0h, 1.0h), shadow);
    return tint * (half3)mainLight.color * albedo;
}
#endif