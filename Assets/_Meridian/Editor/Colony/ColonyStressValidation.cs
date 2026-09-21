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
        static readonly List<string> stressEvidence=new List<string>();static ColonySimulation stressSim;
        static void StressNote(string text){stressEvidence.Add(text);File.WriteAllText("Logs/ColonyStressValidation.txt","RUNNING\n"+string.Join("\n",stressEvidence));}
        [MenuItem("Meridian/Colony/Validate Developed Colony Scale")]
        public static async void Stress()
        {
            stressEvidence.Clear();StressNote("Preparing explicit development fixture: 200 residents, 100 visitors, 150 machines, two acquired regions");
            try
            {
                Catalog=ColonyCatalog.Load();var state=ColonySaves.Load("Library/GameValidation/regional-colony.meridian",Catalog);
                await Task.Run(()=>
                {
                    if(World==null){Planet=PlanetGenerator.Generate(state.setup.seed,state.setup.planetParameters);Planet.ReleaseAppearanceBuffers();World=SurfaceGenerator.Generate(Planet,state.setup.region.localDirection,state.setup.surfaceParameters,default,null,0,state.setup);}
                    stressSim=new ColonySimulation(state,World,Catalog);var extra=new SurfaceTileData[36];
                    Parallel.For(0,36,new ParallelOptions{MaxDegreeOfParallelism=4},i=>extra[i]=World.GenerateTile(new Vector2Int(3+i%6,-3+i/6),default));stressSim.World.AddTiles(extra);
                    CreateStress(stressSim);ProfileStress(stressSim);
                });
                var timer=Stopwatch.StartNew();await ColonySaves.Save("developed-stress",stressSim.State,Catalog,directory:"Library/GameValidation");var restored=ColonySaves.Load("Library/GameValidation/developed-stress.meridian",Catalog);
                Require(JsonUtility.ToJson(restored)==JsonUtility.ToJson(stressSim.State),"Stress snapshot changed authority");StressNote("Full developed snapshot publication/read "+timer.Elapsed.TotalSeconds.ToString("F2")+"s; "+new FileInfo("Library/GameValidation/developed-stress.meridian").Length+" bytes");
                File.WriteAllText("Logs/ColonyStressValidation.txt","PASS\n"+string.Join("\n",stressEvidence));Debug.Log("Meridian developed-colony simulation stress passed.");
            }
            catch(Exception error){if(stressSim!=null)File.WriteAllText("Library/ColonyStressFailure.json",JsonUtility.ToJson(stressSim.State,true));File.WriteAllText("Logs/ColonyStressValidation.txt","FAIL\n"+string.Join("\n",stressEvidence)+"\n"+error);Debug.LogException(error);}
        }
        static void CreateStress(ColonySimulation sim)
        {
            sim.State.name="Development scale fixture";sim.State.credits=250000;sim.State.completedResearch=Catalog.research.Select(r=>r.id).ToList();sim.State.researchQueue.Clear();sim.State.visitorsOpen=false;
            foreach(var job in sim.State.jobs.ToArray())sim.Jobs.Cancel(job);sim.State.jobs.Clear();sim.State.reservations.Clear();
            foreach(var actor in sim.SurfaceActors){actor.job=null;actor.intention=null;sim.Navigation.Stop(actor);}
            var residents=new List<StructureState>();var guests=new List<StructureState>();
            for(int district=0;district<2;district++)
            {
                Vector3 origin=new Vector3(district==0?400:6100,0,100);var all=new List<StructureState>();
                var definitions=Enumerable.Repeat("habitat",13).Concat(Enumerable.Repeat("lodge",7)).Concat(Enumerable.Repeat("greenhouse",4)).Concat(new[]{"canteen","canteen","restaurant","restaurant","sanitation","clinic","lounge","lab","training","reception","attraction","warehouse","airlock","air","air","air","air","air","well","well","well","well","tank","tank","charger","charger","charger","charger","smelter","assembler","medicine","geothermal","geothermal","geothermal","geothermal","geothermal","geothermal","geothermal","geothermal","geothermal","geothermal"}).ToArray();
                for(int i=0;i<definitions.Length;i++)
                {
                    var b=sim.AddStructure(definitions[i],sim.World.Ground(origin+new Vector3(i%10*38,0,i/10*38)),0,true);all.Add(b);
                    if(b.definition=="habitat")residents.Add(b);if(b.definition=="lodge")guests.Add(b);
                    var inv=sim.Stock.Get(b.inventory);if(b.definition=="warehouse"){sim.Stock.Add(inv,Good.Food,800);sim.Stock.Add(inv,Good.Components,150);sim.Stock.Add(inv,Good.Biomass,200);sim.Stock.Add(inv,Good.Metal,200);sim.Stock.Add(inv,Good.IronOre,100);sim.Stock.Add(inv,Good.CopperOre,100);}
                    if(b.definition=="canteen"||b.definition=="restaurant")sim.Stock.Add(inv,Good.Food,150);
                    if(b.definition=="greenhouse"){sim.Stock.Add(inv,Good.Biomass,80);b.tending=sim.Day*2;}
                    if(b.definition=="clinic")sim.Stock.Add(inv,Good.Medicine,30);if(b.definition=="charger")sim.Stock.Add(inv,Good.Components,20);
                    if(b.waterInventory!=null)sim.Stock.Add(sim.Stock.Get(b.waterInventory),Good.Water,sim.Stock.Get(b.waterInventory).capacity);
                    foreach(var obj in sim.World.Nearby(b.position,sim.Definition(b).size.magnitude*.6f).ToArray()){var delta=sim.World.Change(obj.Id);delta.remaining=0;delta.removed=true;}
                    if(i>0)foreach(var kind in new[]{"cable","pipe"})StressLink(sim,kind,all[i-1],b);
                }
                var sealedBuildings=all.Where(b=>sim.Definition(b).sealedModule).ToArray();for(int i=1;i<sealedBuildings.Length;i++)StressLink(sim,"corridor",sealedBuildings[i-1],sealedBuildings[i]);
            }
            int count=0;foreach(var actor in sim.State.actors.Where(a=>a.kind==ActorKind.Colonist)){if(count>=200)break;StagePerson(sim,actor,residents,count++,false);}
            while(count<200){var actor=sim.AddPerson((Profession)(count%6),false);StagePerson(sim,actor,residents,count++,false);}
            for(int i=0;i<100;i++){var actor=sim.AddPerson(Profession.Service,true);StagePerson(sim,actor,guests,i,true);}
            int machines=sim.SurfaceActors.Count(a=>a.kind<ActorKind.Colonist);while(machines<150){int i=machines++;var p=new Vector3(i%2==0?370:6070,0,80+i/2*4);sim.AddMachine((ActorKind)(i%3),sim.World.Ground(p));}
            foreach(var center in new[]{new Vector3(600,0,-80),new Vector3(6250,0,-80)})foreach(var tree in sim.World.Nearby(center,150).Where(o=>o.Kind==SurfaceObjectKind.Tree).Take(90)){var delta=sim.World.Change(tree.Id);delta.designated=true;}
            sim.Networks.Rebuild();sim.Networks.Tick(0);sim.State.speed=1;sim.State.camera=new CameraState{pivot=new Vector3(550,0,190),yaw=-25,pitch=57,distance=380};
            Require(sim.Population==200&&sim.SurfaceActors.Count(a=>a.kind==ActorKind.Visitor)==100&&sim.SurfaceActors.Count(a=>a.kind<ActorKind.Colonist)==150,"Incorrect stress population");
            Require(sim.World.CanFrame(new Vector3(3000,0,0),500),"Camera rejects shared acquired border");sim.State.regions.Add(new RegionState{id=sim.State.Id("region"),x=0,z=1,owned=true});
            Require(!sim.World.CanFrame(new Vector3(2900,0,2900),300)&&sim.World.CanFrame(new Vector3(2600,0,2600),300),"L-shaped camera footprint crosses unowned corner");sim.State.regions.RemoveAt(sim.State.regions.Count-1);
            StressNote(sim.State.structures.Count+" structures; "+sim.World.Objects.Count()+" indexed resources; real jobs/navigation/needs/utility systems enabled; development staging and stock grants, not an opening balance test");
        }
        static void StagePerson(ColonySimulation sim,ActorState actor,List<StructureState> homes,int i,bool visitor)
        {
            var home=homes[i/8];actor.location=PersonLocation.Arrived;actor.home=actor.building=home.id;actor.position=sim.Navigation.InteriorPoint(home,actor,"sleep");actor.health=100;actor.hunger=actor.thirst=10;actor.fatigue=10+i%45;actor.hygiene=90;actor.recreation=60+i%35;actor.workplace=actor.intention=actor.target=null;actor.returnAfter=(float)sim.State.time+sim.Day*10;
            sim.Stock.Add(sim.Stock.Get(actor.inventory),Good.Food,4);sim.Stock.Add(sim.Stock.Get(actor.inventory),Good.Water,4);
        }
        static void StressLink(ColonySimulation sim,string kind,StructureState a,StructureState b)
        {var from=sim.Navigation.Door(a,b.position);var to=sim.Navigation.Door(b,a.position);var link=sim.AddStructure(kind,from,0,true);link.from=a.id;link.to=b.id;link.end=to;link.length=Vector3.Distance(from,to);}
        static void ProfileStress(ColonySimulation sim)
        {
            const int steps=600;var budget=Stopwatch.StartNew();var total=new Stopwatch();var samples=new List<double>();var costs=new double[7];var timer=new Stopwatch();Action<float>[] systems={sim.Networks.Tick,sim.Navigation.Tick,sim.Jobs.Tick,sim.People.Tick,sim.Industry.Tick,sim.Traffic.Tick,sim.Development.Tick};
            File.WriteAllText("Library/GameValidation/stress-source.json",JsonUtility.ToJson(sim.State));
            for(int i=0;i<steps;i++)
            {
                Require(budget.Elapsed.TotalSeconds<90,"Simulation profiling exceeded 90s after "+i+" ticks; costs ms: "+string.Join(", ",costs));
                total.Restart();sim.State.time+=.1f;for(int j=0;j<systems.Length;j++){if(i==0)StressNote("First fixed step: entering system "+j);timer.Restart();systems[j](.1f);costs[j]+=timer.Elapsed.TotalMilliseconds;}samples.Add(total.Elapsed.TotalMilliseconds);
                if(i%20==19)StressNote("Profiled "+(i+1)+" / "+steps+" fixed steps; latest "+samples.Last().ToString("F2")+"ms");
            }
            var sorted=samples.OrderBy(x=>x).ToArray();string[] names={"utilities","navigation","jobs","people","industry","traffic","development"};
            StressNote("0.1s fixed tick mean "+samples.Average().ToString("F2")+"ms / p95 "+sorted[(int)(steps*.95)].ToString("F2")+"ms / max "+sorted.Last().ToString("F2")+"ms; "+string.Join(", ",names.Select((n,i)=>n+" "+(costs[i]/steps).ToString("F2")+"ms")));
            Require(sim.Population==200&&sim.SurfaceActors.Count(a=>a.kind==ActorKind.Visitor)==100,"Stress population lost during measurement");ColonySaves.Validate(sim.State,Catalog);sim.State.speed=0;
        }
        public static void OpenStress()
        {Application.runInBackground=true;var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>();var transition=(ScreenTransition)new SerializedObject(menu).FindProperty("transitionPrefab").objectReferenceValue;ColonyLoadPipeline.Load("Library/GameValidation/developed-stress.meridian",transition,Debug.LogError);}
    }
}
