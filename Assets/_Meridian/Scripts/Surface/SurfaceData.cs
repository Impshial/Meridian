using System;
using System.Threading;
using UnityEngine;

namespace Meridian
{
    /// <summary>Frozen gnomonic tangent frame: +X east, +Z north, +Y local height. The pole fallback is saved, never recomputed per tile.</summary>
    [Serializable]
    public struct SurfaceFrame
    {
        public Vector3 anchor,east,north;
        public float mappingRadius;
        public SurfaceFrame(Vector3 selectedDirection,float radius)
        {
            anchor=selectedDirection.normalized;mappingRadius=radius;
            east=Vector3.Cross(Mathf.Abs(anchor.y)>.999f?Vector3.forward:Vector3.up,anchor).normalized;
            north=Vector3.Cross(anchor,east).normalized;
        }
        public Vector3 Direction(float x,float z)=>(anchor+east*(x/mappingRadius)+north*(z/mappingRadius)).normalized;
        public Vector2 Project(Vector3 direction)
        {
            float denominator=Vector3.Dot(direction,anchor);
            if(denominator<=.01f)return new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            return new Vector2(Vector3.Dot(direction,east),Vector3.Dot(direction,north))*(mappingRadius/denominator);
        }
    }

    public readonly struct SurfaceSample
    {
        public readonly float Height,WaterHeight,Moisture,Temperature,WaterDistance;
        public readonly PlanetWater Water;
        public readonly PlanetBiome Biome;
        public bool IsLand=>Water==PlanetWater.None;
        public SurfaceSample(float height,float waterHeight,float moisture,float temperature,float waterDistance,PlanetWater water,PlanetBiome biome)
        {Height=height;WaterHeight=waterHeight;Moisture=moisture;Temperature=temperature;WaterDistance=waterDistance;Water=water;Biome=biome;}
    }

    public enum SurfaceObjectKind { Tree,Rock,Iron,Copper,Ice }

    [Serializable]
    public sealed class SurfaceObjectData
    {
        public string Id;
        public SurfaceObjectKind Kind;
        public Vector3 Position;
        public Vector2Int Owner;
        public float Radius,Yaw,Scale;
        public bool Blocking=true;
    }

    public sealed class SurfaceWaterMesh
    {
        public Vector3[] Vertices;
        public int[] Triangles;
    }

    public sealed class SurfaceTileData
    {
        public Vector2Int Address;
        public Vector2 Origin;
        public float Size,MinHeight,HeightRange;
        // Unity Terrain order is [z,x], and [z,x,layer]. All tiles share the same height datum.
        public float[,] Heights;
        public float[,,] Layers;
        public SurfaceWaterMesh Water;
        public SurfaceObjectData[] Objects;
    }

    [Serializable]
    public sealed class LandingCandidate
    {
        public string surfaceVersion;
        public int planetSeed;
        public Vector3 regionDirection,logicalPosition,geographicDirection;
        public float terrainHeight,yaw;
        public Vector3 Position=>logicalPosition;
        public LandingCandidate Copy()=>new LandingCandidate { surfaceVersion=surfaceVersion,planetSeed=planetSeed,
            regionDirection=regionDirection,logicalPosition=logicalPosition,geographicDirection=geographicDirection,terrainHeight=terrainHeight,yaw=yaw };
    }

    /// <summary>Numerical session data only. Rendering resources belong to the scene, not this record.</summary>
    public sealed class SurfaceWorldData
    {
        public const string GeneratorVersion="meridian-surface-1";
        public string Version=>GeneratorVersion;
        public int Seed=>Planet.Seed;
        public readonly string RegionId;
        public readonly SurfaceFrame Frame;
        public readonly SurfaceParameters Parameters;
        public readonly Rect Bounds;
        public SurfaceTileData[] Tiles {get;internal set;}
        public SurfaceObjectData[] Objects {get;internal set;}
        public LandingCandidate DefaultLanding {get;internal set;}
        public float BuildableArea {get;internal set;}
        public float InteriorClearance {get;internal set;}
        public double GenerationSeconds {get;internal set;}
        public Vector2 PlainCentre {get;internal set;}
        public float PlainHeight {get;internal set;}
        internal bool ShapePlain;
        internal readonly PlanetData Planet;
        internal readonly SurfaceGenerator.Geography Geography;
        internal SurfaceWorldData(PlanetData planet,SurfaceFrame frame,SurfaceParameters parameters)
        {
            Planet=planet;Frame=frame;Parameters=parameters;
            RegionId=$"{planet.Seed}:{Mathf.RoundToInt(frame.anchor.x*1000000)}:{Mathf.RoundToInt(frame.anchor.y*1000000)}:{Mathf.RoundToInt(frame.anchor.z*1000000)}";
            Bounds=new Rect(-parameters.tileSize,-parameters.tileSize,2*parameters.tileSize,2*parameters.tileSize);
            Geography=new SurfaceGenerator.Geography(planet,frame,parameters);
        }
        public SurfaceSample Sample(float x,float z)=>SurfaceGenerator.Sample(this,x,z);
        public SurfaceSample Sample(Vector2 point)=>Sample(point.x,point.y);
        // Placement checks the uploaded heightfield rather than evaluating thousands of graph samples per frame.
        // Generation continues to use Sample/Slope so generating a tile never depends on which tiles are currently resident.
        public float GroundHeight(float x,float z)
        {
            if(Tiles!=null)foreach(var tile in Tiles)
            {
                float u=(x-tile.Origin.x)/tile.Size,v=(z-tile.Origin.y)/tile.Size;
                if(u<0 || u>1 || v<0 || v>1)continue;
                int n=tile.Heights.GetLength(0)-1;float fx=u*n,fz=v*n;
                int ix=Mathf.Min(Mathf.FloorToInt(fx),n-1),iz=Mathf.Min(Mathf.FloorToInt(fz),n-1);
                float h=Mathf.Lerp(Mathf.Lerp(tile.Heights[iz,ix],tile.Heights[iz,ix+1],fx-ix),
                    Mathf.Lerp(tile.Heights[iz+1,ix],tile.Heights[iz+1,ix+1],fx-ix),fz-iz);
                return tile.MinHeight+h*tile.HeightRange;
            }
            return Sample(x,z).Height;
        }
        public float GroundSlope(float x,float z)
        {
            const float step=2;
            float dx=(GroundHeight(x+step,z)-GroundHeight(x-step,z))/(2*step);
            float dz=(GroundHeight(x,z+step)-GroundHeight(x,z-step))/(2*step);
            return Mathf.Atan(Mathf.Sqrt(dx*dx+dz*dz))*Mathf.Rad2Deg;
        }
        public float Slope(float x,float z)
        {
            const float step=2;
            float dx=(Sample(x+step,z).Height-Sample(x-step,z).Height)/(2*step);
            float dz=(Sample(x,z+step).Height-Sample(x,z-step).Height)/(2*step);
            return Mathf.Atan(Mathf.Sqrt(dx*dx+dz*dz))*Mathf.Rad2Deg;
        }
        public SurfaceTileData GenerateTile(Vector2Int address,CancellationToken cancellation=default)=>SurfaceGenerator.GenerateTile(this,address,cancellation);
    }

    public sealed class SurfaceSurveyException:Exception
    {
        public SurfaceSurveyException(string message):base(message){}
    }
}
