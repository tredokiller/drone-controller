// FPV camera look, ported from Interceptor Drones (FpvCamera.cginc), as in real FPV video:
//  - fisheye lens: the camera frame is rectilinear, the lens bends it into a barrel like 1.8-2.1 mm FPV lenses:
//    the horizon arcs, the centre is larger, the edges are squeezed; frame corners stay corners;
//  - auto exposure (FpvVideoCamera meters the frame with a lag): look at the ground and the sky blows out,
//    look at the sky and the sun and the ground falls into shadow. The frame is HDR, so the sky keeps its clouds;
//  - light bleed: bright areas glow (from the frame mips), against the sun the lens flares and washes out;
//  - soft highlight shoulder by luminance, no per-channel clipping;
//  - analog preset: soft picture, colour bleeding along the line (PAL-like chroma blur), shadow noise, black pedestal.
// Include after _BlitTexture and _VideoTexelSize (xy = 1 / size, zw = size) are declared.
#ifndef UFORCE_FPV_CAMERA_INCLUDED
#define UFORCE_FPV_CAMERA_INCLUDED

// x: fisheye strength (0 is rectilinear), y: tangent of half the camera frame diagonal, z: aspect, w: 1 when the look is on.
float4 _FpvLens;
// x: exposure multiplier, y: glow, z: sun veil, w: vignette.
float4 _FpvExposure;
// x: analog 0..1, y: softness (extra mip), z: noise, w: chroma bleed (part of the frame width).
float4 _FpvAnalog;
// x: highlight knee, y: black lift, z: saturation, w: contrast.
float4 _FpvTone;
// Shadow lift (0 is none): perceptual gamma, digital FPV cameras keep shadows readable.
float _FpvShadowLift;
// Sun for the lens: xy on screen (through the lens, far outside when behind), z: how much the camera faces it,
// w: daylight. _LensLightColor is its colour.
float4 _LensLight;
float4 _LensLightColor;

float4 FpvSampleVideo(float2 uv, float lod)
{
    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, lod);
}

bool FpvOn()
{
    return _FpvLens.w > 0.5;
}

// Fisheye angle at the share rho of the radius to the frame corner: from equidistant (angle grows with radius)
// toward orthographic (sine); the closer to it, the stronger the barrel.
float FpvFisheyeAngle(float rho, float cornerAngle)
{
    float equidistant = rho * cornerAngle;
    float orthographic = asin(saturate(rho * sin(cornerAngle)));
    return lerp(equidistant, orthographic, 0.35);
}

// Screen point -> camera frame point. The frame corner on screen is the camera frame corner; the centre is larger.
float2 FpvLensUV(float2 uv)
{
    if (!FpvOn() || _FpvLens.x <= 0.001)
        return uv;

    float aspect = _FpvLens.z;
    float2 p = (uv - 0.5) * float2(aspect, 1.0);
    float corner = 0.5 * sqrt(aspect * aspect + 1.0);
    float rho = length(p) / corner;

    if (rho < 1e-5)
        return uv;

    float cornerAngle = atan(_FpvLens.y);
    float theta = FpvFisheyeAngle(rho, cornerAngle);
    float rhoSource = lerp(rho, tan(theta) / _FpvLens.y, _FpvLens.x);
    p *= rhoSource / rho;
    return p / float2(aspect, 1.0) + 0.5;
}

// Mip at which the picture is as soft as an analog camera (~480 lines); 0 for digital.
float FpvSoftLod()
{
    return _FpvAnalog.y;
}

float3 FpvSample(float2 sceneUV, float lod)
{
    return FpvSampleVideo(sceneUV, lod).rgb;
}

float FpvHash(float2 p, float salt)
{
    return frac(sin(dot(p + salt, float2(12.9898, 78.233))) * 43758.5453);
}

// Highlight shoulder by luminance: linear up to the knee, then a smooth roll-off to 1, same hue; a channel that still
// exceeds 1 pulls the colour toward white of the same luminance (per channel the sky turned purple and cyan).
float3 FpvShoulder(float3 c, float knee)
{
    float luma = max(dot(c, float3(0.2126, 0.7152, 0.0722)), 1e-5);
    float range = 1.0 - knee;
    float mapped = min(luma, knee) + range * (1.0 - exp(-max(luma - knee, 0.0) / range));
    float3 scaled = c * (mapped / luma);
    float peak = max(scaled.r, max(scaled.g, scaled.b));
    float toWhite = saturate((peak - 1.0) / max(peak - mapped, 1e-4));
    return lerp(scaled, mapped.xxx, toWhite);
}

