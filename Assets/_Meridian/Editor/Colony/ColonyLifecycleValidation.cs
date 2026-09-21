using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Meridian.Colony;

namespace Meridian.Editor
{
    public static partial class ColonyValidation
    {
        static readonly List<string> lifecycleEvidence=new List<string>();
        static void LifeNote(string value){lifecycleEvidence.Add(value);File.WriteAllText("Logs/ColonyLifecycleValidation.txt","RUNNING\n"+string.Join("\n",lifecycleEvidence));}
        static async Task AwaitState(Func<bool> ready,int seconds,string reason){var watch=System.Diagnostics.Stopwatch.StartNew();while(!ready()){Require(watch.Elapsed.TotalSeconds<seconds,reason+" timed out");await Task.Delay(100);}}
        static void SetPrivate(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
        public static async void Lifecycle()
        {
            lifecycleEvidence.Clear();LifeNote("Actual saved-scene flow, arrival variants, queued checkpoint and sequential cleanup");
            var settings=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset");bool previous=settings.overrideSeed;int seed=settings.developmentSeed;
            try
            {
                Application.runInBackground=true;await SizeGame(1920,1080);var current=ColonyRuntime.Current;
                if(current){await AwaitState(()=>!current.Saving,20,"Pending saves");current.ReturnToMenu();await AwaitState(()=>UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>()&&!ScreenTransition.Active,30,"Return to menu");}
                settings.overrideSeed=true;settings.developmentSeed=73129;var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>();menu.OpenPlanetSelection();menu.OpenPlanetSelection();
                await AwaitState(()=>UnityEngine.Object.FindAnyObjectByType<PlanetSelectionController>()?.IsReady==true&&!ScreenTransition.Active,180,"Planet reveal");settings.overrideSeed=previous;settings.developmentSeed=seed;
                var planet=UnityEngine.Object.FindAnyObjectByType<PlanetSelectionController>();var fixture=ColonySaves.Load("Library/GameValidation/connected-colony.meridian",ColonyCatalog.Load());planet.Viewing.CenterOn(fixture.setup.region.localDirection);await Task.Delay(1500);
                var camera=planet.Viewing.ViewingCamera;Require(planet.Select(camera.WorldToScreenPoint(planet.Globe.transform.position)),"Globe land pick rejected");Require(planet.Selection!=null&&planet.Data.Sample(planet.Selection.localDirection).IsLand,"No selected land");
                ScreenCapture.CaptureScreenshot("Captures/Colony-Setup-Planet.png");planet.Continue();await AwaitState(()=>UnityEngine.Object.FindAnyObjectByType<SurfaceSelectionController>()?.IsReady==true&&!ScreenTransition.Active,240,"Landing survey reveal");
                var survey=UnityEngine.Object.FindAnyObjectByType<SurfaceSelectionController>();World=survey.World;Planet=SetupSession.Current.Planet;Catalog=ColonyCatalog.Load();string setup=JsonUtility.ToJson(SetupSession.Current.Record);
                for(int variant=0;variant<3;variant++)
                {
                    if(variant>0)
                    {
                        var game=ColonyRuntime.Current;await AwaitState(()=>!game.Saving,20,"Checkpoint write");game.ReturnToMenu();await AwaitState(()=>UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>()&&!ScreenTransition.Active,30,"Sequential menu");Require(!ColonyRuntime.Current&&UnityEngine.Object.FindObjectsByType<Terrain>().Length==0,"Colony resources survived menu exit");
                        var record=JsonUtility.FromJson<ColonySetupRecord>(setup);record.landingConfirmed=false;SetupSession.Ensure().Restore(record,Planet,World);menu=UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>();var transition=(ScreenTransition)new SerializedObject(menu).FindProperty("transitionPrefab").objectReferenceValue;ScreenTransition.Travel(transition,"LandingSiteSelection","Loading Landing Site...");
                        await AwaitState(()=>UnityEngine.Object.FindAnyObjectByType<SurfaceSelectionController>()?.IsReady==true&&!ScreenTransition.Active,120,"Cached survey reveal");survey=UnityEngine.Object.FindAnyObjectByType<SurfaceSelectionController>();
                    }
                    var landing=World.DefaultLanding.Copy();SetPrivate(survey,"previewPosition",new Vector2(landing.Position.x,landing.Position.z));SetPrivate(survey,"heading",landing.yaw);SetPrivate(survey,"locked",true);SetPrivate(survey,"hasPosition",true);survey.ConfirmLanding();var runtime=ColonyRuntime.Current;Require(runtime&&runtime.Cinematic,"Land Here failed to start arrival");
                    if(variant==0){await Task.Delay(3600);ScreenCapture.CaptureScreenshot("Captures/Colony-Arrival.png");}
                    else{runtime.Save("arrival-check");runtime.Save("arrival-check");await Task.Delay(variant==1?500:6000);runtime.FinishArrival();runtime.FinishArrival();}
                    await AwaitState(()=>!runtime.Cinematic,20,"Arrival completion");runtime.Speed(0);var sim=runtime.Simulation;Require(sim.Population==0&&sim.SurfaceActors.Count()==12&&sim.State.actors.Count(a=>a.location==PersonLocation.Sleeping)==48,"Arrival duplicated passengers or machines");
                    foreach(var amount in Catalog.balance.manifest)Require(Mathf.Abs(sim.State.inventories.Sum(i=>sim.Stock.Count(i,amount.good))-amount.quantity)<.05f,"Arrival cargo differs: "+amount.good);
                    Require(sim.State.credits==20000&&sim.State.structures.Count(b=>b.definition=="ship")==1&&sim.State.structures.Count(b=>b.definition=="apron")==1,"Starter installation duplicated");
                    await AwaitState(()=>!runtime.Saving,30,"Arrival save");if(variant>0){var saved=ColonySaves.List(Catalog).First(s=>s.slot=="arrival-check"&&!s.backup);var restored=ColonySaves.Load(saved.path,Catalog);Require(restored.deployed&&restored.actors.Count(a=>a.kind<ActorKind.Colonist)==12,"Queued cinematic save has invalid deployment");}
                    LifeNote((variant==0?"Watched 12-second descent":variant==1?"Skipped at 0.5 seconds":"Skipped at 6 seconds")+": exact manifest, 12 machines, 48 orbital sleepers, one ship/apron; checkpoint saved");
                }
                var active=ColonyRuntime.Current;var before=active.Simulation.State.worldId;float speed=active.Simulation.State.speed;string failure=null;ColonyLoadPipeline.Load("Library/GameValidation/connected-colony.meridian",active.Transition,error=>failure=error);await Task.Delay(300);ColonyLoadPipeline.Active.Cancel();await AwaitState(()=>!ColonyLoadPipeline.Active,10,"Cancelled load");Require(ColonyRuntime.Current==active&&active.Simulation.State.worldId==before&&active.Simulation.State.speed==speed,"Cancelled load replaced current colony");LifeNote("Cancelled reconstruction preserves active colony, speed and authority");
                active.UI.Open("Guide");ScreenCapture.CaptureScreenshot("Captures/Colony-Landed.png");File.WriteAllText("Logs/ColonyLifecycleValidation.txt","PASS\n"+string.Join("\n",lifecycleEvidence));
            }
            catch(Exception error){File.WriteAllText("Logs/ColonyLifecycleValidation.txt","FAIL\n"+string.Join("\n",lifecycleEvidence)+"\n"+error);Debug.LogException(error);}
            finally{settings.overrideSeed=previous;settings.developmentSeed=seed;}
        }
    }
}
