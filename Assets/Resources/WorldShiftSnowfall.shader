Shader "The Outlaws/Soft Snowfall"
{
    Properties { _Night ("Night", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Night;
            CBUFFER_END
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;half fog:TEXCOORD1;};
            V Vert(A i)
            {
                V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.c=i.c;
                o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float radius=dot(p,p);
                half alpha=(1-smoothstep(.10,1,radius))*.78*i.c.a;
                half3 colour=lerp(half3(.97,.99,1),half3(.78,.87,.98),_Night);
                return half4(MixFog(colour,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
