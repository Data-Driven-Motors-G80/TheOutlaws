Shader "Outlaws/World Shift Joined Surface"
{
    Properties
    {
        _BaseColor ("Prototype colour", Color) = (.5,.5,.5,1)
        _Night ("Night half", Float) = 0
        _EmissionColor ("Readable markers", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                float _Night;
            CBUFFER_END
            float4 _ShiftLampPosition, _ShiftLampForward;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // Independent half-world fill avoids a global sun changing both
                // sides whenever the car crosses the seam. Matte prototype shading.
                half diffuse = saturate(dot(normalize(i.normalWS), normalize(half3(-.4,.8,-.3))));
                half3 day = _BaseColor.rgb * (.62 + diffuse * .65) * half3(1,.94,.82);
                half3 night = _BaseColor.rgb * (.28 + diffuse * .18) * half3(.7,.85,1.1);
                float3 ray = i.positionWS - _ShiftLampPosition.xyz;
                float range = length(ray);
                half cone = smoothstep(.94, .98, dot(ray / max(.01,range), _ShiftLampForward.xyz));
                half beam = cone * pow(saturate(1 - range / 74), 2) * 2.3;
                half3 colour = lerp(day, night + _BaseColor.rgb * beam, _Night) + _EmissionColor.rgb;
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                half fog = smoothstep(lerp(140,65,_Night), lerp(290,180,_Night), dist);
                half3 sky = lerp(half3(.28,.46,.59), half3(.008,.02,.045), _Night);
                return half4(lerp(colour,sky,fog),1);
            }
            ENDHLSL
        }
    }
}
