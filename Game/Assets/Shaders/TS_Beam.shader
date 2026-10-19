// Additive light shaft (borrowed sunlight, lantern cones, reflector beams). Soft at the silhouette edges using a
// view-dependent falloff, fades along its length (uv.y) and carries drifting dust motes.
Shader "TwelfthStamp/Beam"
{
    Properties
    {
        _Color ("Colour", Color) = (1,0.85,0.55,1)
        _Intensity ("Intensity", Float) = 1
        _EdgeSoftness ("Edge softness", Range(0.5,8)) = 2.5
        _LengthFade ("Length fade", Range(0.1,4)) = 1.4
        _Dust ("Dust", Range(0,1)) = 0.35
        _NearFade ("Near-camera fade (start m, range m)", Vector) = (0.5, 1.9, 0, 0)
        _StencilRef ("Stencil ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comp", Float) = 8
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" }

        Pass
        {
            Name "Beam"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _EdgeSoftness;
                float _LengthFade;
                float _Dust;
                float4 _NearFade;
                float _StencilRef;
                float _StencilComp;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float facing = abs(dot(normalize(i.normalWS), v));
                float edge = pow(saturate(facing), _EdgeSoftness);
                float along = saturate(1.0 - pow(saturate(i.uv.y), _LengthFade));
                float startFade = saturate(i.uv.y * 12.0);

                float2 dp = float2(i.uv.x * 40.0, i.uv.y * 30.0 - _Time.y * 0.6);
                float dust = step(0.985, hash21(floor(dp))) * _Dust;

                // Standing inside a shaft (the safe light at a counter, sunlight pouring past you) must not wrap the
                // view in bright panels: surfaces close to the eye fade out, the far part of the shaft stays.
                float nearFade = saturate((distance(GetCameraPositionWS(), i.positionWS) - _NearFade.x) / max(_NearFade.y, 0.01));
                float a = edge * along * startFade * nearFade;
                half3 c = _Color.rgb * _Intensity * (a * 0.35 + dust * a * 2.0);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
