Shader "GoStraight/LocalDarkness"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (0,0,0,1)
        _Density("Density", Float) = 0.5
        _ClearDistance("Clear Distance", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Name "LocalDarkness"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _Density;
                float _ClearDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS.xy);
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    float nearDepth = 1.0;
                    bool sky = rawDepth <= 0.000001;
                #else
                    float nearDepth = UNITY_NEAR_CLIP_VALUE;
                    bool sky = rawDepth >= 0.999999;
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                float3 nearWS = ComputeWorldSpacePosition(uv, nearDepth, UNITY_MATRIX_I_VP);
                float3 originWS = unity_OrthoParams.w > 0.5 ? nearWS : GetCameraPositionWS();
                float3 directionWS = unity_OrthoParams.w > 0.5
                    ? normalize(-UNITY_MATRIX_V[2].xyz) : normalize(nearWS - originWS);
                float sceneDistance = 1000000.0;
                if (!sky)
                {
                    float3 sceneWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                    sceneDistance = max(0.0, dot(sceneWS - originWS, directionWS));
                }
                // Keep ray parameter t in world meters, even when the box is rotated/scaled.
                float3 originOS = TransformWorldToObject(originWS);
                float3 directionOS = mul((float3x3)unity_WorldToObject, directionWS);
                float3 signs = step(0.0, directionOS) * 2.0 - 1.0;
                float3 inverseDirection = signs / max(abs(directionOS), 0.000001);
                float3 first = (-0.5 - originOS) * inverseDirection;
                float3 second = (0.5 - originOS) * inverseDirection;
                float3 entry = min(first, second);
                float3 exit = max(first, second);
                float beginDistance = max(0.0, max(entry.x, max(entry.y, entry.z)));
                float endDistance = min(sceneDistance, min(exit.x, min(exit.y, exit.z)));
                float distanceInFog = max(0.0, endDistance - beginDistance - _ClearDistance);
                // Reach solid black at a finite depth, so nothing beyond the darkness is visible.
                float fade = saturate(max(0.0, _Density) * distanceInFog);
                float opacity = fade * fade * (3.0 - 2.0 * fade) * _FogColor.a;
                return half4(_FogColor.rgb, opacity);
            }
            ENDHLSL
        }
    }
}