// Glow: whatever is brighter than the threshold after exposure spreads over three mip levels.
float3 FpvGlow(float2 sceneUV, float exposure)
{
    float3 g = 0.0;
    g += max(FpvSample(sceneUV, 3.0) * exposure - 0.9, 0.0) * 0.5;
    g += max(FpvSample(sceneUV, 5.0) * exposure - 0.85, 0.0) * 0.35;
    g += max(FpvSample(sceneUV, 7.0) * exposure - 0.8, 0.0) * 0.25;
    return g;
}

// Develops the frame: exposure, glow, sun veil, highlight shoulder, colour, analog artefacts and vignette.
// colour: the frame under this screen point (already through the lens water), uv: screen point, sceneUV: camera frame point.
float3 FpvDevelop(float3 colour, float2 uv, float2 sceneUV)
{
    if (!FpvOn())
        return colour;

    float exposure = _FpvExposure.x;
    float aspect = _FpvLens.z;
    float2 frame = float2(aspect, 1.0);

    // Colour bleeds along the line: own luminance, chroma averaged from the neighbours left and right.
    [branch] if (_FpvAnalog.x > 0.001 && _FpvAnalog.w > 0.0)
    {
        float lod = FpvSoftLod();
        float dx = _FpvAnalog.w;
        float3 smear = FpvSample(sceneUV + float2(dx, 0.0), lod) + FpvSample(sceneUV - float2(dx, 0.0), lod) +
                       FpvSample(sceneUV + float2(dx * 2.2, 0.0), lod) * 0.6 + FpvSample(sceneUV - float2(dx * 2.2, 0.0), lod) * 0.6;
        smear /= 3.2;
        float y = dot(colour, float3(0.299, 0.587, 0.114));
        float ys = dot(smear, float3(0.299, 0.587, 0.114));
        float3 chroma = lerp(colour - y, smear - ys, 0.75 * _FpvAnalog.x);
        colour = y + chroma;
    }

    colour *= exposure;
    colour += FpvGlow(sceneUV, exposure) * _FpvExposure.y;

    // Sun flare: a veil over the whole frame when the camera faces the sun, and a halo around it
    // (also at the frame edge when the sun is just outside).
    float daylight = saturate(_LensLight.w);
    float facing = saturate(_LensLight.z);
    float2 toSun = (uv - _LensLight.xy) * frame;
    float halo = exp(-dot(toSun, toSun) / 0.06) * 0.35 + exp(-dot(toSun, toSun) / 0.5) * 0.1;
    float veil = daylight * _FpvExposure.z * (pow(facing, 8.0) * 0.07 + halo * pow(facing, 2.0));
    float3 sunColour = lerp(float3(1.0, 0.97, 0.9), _LensLightColor.rgb, 0.5);
    colour += sunColour * veil * (1.0 - saturate(dot(colour, 0.33)) * 0.6);

    colour = FpvShoulder(max(colour, 0.0), _FpvTone.x);

    // Colour: saturation and contrast in a near-perceptual space, black pedestal.
    float luma = dot(colour, float3(0.299, 0.587, 0.114));
    colour = lerp(luma, colour, _FpvTone.z);
    float3 perceptual = sqrt(saturate(colour));
    perceptual = pow(perceptual, 1.0 - _FpvShadowLift);
    perceptual = (perceptual - 0.5) * _FpvTone.w + 0.5;
    colour = saturate(perceptual) * saturate(perceptual);
    colour = colour * (1.0 - _FpvTone.y) + _FpvTone.y;

    // Analog: warm shift, luminance noise stronger in the shadows and coloured noise blotches.
    [branch] if (_FpvAnalog.x > 0.001)
    {
        float a = _FpvAnalog.x;
        colour *= lerp(1.0, float3(1.04, 1.0, 0.93), a);
        float2 pixel = floor(uv * _VideoTexelSize.zw * 0.5);
        float t = floor(_Time.y * 30.0);
        float grain = FpvHash(pixel, t) - 0.5;
        float2 blotch = floor(uv * float2(96.0 * aspect, 96.0));
        float3 chromaNoise = float3(FpvHash(blotch, t + 1.3), FpvHash(blotch, t + 2.7), FpvHash(blotch, t + 4.1)) - 0.5;
        float shadows = 1.0 - sqrt(saturate(dot(colour, float3(0.299, 0.587, 0.114))));
        colour += (grain * (0.025 + 0.06 * shadows) + chromaNoise * 0.03 * shadows) * _FpvAnalog.z * a;
    }

    // Lens vignette.
    float2 p = (uv - 0.5) * frame;
    float corner = 0.5 * sqrt(aspect * aspect + 1.0);
    float rho = length(p) / corner;
    colour *= 1.0 - _FpvExposure.w * pow(rho, 2.6);

    return saturate(colour);
}

#endif
