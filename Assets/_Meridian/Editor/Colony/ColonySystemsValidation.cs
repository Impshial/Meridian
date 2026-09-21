using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Meridian.Colony;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace Meridian.Editor
{
    public static partial class ColonyValidation
    {
        static readonly List<string> systemsEvidence=new List<string>();static ColonySimulation systemSim;
        static void SystemNote(string text){systemsEvidence.Add(text);File.WriteAllText("Logs/ColonySystemsValidation.txt","RUNNING\n"+string.Join("\n",systemsEvidence));}
        [MenuItem("Meridian/Colony/Validate Connected Systems")]
        public static async void Systems()
        {
            systemsEvidence.Clear();SystemNote("Restoring the bot-built twelve-person colony for isolated system scenarios");
            try
            {
                Catalog=ColonyCatalog.Load();var source=ColonySaves.Load("Library/GameValidation/connected-colony.meridian",Catalog);string snapshot=JsonUtility.ToJson(source);
                await Task.Run(()=>
                {
                    if(World==null){Planet=PlanetGenerator.Generate(source.setup.seed,source.setup.planetParameters);Planet.ReleaseAppearanceBuffers();World=SurfaceGenerator.Generate(Planet,source.setup.region.localDirection,source.setup.surfaceParameters,default,null,0,source.setup);}
                    TrafficContracts(snapshot);BuilderContract(snapshot);ProductionContract(snapshot);TerraformContract(snapshot);
                });
                await DiskContracts(source);File.WriteAllText("Logs/ColonySystemsValidation.txt","PASS\n"+string.Join("\n",systemsEvidence));Debug.Log("Meridian connected systems validation passed.");
            }
            catch(Exception error){if(systemSim!=null)File.WriteAllText("Library/ColonySystemsFailure.json",JsonUtility.ToJson(systemSim.State,true));File.WriteAllText("Logs/ColonySystemsValidation.txt","FAIL\n"+string.Join("\n",systemsEvidence)+"\n"+error);Debug.LogException(error);}
        }
        static ColonySimulation RestoreScenario(string snapshot)=>systemSim=new ColonySimulation(JsonUtility.FromJson<ColonyState>(snapshot),World,Catalog);
        static void Until(ColonySimulation sim,Func<bool> done,float seconds,string label)
        {
            double limit=sim.State.time+seconds;var timer=Stopwatch.StartNew();while(sim.State.time<limit&&!done())sim.Tick(.2f);
            Require(done(),label+" timed out; "+string.Join(" | ",sim.State.jobs.Where(sim.Jobs.Active).Take(8).Select(j=>j.kind+":"+j.blocker+":"+sim.Actor(j.actor)?.activity+":"+sim.Actor(j.actor)?.reason)));
            SystemNote(label+" · "+timer.Elapsed.TotalSeconds.ToString("F2")+"s CPU");
        }
        static float Total(ColonySimulation sim,Good good)=>sim.State.inventories.Sum(i=>sim.Stock.Count(i,good));
        static ColonySimulation RoundTrip(ColonySimulation sim,string phase)
        {
            ColonySaves.Validate(sim.State,Catalog);string json=JsonUtility.ToJson(sim.State);var state=ColonySaves.Deserialize(json);ColonySaves.Validate(state,Catalog);Require(json==JsonUtility.ToJson(state),phase+" changed authority on JSON reconstruction");SystemNote("Exact reconstruction during "+phase);return systemSim=new ColonySimulation(state,World,Catalog);
        }
        static void TrafficContracts(string snapshot)
        {
            var sim=RestoreScenario(snapshot);float minerals=Total(sim,Good.Minerals),money=sim.State.credits;var purchase=sim.Traffic.Trade(Good.Minerals,50,true);string flight=purchase.id;float fee=purchase.fee;
            Until(sim,()=>purchase.phase==FlightPhase.Transit,20,"Paid purchase launches once");sim=RoundTrip(sim,"inbound freight transit");purchase=sim.State.flights.Find(f=>f.id==flight);
            Until(sim,()=>purchase.phase==FlightPhase.Complete,sim.Day*4,"Imported freight lands and physically unloads");
            Require(Mathf.Abs(Total(sim,Good.Minerals)-minerals-50)<.02f,"Import quantity not conserved");Require(sim.State.ledger.Count(e=>e.transaction==flight+":purchase")==1,"Purchase charged more than once");
            bool rejected=false;try{sim.Traffic.Cancel(purchase);}catch(InvalidOperationException){rejected=true;}Require(rejected||purchase.phase==FlightPhase.Complete,"Delivered flight can be refunded");
            var returned=sim.Traffic.Trade(Good.Minerals,40,true);float beforeReturn=Total(sim,Good.Minerals);Until(sim,()=>returned.phase==FlightPhase.Descending,sim.Day*4,"Second purchase reaches physical descent");sim.Traffic.Cancel(returned);
            Until(sim,()=>returned.phase==FlightPhase.Cancelled,30,"Launched import returns without clock reset");Require(Mathf.Abs(Total(sim,Good.Minerals)-beforeReturn)<.02f&&returned.refunded,"Returned import retained goods or lost refund");
            float exportBefore=Total(sim,Good.Minerals);var export=sim.Traffic.Trade(Good.Minerals,35,false);Until(sim,()=>export.phase==FlightPhase.Departing,sim.Day*2,"Export goods reserved and physically hauled to freighter");
            string exportID=export.id;sim=RoundTrip(sim,"loaded export departure");export=sim.State.flights.Find(f=>f.id==exportID);Until(sim,()=>export.phase==FlightPhase.Complete,20,"Loaded export earns one payment");Require(Mathf.Abs(Total(sim,Good.Minerals)-exportBefore+35)<.02f&&sim.State.ledger.Count(e=>e.transaction==exportID+":sale")==1,"Export duplicated stock or proceeds");
            var recruit=sim.Traffic.Recruit(Profession.Builder);int orbital=sim.State.actors.Count(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Sleeping);Until(sim,()=>recruit.completed,sim.Day*3,"Recruitment arrives in orbital cryosleep");Require(sim.State.actors.Count(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Sleeping)==orbital+1,"Recruitment skipped orbit");sim.Stock.Validate();
        }
        static StructureState FreeSite(ColonySimulation sim,string type)
        {
            var origin=sim.State.setup.landing.Position;
            for(int radius=45;radius<=300;radius+=25)for(int bearing=0;bearing<360;bearing+=30)
            {var at=sim.World.Ground(origin+Quaternion.Euler(0,bearing,0)*Vector3.forward*radius);if(ColonyCommands.ValidatePlacement(sim,type,at,0,default)==null)return ColonyCommands.Place(sim,type,at,0);}
            throw new InvalidOperationException("No free test foundation for "+type);
        }
        static void BuilderContract(string snapshot)
        {
            var sim=RestoreScenario(snapshot);sim.State.preferHumanBuilders=true;var site=FreeSite(sim,"habitat");string building=site.id;
            Until(sim,()=>sim.SurfaceActors.Any(a=>a.kind==ActorKind.Colonist&&a.eva&&a.job!=null),sim.Day*6,"Equipped builder takes civilian construction through an airlock");
            var builder=sim.SurfaceActors.First(a=>a.kind==ActorKind.Colonist&&a.eva&&a.job!=null);string person=builder.id;Require(sim.Stock.Count(sim.Stock.Get(builder.equipment),Good.EVASuit)==1&&sim.Stock.Count(sim.Stock.Get(builder.equipment),Good.Toolkit)==1,"Builder lacks owned protective equipment");
            sim=RoundTrip(sim,"civilian EVA with active construction and owned gear");site=sim.Structure(building);builder=sim.Actor(person);
            Until(sim,()=>site.phase==BuildPhase.Complete&&!builder.eva,sim.Day*5,"Civilian construction completes and builder returns safely");
            Require(site.incorporated.Count>0&&builder.location==PersonLocation.Arrived,"Civilian completion missing paid structure or person");
            var industrial=new JobState{kind=JobKind.Build,target=sim.State.structures.First(b=>b.definition=="smelter").id,machineOnly=true};Require(!sim.Jobs.Eligible(builder,industrial),"Builder may take industrial work after EVA");
        }
        static void ProductionContract(string snapshot)
        {
            var sim=RestoreScenario(snapshot);var smelter=sim.State.structures.First(b=>b.definition=="smelter");
            var ore=sim.Traffic.Trade(Good.IronOre,40,true);Until(sim,()=>ore.phase==FlightPhase.Complete,sim.Day*4,"Paid ore delivery replenishes exhausted starter iron");
            Until(sim,()=>smelter.inputsCommitted&&smelter.production>1,sim.Day*3,"Machine controller commits a real production batch");sim=RoundTrip(sim,"committed production batch");smelter=sim.Structure(smelter.id);var recipe=Catalog.Recipe(smelter.recipe);float before=Total(sim,recipe.outputs[0].good);
            Until(sim,()=>!smelter.inputsCommitted,recipe.seconds*3,"Restored batch produces its output once");Require(Total(sim,recipe.outputs[0].good)>=before+recipe.outputs[0].quantity-.05f,"Committed batch lost output");
            // A non-operating miner retains finite reserve and output through explicit replacement.
            var deposit=sim.World.Objects.Where(o=>o.Kind==SurfaceObjectKind.Iron&&sim.World.Owned(o.Position)).OrderBy(o=>(o.Position-sim.State.setup.landing.Position).sqrMagnitude).First(o=>ColonyCommands.ValidatePlacement(sim,"miner",o.Position,0,default)==null);
            var miner=ColonyCommands.Place(sim,"miner",deposit.Position,0);Until(sim,()=>miner.phase==BuildPhase.Complete,sim.Day*10,"Robots build a real miner on a compatible finite deposit");
            float remaining=sim.World.Remaining(deposit.Id);miner.powerFraction=1;sim.Industry.Tick(1);Require(sim.World.Remaining(deposit.Id)<remaining,"Autonomous extractor failed to consume deposit");float after=sim.World.Remaining(deposit.Id);miner.powerFraction=0;sim.Industry.Tick(1);Require(sim.World.Remaining(deposit.Id)==after,"Unpowered miner extracted material");
            miner.condition=.4f;sim.Jobs.Supply(miner,Good.Metal,1,10);sim.Jobs.Supply(miner,Good.Components,1,10);Until(sim,()=>miner.condition>.99f,sim.Day*8,"Industrial maintenance consumes robot labor and parts");
            sim=RoundTrip(sim,"serviced miner and depleted deposit");miner=sim.Structure(miner.id);after=sim.World.Remaining(deposit.Id);ColonyCommands.Dismantle(sim,miner,true);
            Until(sim,()=>miner.phase==BuildPhase.Removed,sim.Day*8,"Explicit paid miner replacement dismantles old equipment");var replacement=sim.State.structures.Last(b=>b.definition=="miner");Require(replacement.id!=miner.id&&replacement.deposit==deposit.Id&&sim.World.Remaining(deposit.Id)==after,"Replacement reset deposit or reused equipment identity");sim.Stock.Validate();
        }
        static void TerraformContract(string snapshot)
        {
            var sim=RestoreScenario(snapshot);sim.State.completedResearch.Add("planetary");sim.State.environment.programEnabled=true;sim.State.credits=200000;
            var plants=new[]{sim.AddStructure("atmosphere",new Vector3(200,24,100),0,true),sim.AddStructure("climate",new Vector3(230,24,100),0,true),sim.AddStructure("biosphere",new Vector3(260,24,100),0,true)};
            var tank=sim.AddStructure("tank",new Vector3(200,24,130),0,true);foreach(var b in plants){b.powerFraction=1;var pipe=sim.AddStructure("pipe",b.position,0,true);pipe.from=b.id;pipe.to=tank.id;pipe.end=tank.position;pipe.length=30;}sim.Networks.Rebuild();
            float step=sim.Year*.05f;var env=sim.State.environment;float initial=env.atmosphereWork;
            sim.Development.Tick(step);Require(env.atmosphereWork==initial,"Terraforming progressed without physical supplies");
            int iterations=0;while(env.atmosphereWork<100&&iterations++<2000)
            {
                // This is an accelerated branch contract, explicitly supplying the reservoir and buffers.
                sim.Stock.Add(sim.Stock.Get(tank.waterInventory),Good.Water,100);sim.Stock.Add(sim.Stock.Get(plants[1].inventory),Good.Minerals,10);sim.Stock.Add(sim.Stock.Get(plants[2].inventory),Good.Biomass,10);
                float old=env.atmosphereWork;sim.State.time+=step;sim.Development.Tick(step);Require(env.atmosphereWork-old<=Catalog.balance.terraformCap*.05f+.0001f,"Terraform cap exceeded");
            }
            float years=iterations*.05f;Require(years>70&&years<73&&env.climateWork>=80&&env.soilWork>=40&&sim.OutdoorsSafe,"Nominal restoration duration or milestones incorrect");
            float money=sim.State.credits;sim.Development.Tick(sim.Year);Require(sim.State.credits==money,"Completed program still charged annual funding");Require(sim.State.acknowledged.Contains("planet-restored"),"Restoration completion not acknowledged");SystemNote("Accelerated supplied/powered program: "+years.ToString("F2")+" years nominal; actual input/funding consumption, missing-supply stall, rate cap, outdoor milestones, continued play");
        }
        static async Task DiskContracts(ColonyState state)
        {
            const string folder="Library/GameValidation/FailureContracts";Directory.CreateDirectory(folder);await ColonySaves.Save("atomic",state,Catalog,directory:folder);string path=Path.Combine(folder,"atomic.meridian"),first=File.ReadAllText(path);state.name="Snapshot two";await ColonySaves.Save("atomic",state,Catalog,directory:folder);Require(File.ReadAllText(path+".bak")==first,"Previous publication missing backup");
            File.WriteAllText(path+".interrupted.tmp","incomplete");Require(ColonySaves.Load(path,Catalog).name=="Snapshot two","Interrupted temporary write damaged publication");
            File.WriteAllText(path,"corrupt fixture");bool corrupt=false;try{ColonySaves.Load(path,Catalog);}catch{corrupt=true;}Require(corrupt&&ColonySaves.Load(path+".bak",Catalog)!=null,"Corruption or backup handling failed");
            var unsupported=JsonUtility.FromJson<ColonyState>(JsonUtility.ToJson(state));unsupported.schema=900;bool refused=false;try{ColonySaves.Validate(unsupported,Catalog);}catch{refused=true;}Require(refused,"Unsupported schema accepted");
            string blocker=Path.Combine(folder,"file-not-directory");File.WriteAllText(blocker,"test");bool failed=false;try{await ColonySaves.Save("cannot-write",state,Catalog,directory:blocker);}catch{failed=true;}Require(failed&&File.ReadAllText(path+".bak")==first,"Failed write damaged last valid backup");SystemNote("Atomic previous-version backup, corrupt file, unsupported schema, failed write path, and interrupted temporary write recovery");
        }
        public static void FrameColony()
        {
            var game=ColonyRuntime.Current;if(!game)throw new InvalidOperationException("Colony not ready");var ship=game.Simulation.State.setup.landing.Position;game.Camera.Restore(new CameraState{pivot=ship+new Vector3(60,0,0),yaw=-25,pitch=57,distance=245});game.Visuals.ToggleMaster();game.UI.Open("Overview");
        }
    }
}
