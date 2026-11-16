Shader "The Outlaws/Streetlamp optical glow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V Vert(A i)
            {
                V o;
                float3 centre=TransformObjectToWorld(float3(0,0,0));
                float3 view=SafeNormalize(GetCameraPositionWS()-centre);
                float3 right=SafeNormalize(cross(float3(0,1,0),view));
                float3 up=cross(view,right);
                float3 position=centre+(right*i.p.x+up*i.p.y)*1.65;
                o.p=TransformWorldToHClip(position);o.uv=i.uv;
                o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float r=dot(p,p);
                half glow=(exp(-r*8)*.20+exp(-r*45)*.45)*saturate(1-r);
                return half4(MixFogColor(half3(1,.69,.35),half3(0,0,0),i.fog),glow);
            }
            ENDHLSL
        }
    }
}
