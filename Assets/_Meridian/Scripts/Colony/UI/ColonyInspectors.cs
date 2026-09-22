using System;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed partial class ColonyUI
    {
        void Entity(string id)
        {
            var region=sim?.State.regions.Find(r=>r.id==id);
            if(region!=null)
            {
                GUILayout.Label("REGION "+region.x+", "+region.z,title);Actions(id);Row("Territory",region.owned?"Owned · 36 km²":"Surveyed");Row("Dimensions","6,000 × 6,000 metres");Row("Structures",sim.State.structures.Count(b=>b.phase!=BuildPhase.Removed&&Mathf.Abs(b.position.x-region.x*6000)<=3000&&Mathf.Abs(b.position.z-region.z*6000)<=3000).ToString());
                GUILayout.Label("Terrain, water and deposits share the original landing survey. Resource removal persists when you travel away or reload.",muted);Button("Open regional survey",()=>Open("Regions"));return;
            }
            if(sim==null){GUILayout.Label("No active colony");return;}var b=sim.Structure(id);if(b!=null){Building(b);return;}var a=sim.Actor(id);if(a!=null){PersonOrMachine(a);return;}var f=sim.State.flights.Find(x=>x.id==id);if(f!=null){Flight(f);return;}var r=sim.World.Object(id);if(r!=null){Resource(r);return;}GUILayout.Label("This entity is no longer part of the active world.",muted);
        }
        void Actions(string id)
        {GUILayout.BeginHorizontal();Button("Focus  [F]",()=>runtime.Focus(id));Button("Select",()=>runtime.Visuals.Selected=id);GUILayout.EndHorizontal();}
        void Building(StructureState b)
        {
            if(b.definition=="cargo"){Cargo(b);return;}
            var d=sim.Definition(b);GUILayout.Label(b.name,title);b.name=Text(b.id+":name",b.name);GUILayout.Label(d.description,muted);Actions(b.id);Row("Status",b.phase+" · "+b.blocker);Bar("Condition",b.condition*100);
            if(b.phase==BuildPhase.Removed){GUILayout.Label("Dismantled. Recovered stock remains physical cargo at the old site.");return;}
            if(b.phase!=BuildPhase.Complete)
            {
                Bar("Construction",b.progress*100);Header("Material delivery");foreach(var c in ColonyCommands.Cost(sim,b))
                {float delivered=sim.Stock.Count(sim.Stock.Get(b.inventory),c.good);float incorporated=b.incorporated.Where(i=>i.good==c.good).Sum(i=>i.quantity);Row(c.good.ToString(),delivered.ToString("0")+" delivered · "+incorporated.ToString("0")+" incorporated / "+c.quantity.ToString("0"));}
                GUILayout.Label(d.machineOnly?"Only machines can construct this equipment.":"Work robots and equipped builders can construct this civilian site.",muted);
            }
            Row("Power",b.powerFraction.ToString("P0")+" of "+d.power.ToString("0.#")+" kW");if(d.generation>0)Row("Rated generation",d.generation.ToString("0")+" kW");
            if(b.definition=="battery")Bar("Stored energy",b.battery,sim.State.completedResearch.Contains("power")?500:400);
            if(d.sealedModule)
            {
                Bar("Air reserve",b.air,b.airCapacity);int occupied=sim.SurfaceActors.Count(a=>a.building==b.id&&!a.eva&&(a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor));Row("Occupants",occupied.ToString());Row("Buffer time",occupied>0?(b.air/occupied).ToString("0.0")+" days without replenishment":"No current breathing demand");
                Button(runtime.Visuals.Frames(b)?"Show complete exterior":"Reveal structural frame & interior",()=>runtime.Visuals.ToggleDome(b));
                if(b.definition!="corridor")Button("Relocate occupants",()=>Evacuate(b));
            }
            if(d.beds>0)Row("Assigned beds",sim.People.BedsUsed(b.id)+" / "+d.beds);if(d.staff>0)Row("Effective staffing",sim.People.Staffing(b).ToString("0.0")+" / "+d.staff+" "+d.profession);
            if(d.staff>0){int slots=sim.People.StaffCapacity(b);int chosen=Mathf.RoundToInt(GUILayout.HorizontalSlider(slots,0,d.staff));if(chosen!=slots){b.staffingOverride=true;b.staffLimit=chosen;}Row("Requested positions",chosen+" / "+d.staff);}
            if(b.definition=="greenhouse"||b.definition=="field")Row("Tending reserve",(b.tending/sim.Day).ToString("0.00")+" botanist-days; spent during actual growth");
            Row("Water network",sim.Networks.WaterAvailable(b.id).ToString("0.0")+" units connected");
            if(b.deposit!=null)
            {var deposit=sim.World.Object(b.deposit);Header("Extraction controller");Row("Deposit",deposit?.Kind.ToString()??"Missing");Row("Remaining reserves",sim.World.Remaining(b.deposit).ToString("N0"));Row("Rated extraction",d.outputPerDay+" / day");Row("Operator","Autonomous machine controller");Button("Inspect deposit",()=>Inspect(b.deposit));}
            if(d.link)
            {Row("Length",b.length.ToString("0.0")+" m");Button("From: "+sim.Structure(b.from)?.name,()=>Inspect(b.from));Button("To: "+sim.Structure(b.to)?.name,()=>Inspect(b.to));bool isolate=GUILayout.Toggle(b.isolated,"Isolate this connection / bulkhead");if(isolate!=b.isolated){b.isolated=isolate;sim.Networks.Dirty();}}
            var recipes=catalog.recipes.Where(r=>r.facility==b.definition).ToArray();if(recipes.Length>0)
            {
                Header("Production");var active=catalog.Recipe(b.recipe);if(active!=null){Bar(active.name,b.production,active.seconds);GUILayout.Label("Inputs: "+string.Join(", ",active.inputs.Select(i=>i.quantity+" "+i.good)),small);GUILayout.Label(b.inputsCommitted?"Batch inputs committed once; progress persists":"Waiting for next batch",muted);}
                foreach(var recipe in recipes)Button((b.recipe==recipe.id?"✓ ":"")+recipe.name,()=>ColonyCommands.SetRecipe(sim,b,recipe.id),!b.inputsCommitted);
                b.targetStock=Mathf.Round(GUILayout.HorizontalSlider(b.targetStock,1,200));Row("Output target",b.targetStock.ToString("0"));
            }
            Header("Lifecycle");b.paused=GUILayout.Toggle(b.paused,"Pause this site / production");bool enabled=GUILayout.Toggle(b.enabled,"Enable facility");if(enabled!=b.enabled){b.enabled=enabled;sim.Networks.Dirty();}b.priority=GUILayout.SelectionGrid(Mathf.Clamp(b.priority,0,3),new[]{"Low","Normal","High","Urgent"},4);
            if(b.phase==BuildPhase.Complete)Button("Request repair · 1 Metal + 1 Component",()=>{if(!sim.State.jobs.Any(j=>sim.Jobs.Active(j)&&j.kind==JobKind.Repair&&j.target==b.id)){sim.Jobs.Supply(b,Good.Metal,1,10);sim.Jobs.Supply(b,Good.Components,1,10);sim.Jobs.Create(JobKind.Repair,b.id,10,d.machineOnly);}},b.condition<.99f);
            if(b.definition!="ship"&&b.definition!="apron"&&b.definition!="cargo")
            {
                Button(b.phase==BuildPhase.Complete?"Dismantle":"Cancel construction",()=>Confirm("Dismantle "+b.name+"?","Unconsumed cargo is recovered in full. 60% of incorporated building materials becomes recoverable cargo requiring transport.",()=>ColonyCommands.Dismantle(sim,b)));
                if(b.phase==BuildPhase.Complete)Button("Replace with newly constructed equipment",()=>Confirm("Replace "+b.name+"?","The old structure stops, is dismantled, and a new paid construction order uses its reserved site and deposit. New cost: "+string.Join(", ",ColonyCommands.Cost(sim,b).Select(c=>c.quantity+" "+c.good))+". Recovery: 60% incorporated materials plus all cargo.",()=>ColonyCommands.Dismantle(sim,b,true)));
            }
            Inventory(sim.Stock.Get(b.inventory));if(b.waterInventory!=null)Inventory(sim.Stock.Get(b.waterInventory));
            if(sim.Jobs.Storage(b))Filters(sim.Stock.Get(b.inventory));
            foreach(var good in new[]{Good.WorkRobot,Good.ForestryBot,Good.TransportDrone})if(sim.Stock.Available(sim.Stock.Get(b.inventory),good)>=1)Button("Unpack "+good,()=>sim.Industry.DeployCrate(b,good));
            Header("Related jobs");foreach(var j in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&j.target==b.id))Job(j);
        }
        void Cargo(StructureState pile)
        {
            GUILayout.Label(pile.name,title);Actions(pile.id);
            if(pile.phase==BuildPhase.Removed){GUILayout.Label("This pile has been cleared. Materials already aboard carriers continue to storage.");return;}
            GUILayout.Label("Recovered materials",accent);GUILayout.Label("Machines automatically collect these supplies. The pile disappears when empty. It does not block building placement or walking.",muted);
            GUILayout.Label(pile.blocker);Inventory(sim.Stock.Get(pile.inventory));
            Button("Prioritize collection",()=>{sim.Jobs.CollectCargo(pile,true);Toast(pile.blocker);});
            Button("Discard remaining materials…",()=>Confirm("Discard this pile?","Permanently discard the supplies still on the ground. Materials already aboard carriers are kept.\n"+string.Join(", ",sim.Stock.Get(pile.inventory).items.Select(i=>i.quantity.ToString("0.##")+" "+i.good)),()=>{ColonyCommands.DiscardCargo(sim,pile);runtime.Visuals.Sync();}));
            Header("Collection jobs");foreach(var job in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&j.source==pile.inventory))Job(job);
        }
        void Evacuate(StructureState building)
        {
            var occupants=sim.SurfaceActors.Where(a=>a.building==building.id&&(a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor)).ToArray();
            foreach(var a in occupants)
            {
                var destination=sim.State.structures.Where(b=>b.id!=building.id&&sim.Operating(b)&&sim.Definition(b).beds>0&&sim.People.BedsUsed(b.id)<sim.Definition(b).beds&&sim.Networks.Connected(building.id,b.id)).FirstOrDefault();
                if(destination==null)throw new InvalidOperationException("An accessible supplied spare bed is required for each occupant");
                a.home=destination.id;if(a.workplace==building.id)a.workplace=null;if(a.job!=null){var j=sim.State.jobs.Find(j=>j.id==a.job);if(j!=null)sim.Jobs.Cancel(j,"Occupant relocation");}a.intention="evacuate";a.target=destination.id;sim.Navigation.Stop(a);sim.Navigation.Go(a,destination.position,destination.id);
            }
            Toast("Occupants are moving through the sealed network; dismantle once it is empty.");
        }
        void Inventory(InventoryState inventory)
        {
            if(inventory==null)return;Header(inventory.waterOnly?"Water storage":"Owned inventory");Row("Capacity",sim.Stock.Used(inventory).ToString("0.0")+" / "+inventory.capacity+" · "+inventory.items.Count+" stacks");
            foreach(var group in inventory.items.GroupBy(i=>i.good))Row(catalog.Good(group.Key).name,group.Sum(i=>i.quantity).ToString("0.00")+" · "+sim.Stock.Reserved(inventory,group.Key).ToString("0.0")+" reserved");
            if(inventory.items.Count==0)GUILayout.Label("Empty",muted);
            foreach(var r in sim.State.reservations.Where(r=>r.inventory==inventory.id&&r.incoming))GUILayout.Label("Inbound: "+r.quantity.ToString("0.0")+" "+r.good,small);
        }
        void Filters(InventoryState inventory)
        {
            Header("Accept goods");bool all=inventory.filters.Count==0;bool next=GUILayout.Toggle(all,"Accept all goods");if(next&&!all)inventory.filters.Clear();if(!next&&all)inventory.filters.Add(Good.Metal);
            if(inventory.filters.Count>0)foreach(var g in catalog.goods){bool selected=inventory.filters.Contains(g.id);bool chosen=GUILayout.Toggle(selected,g.name);if(chosen&&!selected)inventory.filters.Add(g.id);if(!chosen&&selected&&inventory.filters.Count>1)inventory.filters.Remove(g.id);}
        }
        void PersonOrMachine(ActorState a)
        {
            bool human=a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor;GUILayout.Label(a.name,title);Actions(a.id);Row("Identity",a.kind+" · "+a.location);Row("Activity",a.activity);if(!string.IsNullOrEmpty(a.reason))GUILayout.Label(a.reason,warning);
            if(human)
            {
                GUILayout.BeginHorizontal(card);GUILayout.Label(Portrait(a),GUILayout.Width(96),GUILayout.Height(96));GUILayout.BeginVertical();GUILayout.Label(a.profession+" · "+a.trait,accent);GUILayout.Label("Age "+a.age.ToString("0")+" · "+a.background,small);GUILayout.EndVertical();GUILayout.EndHorizontal();
                Bar("Health",a.health);Bar("Hunger",a.hunger);Bar("Thirst",a.thirst);Bar("Fatigue",a.fatigue);Bar("Hygiene",a.hygiene);Bar("Recreation",a.recreation);Bar("Morale",a.morale);Row("Skill / experience",a.skill.ToString("0.00")+" / "+a.experience.ToString("0.00"));Row("Environment",a.eva?"EVA · suit "+a.suit.ToString("0")+"%":"Sealed access");Row("Home",sim.Structure(a.home)?.name??"No bed assigned");Row("Workplace",sim.Structure(a.workplace)?.name??"Automatic assignment");
                if(a.kind==ActorKind.Visitor){Row("Budget",a.wallet.ToString("0.00")+" credits");Row("Departure",Mathf.Max(0,(a.returnAfter-(float)sim.State.time)/sim.Day).ToString("0.0")+" days");}
                if(a.location==PersonLocation.Arrived&&a.kind==ActorKind.Colonist)
                {
                    Header("Retraining");GUILayout.Label("Requires a staffed training center and "+(sim.People.TrainingTime/sim.Day*24).ToString("0.#")+" colony hours of attendance. Travel and needs breaks extend the elapsed duration; progress is retained.",muted);
                    if(a.training){Bar("Training as "+a.trainAs,a.trainingProgress,sim.People.TrainingTime);Button("Cancel training",()=>{a.training=false;a.trainingCenter=null;a.trainingProgress=0;if(a.intention=="training"){a.intention=null;sim.Navigation.Stop(a);}});}
                    foreach(Profession p in Enum.GetValues(typeof(Profession)))if(p!=a.profession)Button("Train as "+p,()=>{if(!sim.People.ChangeProfession(a,p))Toast("No operational training center or training already in progress");});
                }
                if(a.equipment!=null){Inventory(sim.Stock.Get(a.equipment));foreach(var item in sim.Stock.Get(a.equipment).items)Row(item.good+" condition",item.condition.ToString("P0"));}
            }
            else
            {Bar("Battery charge",a.charge);Bar("Mechanical condition",a.condition*100);Row("Capacity",sim.Stock.Get(a.inventory).capacity+" cargo units");Row("Capabilities",a.kind==ActorKind.WorkRobot?"Build, haul, minerals, repair, recovery":a.kind==ActorKind.ForestryBot?"Forestry, clearing, cargo delivery":"Hauling and machine recovery");a.paused=GUILayout.Toggle(a.paused,"Disable / park this machine");Button("Return for charging & service",()=>{if(a.job!=null){var job=sim.State.jobs.Find(j=>j.id==a.job);if(job!=null)sim.Jobs.Cancel(job,"Charging requested");}a.intention="charge";a.target=null;});}
            Inventory(sim.Stock.Get(a.inventory));var current=sim.State.jobs.Find(j=>j.id==a.job);if(current!=null){Header("Current job");Job(current);}Header("History");foreach(string entry in a.history.TakeLast(8))GUILayout.Label(entry,small);
        }
        void Job(JobState j)
        {
            GUILayout.BeginVertical(card);GUILayout.Label(j.kind+" · "+j.stage,accent);GUILayout.Label(j.blocker,small);Row("Worker",sim.Actor(j.actor)?.name??"Awaiting assignment");if(j.kind==JobKind.Haul)Row("Cargo",j.quantity.ToString("0.0")+" "+j.good+(j.carrying?" physically aboard":" reserved at source"));j.paused=GUILayout.Toggle(j.paused,"Pause job");Button("Cancel / release job",()=>sim.Jobs.Cancel(j));GUILayout.EndVertical();
        }
        void Flight(FlightState f)
        {
            GUILayout.Label(f.name,title);Actions(f.id);Row("Manifest type",f.kind.ToString());Row("Phase",f.phase.ToString());GUILayout.Label(f.blocker,accent);Row("Elapsed phase",f.elapsed.ToString("0.0")+" s");Row("Nominal journey",(f.duration/sim.Day).ToString("0.0")+" days");Row("Landing pad",sim.Structure(f.pad)?.name??"Unavailable");Row("Transaction",f.fee.ToString("N0")+" credits"+(f.committed?" · committed":""));
            foreach(var amount in f.cargo)Row(amount.good.ToString(),amount.quantity.ToString("N0"));foreach(string id in f.passengers){var a=sim.Actor(id);if(a!=null)Button(a.name+" · "+a.location+" · bed "+sim.Structure(a.home)?.name,()=>Inspect(id));}Inventory(sim.Stock.Get(f.inventory));
            if(sim.Traffic.Active(f))Button(f.phase==FlightPhase.Queued||f.phase==FlightPhase.Preparing?"Cancel before launch":"Request safe return",()=>Confirm("Change flight plan?","Prelaunch cancellation releases passenger and cargo reservations. A launched ship returns safely without duplicating its passengers or cargo.",()=>sim.Traffic.Cancel(f)));
        }
        void Resource(SurfaceObjectData resource)
        {
            GUILayout.Label(resource.Kind+" RESOURCE",title);Actions(resource.Id);Row("Reserve",sim.World.Remaining(resource.Id).ToString("N0")+" "+sim.World.Yield(resource));Row("Tile",resource.Owner.ToString());if(resource.Kind==SurfaceObjectKind.Tree)Row("Grove",resource.ResourceGroupId);Row("Ownership",sim.World.Owned(resource.Position)?"Inside owned territory":"Unowned neighboring land");
            var delta=sim.World.Change(resource.Id);if(delta.removed){GUILayout.Label("Depleted / removed. This resource does not regenerate on reload.",muted);return;}
            if(resource.Kind==SurfaceObjectKind.Tree)GUILayout.Label("Forestry bots cut and transport this tree's finite biomass. Clearing a building footprint uses the same harvest process.",muted);
            Button("Designate harvest",()=>delta.designated=true,sim.World.Owned(resource.Position));Button("Cancel harvest designation",()=>{delta.designated=false;foreach(var job in sim.State.jobs.Where(j=>j.target==resource.Id&&sim.Jobs.Active(j)).ToArray())sim.Jobs.Cancel(job);});delta.paused=GUILayout.Toggle(delta.paused,"Pause harvesting");delta.priority=GUILayout.SelectionGrid(Mathf.Clamp(delta.priority,0,3),new[]{"Low","Normal","High","Urgent"},4);
            if(resource.Kind!=SurfaceObjectKind.Tree){string type=resource.Kind==SurfaceObjectKind.Ice?"ice":resource.Kind==SurfaceObjectKind.Rock?"quarry":"miner";Button("Place compatible extractor",()=>{runtime.Focus(resource.Id);runtime.BeginBuild(type);});}
        }
    }
}
