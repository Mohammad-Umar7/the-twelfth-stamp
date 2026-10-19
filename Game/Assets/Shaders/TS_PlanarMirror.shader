// Real planar mirror glass. PlanarMirror renders the room mirrored across the glass into one texture per eye (one on
// desktop) just before the player camera draws; this shader samples it at the glass's own screen position, so the image
// lines up exactly and carries correct stereo depth in VR. The reflection is rendered with a mirrored x axis (so its
// triangles keep their normal winding), hence the 1 - u. The result is linear HDR: the player camera's tonemapping
// and grading apply once, to the mirror and the room alike. _ReflAmount fades to a dim fallback sheen whenever the
// reflection is not being rendered (glass out of view, or the very first frame).
Shader "TwelfthStamp/PlanarMirror"
{
    Properties
    {
        _ReflTexL ("Reflection (left eye / desktop)", 2D) = "black" {}
        _ReflTexR ("Reflection (right eye)", 2D) = "black" {}
        _Tint ("Glass tint", Color) = (0.86,0.9,0.9,1)
        _Fallback ("Fallback colour", Color) = (0.16,0.17,0.18,1)
        _ReflAmount ("Reflection amount", Range(0,1)) = 0
        _Smudge ("Smudges", Range(0,1)) = 0.35
        _Edge ("Edge darkening", Range(0,1)) = 0.5
        _Wrong ("Wrongness (the Margin copy)", Range(0,1)) = 0
        _GlassCenter ("Glass centre (world, set by PlanarMirror)", Vector) = (0,0,0,0)
        _GlassU ("Glass across: axis, half width", Vector) = (1,0,0,0)
        _GlassV ("Glass up: axis, half height", Vector) = (0,1,0,0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ReflTexL_ST;
            half4 _Tint;
            half4 _Fallback;
            half _ReflAmount;
            half _Smudge;
            half _Edge;
            half _Wrong;
            float4 _GlassCenter;
            float4 _GlassU;
            float4 _GlassV;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Mirror"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            TEXTURE2D(_ReflTexL); SAMPLER(sampler_ReflTexL);
            TEXTURE2D(_ReflTexR); SAMPLER(sampler_ReflTexR);

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = p.positionNDC; // 0..w across this eye's view, y up whatever the target
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half3 SampleReflection(float2 suv)
            {
                half3 c = SAMPLE_TEXTURE2D(_ReflTexL, sampler_ReflTexL, suv).rgb;
            #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                if (unity_StereoEyeIndex == 1) c = SAMPLE_TEXTURE2D(_ReflTexR, sampler_ReflTexR, suv).rgb;
            #endif
                return c;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 suv = i.screenPos.xy / i.screenPos.w;
                suv.x = 1.0 - suv.x;

                // Position on the glass in metres, from its centre (world space: the model's UVs are not guaranteed).
                float3 d = i.positionWS - _GlassCenter.xyz;
                float2 onGlass = float2(dot(d, _GlassU.xyz), dot(d, _GlassV.xyz));

                // Smudges and fine dust, fixed to the glass.
                float2 g = onGlass * 9.0;
                float smudge = Noise(g) * 0.6 + Noise(g * 2.7 + 11.0) * 0.4;
                smudge = smoothstep(0.55, 0.95, smudge) * _Smudge;

                half3 refl = SampleReflection(suv);
                if (_Wrong > 0.001)
                {
                    // The Margin's copy: the reflection runs a little cold and dim, as if the room behind the glass
                    // were a degree further from the light.
                    half lum = dot(refl, half3(0.299, 0.587, 0.114));
                    refl = lerp(refl, lum * half3(0.78, 0.9, 1.0), 0.45 * _Wrong) * (1.0 - 0.18 * _Wrong);
                }
                half3 col = refl * _Tint.rgb;
                col = lerp(_Fallback.rgb, col, _ReflAmount);
                // Smudges scatter a little light and soften the image; a faint sheen keeps the glass readable as glass.
                col = col * (1.0 - 0.18 * smudge) + _Fallback.rgb * 0.35 * smudge;

                // A soft shadow line where the glass meets its frame (the last 2.5 cm).
                if (_GlassU.w > 0.001 && _GlassV.w > 0.001)
                {
                    float edgeDist = min(_GlassU.w - abs(onGlass.x), _GlassV.w - abs(onGlass.y));
                    col *= lerp(1.0 - 0.3 * _Edge, 1.0, saturate(edgeDist / 0.025));
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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

            half frag (Varyings i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
