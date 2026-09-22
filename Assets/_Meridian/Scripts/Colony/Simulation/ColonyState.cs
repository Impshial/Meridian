using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meridian.Colony
{
    public enum Good { IronOre,CopperOre,Minerals,Biomass,Metal,Components,Food,Medicine,Water,EVASuit,Toolkit,WorkRobot,ForestryBot,TransportDrone }
    public enum ActorKind { WorkRobot,ForestryBot,Drone,Colonist,Visitor }
    public enum Profession { Builder,Botanist,Medic,Scientist,Technician,Service }
    public enum PersonLocation { Sleeping,Reserved,Waking,InTransit,Arrived,Departed,Dead }
    public enum BuildPhase { Clearing,Delivery,Construction,Complete,Dismantling,Removed }
    public enum JobKind { Harvest,Haul,Build,Repair,Dismantle,Rescue,Train }
    public enum JobStage { Waiting,Pickup,Travelling,Working,Delivering,Complete,Cancelled }
    public enum FlightKind { Personnel,Import,Export,Visitors,Departure }
    public enum FlightPhase { Queued,Preparing,Transit,Holding,Descending,Disembarking,Departing,Complete,Cancelled,Returning }
    [Serializable] public class Amount { public Good good;public float quantity;public Amount(){}public Amount(Good g,float q){good=g;quantity=q;} }
    [Serializable] public class ItemStack { public string id;public Good good;public float quantity,condition=1; }
    [Serializable] public class InventoryState
    {
        public string id,owner;public float capacity;public int slots=1000;public bool waterOnly;
        public List<Good> filters=new List<Good>();public List<ItemStack> items=new List<ItemStack>();
    }
    [Serializable] public class ReservationState { public string id,job,inventory;public Good good;public float quantity;public bool incoming; }
    [Serializable] public class StructureState
    {
        public string id,definition,name,inventory,waterInventory,from,to,deposit,recipe,blocker="Awaiting construction";
        public Vector3 position,end;public float yaw,length,progress,condition=1,battery,air,production,staffed,serviceTime,tending;
        public float powerFraction,waterFraction=1,targetStock=100,airCapacity=8,foundation;public int priority=1;
        public BuildPhase phase;public bool paused,inputsCommitted,enabled=true,isolated,replacement;
        public int domeOverride=-1,staffLimit;public bool staffingOverride;public List<Amount> incorporated=new List<Amount>();public List<Amount> batch=new List<Amount>();
    }
    [Serializable] public class ActorState
    {
        public string id,name,inventory,equipment,home,workplace,job,activity="Idle",reason="",airlock,building,intention,target,background,trait;
        public ActorKind kind;public Profession profession;public PersonLocation location;
        public Vector3 position,destination;public float yaw,health=100,charge=100,condition=1,hunger=10,thirst=10,fatigue=10,hygiene=90,recreation=80,morale=80,skill=1,experience,suit=100;
        public float actionTime,serviceCredit,returnAfter,wallet=200,age,nextDecision;public bool eva,returning,paused,gearRepair;
        public bool training;public Profession trainAs;public float trainingProgress;public string trainingCenter;
        public List<Vector3> path=new List<Vector3>();public List<string> pathRooms=new List<string>();public int pathIndex;public List<string> history=new List<string>();
    }
    [Serializable] public class JobState
    {
        public string id,target,actor,source,destination,reservation,blocker="Waiting for suitable worker";
        public JobKind kind;public JobStage stage;public Good good;public float quantity,progress,retryAt;public int priority=1,retries;public bool machineOnly,paused,carrying;
    }
    [Serializable] public class ResourceDelta { public string id,claim;public float remaining;public bool removed,designated,paused;public int priority=1; }
    [Serializable] public class FlightState
    {
        public string id,name,pad,blocker="Queued",inventory;public FlightKind kind;public FlightPhase phase;public float elapsed,duration,fee,unitPrice;
        public List<string> passengers=new List<string>();public List<string> beds=new List<string>();public List<Amount> cargo=new List<Amount>();
        public bool committed,delivered,returnRequested,refunded;public Vector3 position;
    }
    [Serializable] public class RecruitmentState { public string id;public Profession profession;public float remaining;public bool completed; }
    [Serializable] public class RegionState { public string id;public int x,z;public bool owned;public float price; }
    [Serializable] public class RegionOperation { public int x,z;public string id;public float quotedPrice;public bool active; }
    [Serializable] public class LedgerEntry { public double time;public float amount;public string category,description,transaction; }
    [Serializable] public class EventState { public string id,kind,target;public double begins,ends;public float intensity;public bool applied; }
    [Serializable] public class WindowState { public string key,entity,world;public Vector4 rect;public int dock;public bool open=true,pinned; }
    [Serializable] public class CameraState { public Vector3 pivot;public float yaw,pitch,distance; }
    [Serializable] public class EnvironmentState
    {
        public float atmosphere,climate,soil,atmosphereStart,climateStart,soilStart;
        public float atmosphereWork,climateWork,soilWork,programYears,fundedYears,uptime,storm,wind=.65f;
        public bool programEnabled;public double nextEvent=600;public string blocker="Planetary Engineering research required";
    }
    [Serializable] public class ColonyState
    {
        public const int CurrentSchema=1;
        public int schema=CurrentSchema;public string contentVersion="meridian-game-1",worldId,name="Meridian Colony",savedUtc;
        public long nextId=1;public uint randomState;public double time;public float speed=1,previousSpeed=1,credits=20000,reputation=20;
        public bool deployed,arrivalStarted,masterFrames,guideDismissed,visitorsOpen,failed,preferHumanBuilders;
        public int visitorCap=8,pricePolicy=1,autosaveIndex,workPolicy;public float researchPoints,researchProgress,nextPlanning,visitorTimer;
        public ColonySetupRecord setup;public CameraState camera=new CameraState();public EnvironmentState environment=new EnvironmentState();
        public List<StructureState> structures=new List<StructureState>();public List<ActorState> actors=new List<ActorState>();
        public List<InventoryState> inventories=new List<InventoryState>();public List<ReservationState> reservations=new List<ReservationState>();
        public List<JobState> jobs=new List<JobState>();public List<FlightState> flights=new List<FlightState>();public List<RecruitmentState> recruitment=new List<RecruitmentState>();
        public List<ResourceDelta> resources=new List<ResourceDelta>();public List<RegionState> regions=new List<RegionState>();public RegionOperation expansion=new RegionOperation();
        public List<LedgerEntry> ledger=new List<LedgerEntry>();public List<EventState> events=new List<EventState>();
        public List<string> researchQueue=new List<string>(),completedResearch=new List<string>(),acknowledged=new List<string>();
        public List<WindowState> windows=new List<WindowState>();public int windowLayoutVersion,utilityView;
        public Vector4 dockSizes=new Vector4(380,380,250,250);public string selected;
        public string Id(string prefix)=>worldId+":"+prefix+":"+(nextId++);
        public float Random(){uint x=randomState==0?0xA341316Cu:randomState;x^=x<<13;x^=x>>17;x^=x<<5;randomState=x;return (x&0xFFFFFF)/16777216f;}
    }
    [Serializable] public class BuildingDefinition
    {
        public string id,name,category,description,model,research,recipe;public bool sealedModule,machineOnly=true,link,walkableLink;
        public Vector2 size=new Vector2(12,12);public float height=7,power,generation,capacity=120,waterCapacity,buildSeconds=24,beds,seats,staff,airSupport,waterPerDay,outputPerDay;
        public Profession profession;public List<Amount> cost=new List<Amount>();
    }
    [Serializable] public class RecipeDefinition { public string id,name,facility;public float seconds;public List<Amount> inputs=new List<Amount>(),outputs=new List<Amount>();public string actor; }
    [Serializable] public class ResearchDefinition { public string id,name,description;public float cost;public List<string> prerequisites=new List<string>(); }
    [Serializable] public class GoodDefinition { public Good id;public string name;public float buy,stack=100; }
    [Serializable] public class BalanceDefinition
    {
        public float daySeconds=120,daysPerYear=12,step=.1f,foodPerPersonDay=2,waterPerPersonDay=2,wearPerDay=.003f,maintenanceThreshold=.7f,flightFee=100,flightDays=1,freightDays=3,freightCapacity=200;
        public float researchPerDay=10,greenhousePerDay=30,terraformPerYear=1.4f,terraformCap=2,terraformFunding=2000,recruitCost=300,regionCost=2500,refundFraction=.6f,startingCredits=20000;
        public int workRobots=6,forestryBots=2,drones=4,flightSeats=6,autosaves=5;public float autosaveSeconds=300,tendingSecondsPerDay=18,trainingHours=8;
        public List<Amount> manifest=new List<Amount>();
    }
    [Serializable] public class ColonyCatalog
    {
        public BalanceDefinition balance=new BalanceDefinition();public List<BuildingDefinition> buildings=new List<BuildingDefinition>();
        public List<RecipeDefinition> recipes=new List<RecipeDefinition>();public List<ResearchDefinition> research=new List<ResearchDefinition>();public List<GoodDefinition> goods=new List<GoodDefinition>();
        public BuildingDefinition Building(string id)=>buildings.Find(d=>d.id==id)??throw new InvalidOperationException("Unknown structure: "+id);
        public RecipeDefinition Recipe(string id)=>recipes.Find(d=>d.id==id);
        public ResearchDefinition Research(string id)=>research.Find(d=>d.id==id);
        public GoodDefinition Good(Good id)=>goods.Find(d=>d.id==id);
        public static ColonyCatalog Load(){var asset=Resources.Load<TextAsset>("Colony/Catalog");if(!asset)throw new InvalidOperationException("Missing colony catalog");return JsonUtility.FromJson<ColonyCatalog>(asset.text);}
    }
}
