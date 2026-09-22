using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>Sealed routes use actual corridors; exterior routes use bounded terrain-aware A*.</summary>
    public sealed class ColonyNavigation
    {
        readonly ColonySimulation sim;int routeBudget=3;readonly Dictionary<Vector2,bool> groundCache=new Dictionary<Vector2,bool>();
        readonly Dictionary<Vector2Int,List<StructureState>> obstacles=new Dictionary<Vector2Int,List<StructureState>>();
        int indexedCount;bool indexDirty;
        readonly Dictionary<string,List<StructureState>> passages=new Dictionary<string,List<StructureState>>();int passageRevision=-1;
        public ColonyNavigation(ColonySimulation owner){sim=owner;RebuildIndex();}
        static Vector2Int ObstacleCell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/40),Mathf.FloorToInt(p.z/40));
        public void IndexStructure(StructureState b)
        {
            var d=sim.Definition(b);Vector3 low,high;
            if(d.link){low=Vector3.Min(b.position,b.end)-Vector3.one*(d.size.x+2);high=Vector3.Max(b.position,b.end)+Vector3.one*(d.size.x+2);}
            else{float radius=d.size.magnitude*.5f+2;low=b.position-Vector3.one*radius;high=b.position+Vector3.one*radius;}
            var min=ObstacleCell(low);var max=ObstacleCell(high);
            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++){var cell=new Vector2Int(x,z);if(!obstacles.TryGetValue(cell,out var list)){list=new List<StructureState>();obstacles.Add(cell,list);}list.Add(b);}
            indexedCount=sim.State.structures.Count;indexDirty=true;
        }
        void RebuildIndex(){obstacles.Clear();foreach(var b in sim.State.structures)IndexStructure(b);indexedCount=sim.State.structures.Count;indexDirty=false;}
        public bool Arrived(ActorState actor)=>actor.pathIndex>=actor.path.Count;
        public Vector3 Door(StructureState building,Vector3 toward)
        {
            var d=sim.Definition(building);var q=Quaternion.Euler(0,building.yaw,0);var local=Quaternion.Inverse(q)*(toward-building.position);
            Vector3 port=Mathf.Abs(local.x)>Mathf.Abs(local.z)?new Vector3(Mathf.Sign(local.x)*d.size.x*.5f,0,0):new Vector3(0,0,Mathf.Sign(local.z)*d.size.y*.5f);
            return building.position+q*port+Vector3.up*(building.foundation+.75f);
        }
        public Vector3 InteriorPoint(StructureState b,ActorState actor,string activity)
        {
            var d=sim.Definition(b);if(d.link)return Vector3.Lerp(b.position,b.end,.5f);
            int slot=0;var peers=sim.SurfaceActors.Where(a=>activity=="sleep"?a.home==b.id:a.workplace==b.id).OrderBy(a=>a.id).ToArray();for(int i=0;i<peers.Length;i++)if(peers[i].id==actor.id){slot=i;break;}
            if(peers.Length==0){foreach(char c in actor.id)slot=unchecked(slot*31+c);slot=(slot&int.MaxValue)%8;}
            var local=new Vector3((slot%2==0?-1:1)*d.size.x*.25f,0,(slot/2%4-1.5f)*d.size.y*.18f);
            if(activity!="sleep")local.z-=1.3f;
            return b.position+Quaternion.Euler(0,b.yaw,0)*local+Vector3.up*(b.foundation+.75f);
        }
        public Vector3 ServicePoint(StructureState building,Vector3 toward)
        {
            var d=sim.Definition(building);var candidates=new List<Vector3>();
            if(d.link)
            {var sideways=Vector3.Cross(Vector3.up,(building.end-building.position).normalized)*3.5f;candidates.Add((building.position+building.end)*.5f+sideways);candidates.Add((building.position+building.end)*.5f-sideways);}
            else
            {
                var rotation=Quaternion.Euler(0,building.yaw,0);
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)if(x!=0||z!=0)candidates.Add(building.position+rotation*new Vector3(x*(d.size.x*.5f+2.5f),0,z*(d.size.y*.5f+2.5f)));
            }
            var clear=candidates.Where(p=>Clear(p,building.id,false)).OrderBy(p=>Vector3.SqrMagnitude(p-toward)).ToArray();
            return sim.World.Ground(clear.Length>0?clear[0]:candidates[0])+Vector3.up*.15f;
        }
        public bool Go(ActorState actor,Vector3 target,string building=null,bool allowExterior=false)
        {
            if(routeBudget<=0){actor.reason="Planning route";return false;}routeBudget--;
            List<Vector3> path;var rooms=new List<string>();
            bool person=actor.kind==ActorKind.Colonist||actor.kind==ActorKind.Visitor;
            if(person&&!actor.eva&&!sim.OutdoorsSafe&&!allowExterior)
            {
                path=Sealed(actor.building,building,actor.position,target,out rooms);if(path==null){actor.reason="No safe pressurized route";return false;}
            }
            else path=Exterior(actor.position,target,building,actor.kind==ActorKind.Drone);
            if(path==null){actor.reason="No safe route; check water, slope and clearance";return false;}
            actor.path=path;actor.pathRooms=rooms;actor.pathIndex=0;actor.destination=target;actor.target=building;actor.reason="";return true;
        }
        List<Vector3> Sealed(string start,string finish,Vector3 at,Vector3 target,out List<string> rooms)
        {
            rooms=new List<string>();if(start==finish&&start!=null){rooms.Add(finish);return new List<Vector3>{target};}
            if(!sim.Networks.Connected(start,finish))return null;
            if(passageRevision!=sim.Networks.Revision)
            {
                passages.Clear();foreach(var edge in sim.State.structures)
                {
                    if(edge.definition!="corridor"||edge.phase!=BuildPhase.Complete||edge.condition<=.15f||edge.isolated||!edge.enabled||edge.from==null||edge.to==null)continue;
                    foreach(var node in new[]{edge.from,edge.to}){if(!passages.TryGetValue(node,out var links)){links=new List<StructureState>();passages.Add(node,links);}links.Add(edge);}
                }
                passageRevision=sim.Networks.Revision;
            }
            var prefix=new List<Vector3>();var currentPassage=sim.Structure(start);
            if(currentPassage?.definition=="corridor")
            {bool nearStart=Vector3.SqrMagnitude(at-currentPassage.position)<Vector3.SqrMagnitude(at-currentPassage.end);start=nearStart?currentPassage.from:currentPassage.to;prefix.Add(nearStart?currentPassage.position:currentPassage.end);rooms.Add(start);}
            var previous=new Dictionary<string,(string node,StructureState edge)>();var queue=new Queue<string>();queue.Enqueue(start);previous[start]=(null,null);
            while(queue.Count>0)
            {
                var current=queue.Dequeue();if(current==finish)break;
                if(!passages.TryGetValue(current,out var connected))continue;
                foreach(var edge in connected)
                {
                    string next=edge.from==current?edge.to:edge.to==current?edge.from:null;if(next==null||previous.ContainsKey(next))continue;
                    var node=sim.Structure(next);if(node==null||(!sim.Definition(node).sealedModule&&node.definition!="apron"&&node.definition!="spaceport"))continue;
                    if(next!=finish&&!sim.Networks.Pressurized(next)&&node.definition!="apron")continue;
                    previous[next]=(current,edge);queue.Enqueue(next);
                }
            }
            if(!previous.ContainsKey(finish))return null;
            var edges=new List<(string node,StructureState edge)>();for(string node=finish;node!=start;node=previous[node].node)edges.Add((previous[node].node,previous[node].edge));edges.Reverse();
            var result=prefix;foreach(var item in edges){bool forward=item.edge.from==item.node;result.Add(forward?item.edge.position:item.edge.end);rooms.Add(item.edge.id);result.Add(forward?item.edge.end:item.edge.position);rooms.Add(forward?item.edge.to:item.edge.from);}result.Add(target);rooms.Add(finish);return result;
        }
        bool Clear(Vector3 at,string ignore,bool flying)
        {
            if(!sim.World.Owned(at))return false;if(flying)return true;
            if(obstacles.TryGetValue(ObstacleCell(at),out var candidates))foreach(var b in candidates){if(b.id==ignore||b.phase==BuildPhase.Removed||b.phase==BuildPhase.Clearing||b.phase==BuildPhase.Delivery||b.definition=="apron"||b.definition=="spaceport"||ColonyUtilities.Underground(sim.Definition(b))||b.definition=="cargo"||b.definition=="park"||b.definition=="field")continue;if(ColonyCommands.Contains(sim,b,at,1))return false;}
            var key=new Vector2(at.x,at.z);if(groundCache.TryGetValue(key,out bool passable))return passable;
            if(groundCache.Count>50000)groundCache.Clear();return groundCache[key]=sim.World.Walkable(at);
        }
        bool Direct(Vector3 a,Vector3 b,string ignore,bool flying)
        {int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/6);for(int i=1;i<steps;i++)if(!Clear(Vector3.Lerp(a,b,i/(float)steps),ignore,flying))return false;return true;}
        List<Vector3> Exterior(Vector3 from,Vector3 to,string ignore,bool flying)
        {
            if(!sim.World.Owned(to))return null;
            if(Direct(from,to,ignore,flying))
            {
                if(!flying)return new List<Vector3>{to};
                var flight=new List<Vector3>{from+Vector3.up*35};int segments=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/50));
                for(int i=1;i<=segments;i++){var p=Vector3.Lerp(from,to,i/(float)segments);p.y=Mathf.Max(from.y,to.y,sim.World.Height(p.x,p.z))+35;flight.Add(p);}flight.Add(to);return flight;
            }
            const float cell=5;Vector2Int Key(Vector3 p)=>new Vector2Int(Mathf.RoundToInt(p.x/cell),Mathf.RoundToInt(p.z/cell));
            var points=new Dictionary<Vector2Int,Vector3>();var traversable=new Dictionary<Vector2Int,bool>();
            Vector3 Point(Vector2Int p){if(!points.TryGetValue(p,out var position)){position=sim.World.Ground(new Vector3(p.x*cell,0,p.y*cell));points.Add(p,position);}return position;}
            bool Pass(Vector2Int p){if(!traversable.TryGetValue(p,out bool clear)){clear=Clear(Point(p),ignore,flying);traversable.Add(p,clear);}return clear;}
            var start=Key(from);var finish=Key(to);var costs=new Dictionary<Vector2Int,float>{{start,0}};var previous=new Dictionary<Vector2Int,Vector2Int>();var closed=new HashSet<Vector2Int>();
            var open=new SortedSet<(float score,int order,Vector2Int point)>(Comparer<(float,int,Vector2Int)>.Create((a,b)=>{int cmp=a.Item1.CompareTo(b.Item1);return cmp!=0?cmp:a.Item2.CompareTo(b.Item2);}));int order=0;open.Add(((finish-start).magnitude,order++,start));
            while(open.Count>0&&closed.Count<2400)
            {
                var next=open.Min;open.Remove(next);var here=next.point;if(!closed.Add(here))continue;if(here==finish)
                {var path=new List<Vector3>{to};for(var k=here;k!=start;k=previous[k])path.Add(Point(k));path.Reverse();return path;}
                for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
                {
                    if(x==0&&z==0)continue;var candidate=here+new Vector2Int(x,z);if(closed.Contains(candidate))continue;
                    if(candidate!=finish&&!Pass(candidate))continue;
                    if(x!=0&&z!=0&&(!Pass(here+new Vector2Int(x,0))||!Pass(here+new Vector2Int(0,z))))continue;
                    float cost=costs[here]+(x==0||z==0?1:1.4142f);if(costs.TryGetValue(candidate,out float old)&&old<=cost)continue;
                    costs[candidate]=cost;previous[candidate]=here;open.Add((cost+(finish-candidate).magnitude,order++,candidate));
                }
            }
            return null;
        }
        public void Stop(ActorState actor){actor.path.Clear();actor.pathRooms.Clear();actor.pathIndex=0;}
        public void Tick(float dt)
        {
            routeBudget=3;
            // Link endpoints are filled immediately after creation; refresh once at the next tick.
            if(indexDirty||indexedCount!=sim.State.structures.Count)RebuildIndex();
            foreach(var actor in sim.SurfaceActors)
            {
                if(actor.paused||actor.health<=0||actor.condition<=.08f||Arrived(actor))continue;
                if(!actor.eva&&actor.pathRooms.Count>0&&!sim.Networks.Connected(actor.building,actor.target))
                {Stop(actor);actor.reason="Sealed route interrupted by a disconnected bulkhead";continue;}
                if(actor.kind!=ActorKind.Colonist&&actor.kind!=ActorKind.Visitor&&actor.charge<=0)continue;
                float speed=actor.kind==ActorKind.Drone?18:actor.kind==ActorKind.Colonist||actor.kind==ActorKind.Visitor?4.2f:8;
                Vector3 next=actor.path[actor.pathIndex],delta=next-actor.position;delta.y=0;if(delta.sqrMagnitude>.05f)actor.yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
                actor.position=Vector3.MoveTowards(actor.position,next,speed*dt);
                if(Vector3.Distance(actor.position,next)<.15f){if(actor.pathIndex<actor.pathRooms.Count)actor.building=actor.pathRooms[actor.pathIndex];actor.pathIndex++;if(Arrived(actor)&&actor.target!=null&&sim.Structure(actor.target)!=null)actor.building=actor.target;}
                if(actor.kind!=ActorKind.Colonist&&actor.kind!=ActorKind.Visitor)actor.charge=Mathf.Max(0,actor.charge-dt*.016f*(sim.State.completedResearch.Contains("automation")?.8f:1));
                if(actor.eva)actor.suit=Mathf.Max(0,actor.suit-dt*.12f*(1+sim.State.environment.storm));
            }
        }
    }
}
