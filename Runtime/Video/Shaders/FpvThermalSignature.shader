// Writes the heat of an FpvThermalSignature renderer into the thermal mask of an FPV video camera.
// Occlusion comes from the camera depth texture, so the mask needs no depth buffer of its own.
Shader "UForce/FPV Flight/Thermal Signature"
{
    Properties
    {
        _FpvHeat("Heat", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FpvThermalSignature"

            Cull Back
            ZWrite Off
            ZTest Always
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FpvHeat;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPosition.xy / input.screenPosition.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                // Behind something else in the frame: no heat seen through walls.
                if (input.eyeDepth > sceneDepth + 0.05 + input.eyeDepth * 0.01)
                {
                    discard;
                }

                return half4(_FpvHeat, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
