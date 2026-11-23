Shader "The Outlaws/City and Desert Sky"
{
    Properties
    {
        _Night ("Night", Range(0,1)) = 0
        _Horizon ("Horizon", Color) = (.85,.81,.71,1)
        _Zenith ("Zenith", Color) = (.19,.43,.65,1)
        [HideInInspector] _Snow ("Snow sky art", Float) = 0
        [HideInInspector] _SkyTime ("Sky animation time", Float) = 0
        [HideInInspector] _Mountain ("Mountain night sky", Float) = 0
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
                float _Night, _Snow, _SkyTime, _Mountain;
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
                c+=half3(.65,.77,.9)*crescent*_Night*(1-_Mountain);
                float2 cloud=d.xz/max(.16,d.y+.2);
                float band=sin(cloud.x*2.3+sin(cloud.y*1.8))*.5+sin(cloud.y*3.1+cloud.x)*.25;
                float clouds=smoothstep(.53,.82,band)*smoothstep(.03,.20,h)*(1-smoothstep(.4,.7,h));
                c=lerp(c,lerp(half3(.90,.88,.82),_Horizon.rgb,_Night),clouds*.32);
                float star=step(.9985,frac(sin(dot(floor(d*320),float3(12.98,78.23,39.42)))*43758.54));
                c+=star*_Night*smoothstep(.16,.4,h)*.4;
                if (_Mountain > .5)
                {
                    // Mountain Night: a big full moon low over the peaks, a Milky Way band and dense twinkling stars,
                    // so Night here looks unlike Snow's aurora or the plain City/Desert sky.
                    float3 bigMoon=normalize(float3(.32,.2,.93));
                    float m=dot(d,bigMoon);
                    float disc=smoothstep(.99790,.99815,m);
                    float2 mp=(d-bigMoon*m).xy*260;
                    float maria=.82+.18*sin(mp.x*1.7+sin(mp.y*1.3))*sin(mp.y*1.1);
                    c=lerp(c,half3(.93,.94,.88)*maria,disc*_Night);
                    c+=half3(.30,.38,.52)*(pow(saturate(m),900)*.55+pow(saturate(m),60)*.12)*_Night*(1-disc);
                    float3 galaxy=normalize(float3(.55,.45,-.7));
                    float across=dot(d,galaxy);
                    float band=exp(-across*across/.018)*smoothstep(.08,.35,h);
                    float clump=frac(sin(dot(floor(d*90),float3(4.1,7.3,2.9)))*9134.7);
                    c+=half3(.16,.17,.24)*band*(.45+.55*clump)*_Night*(1-disc);
                    float dense=step(.9935-band*.006,frac(sin(dot(floor(d*420),float3(31.1,17.7,52.3)))*24634.6));
                    float twinkle=.55+.45*sin(_SkyTime*2.3+dot(floor(d*420),float3(3,7,5)));
                    c+=dense*twinkle*_Night*smoothstep(.1,.35,h)*.55*(1-disc);
                }
                if (_Snow > .5)
                {
                    float azimuth=atan2(d.x,d.z);
                    // High, thin cirrus and sparse ice glints leave the road horizon quiet.
                    float wisp=sin(azimuth*12+h*30+sin(azimuth*5+_SkyTime*.015)*2);
                    float cirrus=pow(saturate(wisp*.5+.5),10)*smoothstep(.24,.40,h)*(1-smoothstep(.60,.85,h));
                    c=lerp(c,half3(.87,.95,1),cirrus*.18*(1-_Night));
                    float glint=star*pow(saturate(sin(_SkyTime*.5+dot(floor(d*320),float3(2,5,3)))),12);
                    c+=half3(.24,.32,.38)*glint*(1-_Night)*smoothstep(.36,.62,h);
                    // Tall, rippling curtains reach down into the visible sky instead of forming a thin top border.
                    // Keep the lowest horizon quiet; mountains and gameplay still render in front of this sky.
                    float drift=_SkyTime*.035;
                    float hem=.14+.045*sin(azimuth*2.2+drift)+.025*sin(azimuth*7-drift*.7);
                    float height=h-hem;
                    float folds=azimuth*42+sin(azimuth*6+drift)*2+sin(h*10-drift)*.7;
                    float rays=.30+.70*pow(saturate(sin(folds)*.5+.5),1.5);
                    float curtain=smoothstep(-.015,.035,height)*exp(-max(0,height)*5.2);
                    float greenEdge=exp(-pow(height/.025,2));
                    float violetCrown=exp(-pow((height-.18)/.09,2));
                    half3 curtainColour=lerp(half3(.08,.72,.32),half3(.07,.43,.59),saturate(height*4));
                    half3 aurora=curtainColour*curtain*rays+half3(.12,.76,.34)*greenEdge*.55;
                    aurora+=half3(.39,.13,.55)*violetCrown*rays*.62;
                    // A softer second fold adds depth behind the foreground curtain.
                    float distantHem=.31+.07*sin(azimuth*1.6-drift*.5);
                    float distant=exp(-pow((h-distantHem)/.08,2));
                    aurora+=half3(.09,.37,.42)*distant*.35;
                    float skyMask=smoothstep(.055,.12,h)*(1-smoothstep(.72,.90,h));
                    c+=aurora*skyMask*_Night*.72;
                }
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
