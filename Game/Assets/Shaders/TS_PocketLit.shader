// Destination "pocket" seen through a tear. Self-lit with the destination's own sun/ambient/fog values so that a
// sunny Egypt can be seen from rainy Paris in the same frame. Stencil-tested (Equal 1 by default) and clipped by the
// global tear plane so pocket geometry never pokes out in front of the opening. Also used with Comp=Always for
// distant backdrop silhouettes in the active world.
Shader "TwelfthStamp/PocketLit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _SunDir ("Direction to sun (world)", Vector) = (0.3,0.8,0.2,0)
        _SunColor ("Sun colour", Color) = (1,0.95,0.85,1)
        _SkyAmb ("Sky ambient", Color) = (0.5,0.55,0.6,1)
        _GroundAmb ("Ground ambient", Color) = (0.3,0.25,0.2,1)
        _FogColor ("Fog colour", Color) = (0.7,0.7,0.7,1)
        _FogDensity ("Fog density", Float) = 0.01
        _UsePocketClip ("Clip at tear plane", Float) = 1
        _StencilRef ("Stencil ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comp", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+460" }

        Pass
        {
            Name "PocketForward"
            Tags { "LightMode"="UniversalForward" }
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float4 _SunDir;
                half4 _SunColor;
                half4 _SkyAmb;
                half4 _GroundAmb;
                half4 _FogColor;
                float _FogDensity;
                float _UsePocketClip;
                float _StencilRef;
                float _StencilComp;
            CBUFFER_END

            float4 _TS_PocketPlane; // global, set by TearController: xyz normal toward viewer side, w = -dot(n, p)

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
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_UsePocketClip > 0.5 && (dot(_TS_PocketPlane.xyz, i.positionWS) + _TS_PocketPlane.w) > 0.0)
                    discard;

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float ndl = saturate(dot(n, normalize(_SunDir.xyz)));
                half3 amb = lerp(_GroundAmb.rgb, _SkyAmb.rgb, saturate(n.y * 0.5 + 0.5));
                half3 col = albedo.rgb * (amb + _SunColor.rgb * ndl) + _EmissionColor.rgb;

                float d = distance(GetCameraPositionWS(), i.positionWS) * _FogDensity;
                float fog = 1.0 - exp(-d * d);
                col = lerp(col, _FogColor.rgb, saturate(fog));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
