Shader "The Outlaws/Mountain mist and falling water"
{
    // Mode 0: soft valley mist banks (horizontal sheets below the road on the drop side).
    // Mode 1: falling water on cliff faces, with streaks flowing down and soft edges.
    // _FlowTime runs on scaled time, so pause stops the drift and the falling water.
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,.5)
        _Mode ("Mode", Float) = 0
        _FlowTime ("Flow time", Float) = 0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
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
                half4 _Color;
                float _Mode, _FlowTime, _Seed;
            CBUFFER_END
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+float2(1,1)),f.x),f.y);
            }
            V Vert(A i)
            {
                V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; o.fog=ComputeFogFactor(o.p.z); return o;
            }
            half4 Frag(V i):SV_Target
            {
                float alpha;
                if (_Mode < .5)
                {
                    // Billowing bank: soft oval edge broken up by two drifting noise octaves.
                    float2 p=(i.uv-.5)*2;
                    float edge=saturate(1-dot(p,p));
                    float2 q=i.uv*float2(3,2)+_Seed+float2(_FlowTime*.012,_FlowTime*.004);
                    float billow=Noise(q*2)*.65+Noise(q*5.3)*.35;
                    alpha=smoothstep(.0,.65,edge)*smoothstep(.25,.75,billow+edge*.35);
                }
                else
                {
                    // Water: bright streaks racing down, soft side edges, foam at the bottom.
                    float x=i.uv.x, y=i.uv.y;
                    float sides=smoothstep(0,.22,x)*smoothstep(1,.78,x);
                    float lane=floor(x*9);
                    float streak=frac(y*3.2+_FlowTime*(.9+Hash(float2(lane,_Seed))*.5)+Hash(float2(lane,3)));
                    float body=.55+.45*smoothstep(.55,1,streak)*Noise(float2(x*14,y*6+_FlowTime*2));
                    float foam=smoothstep(.12,0,y);
                    alpha=sides*saturate(body+foam);
                    return half4(MixFog(lerp(_Color.rgb,half3(1,1,1),foam*.6+smoothstep(.85,1,streak)*.35),i.fog),alpha*_Color.a);
                }
                return half4(MixFog(_Color.rgb,i.fog),alpha*_Color.a);
            }
            ENDHLSL
        }
    }
}
