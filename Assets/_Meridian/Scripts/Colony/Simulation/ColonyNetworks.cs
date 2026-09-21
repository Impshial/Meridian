using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>Independent physical power, water and sealed-access graphs. Buffers remain owned by structures.</summary>
    public sealed class ColonyNetworks
    {
        readonly ColonySimulation sim;
        readonly Dictionary<string,string> power=new Dictionary<string,string>(),water=new Dictionary<string,string>(),pressure=new Dictionary<string,string>();
        readonly Dictionary<string,List<StructureState>> powerGroups=new Dictionary<string,List<StructureState>>(),waterGroups=new Dictionary<string,List<StructureState>>(),pressureGroups=new Dictionary<string,List<StructureState>>();
        readonly Dictionary<string,float> availableWater=new Dictionary<string,float>();long waterRevision=-1;
        public float PowerSupply{get;private set;}public float PowerDemand{get;private set;}public float AirSupport{get;private set;}
        bool dirty=true;float refresh;
        public int Revision{get;private set;}
        public ColonyNetworks(ColonySimulation owner){sim=owner;}
        public void Dirty(){dirty=true;}
        public void Rebuild(){Build(power,"cable");Build(water,"pipe");Build(pressure,"corridor");Cache(power,powerGroups);Cache(water,waterGroups);Cache(pressure,pressureGroups);availableWater.Clear();dirty=false;Revision++;}
        void Cache(Dictionary<string,string> graph,Dictionary<string,List<StructureState>> groups)
        {groups.Clear();foreach(var b in sim.State.structures)if(graph.TryGetValue(b.id,out string root)){if(!groups.TryGetValue(root,out var list)){list=new List<StructureState>();groups.Add(root,list);}list.Add(b);}}
        void Build(Dictionary<string,string> graph,string link)
        {
            graph.Clear();foreach(var b in sim.State.structures)if(b.phase==BuildPhase.Complete&&b.condition>.15f&&!b.isolated)graph[b.id]=b.id;
            string Root(string id){while(graph[id]!=id)id=graph[id];return id;}
            foreach(var edge in sim.State.structures)if(edge.definition==link&&edge.phase==BuildPhase.Complete&&!edge.isolated&&edge.enabled&&edge.condition>.15f&&edge.from!=null&&edge.to!=null&&graph.ContainsKey(edge.from)&&graph.ContainsKey(edge.to))
            {string a=Root(edge.from),b=Root(edge.to);if(a!=b)graph[b]=a;graph[edge.id]=a;}
            foreach(string id in graph.Keys.ToArray())graph[id]=Root(id);
        }
        public bool Connected(string a,string b,string kind="corridor")
        {if(dirty)Rebuild();var graph=kind=="cable"?power:kind=="pipe"?water:pressure;return a!=null&&b!=null&&graph.TryGetValue(a,out var aa)&&graph.TryGetValue(b,out var bb)&&aa==bb;}
        public IEnumerable<StructureState> Group(string id,string kind)
        {if(dirty)Rebuild();var graph=kind=="cable"?power:kind=="pipe"?water:pressure;var groups=kind=="cable"?powerGroups:kind=="pipe"?waterGroups:pressureGroups;return id!=null&&graph.TryGetValue(id,out string root)&&groups.TryGetValue(root,out var list)?(IEnumerable<StructureState>)list:Array.Empty<StructureState>();}
        public bool Pressurized(string id){var building=sim.Structure(id);return building!=null&&building.phase==BuildPhase.Complete&&building.condition>.15f&&building.air>.001f;}
        public bool SafeLanding(StructureState pad,out string reason)
        {
            if(pad==null||pad.phase!=BuildPhase.Complete||pad.condition<=.2f||!pad.enabled){reason="No intact landing apron";return false;}
            var hab=sim.State.structures.FirstOrDefault(b=>b.definition=="habitat"&&b.phase==BuildPhase.Complete&&Pressurized(b.id)&&Connected(pad.id,b.id));
            if(hab==null){reason="Landing connection needs a pressurized corridor to a supplied habitat";return false;}
            reason="Safe sealed arrival route";return true;
        }
        public float WaterAvailable(string building)
        {
            if(dirty)Rebuild();if(waterRevision!=sim.Stock.WaterRevision){waterRevision=sim.Stock.WaterRevision;availableWater.Clear();}
            if(building==null||!water.TryGetValue(building,out string root))return 0;
            if(!availableWater.TryGetValue(root,out float amount)){amount=waterGroups[root].Sum(b=>sim.Stock.Available(sim.Stock.Get(b.waterInventory),Good.Water));availableWater.Add(root,amount);}return amount;
        }
        public float DrawWater(string building,float requested,InventoryState destination=null)
        {
            float left=requested;var position=sim.Position(building);
            foreach(var b in Group(building,"pipe").Where(b=>b.waterInventory!=null).OrderBy(b=>Vector3.SqrMagnitude(b.position-position)))
            {
                var source=sim.Stock.Get(b.waterInventory);if(source==null)continue;float quantity=Mathf.Min(left,sim.Stock.Available(source,Good.Water));
                float used=destination==null?(sim.Stock.Consume(source,Good.Water,quantity)?quantity:0):sim.Stock.Transfer(source,destination,Good.Water,quantity);
                left-=used;if(left<=.0001f)break;
            }
            return requested-left;
        }
        public float StoreWater(string source,float quantity)
        {
            float left=quantity;foreach(var b in Group(source,"pipe").OrderBy(b=>b.definition=="tank"?0:1)){var inv=sim.Stock.Get(b.waterInventory);if(inv==null)continue;left-=sim.Stock.Add(inv,Good.Water,left);if(left<=.0001f)break;}return quantity-left;
        }
        public void Tick(float dt)
        {
            refresh-=dt;if(dirty||refresh<=0){Rebuild();refresh=2;}
            PowerSupply=0;PowerDemand=0;AirSupport=0;
            foreach(var b in sim.State.structures)b.powerFraction=sim.Definition(b).power==0?1:0;
            var groups=sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&power.ContainsKey(b.id)).GroupBy(b=>power[b.id]);
            float hours=dt/(sim.Day/24f);
            foreach(var group in groups)
            {
                var buildings=group.Where(b=>b.enabled&&!b.paused&&b.condition>.15f).ToArray();
                float supply=0;
                foreach(var b in buildings){var d=sim.Definition(b);float efficiency=b.definition=="solar"?Mathf.Max(0,Mathf.Sin(((float)(sim.State.time/sim.Day)%1-.25f)*Mathf.PI*2))*(1-sim.State.environment.storm*.8f):b.definition=="wind"?Mathf.Clamp(sim.State.environment.wind,.15f,1):1;supply+=d.generation*efficiency*b.condition;}
                var consumers=buildings.Where(b=>sim.Definition(b).power>0).OrderByDescending(b=>Safety(b.definition)+b.priority).ToArray();
                float demand=consumers.Sum(b=>sim.Definition(b).power*(sim.Definition(b).link?Mathf.Max(1,b.length/10):1));PowerSupply+=supply;PowerDemand+=demand;
                var batteries=buildings.Where(b=>b.definition=="battery").ToArray();float available=supply;
                foreach(var battery in batteries){float draw=Mathf.Min(Mathf.Max(0,demand-available),80,battery.battery/Mathf.Max(hours,.000001f));battery.battery-=draw*hours;available+=draw;}
                foreach(var b in consumers){float wanted=sim.Definition(b).power*(sim.Definition(b).link?Mathf.Max(1,b.length/10):1);float used=Mathf.Min(available,wanted);b.powerFraction=used/wanted;available-=used;if(b.powerFraction<.95f)b.blocker="Insufficient connected power";}
                foreach(var battery in batteries){float capacity=sim.State.completedResearch.Contains("power")?500:400;float input=Mathf.Min(available,80,(capacity-battery.battery)/Mathf.Max(hours,.000001f));battery.battery+=input*hours;available-=input;}
            }
            foreach(var b in sim.State.structures)
            {
                if(b.phase!=BuildPhase.Complete||b.paused||!b.enabled||b.condition<=.15f)continue;
                if(b.definition=="well"&&b.powerFraction>.95f){float stored=StoreWater(b.id,100*dt/sim.Day);b.blocker=stored>.00001f?"Pumping groundwater":"Connected tanks are full";}
                if(b.definition=="air"&&b.powerFraction>.95f)
                {
                    var modules=Group(b.id,"corridor").Where(m=>sim.Definition(m).sealedModule&&m.air<m.airCapacity).ToArray();
                    float wanted=Mathf.Min(32*dt/sim.Day,modules.Sum(m=>m.airCapacity-m.air));float efficiency=sim.State.completedResearch.Contains("life")?.8f:1;
                    float waterNeeded=wanted*10/32*efficiency,provided=DrawWater(b.id,waterNeeded),produced=waterNeeded>0?wanted*provided/waterNeeded:0;
                    if(provided+1e-5f<waterNeeded)b.blocker="Air processor needs connected water";else{b.blocker="Supplying sealed modules";AirSupport+=32;}
                    foreach(var m in modules.OrderBy(m=>m.air/Mathf.Max(.001f,m.airCapacity))){float put=Mathf.Min(produced,m.airCapacity-m.air);m.air+=put;produced-=put;if(produced<=0)break;}
                }
            }
            foreach(var a in sim.SurfaceActors.Where(a=>a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor))
            {
                var building=sim.Structure(a.building);if(a.eva||sim.OutdoorsSafe)continue;
                if(building!=null&&(building.definition=="apron"||building.definition=="spaceport"))building=Group(building.id,"corridor").FirstOrDefault(b=>sim.Definition(b).sealedModule&&b.air>0);
                if(building!=null&&building.air>0)building.air=Mathf.Max(0,building.air-dt/sim.Day);
                else{a.health=Mathf.Max(0,a.health-dt*.15f);a.reason="Breathable air reserve exhausted";}
            }
            foreach(var b in sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&sim.Definition(b).sealedModule))
            {
                // Transfer existing air only through intact sealed links; splitting cannot copy a reserve.
                if(dt>0)foreach(var other in Group(b.id,"corridor").Where(x=>x.id!=b.id&&sim.Definition(x).sealedModule&&x.air<x.airCapacity*.5f).Take(4))
                {float share=Mathf.Min(Mathf.Max(0,b.air-b.airCapacity*.5f),Mathf.Max(0,(other.airCapacity-other.air)*.1f));b.air-=share;other.air+=share;}
                if(b.air<=.001f)b.blocker="No breathable air; connect and supply an air processor";
                else if(b.powerFraction>.95f&&!b.paused&&b.condition>.15f)b.blocker="Ready";
            }
        }
        static int Safety(string definition)=>definition=="air"||definition=="well"||definition=="habitat"||definition=="clinic"?100:0;
    }
}
