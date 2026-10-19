// Particles / soft sprites (rain, sand, fibres, motes, confetti, petals, glints). Blend mode and stencil are
// property-driven so the same shader serves real-world FX (stencil NotEqual 1 keeps them out of open tears) and
// pocket FX (stencil Equal 1, clipped at the tear plane).
Shader "TwelfthStamp/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
        _UsePocketClip ("Clip at tear plane", Float) = 0
        _NearFade ("Near-camera fade (start m, range m)", Vector) = (0.45, 1.3, 0, 0)
        _StencilRef ("Stencil ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comp", Float) = 8
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Pass
        {
            Name "Particle"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                float _Intensity;
                float _SrcBlend;
                float _DstBlend;
                float _UsePocketClip;
                float4 _NearFade;
                float _StencilRef;
                float _StencilComp;
            CBUFFER_END

            float4 _TS_PocketPlane;

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_UsePocketClip > 0.5 && (dot(_TS_PocketPlane.xyz, i.positionWS) + _TS_PocketPlane.w) > 0.0)
                    discard;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color * _TintColor;
                c.rgb *= _Intensity;
                // Fade particles that pass right in front of the eye (rain streaks, sand, motes): they read as
                // smears on the lens on a monitor and are uncomfortable in a headset.
                half fade = saturate((distance(i.positionWS, GetCameraPositionWS()) - _NearFade.x) / max(_NearFade.y, 0.01));
                c.a *= fade;
                if (_DstBlend < 1.5) c.rgb *= fade; // additive (dst = One): alpha alone would not fade it
                return c;
            }
            ENDHLSL
        }
    }
}
