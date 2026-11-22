Shader "The Outlaws/City Shop Poster"
{
    Properties
    {
        _BaseMap ("Illustrated storefront atlas", 2D) = "white" {}
        _Tint ("Backlit print tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END
            struct A { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 position : SV_POSITION; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            V Vert(A input)
            {
                V output;
                output.position=TransformObjectToHClip(input.position.xyz);
                output.uv=input.uv;
                output.fog=ComputeFogFactor(output.position.z);
                return output;
            }
            half4 Frag(V input) : SV_Target
            {
                half3 colour=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_Tint.rgb;
                return half4(MixFog(colour,input.fog),1);
            }
            ENDHLSL
        }
    }
}
