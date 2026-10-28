Shader "Outlaws/World Shift Headlight Haze"
{
    Properties { _ClearRoadLength ("Clear road ahead", Float) = 54 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
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
            CBUFFER_START(UnityPerMaterial)
                float _ClearRoadLength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; half fog : TEXCOORD0; float alongBeam : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.alongBeam = input.positionOS.z;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Add only a faint haze, never a solid sheet over the driving view.
                // Stop before a missing deck; haze over the void resembles a road.
                half road = 1 - smoothstep(max(0, _ClearRoadLength - 4), max(.001, _ClearRoadLength), input.alongBeam);
                return half4(MixFogColor(input.color.rgb, half3(0, 0, 0), input.fog), input.color.a * road);
            }
            ENDHLSL
        }
    }
}
