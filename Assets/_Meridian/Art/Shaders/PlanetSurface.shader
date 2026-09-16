Shader "Meridian/Planet Surface"
{
    Properties
    {
        _ColorMap("Land Color",2D)="white"{}
        _NormalMap("Object Space Normal",2D)="white"{}
        _SurfaceMap("Water, Type, Biome, Freezing Temperature",2D)="black"{}
        _RippleStrength("Water Ripple Strength",Range(0,.3))=.13
        _WaterDetailScale("Water Detail Scale",Range(30,240))=115
        _WaterSpeed("Water Animation Speed",Range(0,1))=.22
        _WaterRoughness("Water Roughness Min / Max",Vector)=(.23,.40,0,0)
        _GlintStrength("Water Glint Strength",Range(0,2))=.85
        _DepthContribution("Ocean Depth Color Contribution",Range(0,1))=.9
        [HideInInspector] _TerrainOffset("Terrain Seed Offset",Vector)=(0,0,0,0)
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
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
            CBUFFER_START(UnityPerMaterial)
                float _RippleStrength, _WaterDetailScale, _WaterSpeed, _GlintStrength;
                float4 _WaterRoughness;
                float _DepthContribution;
                float4 _TerrainOffset;
            CBUFFER_END
            float4 _SurfaceMap_TexelSize;

            float WaterHash(float3 p)
            {
                p=frac(p*.1031);p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            // Continuous 3D value noise and its analytic gradient. No longitude/pole basis.
            float4 WaterNoise(float3 p)
            {
                float3 i=floor(p),f=frac(p),u=f*f*(3-2*f),du=6*f*(1-f);
                float a=WaterHash(i),b=WaterHash(i+float3(1,0,0)),c=WaterHash(i+float3(0,1,0)),d=WaterHash(i+float3(1,1,0));
                float e=WaterHash(i+float3(0,0,1)),g=WaterHash(i+float3(1,0,1)),h=WaterHash(i+float3(0,1,1)),j=WaterHash(i+1);
                float x0=lerp(a,b,u.x),x1=lerp(c,d,u.x),x2=lerp(e,g,u.x),x3=lerp(h,j,u.x);
                float y0=lerp(x0,x1,u.y),y1=lerp(x2,x3,u.y);
                float3 gradient=float3(lerp(lerp(b-a,d-c,u.y),lerp(g-e,j-h,u.y),u.z),
                    lerp(x1-x0,x3-x2,u.z),y1-y0)*du;
                return float4(lerp(y0,y1,u.z),gradient);
            }
            float3 FilteredRipple(float3 direction,float scale,float3 drift,float footprint)
            {
                // Fade unresolved octaves before they alias into distant sparkle.
                float visibility=1-smoothstep(.35,1.25,footprint*scale);
                return WaterNoise(direction*scale+drift).yzw*visibility;
            }
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
                float3 dx=ddx(direction),dy=ddy(direction);
                float footprint=max(length(dx),length(dy));
                // Derive UV gradients from continuous directions, never from wrapped longitude.
                float longitudeDenom=2*PI*max(dot(direction.xz,direction.xz),1e-6);
                float latitudeDenom=PI*sqrt(max(1-direction.y*direction.y,1e-6));
                float2 uvDx=float2((direction.z*dx.x-direction.x*dx.z)/longitudeDenom,dx.y/latitudeDenom);
                float2 uvDy=float2((direction.z*dy.x-direction.x*dy.z)/longitudeDenom,dy.y/latitudeDenom);
                float4 surface=SAMPLE_TEXTURE2D_LOD(_SurfaceMap,sampler_SurfaceMap,uv,0);
                int2 texel=int2(floor(float2(frac(uv.x),saturate(uv.y))*_SurfaceMap_TexelSize.zw));
                texel.y=min(texel.y,(int)_SurfaceMap_TexelSize.w-1);
                float waterType=round(LOAD_TEXTURE2D(_SurfaceMap,texel).g*255);
                // Signed, continuous base-level boundary; CPU eligibility uses its exact .5 contour.
                // Blend only over an output pixel's footprint, rather than blurring the geography.
                float coverage=saturate(.5+(surface.r-.5)/max(fwidth(surface.r),1.0/255));
                float4 normalSample=SAMPLE_TEXTURE2D_GRAD(_NormalMap,sampler_NormalMap,uv,uvDx,uvDy);
                float3 normalOS=normalize(normalSample.rgb*2-1);
                float4 terrain=SAMPLE_TEXTURE2D_GRAD(_ColorMap,sampler_ColorMap,uv,uvDx,uvDy);
                float3 land=terrain.rgb;
                // Seeded object-space microdetail complements the 4K bake at regional zoom.
                // It changes shading only, with smoothly filtered amplitudes at orbital distance.
                float fineVisibility=1-smoothstep(.35,1.25,footprint*620);
                float4 fine=WaterNoise(direction*620+_TerrainOffset.xyz);
                float3 grain=fine.yzw*fineVisibility;
                grain+=.35*FilteredRipple(direction,1430,_TerrainOffset.zxy,footprint);
                grain-=direction*dot(direction,grain);
                normalOS=normalize(normalOS-grain*lerp(.018,.06,normalSample.a));
                land*=1+(fine.x-.5)*fineVisibility*lerp(.08,.19,normalSample.a);
                float3 normal=TransformObjectToWorldNormal(normalOS);
                float3 waterColor=lerp(float3(.018,.085,.10),float3(.004,.024,.050),smoothstep(0,.75,terrain.a));
                // Alpha remaps climate temperature 0.08..0.16 onto 0..1 for a smooth ice transition.
                float ice=1-smoothstep(0,1,surface.a);
                waterColor=lerp(waterColor,float3(.34,.48,.53),ice*.85);
                Light light=GetMainLight();
                float ndl=saturate(dot(normal,light.direction));
                float3 color=land*(.33+.8*ndl)*light.color;
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float spec=pow(saturate(dot(normal,normalize(light.direction+view))),28);
                color+=spec*.008*ndl*light.color;
                if(coverage>0)
                {
                    // Retain the previous liquid-water and frozen-water treatment.
                    float3 radial=TransformObjectToWorldNormal(direction);
                    float radialLight=saturate(dot(radial,light.direction));
                    float3 frozen=waterColor*(.33+.8*radialLight)*light.color;
                    frozen+=pow(saturate(dot(radial,normalize(light.direction+view))),90)*.10*radialLight*light.color;
                    float ocean=1-smoothstep(1.25,1.75,waterType);
                    float time=_Time.y*_WaterSpeed;
                    float3 ripple=FilteredRipple(direction,_WaterDetailScale,float3(time,.31*time,-.23*time),footprint);
                    ripple+=.45*FilteredRipple(direction,_WaterDetailScale*2.17,float3(-.47*time,.83*time,4.1),footprint);
                    ripple+=.2*FilteredRipple(direction,_WaterDetailScale*4.39,float3(7.3,-.61*time,.52*time),footprint);
                    ripple-=direction*dot(direction,ripple);
                    float3 waterNormal=TransformObjectToWorldNormal(normalize(direction-ripple*_RippleStrength*lerp(.3,1,ocean)*(1-ice)));
                    float variation=WaterNoise(direction*19+float3(.025*time,0,0)).x;
                    float roughness=clamp(lerp(_WaterRoughness.x,_WaterRoughness.y,variation),.12,.75);
                    float3 halfVector=normalize(light.direction+view);
                    float nl=saturate(dot(waterNormal,light.direction)),nv=max(.001,saturate(dot(waterNormal,view)));
                    float nh=saturate(dot(waterNormal,halfVector)),vh=saturate(dot(view,halfVector));
                    float a2=pow(roughness,4),denom=nh*nh*(a2-1)+1;
                    float distribution=a2/max(.00001,PI*denom*denom);
                    float k=(roughness+1)*(roughness+1)*.125;
                    float visibility=1/max(.001,4*(nl*(1-k)+k)*(nv*(1-k)+k));
                    float fresnel=.02+.98*pow(1-vh,5);
                    float glint=distribution*visibility*fresnel*nl*_GlintStrength*lerp(.45,1,ocean);
                    float depth=smoothstep(0,.8,terrain.a)*_DepthContribution;
                    float3 body=lerp(float3(.018,.090,.112),float3(.006,.030,.065),depth);
                    body=lerp(float3(.013,.061,.066),body,ocean);
                    float grazing=.02+.98*pow(1-nv,5);
                    float3 liquid=body*(.33+.8*nl)*light.color;
                    liquid=lerp(liquid,float3(.040,.075,.11)*(.4+.6*radialLight),grazing*.55);
                    liquid+=min(glint,.65)*light.color;
                    color=lerp(color,lerp(liquid,frozen,ice),coverage);
                }
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
