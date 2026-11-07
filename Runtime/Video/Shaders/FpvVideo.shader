// FPV drone video, ported from Interceptor Drones (EffectsRawImage): lens water, FPV camera look (fisheye, exposure,
// glow, analog/digital), radio interference, static noise, thermal and night modes, and the OSD on top.
// FpvVideoRenderPass blits the post-processed camera picture through pass 0; pass 1 downsamples it for exposure metering.
Shader "UForce/FPV Flight/Video"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "FpvVideo"

            HLSLPROGRAM
            // integer hash of the analog interference
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // xy: 1 / size, zw: size of the video in pixels.
            float4 _VideoTexelSize;
            float _NoiseIntensity;
            float _Speed;
            float _StripeIntensity;
            float _StripeFrequency;
            float _NoiseOpacity;
            float _UseGrayscale;
            // 0 off, 1 white-hot, 2 colour ramp.
            float _ThermalMode;
            // Season: x spring, y summer, z autumn, w winter; all zero means not set.
            float4 _Season;
            // Signal-loss interference: x signal loss 0..1, y noise refresh rate.
            float4 _FpvVideoInterference;
            // OSD in its own texture (FpvOsdCamera), premultiplied alpha.
            TEXTURE2D(_HudTex);
            float _HudOn;
            // Heat of thermal signatures (FpvThermalSignature) in camera frame space.
            TEXTURE2D(_ThermalMask);
            float4 _ThermalMaskTexelSize;
            float _ThermalMaskOn;

            #include "FpvCamera.hlsl"
            #include "FpvLensWater.hlsl"
            #include "FpvAnalogInterference.hlsl"

            float FpvRand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            float FpvTargetHeat(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_ThermalMask, sampler_LinearClamp, uv, 0.0).r;
            }

            float FpvDilatedTargetHeat(float2 uv)
            {
                float2 texel = _ThermalMaskTexelSize.xy;
                float heat = FpvTargetHeat(uv);
                heat = max(heat, FpvTargetHeat(uv + float2(texel.x, 0.0)));
                heat = max(heat, FpvTargetHeat(uv - float2(texel.x, 0.0)));
                heat = max(heat, FpvTargetHeat(uv + float2(0.0, texel.y)));
                heat = max(heat, FpvTargetHeat(uv - float2(0.0, texel.y)));
                return heat * saturate(_ThermalMaskOn);
            }

            // Background heat in the thermal camera by season: the camera sees heat, not colour. In summer the ground is
            // sun-warmed, so the background is warmer; spring and autumn are cooler; in winter bright (snow, sky) is the
            // coldest and dark (trees, roads, buildings) slightly warmer than snow, so targets stand out sharply.
            float FpvThermalBackground(float3 colour)
            {
                float luminance = dot(colour, float3(0.299, 0.587, 0.114));
                float winter = lerp(0.1 + luminance * 0.3, 0.02, smoothstep(0.12, 0.3, luminance));
                float heat = (0.12 + luminance * 0.72) * _Season.y + luminance * 0.66 * _Season.x +
                             luminance * 0.6 * _Season.z + winter * _Season.w;
                return dot(_Season, 1.0) > 0.5 ? heat : luminance * 0.7;
            }

            float3 FpvWhiteHotRamp(float heat)
            {
                float value = lerp(0.05, 0.5, saturate(heat / 0.7));
                value = lerp(value, 1.0, saturate((heat - 0.7) / 0.3));
                return float3(value, value, value);
            }

            float3 FpvThermalRamp(float heat)
            {
                float3 color = lerp(float3(0.02, 0.0, 0.12), float3(0.45, 0.0, 0.42), saturate(heat / 0.25));
                color = lerp(color, float3(0.88, 0.16, 0.04), saturate((heat - 0.25) / 0.25));
                color = lerp(color, float3(1.0, 0.62, 0.0), saturate((heat - 0.5) / 0.25));
                color = lerp(color, float3(1.0, 1.0, 0.88), saturate((heat - 0.75) / 0.25));
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // The picture through the water on the lens; the radio interference goes on top.
                float4 baseColor = FpvLensWater(uv);
                // The frame is developed like an FPV camera; interference and OSD go over it.
                float2 sceneUV = FpvLensUV(uv);
                baseColor.rgb = FpvDevelop(baseColor.rgb, uv, sceneUV);

                [branch]
                if (_FpvVideoInterference.x > 0.0001)
                {
                    float2 interference = FpvAnalogInterference(uv, _FpvVideoInterference.x, _FpvVideoInterference.y);
                    baseColor.rgb = lerp(baseColor.rgb, interference.xxx, interference.y);
                }

                float noise = FpvRand(uv * _Time.y * _Speed);
                float3 noiseColor = float3(noise, noise, noise);
                float stripe = sin(uv.y * _StripeFrequency + _Time.y * _Speed);
                stripe = saturate(stripe) * _StripeIntensity;

                float3 signalNoise = noiseColor * _NoiseIntensity + stripe;
                float3 noisyImage = baseColor.rgb + signalNoise;
                float3 pureStatic = noiseColor + stripe;
                float3 finalColor = lerp(noisyImage, pureStatic, saturate(_NoiseOpacity));

                if (_ThermalMode > 0.5)
                {
                    float targetHeat = FpvDilatedTargetHeat(sceneUV);
                    float heat = FpvThermalBackground(saturate(finalColor));
                    heat = lerp(heat, 1.0, targetHeat);
                    finalColor = _ThermalMode > 1.5 ? FpvThermalRamp(heat) : FpvWhiteHotRamp(heat);
                }
                else if (_UseGrayscale > 0.5)
                {
                    float gray = dot(finalColor, float3(0.299, 0.587, 0.114));
                    finalColor = float3(gray, gray, gray);
                }

                // OSD over the recoloured picture: its own colour in any mode, with the same signal noise,
                // and it disappears with the picture when the signal is lost. Premultiplied alpha (UI and TMP write it so).
                float signal = 1.0 - saturate(_NoiseOpacity);
                float4 hud = SAMPLE_TEXTURE2D_LOD(_HudTex, sampler_LinearClamp, uv, 0.0) * saturate(_HudOn);
                finalColor = finalColor * (1.0 - hud.a * signal) + (hud.rgb + signalNoise * hud.a) * signal;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FpvVideoMeter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _MeterLod;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, input.texcoord, _MeterLod);
            }
            ENDHLSL
        }
    }
}
