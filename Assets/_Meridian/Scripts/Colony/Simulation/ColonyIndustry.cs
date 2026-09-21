using System;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed class ColonyIndustry
    {
        readonly ColonySimulation sim;float planning;
        public ColonyIndustry(ColonySimulation owner){sim=owner;}
        void Plan()
        {
            foreach(var b in sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&b.enabled&&!b.paused))
            {
                var recipe=sim.Catalog.Recipe(b.recipe);if(recipe!=null)
                    foreach(var input in recipe.inputs)sim.Jobs.Supply(b,input.good,Mathf.Max(input.quantity*4,10),b.priority+1);
                if(b.definition=="biosphere")sim.Jobs.Supply(b,Good.Biomass,20,b.priority);
                if(b.definition=="climate")sim.Jobs.Supply(b,Good.Minerals,16,b.priority);
                var inventory=sim.Stock.Get(b.inventory);
                foreach(var item in inventory.items.ToArray())
                {
                    bool output=b.definition=="cargo"||b.definition=="miner"||b.definition=="quarry"||b.definition=="ice"||((b.definition=="greenhouse"||b.definition=="field")&&item.good==Good.Food)||recipe!=null&&recipe.outputs.Any(o=>o.good==item.good);
                    if(!output||sim.Stock.Available(inventory,item.good)<10)continue;
                    var destination=sim.Jobs.StorageFor(item.good,b.position,inventory.id);var target=sim.Structure(destination?.owner);if(target!=null)sim.Jobs.Supply(target,item.good,Mathf.Min(sim.Stock.Count(destination,item.good)+sim.Stock.Available(inventory,item.good),sim.Stock.Count(destination,item.good)+60),b.priority,destination);
                }
            }
        }
        public void Tick(float dt)
        {
            planning-=dt;if(planning<=0){Plan();planning=2;}
            foreach(var b in sim.State.structures.ToArray())
            {
                if(b.phase!=BuildPhase.Complete||b.definition=="cargo")continue;
                b.condition=Mathf.Max(0,b.condition-sim.Catalog.balance.wearPerDay*dt/sim.Day*(b.enabled?1:.3f));
                if(b.condition<=.15f){b.blocker="Condition critical; awaiting machine service and parts";continue;}
                if(!sim.Operating(b))continue;
                var stock=sim.Stock.Get(b.inventory);float day=dt/sim.Day;
                if(b.definition=="miner"||b.definition=="quarry"||b.definition=="ice")
                {
                    var source=sim.World.Object(b.deposit);if(source==null){b.blocker="No compatible deposit";continue;}
                    if(sim.World.Remaining(b.deposit)<=0){b.blocker="Deposit exhausted; relocate or dismantle";continue;}
                    if(sim.Stock.Free(stock,sim.World.Yield(source))<=.01f){b.blocker="Output full; add storage or transport capacity";continue;}
                    sim.Harvest(b.deposit,stock,sim.Definition(b).outputPerDay*day*b.condition);b.blocker="Extracting "+sim.World.Yield(source);continue;
                }
                if(b.definition=="greenhouse"||b.definition=="field")
                {
                    if(b.definition=="field"&&(!sim.OutdoorsSafe||sim.State.environment.soil<80)){b.blocker="Outdoor growing conditions unsafe";continue;}
                    // Real tending visits support continued biological growth between shifts; the
                    // saved buffer is bounded to one fully staffed day and spent only on output.
                    float positions=sim.SurfaceActors.Count(a=>a.workplace==b.id&&a.profession==Profession.Botanist);
                    float staffed=Mathf.Min(2,positions,b.tending/Mathf.Max(.0001f,dt))/2;if(staffed<=0){b.blocker="Waiting for botanist tending visit";continue;}
                    float modifier=sim.State.completedResearch.Contains("agriculture")?1.2f:1;
                    bool sick=sim.State.events.Any(e=>e.kind=="Greenhouse illness"&&e.target==b.id&&e.ends>sim.State.time);if(sick)modifier*=.65f;
                    float food=sim.Catalog.balance.greenhousePerDay*day*staffed*modifier,bio=4*day*staffed,water=12*day*staffed*(sim.State.completedResearch.Contains("agriculture")?.8f:1);
                    if(sim.Stock.Free(stock,Good.Food)<food){b.blocker="Food output full";continue;}
                    if(sim.Stock.Available(stock,Good.Biomass)<bio){b.blocker="Needs delivered biomass feedstock";continue;}
                    if(sim.Networks.WaterAvailable(b.id)+1e-5f<water){b.blocker="Needs piped irrigation water";continue;}
                    sim.Stock.Consume(stock,Good.Biomass,bio);sim.Networks.DrawWater(b.id,water);sim.Stock.Add(stock,Good.Food,food);b.tending=Mathf.Max(0,b.tending-staffed*2*dt);b.blocker="Growing · "+(sim.Catalog.balance.greenhousePerDay*staffed*modifier).ToString("0.0")+" food/day";
                }
                if(b.definition=="lab")
                {float rate=sim.Catalog.balance.researchPerDay*sim.People.Staffing(b)/2;sim.State.researchPoints+=rate*day;b.blocker=rate>0?"Researching · "+rate.ToString("0.0")+" points/day":"Waiting for scientists";}
                var recipe=sim.Catalog.Recipe(b.recipe);if(recipe==null)continue;
                if(recipe.facility!=b.definition){b.blocker="Invalid recipe for this facility";continue;}
                if(recipe.actor!=""&&sim.SurfaceActors.Count(a=>a.kind.ToString()==recipe.actor)>=b.targetStock){b.blocker="Machine production target reached";continue;}
                if(recipe.outputs.Any(o=>sim.Stock.Count(stock,o.good)>=b.targetStock)){b.blocker="Production target reached";continue;}
                if(!b.inputsCommitted)
                {
                    if(!sim.Stock.Consume(stock,recipe.inputs)){b.blocker="Waiting for recipe inputs";continue;}
                    b.batch=recipe.inputs.Select(i=>new Amount(i.good,i.quantity)).ToList();b.inputsCommitted=true;b.production=0;
                }
                b.production=Mathf.Min(recipe.seconds,b.production+dt*b.condition);b.blocker="Processing · "+recipe.name;
                if(b.production<recipe.seconds)continue;
                if(!OutputFits(stock,recipe)){b.blocker="Output space required; completed batch held";continue;}
                foreach(var output in recipe.outputs)sim.Stock.Add(stock,output.good,output.quantity);
                if(Enum.TryParse<ActorKind>(recipe.actor,out var kind)&&recipe.actor!="")sim.AddMachine(kind,sim.World.Ground(sim.Navigation.Door(b,b.position+Vector3.back*10)+Vector3.back*3));
                b.inputsCommitted=false;b.batch.Clear();b.production=0;sim.Notify(b.id);
            }
        }
        bool OutputFits(InventoryState inventory,RecipeDefinition recipe)
        {return recipe.outputs.Sum(o=>o.quantity)<=inventory.capacity-sim.Stock.Used(inventory)-sim.State.reservations.Where(r=>r.incoming&&r.inventory==inventory.id).Sum(r=>r.quantity)+.001f&&recipe.outputs.All(o=>sim.Stock.Free(inventory,o.good)+.0001f>=o.quantity);}
        public bool DeployCrate(StructureState b,Good good)
        {
            ActorKind kind=good==Good.WorkRobot?ActorKind.WorkRobot:good==Good.ForestryBot?ActorKind.ForestryBot:ActorKind.Drone;
            if(good!=Good.WorkRobot&&good!=Good.ForestryBot&&good!=Good.TransportDrone)return false;
            if(!sim.Stock.Consume(sim.Stock.Get(b.inventory),good,1))return false;sim.AddMachine(kind,sim.World.Ground(sim.Navigation.Door(b,b.position+Vector3.back*10)+Vector3.back*3));return true;
        }
    }
}
