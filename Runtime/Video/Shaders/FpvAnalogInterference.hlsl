// Analog video interference from a weak radio link: line noise, dropouts and rolling bands.
// Ported from Interceptor Drones (FpvSignalRadius/FpvAnalogInterferenceCore.hlsl).
// Needs _Time and #pragma target 3.5 for the integer hash.
#ifndef UFORCE_FPV_ANALOG_INTERFERENCE_INCLUDED
#define UFORCE_FPV_ANALOG_INTERFERENCE_INCLUDED

float FpvInterferenceHash(float2 value)
{
    uint2 cell = (uint2)(int2)floor(value);
    uint hash = cell.x * 374761393u + cell.y * 668265263u;
    hash = (hash ^ (hash >> 13)) * 1274126177u;
    hash ^= hash >> 16;
    return (hash & 0x00ffffffu) * (1.0 / 16777216.0);
}

float FpvInterferenceLineNoise(float x, float row, float seed)
{
    float cell = floor(x);
    float blend = frac(x);
    blend = blend * blend * (3.0 - 2.0 * blend);
    return lerp(FpvInterferenceHash(float2(cell, row) + seed),
                FpvInterferenceHash(float2(cell + 1.0, row) + seed), blend);
}

// x is the noise brightness, y is its opacity over the picture.
float2 FpvAnalogInterference(float2 uv, float signalLoss, float noiseFps)
{
    float strength = saturate(signalLoss);
    float frame = fmod(floor(_Time.y * clamp(noiseFps, 12.0, 120.0)), 4096.0);
    float seed = FpvInterferenceHash(float2(frame, 19.73)) * 1500.0;
    float row = floor(uv.y * 480.0);
    float x = uv.x * 720.0;
    float lineRandom = FpvInterferenceHash(float2(row, seed));
    float fieldRandom = FpvInterferenceHash(float2(floor(frame * 0.25), 83.17));

    float fine = FpvInterferenceHash(float2(floor(x * 1.8), row) + seed);
    float grain = FpvInterferenceLineNoise(x + lineRandom * 3.0, row, seed + 7.0);
    float coarse = FpvInterferenceLineNoise(x * 0.24, row, seed + 113.0);
    float noise = saturate(0.5 + (grain - 0.5) * 1.65 +
        (fine - 0.5) * 0.48 + (coarse - 0.5) * 0.32);

    float envelope = FpvInterferenceLineNoise(uv.y * 11.0 + _Time.y * 1.7,
        floor(frame * 0.17), 51.0);
    float bandPhase = frac(uv.y + _Time.y * 0.31);
    float bandWidth = 0.045 + 0.09 * fieldRandom;
    float rollingBand = 1.0 - smoothstep(bandWidth, bandWidth + 0.025,
        abs(bandPhase - 0.5) + (lineRandom - 0.5) * 0.018);
    float bands = saturate(smoothstep(0.48, 0.79, envelope) + rollingBand * 0.8);

    float weak = smoothstep(0.04, 0.65, strength);
    float collapse = smoothstep(0.32, 0.86, strength);
    float dropoutChance = smoothstep(0.12, 0.95, strength) * 0.94;
    float dropoutSample = FpvInterferenceLineNoise(x * 0.075, row, seed + 613.0);
    float dropout = 1.0 - smoothstep(dropoutChance - 0.075,
        dropoutChance + 0.075, dropoutSample);
    float alpha = saturate(strength * (0.18 + strength * 0.82) +
        dropout * weak * 0.45 + bands * collapse * 0.28);

    float pedestal = (envelope - 0.5) * 0.16 + (fieldRandom - 0.5) * 0.09;
    noise = saturate(noise + pedestal * weak);
    return float2(noise, alpha);
}

#endif
