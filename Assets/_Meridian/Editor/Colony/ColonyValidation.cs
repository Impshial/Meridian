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
        public static SurfaceWorldData World;public static PlanetData Planet;public static ColonyCatalog Catalog;
        public static ColonyState Fixture;static readonly List<string> evidence=new List<string>();
        static void Require(bool test,string reason){if(!test)throw new InvalidOperationException(reason);}
        static void Note(string value){evidence.Add(value);File.WriteAllText("Logs/ColonyValidation.txt","RUNNING\n"+string.Join("\n",evidence));}
        [MenuItem("Meridian/Colony/Validate Simulation Contracts")]
        public static async void Run()
        {
            Directory.CreateDirectory("Logs");evidence.Clear();Note("Preparing representative authoritative geography");
            try
            {
                Catalog=ColonyCatalog.Load();var pp=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset").Snapshot();var sp=AssetDatabase.LoadAssetAtPath<SurfaceGenerationSettings>("Assets/_Meridian/Settings/SurfaceGeneration.asset").Snapshot();
                await Task.Run(()=>{if(World==null)Generate(pp,sp);Contracts();});
                File.WriteAllText("Logs/ColonyValidation.txt","PASS\n"+string.Join("\n",evidence));Debug.Log("Meridian colony contract checks passed.");
            }
            catch(Exception error){File.WriteAllText("Logs/ColonyValidation.txt","FAIL\n"+string.Join("\n",evidence)+"\n"+error);Debug.LogException(error);}
        }
        static void Generate(PlanetParameters pp,SurfaceParameters sp)
        {
            var timer=Stopwatch.StartNew();Planet=PlanetGenerator.Generate(73129,pp);Planet.ReleaseAppearanceBuffers();Vector3 direction=default;float best=float.NegativeInfinity;
            for(int i=0;i<Planet.Graph.Directions.Length;i+=13)
            {
                if(Planet.Water[i]!=PlanetWater.None||Planet.Biomes[i]!=PlanetBiome.Forest)continue;var at=Planet.Graph.Directions[i];var frame=new SurfaceFrame(at,sp.mappingRadius);bool dry=true;
                for(int z=-1;z<=1&&dry;z++)for(int x=-1;x<=1;x++)if(!Planet.Sample(frame.Direction(x*1000,z*1000)).IsLand){dry=false;break;}
                float score=Planet.Moisture[i]-Planet.Elevation[i];if(!dry||score<=best)continue;best=score;direction=at;
            }
            Require(direction.sqrMagnitude>.9f,"No inland forest fixture region");World=SurfaceGenerator.Generate(Planet,direction,sp);Note("4K planet + 36 terrain tiles generated in "+timer.Elapsed.TotalSeconds.ToString("F2")+"s; "+World.Objects.Length+" resources");
        }
        public static ColonyState NewState()
        {
            var direction=World.Frame.anchor;var setup=new ColonySetupRecord{seed=Planet.Seed,planetVersion=Planet.Version,planetParameters=Planet.Parameters,surfaceVersion=World.Version,surfaceParameters=World.Parameters,surfaceFrame=World.Frame,plainCentre=World.PlainCentre,plainHeight=World.PlainHeight,shapePlain=World.HasShapedPlain,region=new PlanetSelection(Planet,direction),landing=World.DefaultLanding.Copy(),landingConfirmed=true,surveyTiles=World.Tiles.Select(t=>t.Address).ToArray()};return ColonySimulation.Create(setup,Catalog);
        }
        static void Contracts()
        {
            var sim=new ColonySimulation(NewState(),World,Catalog);sim.FinalizeArrival();string start=JsonUtility.ToJson(sim.State);sim.FinalizeArrival();Require(start==JsonUtility.ToJson(sim.State),"Deployment is not idempotent");
            Require(sim.State.actors.Count(a=>a.kind==ActorKind.WorkRobot)==6&&sim.State.actors.Count(a=>a.kind==ActorKind.ForestryBot)==2&&sim.State.actors.Count(a=>a.kind==ActorKind.Drone)==4,"Incorrect machine deployment");Require(sim.State.actors.Count(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Sleeping)==48,"Orbital roster must remain asleep");
            var ship=sim.State.structures.First(b=>b.definition=="ship");foreach(var item in Catalog.balance.manifest)Require(Mathf.Abs(sim.Stock.Count(sim.Stock.Get(item.good==Good.Water?ship.waterInventory:ship.inventory),item.good)-item.quantity)<.001f,"Starter manifest mismatch");Note("Arrival exactly once; 6 work / 2 forestry / 4 drones; exact cargo; 48 sleeping people");
            var carrier=sim.State.actors.First(a=>a.kind==ActorKind.Drone);var source=sim.Stock.Get(ship.inventory);var cargo=sim.Stock.Get(carrier.inventory);var dest=sim.Stock.Create(ship.id,100);var job=sim.Jobs.Create(JobKind.Haul,ship.id);job.source=source.id;job.destination=dest.id;job.good=Good.Metal;job.quantity=40;job.actor=carrier.id;carrier.job=job.id;
            float before=sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Metal));Require(sim.Stock.Reserve(job.id,source,dest,Good.Metal,40),"Reservation failed");Require(!sim.Stock.Consume(source,Good.Metal,570),"Reservation permits double spending");Require(sim.Stock.Transfer(source,cargo,Good.Metal,40,job.id)==40,"Pickup mismatch");sim.Stock.Release(job.id,true);job.carrying=true;job.stage=JobStage.Delivering;
            ColonySaves.Validate(sim.State,Catalog);var copy=JsonUtility.FromJson<ColonyState>(JsonUtility.ToJson(sim.State));ColonySaves.Validate(copy,Catalog);var restored=new ColonySimulation(copy,World,Catalog);var copiedJob=copy.jobs.First(j=>j.id==job.id);Require(restored.Stock.Transfer(restored.Stock.Get(carrier.inventory),restored.Stock.Get(dest.id),Good.Metal,40,copiedJob.id)==40,"Loaded in-transit cargo not owned by carrier");restored.Stock.Release(copiedJob.id);Require(Mathf.Abs(before-copy.inventories.Sum(i=>restored.Stock.Count(i,Good.Metal)))<.001f,"Save/reload handoff changed material quantity");restored.Stock.Validate();sim.Jobs.Cancel(job);sim.Stock.Transfer(cargo,source,Good.Metal,40);Note("Reservations exclude double spend; save/reload during loaded haul conserves every metal unit");
            var builder=sim.State.actors.First(a=>a.profession==Profession.Builder&&a.kind==ActorKind.Colonist);builder.location=PersonLocation.Arrived;var miner=sim.AddStructure("miner",ship.position+Vector3.right*70,0);var industrial=sim.Jobs.Create(JobKind.Build,miner.id,1,true);Require(!sim.Jobs.Eligible(builder,industrial),"Builder eligible for industrial construction");sim.State.environment.atmosphere=sim.State.environment.climate=100;Require(!sim.Jobs.Eligible(builder,industrial),"Terraforming bypassed machine-only capability");sim.Jobs.Cancel(industrial);miner.phase=BuildPhase.Removed;builder.location=PersonLocation.Sleeping;sim.State.environment.atmosphere=40;sim.State.environment.climate=50;Note("Industrial eligibility stays machine-only before and after outdoor habitability");
            var tree=World.Objects.First(o=>o.Kind==SurfaceObjectKind.Tree);var forestry=sim.State.actors.First(a=>a.kind==ActorKind.ForestryBot);float reserve=sim.World.Remaining(tree.Id);float harvested=sim.Harvest(tree.Id,sim.Stock.Get(forestry.inventory),17);Require(Mathf.Abs(reserve-sim.World.Remaining(tree.Id)-harvested)<.0001f,"Harvest not conserved");var harvestCopy=JsonUtility.FromJson<ColonyState>(JsonUtility.ToJson(sim.State));var resim=new ColonySimulation(harvestCopy,World,Catalog);Require(Mathf.Abs(resim.World.Remaining(tree.Id)-(reserve-17))<.0001f,"Depletion not persistent");Note("Finite resources and harvested carrier cargo survive reconstruction");
            // Full-stock transfer and refunds on a partially built structure, including delivered and committed inputs.
            var site=sim.AddStructure("stockyard",sim.World.Ground(ship.position+Vector3.right*55),0);sim.Stock.Transfer(source,sim.Stock.Get(site.inventory),Good.Metal,10);site.incorporated.Add(new Amount(Good.Metal,20));site.phase=BuildPhase.Dismantling;
            float recoveredBefore=sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Metal));ColonyCommands.Recover(sim,site);Require(Mathf.Abs(sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Metal))-recoveredBefore-12)<.001f,"Dismantling failed to recover 60% incorporated material");ColonyCommands.Recover(sim,site);Require(Mathf.Abs(sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Metal))-recoveredBefore-12)<.001f,"Demolition callback duplicated refunds");Note("Delivered cargo recovered whole; incorporated refund commits once at 60%");
            // Real workers construct a foundation using only remaining expedition supplies.
            var at=ship.position+new Vector3(40,0,36);at=sim.World.Ground(at);string placement=ColonyCommands.ValidatePlacement(sim,"solar",at,0,default);Require(placement==null,"Opening test site unavailable: "+placement);var solar=ColonyCommands.Place(sim,"solar",at,0);var timer=Stopwatch.StartNew();
            for(int i=0;i<4000&&solar.phase!=BuildPhase.Complete;i++)sim.Tick(.1f);
            Require(solar.phase==BuildPhase.Complete,"Autonomous construction did not complete: "+solar.phase+" "+solar.blocker+"; jobs="+string.Join("|",sim.State.jobs.Where(j=>sim.Jobs.Active(j)).Select(j=>j.kind+":"+j.stage+":"+j.blocker+":"+sim.Actor(j.actor)?.reason)));
            Require(solar.incorporated.Count==Catalog.Building("solar").cost.Count,"Construction lost committed cost");Note("Robots hauled and built solar array in "+sim.State.time.ToString("F1")+" simulation seconds ("+timer.Elapsed.TotalSeconds.ToString("F2")+"s CPU)");
            sim.Stock.Validate();ColonySaves.Validate(sim.State,Catalog);Fixture=sim.State;File.WriteAllText("Library/ColonyContractState.json",JsonUtility.ToJson(Fixture));Note("Completed fixture validates stable references and inventory ownership");
        }
    }
}
