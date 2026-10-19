// Additive edge light for the Passenger's smoked glass: a fresnel term drawn on a copy of each body mesh over the URP
// Lit base, so the silhouette and the layered plates read in dark scenes without the body glowing. Stencil and the
// global tear plane clip work like PocketLit, so the same copy can be shown inside a destination seen through a tear.
Shader "TwelfthStamp/Rim"
{
    Properties
    {
        _RimColor ("Rim colour", Color) = (0.55,0.75,0.7,1)
        _RimPower ("Rim power", Float) = 3.5
        _RimIntensity ("Rim intensity", Float) = 0.35
        _InnerColor ("Inner layer colour", Color) = (0.25,0.3,0.32,1)
        _InnerPower ("Inner layer power", Float) = 1.2
        _InnerIntensity ("Inner layer intensity", Float) = 0.04
        _UsePocketClip ("Clip at tear plane", Float) = 0
        _StencilRef ("Stencil ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comp", Float) = 8
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" "IgnoreProjector"="True" }

        Pass
        {
            Name "Rim"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                half4 _InnerColor;
                float _InnerPower;
                float _InnerIntensity;
                float _UsePocketClip;
                float _StencilRef;
                float _StencilComp;
            CBUFFER_END

            float4 _TS_PocketPlane; // global, set by TearController (see PocketLit)

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_UsePocketClip > 0.5 && (dot(_TS_PocketPlane.xyz, i.positionWS) + _TS_PocketPlane.w) > 0.0)
                    discard;
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                float facing = saturate(dot(n, v));
                float rim = pow(1.0 - facing, _RimPower);
                float inner = pow(1.0 - facing, _InnerPower);
                half3 col = _RimColor.rgb * rim * _RimIntensity + _InnerColor.rgb * inner * _InnerIntensity;
                return half4(col, 0.0);
            }
            ENDHLSL
        }
    }
}
