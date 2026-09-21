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
        static ColonySimulation integrated;static readonly List<string> integratedEvidence=new List<string>();
        static void Evidence(string text){integratedEvidence.Add(text);File.WriteAllText("Logs/ColonyIntegratedValidation.txt","RUNNING\n"+string.Join("\n",integratedEvidence));}
        [MenuItem("Meridian/Colony/Validate Connected Colony")]
        public static async void Integrated()
        {
            integratedEvidence.Clear();Evidence("Preparing connected-colony fixture on the real seeded 6x6km terrain");
            try
            {
                Catalog=ColonyCatalog.Load();var pp=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset").Snapshot();var sp=AssetDatabase.LoadAssetAtPath<SurfaceGenerationSettings>("Assets/_Meridian/Settings/SurfaceGeneration.asset").Snapshot();
                await Task.Run(()=>{if(World==null)Generate(pp,sp);Opening();});
                Fixture=integrated.State;Fixture.camera=new CameraState{pivot=Fixture.setup.landing.Position+Vector3.right*60,yaw=-25,pitch=57,distance=245};await ColonySaves.Save("connected-colony",Fixture,Catalog,directory:"Library/GameValidation");
                var restored=ColonySaves.Load("Library/GameValidation/connected-colony.meridian",Catalog);
                Require(JsonUtility.ToJson(Fixture)==JsonUtility.ToJson(restored),"Atomic disk snapshot differs after round trip");Evidence("Atomic disk save/read reproduces the complete authority DTO exactly");
                File.WriteAllText("Logs/ColonyIntegratedValidation.txt","PASS\n"+string.Join("\n",integratedEvidence));Debug.Log("Meridian connected-colony validation passed.");
            }
            catch(Exception error){if(integrated!=null)File.WriteAllText("Library/ColonyIntegrationFailure.json",JsonUtility.ToJson(integrated.State,true));File.WriteAllText("Logs/ColonyIntegratedValidation.txt","FAIL\n"+string.Join("\n",integratedEvidence)+"\n"+error);Debug.LogException(error);}
        }
        static StructureState Link(ColonySimulation sim,string type,StructureState a,StructureState b)
        {
            var start=sim.Navigation.Door(a,b.position);var end=sim.Navigation.Door(b,a.position);return ColonyCommands.Place(sim,type,start,0,end,a.id,b.id);
        }
        static void RunUntil(ColonySimulation sim,Func<bool> condition,float maximum,string label)
        {
            double end=sim.State.time+maximum;var timer=Stopwatch.StartNew();while(sim.State.time<end&&!condition())sim.Tick(.2f);
            Require(condition(),label+" timed out at day "+(sim.State.time/sim.Day).ToString("0.0")+"; blocked structures: "+string.Join(" | ",sim.State.structures.Where(b=>b.phase!=BuildPhase.Complete&&b.phase!=BuildPhase.Removed).Take(12).Select(b=>b.name+":"+b.phase+":"+b.blocker))+"; jobs: "+string.Join(" | ",sim.State.jobs.Where(j=>sim.Jobs.Active(j)).Take(12).Select(j=>j.kind+":"+j.stage+":"+j.blocker+":"+sim.Actor(j.actor)?.reason)));
            Evidence(label+" · day "+(sim.State.time/sim.Day).ToString("0.00")+" · "+timer.Elapsed.TotalSeconds.ToString("F2")+"s CPU");
        }
        static void Opening()
        {
            var sim=integrated=new ColonySimulation(NewState(),World,Catalog);sim.FinalizeArrival();var ship=sim.State.structures.First(b=>b.definition=="ship");Vector3 origin=ship.position;
            StructureState Place(string type,float x,float z)=>ColonyCommands.Place(sim,type,sim.World.Ground(origin+new Vector3(x,0,z)),0);
            var row1=new[]{Place("habitat",35,0),Place("habitat",65,0),Place("canteen",95,0),Place("greenhouse",125,0),Place("greenhouse",155,0)};
            var row2=new[]{Place("clinic",35,34),Place("lab",65,34),Place("sanitation",95,34),Place("lounge",125,34),Place("warehouse",155,34)};
            var utility=new[]{Place("air",35,-43),Place("well",65,-43),Place("charger",95,-43),Place("tank",125,-43),Place("smelter",155,-43)};
            var energy=new[]{Place("solar",35,-78),Place("solar",65,-78),Place("wind",95,-78),Place("battery",125,-78)};
            var lockModule=Place("airlock",-32,0);Link(sim,"corridor",ship,lockModule);Link(sim,"cable",ship,lockModule);
            var pairs=new List<(StructureState a,StructureState b)>{(ship,row1[0]),(row1[0],row2[0]),(row1[0],utility[0]),(utility[0],energy[0])};
            foreach(var row in new[]{row1,row2,utility,energy})for(int i=1;i<row.Length;i++)pairs.Add((row[i-1],row[i]));
            foreach(var pair in pairs)Link(sim,"cable",pair.a,pair.b);
            foreach(var pair in pairs.Where(p=>!energy.Contains(p.a)&&!energy.Contains(p.b)))Link(sim,"pipe",pair.a,pair.b);
            foreach(var pair in pairs.Where(p=>(sim.Definition(p.a).sealedModule||p.a.definition=="air")&&(sim.Definition(p.b).sealedModule||p.b.definition=="air")))Link(sim,"corridor",pair.a,pair.b);
            foreach(var b in sim.State.structures.Where(b=>b.phase!=BuildPhase.Complete&&sim.Definition(b).link))Require(b.length<500,"Overlong opening network");
            var costs=sim.State.structures.Where(b=>b.phase!=BuildPhase.Complete).SelectMany(b=>ColonyCommands.Cost(sim,b)).GroupBy(i=>i.good).ToDictionary(g=>g.Key,g=>g.Sum(i=>i.quantity));
            foreach(var cost in costs)Require(cost.Value<=sim.State.inventories.Sum(i=>sim.Stock.Count(i,cost.Key)),"Opening layout exceeds starter "+cost.Key+": "+cost.Value);
            Evidence("Real opening blueprints: "+string.Join(", ",costs.Select(c=>c.Value+" "+c.Key))+"; no material grants or instant construction");
            RunUntil(sim,()=>sim.State.structures.All(b=>b.phase==BuildPhase.Complete),sim.Day*12,"Autonomous connected-colony construction");
            RunUntil(sim,()=>sim.People.FreeBeds(false).Count>=12&&row1.All(b=>sim.Operating(b))&&row2.All(b=>sim.Operating(b)),sim.Day*4,"Utility commissioning and air reserves");
            var batch1=new List<string>();foreach(var profession in new[]{Profession.Builder,Profession.Botanist,Profession.Botanist,Profession.Botanist,Profession.Medic,Profession.Service})batch1.Add(sim.State.actors.First(a=>a.kind==ActorKind.Colonist&&a.profession==profession&&a.location==PersonLocation.Sleeping&&!batch1.Contains(a.id)).id);
            var first=sim.Traffic.RequestPersonnel(batch1);Require(first.passengers.Count==6&&batch1.All(id=>sim.Actor(id).location==PersonLocation.Reserved),"Personnel not reserved");
            RunUntil(sim,()=>first.phase==FlightPhase.Complete,sim.Day*3,"First six-person physical flight");Require(sim.Population==6,"Incorrect first arrival population");
            var batch2=new List<string>();foreach(var profession in new[]{Profession.Builder,Profession.Builder,Profession.Scientist,Profession.Scientist,Profession.Technician,Profession.Service})batch2.Add(sim.State.actors.First(a=>a.kind==ActorKind.Colonist&&a.profession==profession&&a.location==PersonLocation.Sleeping&&!batch2.Contains(a.id)).id);
            var second=sim.Traffic.RequestPersonnel(batch2);RunUntil(sim,()=>second.phase==FlightPhase.Complete,sim.Day*3,"Second six-person physical flight");Require(sim.Population==12,"Incorrect second arrival population");
            sim.Development.QueueResearch("agriculture");float foodBefore=sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Food));double start=sim.State.time;
            var attendance=new Dictionary<string,float>();var routines=new Dictionary<string,float>();
            RunUntil(sim,()=>{foreach(var greenhouse in row1.Where(b=>b.definition=="greenhouse")){if(!attendance.ContainsKey(greenhouse.id))attendance[greenhouse.id]=0;attendance[greenhouse.id]+=greenhouse.staffed*.2f;}foreach(var person in sim.SurfaceActors.Where(a=>a.kind==ActorKind.Colonist&&a.profession==Profession.Botanist)){string routine=person.intention??"idle";if(!routines.ContainsKey(routine))routines[routine]=0;routines[routine]+=.2f;}return sim.State.time>=start+sim.Day*6;},sim.Day*7,"Six day/night cycles with autonomous needs, staffing and production");
            Evidence("Greenhouse average effective staff: "+string.Join(", ",attendance.Values.Select(v=>(v/(sim.Day*6)).ToString("F2")))+"; botanist routines: "+string.Join(", ",routines.Select(r=>r.Key+" "+r.Value.ToString("F0")+"s")));
            Require(sim.Population==12&&sim.SurfaceActors.Where(a=>a.kind==ActorKind.Colonist).All(a=>a.health>65),"Crew health/population failed survival check: "+string.Join(" | ",sim.State.actors.Where(a=>a.kind==ActorKind.Colonist&&a.location!=PersonLocation.Sleeping).Select(a=>a.name+":"+a.health+":"+a.hunger+":"+a.thirst+":"+a.activity+":"+a.reason)));
            float foodAfter=sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Food));Require(foodAfter>=foodBefore-15,"Two greenhouses and three botanists are not food-sustainable: "+foodBefore+" → "+foodAfter);Evidence("12 crew healthy; total food "+foodBefore.ToString("F1")+" → "+foodAfter.ToString("F1"));
            Require(sim.State.completedResearch.Contains("agriculture")||sim.State.researchProgress>0,"Scientists produced no research");
            sim.Stock.Validate();ColonySaves.Validate(sim.State,Catalog);
            // Loss and return of a pressure link changes connectivity but cannot copy a reserve.
            var bridge=sim.State.structures.First(b=>b.definition=="corridor"&&b.to==row1[1].id);float air=sim.State.structures.Sum(b=>b.air);bridge.isolated=true;sim.Networks.Dirty();sim.Networks.Rebuild();Require(Mathf.Abs(sim.State.structures.Sum(b=>b.air)-air)<.0001f,"Network split duplicated air");Require(!sim.Networks.Connected(ship.id,row1[1].id),"Isolated bulkhead still supplies network");bridge.isolated=false;sim.Networks.Dirty();sim.Networks.Rebuild();Require(Mathf.Abs(sim.State.structures.Sum(b=>b.air)-air)<.0001f,"Network reconnect duplicated air");Evidence("Pressure split/reconnect conserves finite air and isolates routes");
        }
        public static void OpenFixture()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first");var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuPresentation>();var transition=(ScreenTransition)new SerializedObject(menu).FindProperty("transitionPrefab").objectReferenceValue;
            ColonyLoadPipeline.Load("Library/GameValidation/connected-colony.meridian",transition,Debug.LogError);
        }
        public static void Capture()
        {Directory.CreateDirectory("Captures");ScreenCapture.CaptureScreenshot("Captures/Colony-PlayMode.png");}
        public static string RuntimeStatus()
        {
            var game=ColonyRuntime.Current;if(!game)return ColonyLoadPipeline.Active?ColonyLoadPipeline.Active.Message:"No colony runtime";
            return "Population="+game.Simulation.Population+" structures="+game.Simulation.State.structures.Count+" terrains="+game.Landscape.TerrainCount+" props="+game.Landscape.PropBatchCount+" windows="+game.Simulation.State.windows.Count+" save="+ColonySaves.LastError;
        }
    }
}
