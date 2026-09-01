#ifndef CLEAN_HORROR_LIB_INCLUDED
#define CLEAN_HORROR_LIB_INCLUDED

float HorrorValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
    float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
    float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
    float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float HorrorFBM(float2 p, int octaves)
{
    float v = 0.0;
    float a = 0.5;
    float freq = 1.0;
    for (int i = 0; i < octaves; i++ )
    {
        v += a * HorrorValueNoise(p * freq);
        freq *= 2.17;
        a *= 0.5;
    }
    return v;
}

#ifndef CLEAN_NOISE_LIB_INCLUDED
struct TriplanarUV
{
    float2 uvX;
    float2 uvY;
    float2 uvZ;
    float3 blend;
};

TriplanarUV ComputeTriplanarUV(float3 worldPos, float3 worldNormal, float scale, float sharpness)
{
    TriplanarUV tp;
    tp.uvX = worldPos.yz * scale;
    tp.uvY = worldPos.xz * scale;
    tp.uvZ = worldPos.xy * scale;
    float3 bl = pow(abs(worldNormal), sharpness);
    tp.blend = bl / (bl.x + bl.y + bl.z + 1e-6);
    return tp;
}

half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), TriplanarUV tp)
{
    return SAMPLE_TEXTURE2D(tex, samp, tp.uvX) * tp.blend.x
    +SAMPLE_TEXTURE2D(tex, samp, tp.uvY) * tp.blend.y
    +SAMPLE_TEXTURE2D(tex, samp, tp.uvZ) * tp.blend.z;
}
#endif

half SampleTriplanarR(TEXTURE2D_PARAM(tex, samp), TriplanarUV tp)
{
    half rx = SAMPLE_TEXTURE2D(tex, samp, tp.uvX).r;
    half ry = SAMPLE_TEXTURE2D(tex, samp, tp.uvY).r;
    half rz = SAMPLE_TEXTURE2D(tex, samp, tp.uvZ).r;
    return rx * tp.blend.x + ry * tp.blend.y + rz * tp.blend.z;
}

half SampleTriplanarDualScale(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 worldNormal, float scale1, float scale2, float sharpness, float mix)
{
    TriplanarUV tp1 = ComputeTriplanarUV(worldPos, worldNormal, scale1, sharpness);
    TriplanarUV tp2 = ComputeTriplanarUV(worldPos, worldNormal, scale2, sharpness);
    half a = SampleTriplanarR(TEXTURE2D_ARGS(tex, samp), tp1);
    half b = SampleTriplanarR(TEXTURE2D_ARGS(tex, samp), tp2);
    return lerp(a, b, mix);
}

half3 TriplanarNormalFromNoise(TEXTURE2D_PARAM(noiseTex, noiseSamp), float3 worldPos, float3 worldNormal, float scale, float sharpness, float strength)
{
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, scale, sharpness);
    float texel = 1.0 / 256.0 / scale;
    float2 dx = float2(texel, 0.0);
    float2 dy = float2(0.0, texel);

    float hLx = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvX - dx).r;
    float hRx = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvX + dx).r;
    float hDx = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvX - dy).r;
    float hUx = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvX + dy).r;
    float3 nX = float3(0, (hLx - hRx), (hDx - hUx));

    float hLy = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvY - dx).r;
    float hRy = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvY + dx).r;
    float hDy = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvY - dy).r;
    float hUy = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvY + dy).r;
    float3 nY = float3((hLy - hRy), 0, (hDy - hUy));

    float hLz = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvZ - dx).r;
    float hRz = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvZ + dx).r;
    float hDz = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvZ - dy).r;
    float hUz = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, tp.uvZ + dy).r;
    float3 nZ = float3((hLz - hRz), (hDz - hUz), 0);

    float3 perturbation = nX * tp.blend.x + nY * tp.blend.y + nZ * tp.blend.z;
    return (half3)normalize(worldNormal + perturbation * strength);
}

half3 PerturbNormalNoTBN(half3 normalWS, float3 positionWS, float2 uv, TEXTURE2D_PARAM(noiseTex, noiseSamp), float noiseScale, float strength)
{
    float h = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv * noiseScale).r;
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float3 perturbed = normalize(cross(dpdy, (float3)normalWS) * ddx(h) * strength + cross((float3)normalWS, dpdx) * ddy(h) * strength + (float3)normalWS);
    return (half3)perturbed;
}

