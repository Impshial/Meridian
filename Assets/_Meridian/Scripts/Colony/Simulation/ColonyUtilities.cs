using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>Non-walkable service links share a terrain-relative underground layer.</summary>
    public static class ColonyUtilities
    {
        public const float Depth=3f;
        public static bool Underground(BuildingDefinition definition)=>definition.link&&!definition.sealedModule&&!definition.walkableLink;
        public static Vector3 Buried(ColonyWorld world,Vector3 point)=>world.Ground(point)-Vector3.up*Depth;
        public static Vector3 Port(ColonySimulation sim,StructureState building,Vector3 toward,string type)
        {
            var definition=sim.Definition(building);var rotation=Quaternion.Euler(0,building.yaw,0);
            var direction=Quaternion.Inverse(rotation)*(toward-building.position);
            int side=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)?(direction.x>0?1:3):(direction.z>=0?0:2);
            return PortOnSide(sim,building,side,type);
        }
        public static Vector3 PortOnSide(ColonySimulation sim,StructureState building,int side,string type)
        {
            var d=sim.Definition(building);var q=Quaternion.Euler(0,side*90,0);
            float offset=type=="pipe"?.8f:-.8f;
            var point=building.position+Quaternion.Euler(0,building.yaw,0)*(q*new Vector3(offset,0,(side%2==0?d.size.y:d.size.x)*.5f+1.5f));
            return Buried(sim.World,point);
        }
        public static Vector3[] Path(ColonyWorld world,Vector3 start,Vector3 end)
        {
            int segments=Mathf.Max(1,Mathf.CeilToInt(ColonyWorld.XZ(end-start).magnitude/2));
            var points=new Vector3[segments+1];
            for(int i=0;i<=segments;i++)points[i]=Buried(world,Vector3.Lerp(start,end,i/(float)segments));
            return points;
        }
        public static void Normalize(ColonySimulation sim,StructureState link)
        {
            if(!Underground(sim.Definition(link)))return;
            var from=sim.Structure(link.from);var to=sim.Structure(link.to);
            var start=from!=null?Port(sim,from,to?.position??link.end,link.definition):Buried(sim.World,link.position);
            var end=to!=null?Port(sim,to,from?.position??link.position,link.definition):Buried(sim.World,link.end);
            link.position=start;link.end=end;
        }
    }
}
