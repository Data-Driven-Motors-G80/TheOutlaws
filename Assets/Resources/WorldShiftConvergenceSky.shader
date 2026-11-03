Shader "Outlaws/World Shift Joined Sky"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Pass
        {
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _ShiftSeamRight, _ShiftSeamPoint;
            struct Varyings { float4 positionCS : SV_POSITION; float3 ray : TEXCOORD0; };
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(vertex.xyz);
                o.ray = vertex.xyz;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 ray = normalize(i.ray);
                float side = dot(ray, _ShiftSeamRight.xyz) + dot(_WorldSpaceCameraPos - _ShiftSeamPoint.xyz, _ShiftSeamRight.xyz) / 140;
                half night = smoothstep(-.003,.003,side);
                half3 day = lerp(half3(.48,.54,.54),half3(.22,.39,.56),saturate(ray.y));
                half3 echo = lerp(half3(.018,.035,.065),half3(.005,.012,.035),saturate(ray.y));
                return half4(lerp(day,echo,night),1);
            }
            ENDHLSL
        }
    }
}
