Shader "The Outlaws/Ocean Coast Sky"
{
    Properties { _Night ("Night", Range(0,1)) = 0 }
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
                float _Night;
            CBUFFER_END
            struct V { float4 p:SV_POSITION; float3 direction:TEXCOORD0; };
            V Vert(float4 p:POSITION) { V o; o.p=TransformObjectToHClip(p.xyz); o.direction=p.xyz; return o; }
            half4 Frag(V i):SV_Target
            {
                float3 d=normalize(i.direction);
                float h=saturate(d.y);
                half3 horizon=lerp(half3(.73,.88,.85),half3(.075,.19,.28),_Night);
                half3 zenith=lerp(half3(.13,.52,.76),half3(.009,.025,.085),_Night);
                half3 c=lerp(horizon,zenith,pow(h,.45));
                // Warm haze hugging the horizon (day) or a faint teal afterglow (night).
                c=lerp(c,lerp(half3(.98,.86,.66),half3(.10,.26,.33),_Night),exp(-h*14)*.45);
                float3 sun=normalize(float3(-.45,.24,.8));
                float disc=smoothstep(.9991,.9995,dot(d,sun));
                float halo=pow(saturate(dot(d,sun)),45)*.3;
                c+=lerp(half3(1,.74,.35),half3(.65,.86,1),_Night)*(disc+halo)*(1-_Night);
                float3 moon=normalize(float3(.5,.32,.8));
                float moonDisc=smoothstep(.9985,.999,dot(d,moon))*(1-smoothstep(.9985,.999,dot(d,normalize(moon+float3(.012,.008,0)))));
                c+=half3(.95,.97,1)*moonDisc*_Night*1.2+half3(.4,.55,.75)*pow(saturate(dot(d,moon)),60)*.25*_Night;
                float2 cloud=d.xz/max(.16,d.y+.2);
                float bank=sin(cloud.x*2.3+sin(cloud.y*1.8))*.5+sin(cloud.y*3.1+cloud.x)*.25;
                bank+=sin(cloud.x*7.1+cloud.y*5.3)*.12;
                float clouds=smoothstep(.43,.75,bank)*smoothstep(.04,.18,h)*(1-smoothstep(.4,.7,h));
                // Sunlit tops, slightly shaded undersides.
                half3 cloudColor=lerp(lerp(half3(.80,.84,.86),half3(1,.98,.93),saturate(bank*1.4-.4)),half3(.18,.28,.40),_Night);
                c=lerp(c,cloudColor,clouds*.72);
                float3 cell=floor(d*320);
                float star=step(.998,frac(sin(dot(cell,float3(12.98,78.23,39.42)))*43758.54));
                c+=star*_Night*smoothstep(.12,.35,h)*.65;
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
