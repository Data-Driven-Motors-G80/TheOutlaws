Shader "The Outlaws/Distant Sky Art"
{
    Properties
    {
        _Opacity ("Opacity", Range(0,1)) = 1
        _Night ("Night", Range(0,1)) = 0
    }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Opacity, _Night;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;};
            struct V {float4 p:SV_POSITION;half4 c:COLOR;};
            V Vert(A i)
            {
                V o;o.p=TransformObjectToHClip(i.p.xyz);
                half shade=.72+.28*saturate(dot(TransformObjectToWorldNormal(i.n),normalize(float3(-.4,1,-.3))));
                o.c=half4(i.c.rgb*shade*lerp(1,.80,_Night),i.c.a*_Opacity);return o;
            }
            // Deliberately no ground fog: distant sky figures remain visible above the fog bank.
            // Normal depth testing lets mountains and buildings occlude them.
            half4 Frag(V i):SV_Target {return i.c;}
            ENDHLSL
        }
    }
}
