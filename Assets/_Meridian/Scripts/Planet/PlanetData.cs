using System;
using UnityEngine;

namespace Meridian
{
    public enum PlanetBiome : byte { Ocean, Lake, River, Plains, Forest, Desert, Rock, Snow }
    public enum PlanetWater : byte { None, Ocean, Lake, River }

    public readonly struct PlanetSample
    {
        public readonly float Elevation, Moisture;
        public readonly PlanetBiome Biome;
        public readonly PlanetWater Water;
        public bool IsLand => Water == PlanetWater.None;
        public PlanetSample(float elevation, float moisture, PlanetBiome biome, PlanetWater water)
        { Elevation=elevation; Moisture=moisture; Biome=biome; Water=water; }
    }

    [Serializable]
    public sealed class PlanetSelection
    {
        public int seed;
        public string generatorVersion;
        public Vector3 localDirection;
        public float elevation, moisture;
        public PlanetBiome biome;
        public PlanetSelection(PlanetData data, Vector3 direction)
        {
            seed=data.Seed; generatorVersion=data.Version; localDirection=direction.normalized;
            var s=data.Sample(localDirection); elevation=s.Elevation; moisture=s.Moisture; biome=s.Biome;
        }
    }

    /// <summary>Visit-owned geography. GPU maps are uploads of these exact CPU fields; no separate picking generator.</summary>
    public sealed class PlanetData
    {
        public const string GeneratorVersion="meridian-planet-1";
        public readonly int Seed;
        public string Version => GeneratorVersion;
        public readonly PlanetParameters Parameters;
        public readonly SphericalGraph Graph;
        public readonly float[] Elevation, Moisture, Temperature, DrainageHeight, Flow;
        public readonly int[] Downstream;
        public readonly PlanetWater[] Water;
        public readonly PlanetBiome[] Biomes;
        public readonly Color32[] ColorMap, NormalMap, SurfaceMap;
        public readonly float[] MapElevation, MapMoisture;
        public float LandFraction;
        public double GenerationSeconds;
        public int Width => Parameters.mapWidth;
        public int Height => Width/2;

        public PlanetData(int seed, PlanetParameters parameters, SphericalGraph graph)
        {
            Seed=seed; Parameters=parameters; Graph=graph;
            int count=graph.Directions.Length;
            Elevation=new float[count]; Moisture=new float[count]; Temperature=new float[count];
            DrainageHeight=new float[count]; Flow=new float[count]; Downstream=new int[count];
            Water=new PlanetWater[count]; Biomes=new PlanetBiome[count];
            int pixels=Width*Height;
            ColorMap=new Color32[pixels]; NormalMap=new Color32[pixels]; SurfaceMap=new Color32[pixels];
            MapElevation=new float[pixels]; MapMoisture=new float[pixels];
        }

        public static Vector2 Coordinates(Vector3 direction)
        {
            var d=direction.normalized;
            return new Vector2(Mathf.Repeat(Mathf.Atan2(d.x,d.z)/(2*Mathf.PI)+.5f,1), Mathf.Asin(Mathf.Clamp(d.y,-1,1))/Mathf.PI+.5f);
        }
        public static Vector3 Direction(float u,float v)
        {
            if(v<=0)return Vector3.down;
            if(v>=1)return Vector3.up;
            u=Mathf.Repeat(u,1);
            float lon=(u-.5f)*2*Mathf.PI, lat=(v-.5f)*Mathf.PI, cos=Mathf.Cos(lat);
            return new Vector3(Mathf.Sin(lon)*cos, Mathf.Sin(lat), Mathf.Cos(lon)*cos);
        }

        // Matches GPU bilinear sampling at texel centres, Repeat U / Clamp V, explicit LOD 0.
        public PlanetSample Sample(Vector3 localUnitDirection)
        {
            Vector2 uv=Coordinates(localUnitDirection);
            float x=uv.x*Width-.5f, y=uv.y*Height-.5f;
            int ix=Mathf.FloorToInt(x), iy=Mathf.FloorToInt(y);
            float fx=x-ix,fy=y-iy;
            int Index(int xx,int yy)=>Mathf.Clamp(yy,0,Height-1)*Width+((xx%Width)+Width)%Width;
            int a=Index(ix,iy),b=Index(ix+1,iy),c=Index(ix,iy+1),d=Index(ix+1,iy+1);
            float Blend(float aa,float bb,float cc,float dd)=>Mathf.Lerp(Mathf.Lerp(aa,bb,fx),Mathf.Lerp(cc,dd,fx),fy);
            float water=Blend(SurfaceMap[a].r,SurfaceMap[b].r,SurfaceMap[c].r,SurfaceMap[d].r)/255f;
            int nearest=Index(Mathf.RoundToInt(x),Mathf.RoundToInt(y));
            var type=water>=.5f ? (PlanetWater)Mathf.Clamp(SurfaceMap[nearest].g,1,3) : PlanetWater.None;
            // Near a boundary the nearest texel may be land; take the dominant wet neighbour's type.
            if(type!=PlanetWater.None && SurfaceMap[nearest].r==0)
            {
                int wet=SurfaceMap[a].r>0?a:SurfaceMap[b].r>0?b:SurfaceMap[c].r>0?c:d;
                type=(PlanetWater)Mathf.Clamp(SurfaceMap[wet].g,1,3);
            }
            return new PlanetSample(Blend(MapElevation[a],MapElevation[b],MapElevation[c],MapElevation[d]),
                Blend(MapMoisture[a],MapMoisture[b],MapMoisture[c],MapMoisture[d]),(PlanetBiome)SurfaceMap[nearest].b,type);
        }
    }
}
