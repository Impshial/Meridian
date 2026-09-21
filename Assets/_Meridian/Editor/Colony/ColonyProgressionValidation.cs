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
        static readonly List<string> progressionEvidence=new List<string>();static ColonySimulation progressionSim;
        static void ProgressNote(string value){progressionEvidence.Add(value);File.WriteAllText("Logs/ColonyProgressionValidation.txt","RUNNING\n"+string.Join("\n",progressionEvidence));}
        [MenuItem("Meridian/Colony/Validate Visitors and Regions")]
        public static async void Progression()
        {
            progressionEvidence.Clear();ProgressNote("Preparing connected-colony progression scenarios");
            try
            {
                Catalog=ColonyCatalog.Load();var state=ColonySaves.Load("Library/GameValidation/connected-colony.meridian",Catalog);string snapshot=JsonUtility.ToJson(state);
                await Task.Run(()=>
                {
                    if(World==null){Planet=PlanetGenerator.Generate(state.setup.seed,state.setup.planetParameters);Planet.ReleaseAppearanceBuffers();World=SurfaceGenerator.Generate(Planet,state.setup.region.localDirection,state.setup.surfaceParameters,default,null,0,state.setup);}
                    VisitorScenario(snapshot);RegionScenario(snapshot);
                });
                await ColonySaves.Save("regional-colony",progressionSim.State,Catalog,directory:"Library/GameValidation");File.WriteAllText("Logs/ColonyProgressionValidation.txt","PASS\n"+string.Join("\n",progressionEvidence));Debug.Log("Meridian visitors and regional progression checks passed.");
            }
            catch(Exception error){if(progressionSim!=null)File.WriteAllText("Library/ColonyProgressionFailure.json",JsonUtility.ToJson(progressionSim.State,true));File.WriteAllText("Logs/ColonyProgressionValidation.txt","FAIL\n"+string.Join("\n",progressionEvidence)+"\n"+error);Debug.LogException(error);}
        }
        static void WaitFor(ColonySimulation sim,Func<bool> done,float seconds,string label)
        {
            double end=sim.State.time+seconds;var timer=Stopwatch.StartNew();while(sim.State.time<end&&!done())sim.Tick(.2f);
            Require(done(),label+" timed out; "+string.Join(" | ",sim.State.structures.Where(b=>b.phase!=BuildPhase.Complete&&b.phase!=BuildPhase.Removed).Take(6).Select(b=>b.name+":"+b.blocker))+"; "+string.Join(" | ",sim.State.jobs.Where(sim.Jobs.Active).Take(6).Select(j=>j.kind+":"+j.blocker+":"+sim.Actor(j.actor)?.reason)));
            ProgressNote(label+" · "+timer.Elapsed.TotalSeconds.ToString("F2")+"s CPU · day "+(sim.State.time/sim.Day).ToString("F2"));
        }
        static void VisitorScenario(string snapshot)
        {
            var sim=progressionSim=new ColonySimulation(JsonUtility.FromJson<ColonyState>(snapshot),World,Catalog);
            // Explicit development grant isolates the later visitor branch from the separately verified opening.
            foreach(var pending in sim.State.researchQueue.ToArray())sim.Development.RemoveResearch(pending);sim.State.researchPoints+=100;sim.Development.QueueResearch("visitor");sim.Development.Tick(.2f);Require(sim.State.completedResearch.Contains("visitor"),"Visitor research did not spend the granted test points");
            var ship=sim.State.structures.First(b=>b.definition=="ship");sim.Stock.Add(sim.Stock.Get(ship.inventory),Good.Metal,350);sim.Stock.Add(sim.Stock.Get(ship.inventory),Good.Minerals,250);sim.Stock.Add(sim.Stock.Get(ship.inventory),Good.Components,100);ProgressNote("Visitor branch development fixture grants: 100 science, 350 Metal, 250 Minerals, 100 Components; all branch construction/hauls still use real bots");
            var row=new List<StructureState>();string[] types={"habitat","reception","lodge","restaurant","attraction"};
            for(int i=0;i<types.Length;i++)row.Add(ColonyCommands.Place(sim,types[i],sim.World.Ground(ship.position+new Vector3(35+i*30,0,70)),0));
            var clinic=sim.State.structures.First(b=>b.definition=="clinic");var previous=clinic;
            foreach(var b in row){foreach(string type in new[]{"cable","pipe","corridor"})Link(sim,type,previous,b);previous=b;}
            var previousPower=sim.State.structures.First(b=>b.definition=="wind");for(int i=0;i<3;i++){var wind=ColonyCommands.Place(sim,"wind",sim.World.Ground(ship.position+new Vector3(95-i*30,0,-115)),0);Link(sim,"cable",previousPower,wind);previousPower=wind;}
            WaitFor(sim,()=>sim.State.structures.All(b=>b.phase==BuildPhase.Complete),sim.Day*15,"Robots construct and connect the visitor district");
            WaitFor(sim,()=>sim.People.FreeBeds(false).Count>=6&&row.All(sim.Operating),sim.Day*4,"Visitor district pressure and power commission");
            var people=sim.State.actors.Where(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Sleeping&&a.profession==Profession.Service).Take(6).Select(a=>a.id).ToArray();var crew=sim.Traffic.RequestPersonnel(people);
            WaitFor(sim,()=>crew.phase==FlightPhase.Complete,sim.Day*3,"Six hospitality workers arrive by reserved personnel flight");sim.State.visitorsOpen=true;
            WaitFor(sim,()=>sim.SurfaceActors.Any(a=>a.kind==ActorKind.Visitor),sim.Day*10,"Staffed destination admits a real arriving visitor group");
            sim.State.visitorsOpen=false;var guests=sim.State.actors.Where(a=>a.kind==ActorKind.Visitor).Select(a=>a.id).ToArray();Require(guests.Length>=2&&guests.Length<=6,"Visitor group size outside admission range");
            var copy=JsonUtility.FromJson<ColonyState>(JsonUtility.ToJson(sim.State));ColonySaves.Validate(copy,Catalog);sim=progressionSim=new ColonySimulation(copy,World,Catalog);
            WaitFor(sim,()=>guests.All(id=>sim.Actor(id).location==PersonLocation.Departed),sim.Day*9,"Visitors use services, spend finite wallets and board a physical departure");
            float income=sim.State.ledger.Where(l=>l.category=="Visitors").Sum(l=>l.amount),spent=guests.Sum(id=>200-sim.Actor(id).wallet);
            Require(income>0&&Mathf.Abs(income-spent)<.02f,"Visitor income does not match wallet spending");Require(guests.All(id=>sim.Actor(id).home==null&&sim.Actor(id).health>0),"Departed guests kept beds or died before leaving");
            ProgressNote("Visitor wallets paid "+income.ToString("F2")+" credits exactly; "+guests.Length+" guests released their beds; live reputation "+sim.State.reputation.ToString("F1"));
        }
        static void RegionScenario(string snapshot)
        {
            var sim=progressionSim=new ColonySimulation(JsonUtility.FromJson<ColonyState>(snapshot),World,Catalog);sim.State.completedResearch.Add("regions");
            var coordinate=new[]{new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(-1,0),new Vector2Int(0,-1)}.First(p=>sim.Development.RegionBlocker(p.x,p.y)==null);
            var cancelled=sim.Development.BeginRegion(coordinate.x,coordinate.y);cancelled.active=false;float funds=sim.State.credits;var operation=sim.Development.BeginRegion(coordinate.x,coordinate.y);var tiles=new SurfaceTileData[36];var timer=Stopwatch.StartNew();
            Parallel.For(0,36,new ParallelOptions{MaxDegreeOfParallelism=4},i=>tiles[i]=World.GenerateTile(new Vector2Int(coordinate.x*6-3+i%6,coordinate.y*6-3+i/6),default));
            sim.Development.CompleteRegion(cancelled,tiles);Require(sim.State.credits==funds&&sim.State.regions.Count==1,"Cancelled acquisition committed");sim.Development.CompleteRegion(operation,tiles);float cost=operation.quotedPrice;sim.Development.CompleteRegion(operation,tiles);
            Require(sim.State.regions.Count==2&&Mathf.Abs(sim.State.credits-funds+cost)<.001f,"Acquisition charged or committed more than once");ProgressNote("36 adjacent tiles generated in "+timer.Elapsed.TotalSeconds.ToString("F2")+"s; cancellation and repeated completion preserve one charge and one ownership commit");
            var byAddress=sim.World.Tiles.ToDictionary(t=>t.Address);int edges=0;float maximum=0;
            foreach(var tile in tiles)foreach(var direction in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
            {
                if(!byAddress.TryGetValue(tile.Address+direction,out var neighbor)||tiles.Contains(neighbor))continue;int n=tile.Heights.GetLength(0)-1;
                for(int i=0;i<=n;i++){int x=direction.x<0?0:direction.x>0?n:i,z=direction.y<0?0:direction.y>0?n:i,nx=direction.x<0?n:direction.x>0?0:i,nz=direction.y<0?n:direction.y>0?0:i;float a=tile.MinHeight+tile.HeightRange*tile.Heights[z,x],b=neighbor.MinHeight+neighbor.HeightRange*neighbor.Heights[nz,nx];maximum=Mathf.Max(maximum,Mathf.Abs(a-b));}edges++;
            }
            Require(edges==6&&maximum<.02f,"Neighbor terrain border mismatch: "+edges+" edges / "+maximum+"m");
            var tree=tiles.SelectMany(t=>t.Objects).First(o=>o.Kind==SurfaceObjectKind.Tree);var drone=sim.SurfaceActors.First(a=>a.kind==ActorKind.Drone);sim.Harvest(tree.Id,sim.Stock.Get(drone.inventory),sim.World.Remaining(tree.Id));float reserve=sim.World.Remaining(tree.Id);
            // Separate, stocked cross-border staging points test distance without granting a teleporting inventory API.
            Vector3 normal=new Vector3(coordinate.x,0,coordinate.y),side=new Vector3(-coordinate.y,0,coordinate.x);StructureState source=null,destination=null;
            for(int offset=-2200;offset<2200;offset+=100){var a=sim.World.Ground(normal*2850+side*offset);var b=sim.World.Ground(normal*3150+side*offset);if(ColonyCommands.ValidatePlacement(sim,"stockyard",a,0,default)!=null||ColonyCommands.ValidatePlacement(sim,"stockyard",b,0,default)!=null)continue;source=sim.AddStructure("stockyard",a,0,true);destination=sim.AddStructure("stockyard",b,0,true);break;}
            Require(source!=null,"No cross-border staging fixture");sim.Stock.Add(sim.Stock.Get(source.inventory),Good.Metal,30);var job=sim.Jobs.Create(JobKind.Haul,destination.id,100,true);job.source=source.inventory;job.destination=destination.inventory;job.good=Good.Metal;job.quantity=30;Require(sim.Stock.Reserve(job.id,sim.Stock.Get(source.inventory),sim.Stock.Get(destination.inventory),Good.Metal,30),"Cross-border claim failed");
            WaitFor(sim,()=>job.carrying,sim.Day*6,"Carrier physically reaches the remote source and loads cargo");Require(sim.Stock.Count(sim.Stock.Get(destination.inventory),Good.Metal)==0,"Cargo appeared remotely before transport");
            var state=JsonUtility.FromJson<ColonyState>(JsonUtility.ToJson(sim.State));ColonySaves.Validate(state,Catalog);sim=progressionSim=new ColonySimulation(state,World,Catalog);sim.World.AddTiles(tiles);job=sim.State.jobs.First(j=>j.id==job.id);destination=sim.Structure(destination.id);
            WaitFor(sim,()=>job.stage==JobStage.Complete,sim.Day*3,"Saved cross-region carrier completes actual travel and delivery");Require(Mathf.Abs(sim.Stock.Count(sim.Stock.Get(destination.inventory),Good.Metal)-30)<.001f&&Mathf.Abs(sim.World.Remaining(tree.Id)-reserve)<.001f,"Regional load changed cargo or depleted resource");
            ProgressNote("Six shared borders agree within "+maximum.ToString("G3")+"m; original frame retained, new-region depletion restored, cross-border cargo conserved");sim.State.camera=new CameraState{pivot=source.position,yaw=-25,pitch=57,distance=500};
        }
    }
}
