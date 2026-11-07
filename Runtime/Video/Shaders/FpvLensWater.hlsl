// Water on the FPV camera lens, ported from Interceptor Drones (LensWater.cginc). The camera is focused far away and
// the drop lies on the lens itself, so it is never seen as a drop, as in FPV video in the rain:
//  - a small drop is a soft round disc (like bokeh) with a milky veil inside, its edge dissolves without a rim;
//  - a large drop or a water film is an irregular spot of strong blur with a dissolved edge and almost no veil;
//  - a wet lens is slightly hazier and softer as a whole.
// A snowflake on the lens is out of focus too: a white translucent spot, denser to the middle, with six blurred rays;
// the picture behind it is strongly blurred. Melting, it becomes an ordinary drop.
// Blur samples the video mips. Drops lie on the lens (screen points); the picture under them comes from the camera
// frame through the fisheye (FpvCamera.hlsl, FpvLensUV). Include after FpvCamera.hlsl.
#ifndef UFORCE_FPV_LENS_WATER_INCLUDED
#define UFORCE_FPV_LENS_WATER_INCLUDED

#define FPV_LENS_MAX_DROPS 8

// xy: centre in UV, z: radius in frame heights, w: visibility.
float4 _LensDrops[FPV_LENS_MAX_DROPS];
// x: shape seed, y: irregularity (0 is a round disc, 1 is a spot), z: veil, w: blur.
float4 _LensDropShapes[FPV_LENS_MAX_DROPS];
// x: how much snow is in the drop (1 is a flake, 0 is water), y: ray rotation.
float4 _LensDropSnow[FPV_LENS_MAX_DROPS];
float _LensDropCount;
// 0..1: how wet the whole lens is.
float _LensWetness;

// Distance to the spot edge in its radii (1 is the edge); an irregular spot has a wavy radius.
float FpvLensSpotDistance(float2 p, float seed, float irregular)
{
    float d = length(p);
    if (irregular <= 0.0)
        return d;

    float around = atan2(p.y, p.x);
    float edge = 1.0
        + sin(around * 2.0 + seed * 1.7) * 0.16
        + sin(around * 3.0 + seed * 3.1) * 0.12
        + sin(around * 5.0 + seed * 5.3) * 0.05;
    return d / lerp(1.0, edge, irregular);
}

float4 FpvLensWaterOver(float2 uv, float4 scene)
{
    float aspect = _VideoTexelSize.z / max(_VideoTexelSize.w, 1.0);
    float2 frame = float2(aspect, 1.0);
    float blur = 0.0;
    float veil = 0.0;
    float flake = 0.0;
    float2 shift = 0.0;

    [loop]
    for (int k = 0; k < FPV_LENS_MAX_DROPS; k++)
    {
        if (k >= _LensDropCount)
            break;

        float4 drop = _LensDrops[k];
        float4 shape = _LensDropShapes[k];
        float frozen = _LensDropSnow[k].x;
        if (drop.z <= 0.0 || drop.w <= 0.0)
            continue;

        float2 p = (uv - drop.xy) * frame / drop.z;
        if (dot(p, p) > 2.9)
            continue;

        float d = FpvLensSpotDistance(p, shape.x, shape.y);
        // Out-of-focus edge: a disc fades over half its radius, a spot over almost all of it;
        // the veil gathers to the middle so the edge shows no rim.
        float feather = lerp(0.45, 0.2, shape.y);
        float m = drop.w * (1.0 - smoothstep(feather, 1.05, d));

        // A flake scatters everything behind it, so it blurs more; its whiteness is below, the veil belongs to water.
        blur = max(blur, m * lerp(shape.w, 1.3, frozen));
        veil = max(veil, m * m * shape.z * (1.0 - frozen));
        // Rays are blurred and uneven: six weak ones plus three crooked ones, not a spoked wheel.
        float angle = atan2(p.y, p.x) + _LensDropSnow[k].y * 6.2831853;
        float arms = 0.82 + 0.1 * cos(angle * 6.0) + 0.08 * cos(angle * 3.0 + shape.x);
        flake = max(flake, frozen * m * lerp(0.5, 0.85, 1.0 - saturate(d)) * lerp(1.0, arms, 0.5 * saturate(1.0 - d)));
        // Water slightly magnifies the picture under it (a flake does not).
        shift -= p * drop.z * 0.04 * m * (1.0 - frozen);
    }

    // Water glows from the sun: more when the camera faces the light, even more near the sun in the frame.
    float2 toSun = (uv - _LensLight.xy) * frame;
    float glow = _LensLight.w * (lerp(0.3, 1.0, _LensLight.z * _LensLight.z) + 1.4 * exp(-dot(toSun, toSun) / 0.12));

    blur = max(blur, _LensWetness * 0.12);
    veil = max(veil, _LensWetness * 0.1 * (0.5 + glow));
    veil *= lerp(0.35, 1.35, saturate(glow));

    float2 waterUV = FpvLensUV(uv) + shift / frame;
    // Blur 1 is about 4% of the frame height.
    float pixels = max(1.0, blur * 0.04 * _VideoTexelSize.w);
    float lod = log2(pixels);
    float2 offset = _VideoTexelSize.xy * pixels * 0.6;
    float4 blurred = FpvSampleVideo(waterUV, lod) * 0.36
        + FpvSampleVideo(waterUV + float2(offset.x, 0.0), lod) * 0.16
        + FpvSampleVideo(waterUV - float2(offset.x, 0.0), lod) * 0.16
        + FpvSampleVideo(waterUV + float2(0.0, offset.y), lod) * 0.16
        + FpvSampleVideo(waterUV - float2(0.0, offset.y), lod) * 0.16;

    float4 colour = lerp(scene, blurred, saturate(blur * 2.0));
    // The veil is light scattered by the water: the picture mixed with the average light around it, a bit brighter.
    float3 around = FpvSampleVideo(waterUV, 6.0).rgb;
    float3 milky = colour.rgb * 0.6 + around * 0.3 + 0.05 + _LensLightColor.rgb * (0.12 * glow);
    colour.rgb = lerp(colour.rgb, milky, saturate(veil));
    // A flake glows with the scattered light around it (white by day, brighter against the sun, barely visible at night).
    float3 snowLight = around * 1.35 + 0.15 * saturate(_LensLight.w + 0.2) + _LensLightColor.rgb * (0.2 * glow);
    colour.rgb = lerp(colour.rgb, snowLight, saturate(flake));
    return colour;
}

float4 FpvLensWater(float2 uv)
{
    float4 scene = FpvSampleVideo(FpvLensUV(uv), FpvSoftLod());

    [branch]
    if (_LensDropCount > 0.5 || _LensWetness > 0.001)
        return FpvLensWaterOver(uv, scene);

    return scene;
}

#endif
