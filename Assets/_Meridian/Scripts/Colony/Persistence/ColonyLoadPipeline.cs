using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>Prepare and validate the replacement off-thread while the current session remains intact.</summary>
    public sealed class ColonyLoadPipeline:MonoBehaviour
    {
        public static ColonyLoadPipeline Active{get;private set;}public static ColonyState PendingState{get;private set;}public static SurfaceTileData[] PendingTiles{get;private set;}
        public string Message{get;private set;}="Validating save";public float Progress{get;private set;}
        CancellationTokenSource cancellation;Action<string> failure;ScreenTransition transition;ColonyState state;float priorSpeed;
        public static void Load(string path,ScreenTransition transition,Action<string> failed)
        {
            if(Active||ScreenTransition.Active)return;ColonyState data;
            try{data=ColonySaves.Load(path,ColonyCatalog.Load());}catch(Exception error){failed?.Invoke(error.Message);return;}
            var runner=new GameObject("Colony reconstruction").AddComponent<ColonyLoadPipeline>();Active=runner;DontDestroyOnLoad(runner.gameObject);runner.transition=transition;runner.failure=failed;runner.state=data;
            if(ColonyRuntime.Current){runner.priorSpeed=ColonyRuntime.Current.Simulation.State.speed;ColonyRuntime.Current.Simulation.State.speed=0;}
            runner.StartCoroutine(runner.Prepare());
        }
        IEnumerator Prepare()
        {
            cancellation=new CancellationTokenSource();var token=cancellation.Token;var progress=new SurfaceLoadProgress();var watch=System.Diagnostics.Stopwatch.StartNew();
            var task=Task.Run(()=>
            {
                var planet=PlanetGenerator.Generate(state.setup.seed,state.setup.planetParameters,token);planet.ReleaseAppearanceBuffers();progress.Report(.01f,"Restoring recorded surface");
                var surface=SurfaceGenerator.Generate(planet,state.setup.region.localDirection,state.setup.surfaceParameters,token,progress,0,state.setup);
                var tiles=new List<SurfaceTileData>();var regions=state.regions.Where(r=>r.owned&&(r.x!=0||r.z!=0)).ToArray();int done=0;
                foreach(var region in regions)
                {
                    var restored=new SurfaceTileData[36];int completed=0;
                    Parallel.For(0,36,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},i=>{restored[i]=surface.GenerateTile(new Vector2Int(region.x*6-3+i%6,region.z*6-3+i/6),token);int count=Interlocked.Increment(ref completed);progress.Report(.75f+.23f*(done+count/36f)/Math.Max(1,regions.Length),"Restoring region "+(done+1)+" / "+regions.Length+" · "+count+" / 36 tiles");});
                    tiles.AddRange(restored);done++;
                }
                return(planet,surface,tiles:tiles.ToArray());
            },token);
            _=task.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            double deadline=180+90*state.regions.Count(r=>r.owned);
            while(!task.IsCompleted)
            {
                if(token.IsCancellationRequested){Fail("Load cancelled; the current colony was preserved");yield break;}
                if(watch.Elapsed.TotalSeconds>deadline){cancellation.Cancel();Fail("Colony reconstruction timed out. The current colony was preserved; try the previous backup.");yield break;}
                Message=progress.Current.Detail;Progress=progress.Current.Fraction;yield return null;
            }
            if(task.IsCanceled||token.IsCancellationRequested){Fail("Load cancelled; the current colony was preserved");yield break;}
            if(task.IsFaulted){Fail(task.Exception.GetBaseException().Message);yield break;}
            try
            {
                // Validate resource references against the reproduced world before discarding any scene/session.
                var verify=new ColonyWorld(task.Result.surface,state);verify.AddTiles(task.Result.tiles);
                foreach(var resource in state.resources)if(verify.Object(resource.id)==null)throw new InvalidOperationException("Saved resource identity could not be reproduced: "+resource.id);
                foreach(var b in state.structures)if(b.deposit!=null&&verify.Object(b.deposit)==null)throw new InvalidOperationException("Saved deposit could not be reproduced");
                state.previousSpeed=state.speed>0?state.speed:state.previousSpeed;state.speed=0;state.expansion.active=false;
                foreach(var a in state.actors){a.path.Clear();a.pathRooms.Clear();a.pathIndex=0;}
                PendingState=state;PendingTiles=task.Result.tiles;SetupSession.Ensure().Restore(state.setup,task.Result.planet,task.Result.surface);
                Message="Opening colony";Progress=1;ScreenTransition.Travel(transition,"LandingSiteSelection","Restoring your colony...");
                Debug.Log("Meridian save reconstructed in "+watch.Elapsed.TotalSeconds.ToString("F2")+"s; "+state.actors.Count+" people/machines, "+state.structures.Count+" structures");Active=null;Destroy(gameObject);
            }
            catch(Exception error){Fail(error.Message);}
        }
        public static ColonyState TakeState(out SurfaceTileData[] tiles){var result=PendingState;tiles=PendingTiles;PendingState=null;PendingTiles=null;return result;}
        public static void ClearPending(){PendingState=null;PendingTiles=null;}
        public void Cancel(){cancellation?.Cancel();Message="Cancelling reconstruction...";}
        void Fail(string message){if(ColonyRuntime.Current)ColonyRuntime.Current.Simulation.State.speed=priorSpeed;failure?.Invoke(message);Active=null;Destroy(gameObject);}
        void OnDestroy(){cancellation?.Cancel();cancellation?.Dispose();if(Active==this)Active=null;}
        void OnGUI()
        {
            GUI.depth=-20000;GUI.Box(new Rect(0,0,Screen.width,Screen.height),GUIContent.none);var rect=new Rect(Screen.width*.5f-250,Screen.height*.5f-90,500,180);GUI.Box(rect,"RESTORING COLONY");GUI.Label(new Rect(rect.x+25,rect.y+45,450,60),Message+"  "+Mathf.FloorToInt(Progress*100)+"%");if(GUI.Button(new Rect(rect.x+150,rect.y+125,200,34),"Cancel load"))Cancel();
        }
    }
}
