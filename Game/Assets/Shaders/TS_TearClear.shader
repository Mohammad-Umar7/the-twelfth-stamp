// Tear opening, step 2 of 2 (queue 2451): inside stencil == 1, paint the destination's fill colour and push
// depth to the far plane so the destination pocket (stencil-tested PocketLit, queue 2460+) shows through.
Shader "TwelfthStamp/TearClear"
{
    Properties
    {
        _FillColor ("Fill Colour", Color) = (0.2,0.2,0.2,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+451" "IgnoreProjector"="True" }

        Pass
        {
            Name "TearClear"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Stencil { Ref 1 Comp Equal }
            ZWrite On
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i, out float depth : SV_Depth) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if UNITY_REVERSED_Z
                    depth = 0.0;
                #else
                    depth = 1.0;
                #endif
                return _FillColor;
            }
            ENDHLSL
        }
    }
}
