Shader "The Outlaws/City and Desert Sky"
{
    Properties
    {
        _Night ("Night", Range(0,1)) = 0
        _Horizon ("Horizon", Color) = (.85,.81,.71,1)
        _Zenith ("Zenith", Color) = (.19,.43,.65,1)
        [HideInInspector] _Snow ("Snow sky art", Float) = 0
        [HideInInspector] _SkyTime ("Sky animation time", Float) = 0
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
                float _Night, _Snow, _SkyTime;
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
                if (_Snow > .5)
                {
                    float azimuth=atan2(d.x,d.z);
                    // High, thin cirrus and sparse ice glints leave the road horizon quiet.
                    float wisp=sin(azimuth*12+h*30+sin(azimuth*5+_SkyTime*.015)*2);
                    float cirrus=pow(saturate(wisp*.5+.5),10)*smoothstep(.24,.40,h)*(1-smoothstep(.60,.85,h));
                    c=lerp(c,half3(.87,.95,1),cirrus*.18*(1-_Night));
                    float glint=star*pow(saturate(sin(_SkyTime*.5+dot(floor(d*320),float3(2,5,3)))),12);
                    c+=half3(.24,.32,.38)*glint*(1-_Night)*smoothstep(.36,.62,h);
                    // Three broad ribbons with finer vertical folds: green, teal and violet.
                    // This is background-only art, masked well above the road and landing markers.
                    float drift=_SkyTime*.035;
                    float centre=.34+.055*sin(azimuth*2.4+drift)+.025*sin(azimuth*6-drift*.7);
                    float curtain=.56+.44*pow(saturate(sin(azimuth*52+drift*2)*.5+.5),2);
                    float lower=exp(-pow((h-centre)/.040,2));
                    float middle=exp(-pow((h-centre-.025)/.043,2));
                    float upper=exp(-pow((h-centre+.045)/.026,2));
                    float skyMask=smoothstep(.23,.30,h)*(1-smoothstep(.72,.86,h));
                    half3 aurora=half3(.12,.74,.34)*lower+half3(.06,.43,.54)*middle+half3(.37,.12,.53)*upper;
                    c+=aurora*curtain*skyMask*_Night*.56;
                }
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
