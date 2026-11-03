Shader "Outlaws/WorldShiftSplitView"
{
    Properties
    {
        _MainTex ("Complete Day image", 2D) = "white" {}
        _NightTex ("Complete Night image", 2D) = "black" {}
        _ViewMode ("0 Day / 1 Night / 2 Joined", Float) = 0
        _SplitPosition ("Day / Night seam", Range(0,1)) = .5
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            sampler2D _NightTex;
            float _ViewMode;
            float _SplitPosition;
            Output vert(Input v)
            {
                Output o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                // Both textures use unchanged full-screen UVs. This is a crop,
                // not two half-width cameras stretching two entire roads.
                bool night = _ViewMode > 1.5 ? i.uv.x >= _SplitPosition : _ViewMode > .5;
                fixed4 colour = night ? tex2D(_NightTex, i.uv) : tex2D(_MainTex, i.uv);
                return fixed4(colour.rgb, 1);
            }
            ENDCG
        }
    }
}
