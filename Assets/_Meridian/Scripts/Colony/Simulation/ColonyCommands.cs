using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public static class ColonyCommands
    {
        public static void DiscardCargo(ColonySimulation sim,StructureState pile)
        {
            if(pile==null||pile.definition!="cargo"||pile.phase==BuildPhase.Removed)return;
            foreach(var job in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&j.source==pile.inventory&&!j.carrying).ToArray())
                sim.Jobs.Cancel(job,"Uncollected pile discarded");
            var stock=sim.Stock.Get(pile.inventory);
            foreach(var item in stock.items.ToArray())sim.Stock.Consume(stock,item.good,item.quantity);
            pile.phase=BuildPhase.Removed;pile.blocker="Discarded by player";sim.Notify(pile.id);
        }
        public static bool Contains(ColonySimulation sim,StructureState b,Vector3 point,float margin=0)
        {
            var d=sim.Definition(b);
            if(d.link){var delta=b.end-b.position;float t=Mathf.Clamp01(Vector3.Dot(point-b.position,delta)/Mathf.Max(.001f,delta.sqrMagnitude));return ColonyWorld.XZ(point-Vector3.Lerp(b.position,b.end,t)).magnitude<d.size.x*.5f+margin;}
            var p=Quaternion.Euler(0,-b.yaw,0)*(point-b.position);return Mathf.Abs(p.x)<d.size.x*.5f+margin&&Mathf.Abs(p.z)<d.size.y*.5f+margin;
        }
        public static IEnumerable<SurfaceObjectData> Obstacles(ColonySimulation sim,string definition,Vector3 at,float yaw,Vector3 end)
        {
            var b=new StructureState{definition=definition,position=at,yaw=yaw,end=end};var d=sim.Definition(b);
            if(ColonyUtilities.Underground(d))return Array.Empty<SurfaceObjectData>();
            var center=d.link?(at+end)*.5f:at;float radius=d.link?Vector3.Distance(at,end)*.5f+5:d.size.magnitude*.5f+5;
            return sim.World.Nearby(center,radius).Where(o=>Contains(sim,b,o.Position,o.Radius+.5f));
        }
        public static List<Amount> Cost(ColonySimulation sim,StructureState b)
        {float factor=sim.Definition(b).link?Mathf.Max(1,b.length/10):1;return sim.Definition(b).cost.Select(c=>new Amount(c.good,Mathf.Ceil(c.quantity*factor))).ToList();}
        public static string ValidatePlacement(ColonySimulation sim,string definition,Vector3 at,float yaw,Vector3 end,string from=null,string to=null,string ignore=null)
        {
            var d=sim.Catalog.Building(definition);if(!string.IsNullOrEmpty(d.research)&&!sim.State.completedResearch.Contains(d.research))return "Research required: "+sim.Catalog.Research(d.research).name;
            if((definition=="park"||definition=="field")&&!sim.OutdoorsSafe)return "Atmosphere and climate must both reach 80%";
            if(definition=="field"&&sim.State.environment.soil<80)return "Soil suitability must reach 80%";
            var b=new StructureState{definition=definition,position=at,yaw=yaw,end=end};var q=Quaternion.Euler(0,yaw,0);var points=new List<Vector3>();
            if(d.link)
            {
                var source=sim.Structure(from);var target=sim.Structure(to);if(source==null||target==null||from==to)return "Select two different connection ports";
                if(source.definition=="cargo"||target.definition=="cargo"||ColonyUtilities.Underground(d)&&(sim.Definition(source).link||sim.Definition(target).link))return "Choose a facility connection port";
                if(source.phase==BuildPhase.Removed||target.phase==BuildPhase.Removed)return "Connection endpoint no longer exists";
                if(definition=="corridor"&&(!PressurePort(sim,source)||!PressurePort(sim,target)))return "Sealed corridors connect domes, airlocks, landing pads and air processors";
                if(sim.State.structures.Any(x=>x.phase!=BuildPhase.Removed&&x.definition==definition&&(x.from==from&&x.to==to||x.from==to&&x.to==from)))return "These ports are already connected";
                float length=Vector3.Distance(at,end);if(length>500)return "Maximum connection length is 500 m; add an intermediate hub";
                for(float t=0;t<=1;t+=1f/Mathf.Max(1,Mathf.Ceil(length/5)))points.Add(Vector3.Lerp(at,end,t));
            }
            else for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)points.Add(at+q*new Vector3(x*d.size.x*.5f,0,z*d.size.y*.5f));
            float min=float.MaxValue,max=float.MinValue;
            foreach(var point in points)
            {
                if(!sim.World.Owned(point))return "Footprint extends outside owned land";
                if(!sim.World.Surface.Sample(point.x,point.z).IsLand)return "Footprint overlaps water";
                float height=sim.World.Height(point.x,point.z);min=Mathf.Min(min,height);max=Mathf.Max(max,height);
                if(!ColonyUtilities.Underground(d)&&sim.World.Slope(point.x,point.z)>(d.link?22:12))return "Terrain slope exceeds foundation tolerance";
            }
            if(!d.link&&max-min>3.5f)return "Height difference exceeds the 3.5 m foundation limit";
            foreach(var other in sim.State.structures.Where(s=>s.phase!=BuildPhase.Removed&&s.id!=ignore&&s.id!=from&&s.id!=to&&s.definition!="cargo"))
            {
                if(ColonyUtilities.Underground(d))continue;
                if(sim.Definition(other).link&&(definition!="corridor"||other.definition!="corridor"))continue;
                if(points.Any(p=>Contains(sim,other,p,d.link?.4f:2))||!d.link&&Contains(sim,b,other.position,2))return "Footprint or doorway clearance overlaps "+other.name;
            }
            if(definition=="miner"||definition=="quarry"||definition=="ice")
            {
                var deposit=Deposit(sim,definition,at);if(deposit==null)return definition=="miner"?"Place over an iron or copper deposit":definition=="ice"?"Place over an ice deposit":"Place over a mineral deposit";
                if(sim.State.structures.Any(s=>s.phase!=BuildPhase.Removed&&s.deposit==deposit.Id&&s.id!=ignore))return "Deposit already has extraction equipment";
            }
            return null;
        }
        public static bool PressurePort(ColonySimulation sim,StructureState b)=>sim.Definition(b).sealedModule||b.definition=="apron"||b.definition=="spaceport"||b.definition=="air";
        public static SurfaceObjectData Deposit(ColonySimulation sim,string type,Vector3 at)=>sim.World.Nearby(at,18).Where(o=>type=="miner"?(o.Kind==SurfaceObjectKind.Iron||o.Kind==SurfaceObjectKind.Copper):type=="ice"?o.Kind==SurfaceObjectKind.Ice:o.Kind==SurfaceObjectKind.Rock&&o.Radius>2.5f).OrderBy(o=>Vector3.SqrMagnitude(o.Position-at)).FirstOrDefault();
        public static StructureState Place(ColonySimulation sim,string type,Vector3 position,float yaw,Vector3 end=default,string from=null,string to=null)
        {
            string error=ValidatePlacement(sim,type,position,yaw,end,from,to);if(error!=null)throw new InvalidOperationException(error);
            var b=sim.AddStructure(type,sim.Catalog.Building(type).link?position:sim.World.Ground(position),yaw);b.end=end;b.from=from;b.to=to;b.length=Vector3.Distance(position,end);
            if(type=="miner"||type=="quarry"||type=="ice")b.deposit=Deposit(sim,type,position)?.Id;
            ColonyUtilities.Normalize(sim,b);sim.Notify(b.id);return b;
        }
        public static void Dismantle(ColonySimulation sim,StructureState b,bool replacement=false)
        {
            if(b==null||b.phase==BuildPhase.Removed||b.definition=="ship"||b.definition=="apron")throw new InvalidOperationException("The expedition ship and arrival apron are permanent infrastructure");
            if(sim.SurfaceActors.Any(a=>a.building==b.id&&(a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor)))throw new InvalidOperationException("Occupied module: relocate residents before dismantling");
            foreach(var j in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&j.target==b.id).ToArray())sim.Jobs.Cancel(j,"Site dismantling");
            b.replacement=replacement;b.enabled=false;b.paused=false;b.phase=BuildPhase.Dismantling;sim.Networks.Dirty();sim.Notify(b.id);
        }
        public static void Recover(ColonySimulation sim,StructureState b)
        {
            if(b.phase==BuildPhase.Removed)return;
            foreach(var j in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&(j.source==b.inventory||j.destination==b.inventory||b.waterInventory!=null&&(j.source==b.waterInventory||j.destination==b.waterInventory))).ToArray())sim.Jobs.Cancel(j,"Storage dismantled; cargo recoverable");
            var pile=sim.DropCargo(b.position,b.incorporated.Select(c=>new Amount(c.good,c.quantity*sim.Catalog.balance.refundFraction)),"Recovered · "+b.name);
            var storage=sim.Stock.Get(pile.inventory);
            foreach(var source in new[]{sim.Stock.Get(b.inventory),sim.Stock.Get(b.waterInventory)})
                if(source!=null)foreach(var item in source.items.ToArray())sim.Stock.Transfer(source,storage,item.good,item.quantity);
            // Committed recipe inputs are physically inside the dismantled machine and are recoverable, not re-issued production.
            foreach(var item in b.batch)sim.Stock.Add(storage,item.good,item.quantity);b.batch.Clear();b.inputsCommitted=false;b.incorporated.Clear();b.phase=BuildPhase.Removed;
            sim.Networks.Dirty();sim.Notify(b.id);
            if(b.replacement)
            {
                var next=sim.AddStructure(b.definition,b.position,b.yaw);next.from=b.from;next.to=b.to;next.end=b.end;next.length=b.length;next.deposit=b.deposit;next.recipe=b.recipe;next.name=b.name;
                foreach(var edge in sim.State.structures.Where(e=>sim.Definition(e).link&&e.phase!=BuildPhase.Removed)){if(edge.from==b.id)edge.from=next.id;if(edge.to==b.id)edge.to=next.id;}
                sim.Notify(next.id);
            }
        }
        public static void Designate(ColonySimulation sim,Vector3 center,float radius)
        {foreach(var o in sim.World.Nearby(center,radius)){if(!sim.World.Owned(o.Position))continue;var d=sim.World.Change(o.Id);d.designated=true;}}
        public static void SetRecipe(ColonySimulation sim,StructureState b,string recipe)
        {
            var definition=sim.Catalog.Recipe(recipe);if(definition==null||definition.facility!=b.definition)throw new InvalidOperationException("Incompatible recipe");
            if(b.inputsCommitted)throw new InvalidOperationException("Finish the committed batch before changing recipe");b.recipe=recipe;b.production=0;
        }
    }
}
