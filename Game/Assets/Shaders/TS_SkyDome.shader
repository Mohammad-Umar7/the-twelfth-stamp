// Gradient sky dome (inverted sphere). Used for the active cell (stencil Always, queue 2440) and inside a
// destination pocket (stencil Equal 1, queue 2470). Optional sun disc, soft cloud bands and stars.
Shader "TwelfthStamp/SkyDome"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.25,0.35,0.55,1)
        _HorizonColor ("Horizon", Color) = (0.75,0.7,0.65,1)
        _GroundColor ("Below horizon", Color) = (0.2,0.18,0.16,1)
        _SunDir ("Direction to sun", Vector) = (0.3,0.6,0.2,0)
        _SunColor ("Sun colour (alpha = disc strength)", Color) = (1,0.9,0.7,1)
        _SunSize ("Sun size", Range(0.0005,0.05)) = 0.004
        _HorizonSharpness ("Horizon sharpness", Range(0.5,8)) = 2.5
        _CloudColor ("Cloud colour (alpha = amount)", Color) = (1,1,1,0)
        _StarAmount ("Stars", Range(0,1)) = 0
        _Exposure ("Exposure", Float) = 1
        _StencilRef ("Stencil ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil comp", Float) = 8
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Background" "Queue"="Geometry+440" "PreviewType"="Skybox" }

        Pass
        {
            Name "SkyDome"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }
            ZWrite On
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                float4 _SunDir;
                half4 _SunColor;
                float _SunSize;
                float _HorizonSharpness;
                half4 _CloudColor;
                float _StarAmount;
                float _Exposure;
                float _StencilRef;
                float _StencilComp;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dirOS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // Direction in world orientation (dome is never rotated except with its cell).
                o.dirOS = mul((float3x3)UNITY_MATRIX_M, v.positionOS.xyz);
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i), n100 = hash31(i + float3(1,0,0)), n010 = hash31(i + float3(0,1,0)), n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1)), n101 = hash31(i + float3(1,0,1)), n011 = hash31(i + float3(0,1,1)), n111 = hash31(i + float3(1,1,1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dirOS);
                float h = d.y;
                half3 col;
                if (h >= 0)
                {
                    float t = pow(saturate(1.0 - h), _HorizonSharpness);
                    col = lerp(_ZenithColor.rgb, _HorizonColor.rgb, t);
                }
                else
                {
                    float t = saturate(-h * 6.0);
                    col = lerp(_HorizonColor.rgb, _GroundColor.rgb, t);
                }

                // Soft cloud bands.
                if (_CloudColor.a > 0.001 && h > -0.05)
                {
                    float3 cp = d / max(h + 0.25, 0.08) * 1.6;
                    float c = noise3(cp * 1.3) * 0.6 + noise3(cp * 3.1) * 0.3 + noise3(cp * 7.0) * 0.1;
                    c = smoothstep(0.45, 0.85, c) * saturate(h * 4.0 + 0.2);
                    col = lerp(col, _CloudColor.rgb, c * _CloudColor.a);
                }

                // Stars.
                if (_StarAmount > 0.001 && h > 0.02)
                {
                    float3 sp = floor(d * 380.0);
                    float s = hash31(sp);
                    float star = step(0.9965, s) * (0.5 + 0.5 * hash31(sp + 7.1));
                    col += star * _StarAmount * saturate(h * 3.0);
                }

                // Sun disc + glow.
                float3 sd = normalize(_SunDir.xyz);
                float sdot = dot(d, sd);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sdot);
                float glow = pow(saturate(sdot), 64.0) * 0.35 + pow(saturate(sdot), 8.0) * 0.12;
                col += _SunColor.rgb * (disc * 6.0 + glow) * _SunColor.a;

                return half4(col * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }
}
