using UnityEngine;

namespace Meridian
{
    public readonly struct PlacementResult
    {
        public readonly bool Valid;
        public readonly string Reason;
        public readonly LandingCandidate Candidate;
        public PlacementResult(bool valid,string reason,LandingCandidate candidate=null){Valid=valid;Reason=reason;Candidate=candidate;}
    }

    public static class LandingPlacement
    {
        // The access ramp extends from the rear (-Z). This rectangle includes hull, access, and safety on every side.
        public static Rect Footprint(SurfaceParameters p)=>new Rect(-p.shipWidth*.5f-p.safetyMargin,
            -p.shipLength*.5f-p.accessLength-p.safetyMargin,p.shipWidth+2*p.safetyMargin,p.shipLength+p.accessLength+2*p.safetyMargin);

        public static PlacementResult Evaluate(SurfaceWorldData world,Vector2 logicalPosition,float yaw)
        {
            if(world==null)return new PlacementResult(false,"Survey is not ready");
            var p=world.Parameters;Rect footprint=Footprint(p);
            float angle=yaw*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            Vector2 Rotate(float x,float z)=>logicalPosition+new Vector2(x*c+z*s,-x*s+z*c);
            int nx=Mathf.CeilToInt(footprint.width/p.placementSampleSpacing),nz=Mathf.CeilToInt(footprint.height/p.placementSampleSpacing);
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                Vector2 at=Rotate(footprint.xMin+footprint.width*x/nx,footprint.yMin+footprint.height*z/nz);
                if(at.x<world.Bounds.xMin+2 || at.x>world.Bounds.xMax-2 || at.y<world.Bounds.yMin+2 || at.y>world.Bounds.yMax-2)
                    return new PlacementResult(false,"Deployment area crosses the survey boundary");
                SurfaceSample sample=world.Sample(at);
                if(!sample.IsLand)return new PlacementResult(false,"Deployment area overlaps water");
                if(world.GroundSlope(at.x,at.y)>p.maximumLandingSlope)return new PlacementResult(false,"Ground is too steep for landing");
                float ground=world.GroundHeight(at.x,at.y);
                low=Mathf.Min(low,ground);high=Mathf.Max(high,ground);
                if(high-low>p.maximumLandingVariation)return new PlacementResult(false,"Ground is too uneven for a level landing");
            }
            if(world.Objects!=null)foreach(var obj in world.Objects)
            {
                if(!obj.Blocking)continue;
                Vector2 delta=new Vector2(obj.Position.x,obj.Position.z)-logicalPosition;
                float lx=delta.x*c-delta.y*s,lz=delta.x*s+delta.y*c;
                float dx=Mathf.Max(Mathf.Max(footprint.xMin-lx,0),lx-footprint.xMax),dz=Mathf.Max(Mathf.Max(footprint.yMin-lz,0),lz-footprint.yMax);
                if(dx*dx+dz*dz<=obj.Radius*obj.Radius)
                    return new PlacementResult(false,obj.Kind==SurfaceObjectKind.Tree?"A tree obstructs the landing or access area":"A rock or deposit obstructs the landing area");
            }
            return new PlacementResult(true,"Clear for landing",new LandingCandidate {
                surfaceVersion=world.Version,planetSeed=world.Seed,regionDirection=world.Frame.anchor,
                logicalPosition=new Vector3(logicalPosition.x,high+.3f,logicalPosition.y),terrainHeight=world.GroundHeight(logicalPosition.x,logicalPosition.y),yaw=Mathf.Repeat(yaw,360),
                geographicDirection=world.Frame.Direction(logicalPosition.x,logicalPosition.y)
            });
        }
    }
}
