Shader "Meridian/Underground Services"
{
    Properties
    {
        _Color("Color", Color) = (1,.65,.2,1)
        _ZTest("Depth comparison", Float) = 8
        _Grid("Survey grid", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZTest [_ZTest] ZWrite Off Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Grid;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            Varyings Vert(Attributes input)
            { Varyings o; o.world=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o; }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 coord=i.world.xz/10;
                float2 lines=abs(frac(coord-.5)-.5)/max(fwidth(coord),.001);
                float grid=1-saturate(min(lines.x,lines.y));
                return half4(_Color.rgb+grid*.07*_Grid,_Color.a);
            }
            ENDHLSL
        }
    }
}
