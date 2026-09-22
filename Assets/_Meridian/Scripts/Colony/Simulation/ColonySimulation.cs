using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>Frozen geography plus sparse persistent resource deltas. No resource GameObject is authoritative.</summary>
    public sealed class ColonyWorld
    {
        public readonly SurfaceWorldData Surface;readonly ColonyState state;
        readonly Dictionary<string,SurfaceObjectData> objects=new Dictionary<string,SurfaceObjectData>();
        readonly Dictionary<Vector2Int,List<SurfaceObjectData>> cells=new Dictionary<Vector2Int,List<SurfaceObjectData>>();
        readonly Dictionary<string,ResourceDelta> changes=new Dictionary<string,ResourceDelta>();
        readonly Dictionary<Vector2Int,SurfaceTileData> tiles=new Dictionary<Vector2Int,SurfaceTileData>();
        public IEnumerable<SurfaceObjectData> Objects=>objects.Values;
        public IEnumerable<SurfaceTileData> Tiles=>tiles.Values;
        public ColonyWorld(SurfaceWorldData surface,ColonyState data)
        {Surface=surface;state=data;foreach(var change in data.resources)changes.Add(change.id,change);AddTiles(surface.Tiles);}
        public void AddTiles(IEnumerable<SurfaceTileData> added)
        {
            foreach(var tile in added){if(tiles.ContainsKey(tile.Address))continue;tiles.Add(tile.Address,tile);foreach(var obj in tile.Objects){objects[obj.Id]=obj;var cell=Cell(obj.Position);if(!cells.TryGetValue(cell,out var list)){list=new List<SurfaceObjectData>();cells.Add(cell,list);}list.Add(obj);}}
        }
        static Vector2Int Cell(Vector3 point)=>new Vector2Int(Mathf.FloorToInt(point.x/80),Mathf.FloorToInt(point.z/80));
        public SurfaceObjectData Object(string id)=>id!=null&&objects.TryGetValue(id,out var value)?value:null;
        public ResourceDelta Existing(string id)=>id!=null&&changes.TryGetValue(id,out var value)?value:null;
        public float InitialAmount(SurfaceObjectData obj)=>obj.Kind==SurfaceObjectKind.Tree?obj.WoodAmount:obj.Kind==SurfaceObjectKind.Rock?(obj.Radius>2.5f?1600:40):5000;
        public ResourceDelta Change(string id){if(changes.TryGetValue(id,out var value))return value;var obj=Object(id);if(obj==null)return null;value=new ResourceDelta{id=id,remaining=InitialAmount(obj)};state.resources.Add(value);changes.Add(id,value);return value;}
        public float Remaining(string id){var delta=Existing(id);return delta!=null?delta.remaining:Object(id)!=null?InitialAmount(Object(id)):0;}
        public bool Removed(string id)=>Existing(id)?.removed??false;
        public Good Yield(SurfaceObjectData obj)=>obj.Kind==SurfaceObjectKind.Tree?Good.Biomass:obj.Kind==SurfaceObjectKind.Iron?Good.IronOre:obj.Kind==SurfaceObjectKind.Copper?Good.CopperOre:obj.Kind==SurfaceObjectKind.Ice?Good.Water:Good.Minerals;
        public IEnumerable<SurfaceObjectData> Nearby(Vector3 point,float radius)
        {
            var min=Cell(point-new Vector3(radius,0,radius));var max=Cell(point+new Vector3(radius,0,radius));float square=radius*radius;
            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++)if(cells.TryGetValue(new Vector2Int(x,z),out var list))foreach(var obj in list)
                if(!Removed(obj.Id)&&XZ(obj.Position-point).sqrMagnitude<=square)yield return obj;
        }
        public static Vector2 XZ(Vector3 value)=>new Vector2(value.x,value.z);
        public float Height(float x,float z)
        {
            var address=Surface.TileOwner(new Vector2(x,z));if(!tiles.TryGetValue(address,out var tile))return Surface.Sample(x,z).Height;
            float fx=Mathf.Clamp01((x-tile.Origin.x)/tile.Size)*(tile.Heights.GetLength(0)-1),fz=Mathf.Clamp01((z-tile.Origin.y)/tile.Size)*(tile.Heights.GetLength(0)-1);
            int n=tile.Heights.GetLength(0)-1,ix=Mathf.Min(n-1,Mathf.FloorToInt(fx)),iz=Mathf.Min(n-1,Mathf.FloorToInt(fz));
            return tile.MinHeight+tile.HeightRange*Mathf.Lerp(Mathf.Lerp(tile.Heights[iz,ix],tile.Heights[iz,ix+1],fx-ix),Mathf.Lerp(tile.Heights[iz+1,ix],tile.Heights[iz+1,ix+1],fx-ix),fz-iz);
        }
        public Vector3 Ground(Vector3 point){point.y=Height(point.x,point.z);return point;}
        public float Slope(float x,float z){float a=(Height(x+2,z)-Height(x-2,z))*.25f,b=(Height(x,z+2)-Height(x,z-2))*.25f;return Mathf.Atan(Mathf.Sqrt(a*a+b*b))*Mathf.Rad2Deg;}
        public bool Owned(Vector3 point){int x=Mathf.FloorToInt((point.x+3000)/6000),z=Mathf.FloorToInt((point.z+3000)/6000);return state.regions.Any(r=>r.owned&&r.x==x&&r.z==z);}
        public bool CanFrame(Vector3 focus,float radius)
        {
            if(!Owned(focus))return false;
            int x0=Mathf.FloorToInt((focus.x-radius+3000)/6000),x1=Mathf.FloorToInt((focus.x+radius+3000)/6000);
            int z0=Mathf.FloorToInt((focus.z-radius+3000)/6000),z1=Mathf.FloorToInt((focus.z+radius+3000)/6000);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                if(state.regions.Any(r=>r.owned&&r.x==x&&r.z==z))continue;
                float dx=focus.x-Mathf.Clamp(focus.x,x*6000-3000,x*6000+3000),dz=focus.z-Mathf.Clamp(focus.z,z*6000-3000,z*6000+3000);
                if(dx*dx+dz*dz<radius*radius-.01f)return false;
            }
            return true;
        }
        public Vector3 NearestFrame(Vector3 focus,float radius)
        {
            if(CanFrame(focus,radius))return focus;
            return state.regions.Where(r=>r.owned).Select(r=>new Vector3(Mathf.Clamp(focus.x,r.x*6000-3000+radius,r.x*6000+3000-radius),focus.y,Mathf.Clamp(focus.z,r.z*6000-3000+radius,r.z*6000+3000-radius))).OrderBy(p=>(p-focus).sqrMagnitude).First();
        }
        public Rect Bounds {get{var owned=state.regions.Where(r=>r.owned).ToArray();return Rect.MinMaxRect(owned.Min(r=>r.x*6000-3000),owned.Min(r=>r.z*6000-3000),owned.Max(r=>r.x*6000+3000),owned.Max(r=>r.z*6000+3000));}}
        public bool Dry(Vector3 point)
        {
            if(tiles.TryGetValue(Surface.TileOwner(new Vector2(point.x,point.z)),out var tile)&&tile.WaterClearance!=null)
            {
                var water=tile.WaterClearance;int n=water.GetLength(0)-1;
                int x=Mathf.Clamp(Mathf.FloorToInt((point.x-tile.Origin.x)/tile.Size*n),0,n-1),z=Mathf.Clamp(Mathf.FloorToInt((point.z-tile.Origin.y)/tile.Size*n),0,n-1);
                float margin=tile.Size/n*2,min=Mathf.Min(water[z,x],water[z,x+1],water[z+1,x],water[z+1,x+1]),max=Mathf.Max(water[z,x],water[z,x+1],water[z+1,x],water[z+1,x+1]);
                if(min>margin)return true;if(max< -margin)return false;
            }
            // Near a shore or narrow channel, use the exact shared geographic classification.
            return Surface.Sample(point.x,point.z).IsLand;
        }
        public bool Walkable(Vector3 point)=>Owned(point)&&Dry(point)&&Slope(point.x,point.z)<32;
    }

    /// <summary>One fixed-step authority. Subsystems share this state and stock service; UI never simulates independently.</summary>
    public sealed class ColonySimulation
    {
        public readonly ColonyState State;public readonly ColonyCatalog Catalog;public readonly ColonyInventory Stock;public readonly ColonyWorld World;
        public readonly ColonyNetworks Networks;public readonly ColonyNavigation Navigation;public readonly ColonyJobs Jobs;public readonly ColonyPeople People;
        public readonly ColonyIndustry Industry;public readonly ColonyTraffic Traffic;public readonly ColonyDevelopment Development;
        readonly Dictionary<string,StructureState> structures=new Dictionary<string,StructureState>();readonly Dictionary<string,ActorState> actors=new Dictionary<string,ActorState>();
        readonly Dictionary<string,BuildingDefinition> definitions;
        public event Action<string> Changed;public event Action<string> ResourceRemoved;
        public float Day=>Catalog.balance.daySeconds;public float Year=>Day*Catalog.balance.daysPerYear;
        public bool OutdoorsSafe=>State.environment.atmosphere>=80&&State.environment.climate>=80;
        public int Population=>State.actors.Count(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Arrived);
        public IEnumerable<ActorState> SurfaceActors=>State.actors.Where(a=>a.location==PersonLocation.Arrived);
        public ColonySimulation(ColonyState state,SurfaceWorldData surface,ColonyCatalog catalog)
        {
            State=state;ColonySaves.NormalizeReferences(state);Catalog=catalog;definitions=catalog.buildings.ToDictionary(d=>d.id);Stock=new ColonyInventory(state,catalog);World=new ColonyWorld(surface,state);Reindex();
            foreach(var b in state.structures)ColonyUtilities.Normalize(this,b);
            Networks=new ColonyNetworks(this);Navigation=new ColonyNavigation(this);Jobs=new ColonyJobs(this);People=new ColonyPeople(this);
            Industry=new ColonyIndustry(this);Traffic=new ColonyTraffic(this);Development=new ColonyDevelopment(this);Networks.Rebuild();
        }
        public void Reindex(){structures.Clear();actors.Clear();foreach(var b in State.structures)structures.Add(b.id,b);foreach(var a in State.actors)actors.Add(a.id,a);}
        public StructureState Structure(string id)=>id!=null&&structures.TryGetValue(id,out var value)?value:null;
        public ActorState Actor(string id)=>id!=null&&actors.TryGetValue(id,out var value)?value:null;
        public Vector3 Position(string id)
        {var region=State.regions.Find(r=>r.id==id);if(region!=null)return World.Ground(new Vector3(region.x*6000,0,region.z*6000));var flight=State.flights.Find(f=>f.id==id);return Structure(id)?.position??Actor(id)?.position??World.Object(id)?.Position??(flight!=null?Structure(flight.pad)?.position:null)??State.setup.landing.Position;}
        public Vector3 InventoryPosition(string id)=>Position(Stock.Get(id)?.owner);
        public BuildingDefinition Definition(StructureState building)=>definitions[building.definition];
        public bool Operating(StructureState building)=>building!=null&&building.phase==BuildPhase.Complete&&building.enabled&&!building.paused&&building.condition>.15f&&building.powerFraction>.95f&&(!Definition(building).sealedModule||Networks.Pressurized(building.id));
        public void Notify(string id){Changed?.Invoke(id);}
        public void Tick(float dt)
        {
            if(!State.deployed||State.failed)return;State.time+=dt;
            Networks.Tick(dt);Navigation.Tick(dt);Jobs.Tick(dt);People.Tick(dt);Industry.Tick(dt);Traffic.Tick(dt);Development.Tick(dt);
        }
        public static ColonyState Create(ColonySetupRecord setup,ColonyCatalog catalog)
        {
            var state=new ColonyState{worldId=Guid.NewGuid().ToString("N"),setup=setup,randomState=unchecked((uint)setup.seed)^0x513AB92Fu,credits=catalog.balance.startingCredits,arrivalStarted=true};
            state.regions.Add(new RegionState{id=state.Id("region"),x=0,z=0,owned=true});
            float a=35+state.Random()*6+setup.region.moisture*6,c=48+state.Random()*7+(1-Mathf.Abs(setup.region.localDirection.y))*8,s=65+state.Random()*5+setup.region.moisture*14+(setup.region.biome==PlanetBiome.Forest?3:0);
            state.environment=new EnvironmentState{atmosphere=a,atmosphereStart=a,climate=c,climateStart=c,soil=s,soilStart=s,wind=.65f,nextEvent=720};return state;
        }
        public void FinalizeArrival()
        {
            if(State.deployed)return;
            var ship=AddStructure("ship",State.setup.landing.Position,State.setup.landing.yaw,true);ship.name="Expedition Cargo Ship";ship.air=ship.airCapacity=12;
            var q=Quaternion.Euler(0,ship.yaw,0);var apron=AddStructure("apron",World.Ground(ship.position+q*new Vector3(0,0,-26)),ship.yaw,true);apron.name="Personnel Landing Apron";
            var gangway=AddStructure("corridor",World.Ground(ship.position+q*new Vector3(0,0,-13)),ship.yaw,true);gangway.from=ship.id;gangway.to=apron.id;gangway.end=World.Ground(ship.position+q*new Vector3(0,0,-18));gangway.length=5;gangway.name="Sealed Arrival Gangway";
            foreach(var item in Catalog.balance.manifest){var inv=Stock.Get(item.good==Good.Water?ship.waterInventory:ship.inventory);if(Stock.Add(inv,item.good,item.quantity)<item.quantity-.01f)throw new InvalidOperationException("Starter cargo exceeds manifest capacity");}
            for(int i=0;i<Catalog.balance.workRobots;i++)AddMachine(ActorKind.WorkRobot,World.Ground(ship.position+q*new Vector3(-5+i*2,0,-28)));
            for(int i=0;i<Catalog.balance.forestryBots;i++)AddMachine(ActorKind.ForestryBot,World.Ground(ship.position+q*new Vector3(-5+i*10,0,-32)));
            for(int i=0;i<Catalog.balance.drones;i++)AddMachine(ActorKind.Drone,World.Ground(ship.position+q*new Vector3(-6+i*4,0,-23)));
            int[] counts={12,8,4,6,6,12};foreach(Profession profession in Enum.GetValues(typeof(Profession)))for(int i=0;i<counts[(int)profession];i++)AddPerson(profession,false);
            State.deployed=true;State.speed=1;State.time=0;Networks.Rebuild();Notify(ship.id);
        }
        public StructureState AddStructure(string definition,Vector3 position,float yaw,bool complete=false)
        {
            var d=Catalog.Building(definition);var b=new StructureState{id=State.Id("building"),definition=definition,name=d.name,position=position,yaw=yaw,phase=complete?BuildPhase.Complete:BuildPhase.Clearing,progress=complete?1:0,recipe=d.recipe};
            if(!d.link&&definition!="cargo")
            {float highest=position.y;var rotation=Quaternion.Euler(0,yaw,0);for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){var p=position+rotation*new Vector3(x*d.size.x*.5f,0,z*d.size.y*.5f);highest=Mathf.Max(highest,World.Height(p.x,p.z));}b.foundation=Mathf.Max(0,highest-position.y)+.12f;}
            b.airCapacity=d.sealedModule?Mathf.Max(4,d.beds)*(State.completedResearch.Contains("life")?1.25f:1):0;b.air=complete?b.airCapacity:0;
            if(definition=="robotworks")b.targetStock=SurfaceActors.Count(a=>a.kind==ActorKind.WorkRobot)+1;
            b.inventory=Stock.Create(b.id,Mathf.Max(d.capacity,d.cost.Sum(a=>a.quantity)+20)).id;
            if(d.waterCapacity>0)b.waterInventory=Stock.Create(b.id,d.waterCapacity,1000,true).id;
            State.structures.Add(b);structures.Add(b.id,b);Navigation?.IndexStructure(b);return b;
        }
        public ActorState AddMachine(ActorKind kind,Vector3 position)
        {
            var a=new ActorState{id=State.Id("machine"),name=kind==ActorKind.ForestryBot?"Forestry Bot":kind==ActorKind.Drone?"Transport Drone":"Work Robot",kind=kind,position=position,location=PersonLocation.Arrived};
            a.name+=" "+(State.actors.Count(x=>x.kind==kind)+1);a.inventory=Stock.Create(a.id,kind==ActorKind.WorkRobot?60:kind==ActorKind.ForestryBot?80:100).id;
            State.actors.Add(a);actors.Add(a.id,a);Notify(a.id);return a;
        }
        public ActorState AddPerson(Profession profession,bool visitor)
        {
            string[] first={"Ada","Lena","Mira","Jules","Kai","Tomas","Noor","Ellis","Maya","Soren","Iris","Leon","Amara","Eli","Rhea","Owen"};string[] last={"Chen","Okoro","Navarro","Singh","Park","Moreau","Vega","Reed","Khan","Silva","Novak","Ito"};string[] traits={"Patient","Curious","Sociable","Resilient","Diligent","Independent"};
            var a=new ActorState{id=State.Id("person"),name=first[(int)(State.Random()*first.Length)]+" "+last[(int)(State.Random()*last.Length)],kind=visitor?ActorKind.Visitor:ActorKind.Colonist,profession=profession,location=PersonLocation.Sleeping,trait=traits[(int)(State.Random()*traits.Length)],skill=.85f+State.Random()*.3f,age=24+Mathf.Floor(State.Random()*30),background="Galactic settlement expedition",activity="Orbital cryosleep"};
            a.inventory=Stock.Create(a.id,20,8).id;a.equipment=Stock.Create(a.id,2,2).id;State.actors.Add(a);actors.Add(a.id,a);return a;
        }
        public void Credit(float amount,string category,string reason,string transaction=null)
        {
            if(transaction!=null&&State.ledger.Any(l=>l.transaction==transaction))return;
            if(State.credits+amount<-.001f)throw new InvalidOperationException("Insufficient credits");State.credits+=amount;
            State.ledger.Add(new LedgerEntry{time=State.time,amount=amount,category=category,description=reason,transaction=transaction??State.Id("transaction")});
        }
        public float Harvest(string id,InventoryState carrier,float requested)
        {
            var obj=World.Object(id);if(obj==null)return 0;var delta=World.Change(id);Good good=World.Yield(obj);
            float take=Mathf.Min(requested,delta.remaining,Stock.Free(carrier,good));if(take<=0)return 0;float added=Stock.Add(carrier,good,take);delta.remaining-=added;
            if(delta.remaining<=.001f){delta.remaining=0;delta.removed=true;delta.designated=false;ResourceRemoved?.Invoke(id);}Notify(id);return added;
        }
        public StructureState DropCargo(Vector3 position,IEnumerable<Amount> cargo,string name="Recoverable Cargo")
        {
            var pile=AddStructure("cargo",World.Ground(position),0,true);pile.name=name;
            foreach(var item in cargo)Stock.Add(Stock.Get(pile.inventory),item.good,item.quantity);Notify(pile.id);return pile;
        }
    }
}
