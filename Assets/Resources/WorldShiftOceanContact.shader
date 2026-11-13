Shader "The Outlaws/Ocean Soft Contact"
{
    Properties { _Opacity ("Contact strength", Range(0,1)) = .35 }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend DstColor Zero
        ZWrite Off Cull Off
        Offset -1,-1
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
            CBUFFER_END
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V Vert(float4 p:POSITION)
            {
                V o; o.p=TransformObjectToHClip(p.xyz);o.uv=p.xz*2;
                o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float d=length(i.uv);
                half strength=(1-smoothstep(.38,1,d))*_Opacity;
                // Fog must fade the shadow towards neutral white for multiplicative blending.
                strength*=ComputeFogIntensity(i.fog);
                return half4(1-strength,1-strength*.94,1-strength*.86,1);
            }
            ENDHLSL
        }
    }
}
