using System;
using System.Collections;
using UnityEngine;

namespace Meridian
{
    /// <summary>Seamless ground materials, with physical-scale grain and matching normals/roughness.</summary>
    internal static class SurfaceGroundTextures
    {
        const int Size=1024;
        const float RepeatMetres=8;
        public static IEnumerator Create(Action<UnityEngine.Object> own,Action<TerrainLayer[]> finished)
        {
            string[] names={"Grass and forest soil","Dry sand","Exposed stone","Snow","Damp earth"};
            var layers=new TerrainLayer[names.Length];
            for(int layer=0;layer<layers.Length;layer++)
            {
                var diffuse=new Color32[Size*Size];var normals=new Color32[Size*Size];var mask=new Color32[Size*Size];var heights=new float[Size*Size];
                int seed=1201+layer*137;
                for(int z=0;z<Size;z++)
                {
                    for(int x=0;x<Size;x++)
                    {
                        float u=x/(float)Size,v=z/(float)Size;
                        float broad=Noise(u,v,3,seed),warpU=u+Noise(u,v,5,seed+1)*.045f,warpV=v+Noise(u,v,5,seed+2)*.045f;
                        float patch=Noise(warpU,warpV,11,seed+3),grain=Noise(warpU,warpV,157,seed+4),fine=Random(x,z,seed+5);
                        Color color;float height,smoothness,occlusion;
                        switch(layer)
                        {
                            case 0:
                                // Keep metre-scale colour patches in the world blend map, not this repeated tile.
                                float grass=.92f+.08f*Smooth(.29f,.70f,patch*.72f+broad*.28f);
                                var soil=Color.Lerp(new Color(.24f,.205f,.13f),new Color(.34f,.285f,.19f),grain);
                                var blades=Color.Lerp(new Color(.18f,.245f,.10f),new Color(.39f,.425f,.215f),grain*.65f+fine*.35f);
                                color=Color.Lerp(soil,blades,grass);
                                height=patch*.028f+grain*.012f+fine*.003f;smoothness=.035f;occlusion=.82f+.18f*grain;break;
                            case 1:
                                color=Color.Lerp(new Color(.46f,.365f,.23f),new Color(.69f,.59f,.405f),broad*.30f+patch*.32f+grain*.25f+fine*.13f);
                                if(fine>.965f)color*=1.18f;
                                height=patch*.021f+grain*.006f+fine*.0015f;smoothness=.055f;occlusion=.94f;break;
                            case 2:
                                float joint=Stone(warpU,warpV,14,seed+7,out float stone);
                                float crack=1-Smooth(.008f,.045f,joint);
                                color=Color.Lerp(new Color(.235f,.245f,.24f),new Color(.48f,.465f,.40f),stone*.52f+patch*.30f+grain*.18f);
                                color*=1-crack*.40f;
                                height=Smooth(0,.085f,joint)*.085f+patch*.045f+grain*.012f;smoothness=.09f+stone*.08f;occlusion=1-crack*.45f;break;
                            case 3:
                                color=Color.Lerp(new Color(.66f,.725f,.755f),new Color(.91f,.925f,.91f),.40f+broad*.20f+patch*.22f+grain*.12f);
                                height=patch*.033f+grain*.007f+fine*.001f;smoothness=.16f+grain*.15f;occlusion=.96f;break;
                            default:
                                color=Color.Lerp(new Color(.14f,.105f,.065f),new Color(.34f,.255f,.155f),patch*.43f+grain*.42f+fine*.15f);
                                float pebble=Smooth(.70f,.83f,grain);
                                color=Color.Lerp(color,new Color(.38f,.365f,.30f),pebble*.65f);
                                height=patch*.024f+grain*.018f+pebble*.019f+fine*.002f;smoothness=.10f+(.7f-patch)*.12f;occlusion=.76f+grain*.24f;break;
                        }
                        int i=z*Size+x;heights[i]=height;color.a=1;diffuse[i]=color;
                        mask[i]=new Color(0,occlusion,Mathf.Clamp01(height/.15f),smoothness);
                    }
                    if(z%64==63)yield return null;
                }
                yield return GroundDetail(layer,seed,diffuse,heights,mask);
                for(int z=0;z<Size;z++)
                {
                    for(int x=0;x<Size;x++)
                    {
                        float dx=(heights[z*Size+(x+1)%Size]-heights[z*Size+(x+Size-1)%Size])/(2*RepeatMetres/Size);
                        float dz=(heights[(z+1)%Size*Size+x]-heights[(z+Size-1)%Size*Size+x])/(2*RepeatMetres/Size);
                        Vector3 normal=new Vector3(-dx,-dz,1).normalized;
                        normals[z*Size+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);
                    }
                    if(z%128==127)yield return null;
                }
                Texture2D Texture(string suffix,Color32[] pixels,bool linear)
                {
                    var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,true,linear){name="Survey "+names[layer]+suffix,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
                    own(texture);texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
                }
                var albedo=Texture(" albedo",diffuse,false);yield return null;
                var normalMap=Texture(" normals",normals,true);var surface=Texture(" surface",mask,true);
                var terrainLayer=new TerrainLayer{name=names[layer],diffuseTexture=albedo,normalMapTexture=normalMap,maskMapTexture=surface,
                    tileSize=new Vector2(RepeatMetres,RepeatMetres),normalScale=.75f,metallic=0,smoothness=.08f};
                own(terrainLayer);layers[layer]=terrainLayer;yield return null;
            }
            finished(layers);
        }
        static IEnumerator GroundDetail(int layer,int seed,Color32[] colors,float[] heights,Color32[] masks)
        {
            // Physical features, not just extra noise pixels: short grass leaves and mineral fragments.
            // Stamping wraps at both tile edges so every mip level remains seamless.
            bool grass=layer==0;
            int count=grass?28000:layer==4?14000:7000;
            for(int feature=0;feature<count;feature++)
            {
                float R(int channel)=>Random(feature,channel,seed+31);
                float cx=R(0)*Size,cz=R(1)*Size,angle=R(2)*Mathf.PI*2;
                float dx=Mathf.Cos(angle),dz=Mathf.Sin(angle);
                float length=grass?Mathf.Lerp(5,15,R(3)):Mathf.Lerp(.8f,3.5f,R(3));
                float width=grass?Mathf.Lerp(.6f,1.3f,R(4)):length*Mathf.Lerp(.45f,.85f,R(4));
                Color tint;
                if(grass)
                    tint=R(5)<.18f?Color.Lerp(new Color(.31f,.285f,.15f),new Color(.49f,.445f,.24f),R(6)):
                        Color.Lerp(new Color(.12f,.20f,.075f),new Color(.39f,.46f,.21f),R(6));
                else if(layer==1)tint=Color.Lerp(new Color(.32f,.27f,.20f),new Color(.77f,.71f,.56f),R(6));
                else if(layer==3)tint=Color.Lerp(new Color(.75f,.82f,.86f),new Color(.98f,.98f,.96f),R(6));
                else tint=Color.Lerp(new Color(.20f,.195f,.17f),new Color(.48f,.45f,.37f),R(6));
                float bump=grass?.003f:Mathf.Lerp(.002f,.012f,R(7));
                int extent=Mathf.CeilToInt(length+width+1);
                for(int z=Mathf.FloorToInt(cz)-extent;z<=Mathf.CeilToInt(cz)+extent;z++)
                for(int x=Mathf.FloorToInt(cx)-extent;x<=Mathf.CeilToInt(cx)+extent;x++)
                {
                    float rx=x-cx,rz=z-cz,along=(rx*dx+rz*dz)/length;
                    if(Mathf.Abs(along)>1)continue;
                    float across=(-rx*dz+rz*dx)/width;
                    // Tapered, slightly curved leaves; rounded, irregularly proportioned pebbles.
                    if(grass)across-=.5f*(1-along*along);
                    float shape=1-along*along-across*across;
                    if(shape<=0)continue;
                    float coverage=Smooth(0,.45f,shape);
                    int i=((z%Size+Size)%Size)*Size+(x%Size+Size)%Size;
                    colors[i]=Color.Lerp(colors[i],tint,coverage*(grass?.82f:.68f));
                    heights[i]+=bump*shape;
                    Color surface=masks[i];surface.g=Mathf.Lerp(surface.g,.9f,coverage);
                    surface.b=Mathf.Clamp01(heights[i]/.15f);masks[i]=surface;
                }
                if(feature%1024==1023)yield return null;
            }
        }
        static float Noise(float u,float v,int period,int seed)
        {
            float x=u*period,z=v*period;int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);float tx=x-ix,tz=z-iz;
            tx=tx*tx*tx*(tx*(tx*6-15)+10);tz=tz*tz*tz*(tz*(tz*6-15)+10);
            float At(int a,int b)=>Random((a%period+period)%period,(b%period+period)%period,seed);
            return Mathf.Lerp(Mathf.Lerp(At(ix,iz),At(ix+1,iz),tx),Mathf.Lerp(At(ix,iz+1),At(ix+1,iz+1),tx),tz);
        }
        static float Stone(float u,float v,int period,int seed,out float shade)
        {
            float x=u*period,z=v*period;int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);float first=100,second=100;shade=0;
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                int cx=ix+dx,cz=iz+dz,wx=(cx%period+period)%period,wz=(cz%period+period)%period;
                float px=cx+.15f+.7f*Random(wx,wz,seed),pz=cz+.15f+.7f*Random(wx,wz,seed+1);
                float distance=(x-px)*(x-px)+(z-pz)*(z-pz);
                if(distance<first){second=first;first=distance;shade=Random(wx,wz,seed+2);}else if(distance<second)second=distance;
            }
            return Mathf.Sqrt(second)-Mathf.Sqrt(first);
        }
        static float Random(int x,int z,int seed)=>(SurfaceGenerator.Hash(seed,x,z,9)&0xFFFFFF)/16777215f;
        static float Smooth(float a,float b,float value){float t=Mathf.Clamp01((value-a)/(b-a));return t*t*(3-2*t);}
    }
}
