using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Meridian.Colony;

namespace Meridian.Editor
{
    public static partial class ColonyValidation
    {
        static readonly List<string> recoveryEvidence=new List<string>();
        static void RecoveryNote(string value){recoveryEvidence.Add(value);File.WriteAllText("Logs/ColonyRecoveryValidation.txt","RUNNING\n"+string.Join("\n",recoveryEvidence));}
        public static async void Recovery()
        {
            recoveryEvidence.Clear();RecoveryNote("Checking retained terrain mask, saved gear servicing, retraining and machine rescue");
            try
            {
                Catalog=ColonyCatalog.Load();var source=ColonySaves.Load("Library/GameValidation/connected-colony.meridian",Catalog);string snapshot=JsonUtility.ToJson(source);
                if(ColonyRuntime.Current){World=ColonyRuntime.Current.Simulation.World.Surface;Planet=SetupSession.Current.Planet;}
                await Task.Run(()=>
                {
                    if(World==null){Planet=PlanetGenerator.Generate(source.setup.seed,source.setup.planetParameters);Planet.ReleaseAppearanceBuffers();World=SurfaceGenerator.Generate(Planet,source.setup.region.localDirection,source.setup.surfaceParameters,default,null,0,source.setup);}
                    var sim=RestoreScenario(snapshot);
                    for(int i=0;i<1600;i++){float x=-2999+(i%40)*149.8f,z=-2999+(i/40)*149.8f;Require(sim.World.Dry(new Vector3(x,0,z))==World.Sample(x,z).IsLand,"Retained water mask disagrees with geographic sample");}
                    RecoveryNote("1,600 navigation water-mask samples match shared geography");
                    var builder=sim.SurfaceActors.First(a=>a.kind==ActorKind.Colonist&&a.profession==Profession.Builder);var airlock=sim.State.structures.First(b=>b.definition=="airlock");var ship=sim.State.structures.First(b=>b.definition=="ship");var site=FreeSite(sim,"habitat");
                    var gear=sim.Stock.Get(builder.equipment);foreach(var good in new[]{Good.EVASuit,Good.Toolkit})if(sim.Stock.Count(gear,good)<1)sim.Stock.Transfer(sim.Stock.Get(ship.inventory),gear,good,1);
                    foreach(var item in gear.items)item.condition=.1f;builder.position=airlock.position;builder.building=airlock.id;builder.eva=builder.returning=false;sim.Navigation.Stop(builder);sim.Stock.Add(sim.Stock.Get(airlock.inventory),Good.Components,2);sim.Networks.Tick(0);sim.State.environment.storm=0;
                    float components=Total(sim,Good.Components);for(int i=0;i<22;i++)sim.People.PrepareBuilder(builder,site,.1f);Require(builder.gearRepair,"Worn gear did not enter servicing");
                    string who=builder.id,where=site.id;sim=RoundTrip(sim,"paid EVA gear servicing");builder=sim.Actor(who);site=sim.Structure(where);
                    for(int i=0;i<65&&!builder.eva;i++)sim.People.PrepareBuilder(builder,site,.1f);
                    Require(builder.eva&&sim.Stock.Get(builder.equipment).items.All(i=>i.condition>.99f)&&Mathf.Abs(components-Total(sim,Good.Components)-1)<.001f,"Gear service lost progress or charged twice");RecoveryNote("Worn suit/toolkit consumes one component; servicing save resumes and exits safely");
                    sim=RestoreScenario(snapshot);ship=sim.State.structures.First(b=>b.definition=="ship");var school=sim.AddStructure("training",sim.World.Ground(ship.position+new Vector3(-32,0,35)),0,true);
                    foreach(var kind in new[]{"corridor","cable","pipe"})StressLink(sim,kind,ship,school);school.air=school.airCapacity;sim.Networks.Rebuild();sim.Networks.Tick(0);
                    var teacher=sim.SurfaceActors.First(a=>a.kind==ActorKind.Colonist&&a.profession==Profession.Technician);teacher.workplace=teacher.building=school.id;teacher.intention="work";teacher.target=school.id;teacher.position=sim.Navigation.InteriorPoint(school,teacher,"work");sim.Navigation.Stop(teacher);school.staffed=1;
                    builder=sim.SurfaceActors.First(a=>a.kind==ActorKind.Colonist&&a.profession==Profession.Builder);builder.intention=null;builder.job=null;Require(sim.People.ChangeProfession(builder,Profession.Scientist),"Training enrolment refused");who=builder.id;
                    Until(sim,()=>builder.trainingProgress>15,sim.Day*3,"Real training attendance");float earned=builder.trainingProgress;builder.fatigue=90;builder.intention=null;sim.Navigation.Stop(builder);sim=RoundTrip(sim,"interrupted profession training");builder=sim.Actor(who);Require(builder.trainingProgress==earned,"Training progress reset on load");
                    Until(sim,()=>!builder.training,sim.Day*12,"Training resumes after sleep and meals");Require(builder.profession==Profession.Scientist&&builder.health>65,"Training failed to change profession safely");RecoveryNote("Actual technician-led training survives needs interruption and save; new profession applied");
                    sim=RestoreScenario(snapshot);foreach(var greenhouse in sim.State.structures.Where(b=>b.definition=="greenhouse"))greenhouse.paused=true; // Isolate rescue conservation from legitimate biomass consumption.
                    var machine=sim.SurfaceActors.First(a=>a.kind==ActorKind.ForestryBot);machine.charge=0;machine.intention=null;machine.position=sim.World.Ground(ship.position+new Vector3(-65,0,-45));sim.Navigation.Stop(machine);who=machine.id;float biomass=Total(sim,Good.Biomass);sim.Stock.Add(sim.Stock.Get(machine.inventory),Good.Biomass,5);
                    Until(sim,()=>machine.charge>50,sim.Day*3,"Disabled forestry bot towed and recharged");Require(Total(sim,Good.Biomass)>=biomass+4.9f,"Rescue lost inventory");RecoveryNote("Real recovery job tows a disabled machine to powered charging; cargo preserved");
                    var invalid=JsonUtility.FromJson<ColonyState>(snapshot);invalid.actors[0].health=float.NaN;bool rejected=false;try{ColonySaves.Validate(invalid,Catalog);}catch{rejected=true;}Require(rejected,"Non-finite state accepted");invalid=JsonUtility.FromJson<ColonyState>(snapshot);invalid.inventories[0].items=null;rejected=false;try{ColonySaves.Validate(invalid,Catalog);}catch{rejected=true;}Require(rejected,"Missing nested inventory accepted");RecoveryNote("Non-finite values and missing nested inventory tables rejected");
                });
                File.WriteAllText("Logs/ColonyRecoveryValidation.txt","PASS\n"+string.Join("\n",recoveryEvidence));
            }
            catch(Exception error){if(systemSim!=null)File.WriteAllText("Library/ColonyRecoveryFailure.json",JsonUtility.ToJson(systemSim.State,true));File.WriteAllText("Logs/ColonyRecoveryValidation.txt","FAIL\n"+string.Join("\n",recoveryEvidence)+"\n"+error);Debug.LogException(error);}
        }
        [MenuItem("Meridian/Colony/Build Windows Player")]
        public static void BuildPlayer()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first");ColonyBuildSupport.Validate();AssetDatabase.SaveAssets();Directory.CreateDirectory("Builds/Windows");File.WriteAllText("Logs/ColonyBuild.txt","RUNNING");
            EditorApplication.delayCall+=()=>{try{var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/Windows/Meridian.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});File.WriteAllText("Logs/ColonyBuild.txt",report.summary.result+"\n"+report.summary.totalErrors+" errors / "+report.summary.totalWarnings+" warnings\n"+report.summary.totalTime.TotalSeconds.ToString("F1")+" seconds / "+report.summary.totalSize+" bytes");}catch(Exception error){File.WriteAllText("Logs/ColonyBuild.txt","FAIL\n"+error);Debug.LogException(error);}};
        }
    }
}