half BloodSplatTriplanar(TEXTURE2D_PARAM(noiseTex, noiseSamp), float3 worldPos, float3 worldNormal, float scale, float sharpness, float threshold, float edgeSoftness)
{
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, scale, sharpness);
    half noise = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    return (half)smoothstep(threshold - edgeSoftness, threshold + edgeSoftness, noise);
}

half BloodDripTriplanar(TEXTURE2D_PARAM(noiseTex, noiseSamp), float3 worldPos, float3 worldNormal, float scale, float sharpness, float dripSpeed, float time)
{
    float3 dripPos = float3(worldPos.x, worldPos.y + time * dripSpeed, worldPos.z);
    TriplanarUV tp = ComputeTriplanarUV(dripPos, worldNormal, scale, sharpness);
    half noise = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    half verticalFactor = 1.0h - abs((half)worldNormal.y);
    return (half)smoothstep(0.35, 0.55, noise) * verticalFactor;
}

half BloodDripMask(float2 uv, float speed, float scale, float time)
{
    float2 dripUV = uv * scale;
    dripUV.y -= time * speed;
    half noise = HorrorValueNoise(dripUV);
    return (half)smoothstep(0.35, 0.55, noise);
}

half3 BloodColorGradient(half wetness, half3 freshColor, half3 driedColor)
{
    return lerp(driedColor, freshColor, wetness);
}

half BloodPulse(float time, float rate, float strength)
{
    float beat = pow(abs(sin(time * rate * 6.2831)), 4.0);
    return (half)(1.0 + beat * strength);
}

half WaterDamageStain(float3 worldPos, float3 worldNormal, float stainHeight, float stainSoftness, TEXTURE2D_PARAM(noiseTex, noiseSamp), float noiseScale)
{
    half isWall = 1.0h - abs((half)worldNormal.y);
    float localHeight = frac(worldPos.y * 0.5);
    half heightMask = (half)smoothstep(stainHeight + stainSoftness, stainHeight - stainSoftness, localHeight);
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, noiseScale, 4.0);
    half noise = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    heightMask *= (half)smoothstep(0.3, 0.6, noise);
    return heightMask * isWall;
}

half CeilingStain(float3 worldPos, float3 worldNormal, TEXTURE2D_PARAM(noiseTex, noiseSamp), float noiseScale, float threshold)
{
    half isCeiling = (half)smoothstep(0.7, 0.9, worldNormal.y * -1.0);
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, noiseScale, 4.0);
    half noise = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    return isCeiling * (half)smoothstep(threshold - 0.1, threshold + 0.1, noise);
}

half DirtOverlay(float3 worldPos, float3 worldNormal, TEXTURE2D_PARAM(noiseTex, noiseSamp), float scale, float amount)
{
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, scale, 4.0);
    half noise = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    half heightBias = (half)saturate(1.0 - worldPos.y * 0.3);
    return (half)smoothstep(1.0 - amount, 1.0, noise + heightBias * 0.3);
}

half RustMaskTriplanar(float3 worldPos, float3 worldNormal, float scale, float sharpness, float threshold, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, scale, sharpness);
    half noise = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    return (half)smoothstep(threshold - 0.1, threshold + 0.1, noise);
}

half RustMask(float2 uv, float scale, float threshold, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    half noise = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv * scale).r;
    return (half)smoothstep(threshold - 0.1, threshold + 0.1, noise);
}

half EdgeCorrosion(float NdotV, float wearAmount, float sharpness)
{
    float edge = 1.0 - saturate(NdotV);
    return (half)smoothstep(1.0 - wearAmount, 1.0 - wearAmount + sharpness, edge);
}

half3 ApplyRust(half3 baseColor, half3 rustColor, half rustMask)
{
    return lerp(baseColor, rustColor, rustMask);
}

