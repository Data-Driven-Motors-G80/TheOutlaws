Shader "The Outlaws/Ocean Coast Water"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.015, 0.19, 0.32, 1)
        _ShallowColor ("Turquoise Water", Color) = (0.03, 0.56, 0.61, 1)
        _FoamColor ("Sea Foam", Color) = (0.7, 0.94, 0.92, 1)
        _RippleScale ("Ripple Scale", Float) = 0.9
        _WaveSpeed ("Wave Speed", Float) = 0.8
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.18
        _SkyColor ("Sky Reflection", Color) = (0.73, 0.88, 0.85, 1)
        [HideInInspector] _OceanTime ("Ocean Time", Float) = 0
        [HideInInspector] _ChannelMask ("Navigable Channel", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "OceanForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                float _RippleScale;
                float _WaveSpeed;
                float _FoamAmount;
                float _OceanTime;
                float _ChannelMask;
                half4 _SkyColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // World coordinates keep streamed sections and the surrounding sea seamless.
                float2 p = input.positionWS.xz * _RippleScale;
                float t = _OceanTime * _WaveSpeed;
                float a = dot(p, float2(0.35, 0.8)) - t;
                float b = dot(p, float2(-1.1, 0.45)) + t * 1.4;
                float c = dot(p, float2(1.8, 1.3)) - t * 1.8;
                float swell = sin(a) * 0.5 + sin(b) * 0.3 + sin(c) * 0.12;
                float2 slope = cos(a) * float2(0.35, 0.8) * 0.16
                    + cos(b) * float2(-1.1, 0.45) * 0.09
                    + cos(c) * float2(1.8, 1.3) * 0.04;
                // A finer, faster chop layered over the swell breaks up the flat look and catches the sun.
                float2 q = input.positionWS.xz * _RippleScale * 3.7;
                float d1 = dot(q, float2(0.9, -0.4)) - t * 2.6;
                float d2 = dot(q, float2(0.2, 1.0)) + t * 2.1;
                slope += (cos(d1) * float2(0.9, -0.4) + cos(d2) * float2(0.2, 1.0)) * 0.035;
                half3 normalWS = normalize(float3(-slope.x, 1, -slope.y));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();
                half fresnel = pow(1 - saturate(dot(normalWS, view)), 5);
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(0.48 + swell * 0.17));
                color *= 0.78 + saturate(dot(normalWS, sun.direction)) * 0.22;
                // Grazing angles reflect the sky so the sea melts into the horizon.
                color = lerp(color, _SkyColor.rgb, saturate(0.08 + fresnel * 0.85));
                half glint = pow(saturate(dot(normalWS, SafeNormalize(sun.direction + view))), 180);
                half sparkle = step(0.93, frac(sin(dot(floor(input.positionWS.xz * 1.5), float2(12.9898, 78.233))) * 43758.5) + sin(t * 3 + swell * 6) * 0.04);
                color += sun.color * (glint * 1.1 + glint * sparkle * 2.0);
                float foam = smoothstep(0.72, 0.91, swell) * _FoamAmount;
                color = lerp(color, _FoamColor.rgb, foam);
                // A soft turquoise edge marks the driving channel without painted lane stripes.
                float edge = smoothstep(0.78, 1.0, abs(input.uv.x * 2 - 1)) * _ChannelMask;
                color = lerp(color, _ShallowColor.rgb * 1.25, edge * 0.6);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
}
