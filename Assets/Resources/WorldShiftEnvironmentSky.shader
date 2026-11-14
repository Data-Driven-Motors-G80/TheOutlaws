Shader "The Outlaws/City and Desert Sky"
{
    Properties
    {
        _Night ("Night", Range(0,1)) = 0
        _Horizon ("Horizon", Color) = (.85,.81,.71,1)
        _Zenith ("Zenith", Color) = (.19,.43,.65,1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon, _Zenith;
                float _Night;
            CBUFFER_END
            struct V { float4 p:SV_POSITION; float3 d:TEXCOORD0; };
            V Vert(float4 p:POSITION) { V o; o.p=TransformObjectToHClip(p.xyz); o.d=p.xyz; return o; }
            half4 Frag(V i):SV_Target
            {
                float3 d=normalize(i.d);
                float h=saturate(d.y);
                half3 c=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.55));
                float3 sun=normalize(float3(-.45,.24,.8));
                float disk=smoothstep(.9990,.9996,dot(d,sun));
                c+=half3(1,.78,.45)*(disk*.75+pow(saturate(dot(d,sun)),35)*.13)*(1-_Night);
                float3 moon=normalize(float3(.5,.35,.8));
                float crescent=smoothstep(.9990,.9995,dot(d,moon))*(1-smoothstep(.9990,.9995,dot(d,normalize(moon+float3(.013,.008,0)))));
                c+=half3(.65,.77,.9)*crescent*_Night;
                float2 cloud=d.xz/max(.16,d.y+.2);
                float band=sin(cloud.x*2.3+sin(cloud.y*1.8))*.5+sin(cloud.y*3.1+cloud.x)*.25;
                float clouds=smoothstep(.53,.82,band)*smoothstep(.03,.20,h)*(1-smoothstep(.4,.7,h));
                c=lerp(c,lerp(half3(.90,.88,.82),_Horizon.rgb,_Night),clouds*.32);
                float star=step(.9985,frac(sin(dot(floor(d*320),float3(12.98,78.23,39.42)))*43758.54));
                c+=star*_Night*smoothstep(.16,.4,h)*.4;
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
