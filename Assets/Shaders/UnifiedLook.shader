Shader "GoStraight/UnifiedLook"
{
    Properties
    {
        _Strength("Overall Strength", Range(0,1)) = 0.7
        _StyleTint("Style Tint", Color) = (0.94,1.02,1.08,1)
        _OutlineStrength("Outline Strength", Range(0,0.2)) = 0.14
        _OutlineWidth("Outline Width (Pixels)", Range(0,3)) = 1
        _DepthThreshold("Relative Depth Threshold", Range(0.005,0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _Strength;
            float4 _StyleTint;
            float _OutlineStrength;
            float _OutlineWidth;
            float _DepthThreshold;
        CBUFFER_END
        struct Attributes { uint vertexID : SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            return output;
        }
        half4 Tint(Varyings input) : SV_Target
        {
            // Fixed-function multiplication retains the existing framebuffer.
            // No screen-color texture is sampled, and tint can never crush its brightness.
            float3 tint = lerp(float3(1,1,1), clamp(_StyleTint.rgb, 0.9, 1.1), saturate(_Strength));
            // Keep neutral surfaces at the same luminance while making the palette more coherent.
            tint /= max(0.001, dot(tint, float3(0.2126,0.7152,0.0722)));
            tint = clamp(tint, 0.9, 1.1);
            return half4(tint, 1);
        }
        float EyeDepth(float2 uv)
        {
            float raw = SampleSceneDepth(saturate(uv));
            if (unity_OrthoParams.w > 0.5)
            {
                #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                #endif
                return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
            }
            return LinearEyeDepth(raw, _ZBufferParams);
        }
        float Difference(float a, float b) { return abs(a-b) / max(1.0, min(a,b)); }
        half4 Outline(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = GetNormalizedScreenSpaceUV(input.positionCS.xy);
            float2 pixel = _OutlineWidth / _ScaledScreenParams.xy;
            float center = EyeDepth(uv);
            float edge = max(Difference(center, EyeDepth(uv + float2(pixel.x,0))), Difference(center, EyeDepth(uv - float2(pixel.x,0))));
            edge = max(edge, Difference(center, EyeDepth(uv + float2(0,pixel.y))));
            edge = max(edge, Difference(center, EyeDepth(uv - float2(0,pixel.y))));
            edge = smoothstep(_DepthThreshold, _DepthThreshold * 2.0, edge);
            // Black-only lines cannot reveal hidden geometry through the darkness volume.
            return half4(0,0,0, edge * clamp(_OutlineStrength,0,0.2) * saturate(_Strength));
        }
        ENDHLSL
        Pass
        {
            Name "GentleTint"
            ZWrite Off ZTest Always Cull Off
            Blend DstColor Zero
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Tint
            ENDHLSL
        }
        Pass
        {
            Name "OptionalOutline"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Outline
            ENDHLSL
        }
    }
}
