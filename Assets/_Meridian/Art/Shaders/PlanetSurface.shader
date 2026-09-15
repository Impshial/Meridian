Shader "Meridian/Planet Surface"
{
    Properties
    {
        _ColorMap("Land Color",2D)="white"{}
        _NormalMap("Object Space Normal",2D)="white"{}
        _SurfaceMap("Water, Type, Biome, Temperature",2D)="black"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionOS:TEXCOORD0;float3 positionWS:TEXCOORD1;};
            Varyings Vert(Attributes input)
            {
                Varyings output;output.positionOS=input.positionOS.xyz;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 direction=normalize(input.positionOS);
                float2 uv=float2(atan2(direction.x,direction.z)/(2*PI)+.5,asin(clamp(direction.y,-1,1))/PI+.5);
                float4 surface=SAMPLE_TEXTURE2D_LOD(_SurfaceMap,sampler_SurfaceMap,uv,0);
                float3 normalOS=normalize(SAMPLE_TEXTURE2D_LOD(_NormalMap,sampler_NormalMap,uv,0).rgb*2-1);
                bool water=surface.r>=.5;
                float3 normal=TransformObjectToWorldNormal(water?direction:normalOS);
                float4 terrain=SAMPLE_TEXTURE2D_LOD(_ColorMap,sampler_ColorMap,uv,0);
                float3 land=terrain.rgb;
                float3 waterColor=lerp(float3(.018,.085,.10),float3(.004,.024,.050),smoothstep(0,.75,terrain.a));
                float ice=1-smoothstep(.08,.16,surface.a);
                waterColor=lerp(waterColor,float3(.34,.48,.53),ice*.85);
                float3 albedo=water?waterColor:land;
                Light light=GetMainLight();
                float ndl=saturate(dot(normal,light.direction));
                float3 color=albedo*(.33+.8*ndl)*light.color;
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float spec=pow(saturate(dot(normal,normalize(light.direction+view))),water?90:28);
                color+=spec*(water?.10:.008)*ndl*light.color;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
