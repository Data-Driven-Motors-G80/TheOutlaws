Shader "The Outlaws/Ocean Coast Surface"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Emission ("Glow", Range(0,1)) = 0
        _Grain ("Asphalt grain", Range(0,1)) = 0
        _Smoothness ("Finish smoothness", Range(0,1)) = .25
        _Specular ("Specular strength", Range(0,1)) = .08
        _Finish ("Finish: paint, metal, timber, concrete, rubber, glass", Float) = 0
        _Wear ("Salt and surface wear", Range(0,1)) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Emission, _Grain, _Smoothness, _Specular, _Finish, _Wear;
            CBUFFER_END
            struct A { float4 p:POSITION; float3 n:NORMAL; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; half3 n:TEXCOORD1; half4 c:COLOR; half fog:TEXCOORD2; };
            V Vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.p.xyz);
                o.p=p.positionCS; o.world=p.positionWS; o.n=TransformObjectToWorldNormal(i.n);
                o.c=i.c; o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(V i):SV_Target
            {
                Light sun=GetMainLight();
                half3 n=normalize(i.n);
                // Sky-from-above / sea-bounce-from-below ambient gives flat-shaded props readable form.
                half3 ambient=lerp(half3(.22,.30,.31),half3(.50,.60,.66),n.y*.5+.5);
                half wrap=saturate((dot(n,sun.direction)+.25)/1.25);
                half3 light=ambient + sun.color*wrap*.62;
                half grain=frac(sin(dot(floor(i.world.xz*28),float2(12.9898,78.233)))*43758.5453);
                half3 albedo=i.c.rgb*_BaseColor.rgb;
                float fine=frac(sin(dot(floor(i.world*21),float3(12.9898,78.233,37.719)))*43758.5453);
                float salt=frac(sin(dot(floor(i.world*2.2),float3(41.13,23.72,89.31)))*12345.67);
                if (_Finish>1.5 && _Finish<2.5)
                {
                    float grainWood=sin(i.world.z*31+sin(i.world.x*3)*2+sin(i.world.y*5))*.5+.5;
                    albedo*=.78+grainWood*.22;
                }
                if (_Finish>2.5 && _Finish<3.5) albedo*=.90+fine*.10;
                float weather=smoothstep(.71,.98,salt)*_Wear;
                albedo=lerp(albedo,albedo*.78+half3(.13,.12,.095),weather*.45);
                half3 c=albedo*lerp(light,half3(1,1,1),_Emission);
                c*=1-_Grain*grain*.19;
                half3 viewDirection=GetWorldSpaceNormalizeViewDir(i.world);
                half3 halfDirection=SafeNormalize(sun.direction+viewDirection);
                half highlight=pow(saturate(dot(n,halfDirection)),lerp(12,128,_Smoothness));
                c+=sun.color*highlight*_Specular*(1-_Emission)*(1-weather*.5);
                if (_Finish>4.5)
                {
                    half fresnel=pow(1-saturate(dot(n,viewDirection)),4);
                    c=lerp(c,half3(.24,.43,.52)*_BaseColor.rgb,fresnel*.48);
                }
                return half4(MixFog(c,i.fog),1);
            }
            ENDHLSL
        }
    }
}