half CrackPatternTriplanar(float3 worldPos, float3 worldNormal, float scale, float sharpness, float crackWidth, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    TriplanarUV tp = ComputeTriplanarUV(worldPos, worldNormal, scale, sharpness);
    half n = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp);
    half ridge = abs(n - 0.5h) * 2.0h;
    return 1.0h - (half)smoothstep(0.0, crackWidth, ridge);
}

half CrackPattern(float2 uv, float scale, float crackWidth, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    half n = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv * scale).r;
    half ridge = abs(n - 0.5h) * 2.0h;
    return 1.0h - (half)smoothstep(0.0, crackWidth, ridge);
}

half3 GlowingCracksTriplanar(float3 worldPos, float3 worldNormal, float scale, float sharpness, float crackWidth, half3 glowColor, float pulseTime, float pulseRate, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    half crack = CrackPatternTriplanar(worldPos, worldNormal, scale, sharpness, crackWidth, TEXTURE2D_ARGS(noiseTex, noiseSamp));
    half pulse = BloodPulse(pulseTime, pulseRate, 0.4);
    return glowColor * crack * pulse;
}

half3 GlowingCracks(float2 uv, float scale, float crackWidth, half3 glowColor, float pulseTime, float pulseRate, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    half crack = CrackPattern(uv, scale, crackWidth, TEXTURE2D_ARGS(noiseTex, noiseSamp));
    half pulse = BloodPulse(pulseTime, pulseRate, 0.4);
    return glowColor * crack * pulse;
}

half FlickerIntensity(float time, float flickerSpeed)
{
    float f = sin(time * flickerSpeed * 43.0) * sin(time * flickerSpeed * 17.3) * sin(time * flickerSpeed * 7.7);
    return (half)saturate(f * 0.5 + 0.5);
}

half VeinPatternTriplanar(float3 worldPos, float3 worldNormal, float scale, float sharpness, float thickness, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    TriplanarUV tp1 = ComputeTriplanarUV(worldPos, worldNormal, scale, sharpness);
    TriplanarUV tp2 = ComputeTriplanarUV(worldPos, worldNormal, scale * 2.3, sharpness);
    half n1 = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp1);
    half n2 = SampleTriplanarR(TEXTURE2D_ARGS(noiseTex, noiseSamp), tp2);
    return 1.0h - (half)smoothstep(0.0, thickness, abs(n1 - n2));
}

half VeinPattern(float2 uv, float scale, float thickness, TEXTURE2D_PARAM(noiseTex, noiseSamp))
{
    half n1 = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv * scale).r;
    half n2 = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv * scale * 2.3).r;
    return 1.0h - (half)smoothstep(0.0, thickness, abs(n1 - n2));
}

half3 FakeSSS(float NdotL, float NdotV, half3 sssColor, half sssStrength, half sssDistortion, float3 N, float3 L, float3 V)
{
    float3 sssLight = L + N * sssDistortion;
    float sss = pow(saturate(dot(V, -sssLight)), 3.0) * sssStrength;
    return sssColor * (half)sss;
}

float OrganicPulse(float3 worldPos, float time, float speed, float scale, float amplitude)
{
    float n = HorrorValueNoise(worldPos.xz * scale + time * speed);
    float beat = sin(time * speed * 2.0) * 0.5 + 0.5;
    return (n * 0.5 + beat * 0.5) * amplitude;
}

half3 NormalFromNoise(TEXTURE2D_PARAM(noiseTex, noiseSamp), float2 uv, float texelSize, float strength)
{
    float2 dx = float2(texelSize, 0.0);
    float2 dy = float2(0.0, texelSize);
    float hL = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv - dx).r;
    float hR = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv + dx).r;
    float hD = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv - dy).r;
    float hU = SAMPLE_TEXTURE2D(noiseTex, noiseSamp, uv + dy).r;
    half3 n;
    n.x = (half)((hL - hR) * strength);
    n.y = (half)((hD - hU) * strength);
    n.z = 1.0h;
    return normalize(n);
}

half3 ApplyTangentNormal(half3 tangentNormal, half3 normalWS, half4 tangentWS)
{
    half3 bitangent = cross(normalWS, tangentWS.xyz) * tangentWS.w;
    return normalize(tangentNormal.x * tangentWS.xyz + tangentNormal.y * bitangent + tangentNormal.z * normalWS);
}

#endif