// The FPV video with no signal: Interceptor Drones' static at full loss (EffectsRawImage noise and stripes) under its
// analog interference (FpvSignalRadius). Drawn by FpvVideoNoSignal.Render into a video output when no drone sends a picture.
Shader "Hidden/UForce/FPV Flight/No Signal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Intensity ("Intensity", Range(0, 1)) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            // integer hash of the analog interference
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "Packages/com.uforce.fpv-flight/Runtime/Video/Shaders/FpvAnalogInterference.hlsl"

            float _Intensity;

            float FpvRand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 Frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                // Full loss in FpvVideoCamera: speed 10, stripe frequency 30, stripe intensity 0.1.
                float noise = FpvRand(uv * (_Time.y * 10.0 + 1.0));
                float stripe = saturate(sin(uv.y * 30.0 + _Time.y * 10.0)) * 0.1;
                float3 color = noise + stripe;

                float2 interference = FpvAnalogInterference(uv, 1.0, 60.0);
                color = lerp(color, interference.xxx, interference.y * 0.5);
                return fixed4(saturate(color) * _Intensity, 1.0);
            }
            ENDCG
        }
    }
}
