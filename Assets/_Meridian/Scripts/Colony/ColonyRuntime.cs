using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meridian.Colony
{
    public sealed class ColonyRuntime:MonoBehaviour
    {
        public static ColonyRuntime Current{get;private set;}public ColonySimulation Simulation{get;private set;}public ColonyVisuals Visuals{get;private set;}public ColonyUI UI{get;private set;}public SurveyCamera Camera{get;private set;}public SurfaceLandscape Landscape{get;private set;}public ScreenTransition Transition{get;private set;}public ColonyAudio Audio{get;private set;}
        public string BuildType{get;private set;}public string PlacementReason{get;private set;}public string Selected=>Visuals.Selected;public bool HarvestMode{get;private set;}public bool Cinematic{get;private set;}public float ExpansionProgress{get;private set;}public string ExpansionStatus{get;private set;}
        public bool SaveQueued=>queuedSaves.Count>0;
        public bool Saving=>pendingWrites>0||ColonySaves.Writing;
        int pendingWrites;
        ColonyEnvironment environment;
        Vector3 placement,end;string linkFrom,linkTo;float yaw,stepClock,autosaveClock,terrainClock,previewClock,arrivalClock;bool active,worldCapture;Vector2 press;float excursion;readonly Queue<string> queuedSaves=new Queue<string>();CancellationTokenSource expansionCancellation;Task expansionTask;
        public void Initialize(SurfaceSelectionController survey,ColonyState loaded,SurfaceTileData[] tiles)
        {
            Current=this;Camera=survey.Viewing;Landscape=survey.Landscape;Transition=survey.Transition;
            if(!FindAnyObjectByType<AudioListener>())Camera.Lens.gameObject.AddComponent<AudioListener>();
            var catalog=ColonyCatalog.Load();var state=loaded??ColonySimulation.Create(SetupSession.Current.Record,catalog);Simulation=new ColonySimulation(state,survey.World,catalog);if(tiles!=null)Simulation.World.AddTiles(tiles);
            Landscape.Register(Simulation.World.Tiles);Landscape.SetRemovalQuery(Simulation.World.Removed,false);Simulation.ResourceRemoved+=ResourceRemoved;
            Visuals=gameObject.AddComponent<ColonyVisuals>();Visuals.Initialize(Simulation);Audio=gameObject.AddComponent<ColonyAudio>();Audio.Initialize(Simulation);
            UI=gameObject.AddComponent<ColonyUI>();UI.Initialize(this,Transition);
            environment=gameObject.AddComponent<ColonyEnvironment>();environment.Initialize(this);
            Camera.ConfigureColony(Simulation.World.Bounds,Simulation.World.Height,UI.OverUI,()=>UI.TextFocused||UI.Modal||ColonyLoadPipeline.Active,Simulation.World.CanFrame,Simulation.World.NearestFrame);Camera.Focus(state.setup.landing.Position);
            Camera.SetHome(state.setup.landing.Position);
            if(loaded!=null){Simulation.Networks.Tick(0);Camera.Restore(state.camera);UI.Toast("Colony restored and paused. Press Space to resume.");}
            else{Cinematic=true;Visuals.BeginArrival();Camera.enabled=false;UI.Open("Guide");}
        }
        public void SetInteraction(bool value){active=value;worldCapture=false;Camera.SetInteraction(value&&!Cinematic);}
        void ResourceRemoved(string id){Landscape.ResourceChanged(Simulation.World.Object(id));}
        public void FinishArrival()
        {
            if(!Cinematic)return;Simulation.FinalizeArrival();Cinematic=false;Visuals.FinishArrival();environment.FinishArrival();Camera.enabled=true;Camera.Restore(new CameraState{pivot=Simulation.State.setup.landing.Position,yaw=-25,pitch=57,distance=245});Camera.SetInteraction(active);Visuals.Sync();UI.Toast("Expedition deployed: 12 machines. All 48 colonists remain in orbit.");Save("autosave-0");
            while(queuedSaves.Count>0)Save(queuedSaves.Dequeue());
        }
        public void TogglePause(){var s=Simulation.State;if(s.speed>0){s.previousSpeed=s.speed;s.speed=0;}else s.speed=s.previousSpeed>0?s.previousSpeed:1;}
        public void Speed(float value){Simulation.State.speed=value;if(value>0)Simulation.State.previousSpeed=value;}
        public void BeginBuild(string definition){BuildType=definition;HarvestMode=false;linkFrom=linkTo=null;PlacementReason=Simulation.Catalog.Building(definition).link?"Choose the first connection port":"Choose clear, level owned ground";worldCapture=false;}
        public void BeginHarvest(){CancelTool();HarvestMode=true;UI.Toast("Click a resource, or drag a rectangle across trees and deposits.");}
        public bool CancelTool(){bool had=BuildType!=null||HarvestMode;BuildType=null;HarvestMode=false;linkFrom=linkTo=null;Visuals.Preview(null,default,0,default,false);worldCapture=false;return had;}
        public void Select(string id){Visuals.Selected=id;if(id!=null)UI.Inspect(id);}
        public void Focus(string id){if(id!=null)Camera.Focus(Simulation.Position(id));}
        void Update()
        {
            if(Simulation==null||UI==null)return;Camera.Sensitivity=ColonySettings.Current.cameraSensitivity;Camera.ZoomSensitivity=ColonySettings.Current.zoomSensitivity;Camera.TiltSensitivity=ColonySettings.Current.tiltSensitivity;Camera.EdgePan=ColonySettings.Current.edgePan;
            if(Cinematic)
            {
                if(!active||ScreenTransition.Active)return;arrivalClock+=Time.unscaledDeltaTime;float t=arrivalClock/12;Visuals.Arrival(t);environment.Arrival(t);
                if(Keyboard.current?.f5Key.wasPressedThisFrame==true)Save("quicksave");
                var heading=Quaternion.Euler(0,Simulation.State.setup.landing.yaw+35,0);Vector3 focus=Visuals.ArrivalPosition+Vector3.up*6;Vector3 at=focus+heading*new Vector3(60,Mathf.Lerp(42,24,t),-75);Camera.Lens.transform.SetPositionAndRotation(at,Quaternion.LookRotation(focus-at));
                if(t>=1||Keyboard.current?.escapeKey.wasPressedThisFrame==true)FinishArrival();return;
            }
            if(!active||ScreenTransition.Active||ColonyLoadPipeline.Active)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard!=null&&!UI.TextFocused)
            {
                if(keyboard.escapeKey.wasPressedThisFrame){if(!UI.DismissModal()&&!CancelTool())UI.Escape();}
                if(!UI.Modal)
                {
                    if(keyboard.spaceKey.wasPressedThisFrame)TogglePause();if(keyboard.digit1Key.wasPressedThisFrame)Speed(1);if(keyboard.digit2Key.wasPressedThisFrame)Speed(2);if(keyboard.digit3Key.wasPressedThisFrame)Speed(4);
                    if(keyboard.bKey.wasPressedThisFrame)UI.Open("Build");if(keyboard.hKey.wasPressedThisFrame)BeginHarvest();if(keyboard.fKey.wasPressedThisFrame)Focus(Selected);
                    if(keyboard.f5Key.wasPressedThisFrame)Save("quicksave");if(keyboard.f9Key.wasPressedThisFrame)UI.Quickload();
                    if(keyboard.rKey.wasPressedThisFrame&&BuildType!=null)yaw+=keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed?-45:45;
                }
            }
            if(mouse!=null&&!UI.Modal&&!UI.TextFocused)Input(mouse);
            if(Simulation.State.speed>0)
            {
                stepClock+=Mathf.Min(.15f,Time.unscaledDeltaTime)*Simulation.State.speed;int steps=0;float fixedStep=Simulation.Catalog.balance.step;
                try{while(stepClock>=fixedStep&&steps++<8){Simulation.Tick(fixedStep);stepClock-=fixedStep;}}
                catch(Exception error){Simulation.State.speed=0;stepClock=0;UI.Toast("Simulation paused after an error. Load a previous save or return to the menu. See the player log for details.");Debug.LogException(error);}
                autosaveClock+=Time.unscaledDeltaTime;if(autosaveClock>=Simulation.Catalog.balance.autosaveSeconds){autosaveClock=0;Simulation.State.autosaveIndex=(Simulation.State.autosaveIndex+1)%Simulation.Catalog.balance.autosaves;Save("autosave-"+Simulation.State.autosaveIndex);}
            }
            terrainClock-=Time.unscaledDeltaTime;if(terrainClock<=0){Landscape.Maintain(Camera.Pivot);terrainClock=.4f;}
        }
        void Input(Mouse mouse)
        {
            Vector2 pointer=mouse.position.ReadValue();bool over=UI.OverUI(pointer);var ray=Camera.Lens.ScreenPointToRay(pointer);
            if(mouse.rightButton.wasPressedThisFrame){CancelTool();return;}
            if(BuildType!=null&&!over&&!Camera.IsPanning&&Time.unscaledTime>=previewClock)
            {
                previewClock=Time.unscaledTime+.08f;var d=Simulation.Catalog.Building(BuildType);
                if(d.link)
                {
                    string hit=Visuals.Pick(ray,Landscape,out var ground);var to=Simulation.Structure(hit);var from=Simulation.Structure(linkFrom);linkTo=to?.id;
                    placement=from!=null?Simulation.Navigation.Door(from,to?.position??ground):ground;end=to!=null?Simulation.Navigation.Door(to,from?.position??ground):ground;
                    PlacementReason=from==null?"Choose a building's connection port":ColonyCommands.ValidatePlacement(Simulation,BuildType,placement,0,end,linkFrom,linkTo);
                }
                else if(Landscape.Pick(ray,out var hit)){placement=hit.point;PlacementReason=ColonyCommands.ValidatePlacement(Simulation,BuildType,placement,yaw,default);}
                else PlacementReason="Point at owned terrain";
                Visuals.Preview(BuildType,placement,yaw,end,PlacementReason==null);
            }
            if(mouse.leftButton.wasPressedThisFrame){worldCapture=!over&&!Camera.IsPanning;press=pointer;excursion=0;}
            if(worldCapture)excursion=Mathf.Max(excursion,(pointer-press).magnitude);
            if(!mouse.leftButton.wasReleasedThisFrame)return;bool accept=worldCapture;worldCapture=false;if(!accept)return;
            if(HarvestMode)
            {
                if(excursion>7){var rect=Rect.MinMaxRect(Mathf.Min(press.x,pointer.x),Mathf.Min(press.y,pointer.y),Mathf.Max(press.x,pointer.x),Mathf.Max(press.y,pointer.y));foreach(var obj in Simulation.World.Objects){var screen=Camera.Lens.WorldToScreenPoint(obj.Position);if(screen.z>0&&rect.Contains(screen)&&Simulation.World.Owned(obj.Position)&&!Simulation.World.Removed(obj.Id))Simulation.World.Change(obj.Id).designated=true;}}
                else if(Landscape.Pick(ray,out var hit))ColonyCommands.Designate(Simulation,hit.point,14);return;
            }
            if(excursion>7*Mathf.Max(.5f,Screen.height/1080f))return;
            if(BuildType!=null)
            {
                if(Simulation.Catalog.Building(BuildType).link&&linkFrom==null){linkFrom=Simulation.Structure(Visuals.Pick(ray,Landscape,out _))?.id;return;}
                if(PlacementReason!=null){UI.Toast(PlacementReason);return;}
                try{var b=ColonyCommands.Place(Simulation,BuildType,placement,yaw,end,linkFrom,linkTo);Audio.Click();Select(b.id);Visuals.Sync();if(Simulation.Catalog.Building(BuildType).link)linkFrom=linkTo;}
                catch(Exception error){UI.Toast(error.Message);}return;
            }
            string selected=Visuals.Pick(ray,Landscape,out _);
            if(selected==null&&Landscape.Pick(ray,out var terrainHit))
            {
                var point=terrainHit.point;
                selected=Simulation.State.regions.Find(r=>r.owned&&Mathf.Abs(point.x-r.x*6000)<=3000&&Mathf.Abs(point.z-r.z*6000)<=3000)?.id;
            }
            Select(selected);
        }
        public Rect? HarvestRectangle
        {get{if(!worldCapture||!HarvestMode||Mouse.current==null)return null;var point=Mouse.current.position.ReadValue();return Rect.MinMaxRect(Mathf.Min(press.x,point.x),Mathf.Min(press.y,point.y),Mathf.Max(press.x,point.x),Mathf.Max(press.y,point.y));}}
        public void Save(string slot)
        {
            if(Cinematic){if(!queuedSaves.Contains(slot))queuedSaves.Enqueue(slot);UI.Toast("Save queued for the completed landing checkpoint.");return;}pendingWrites++;StartCoroutine(WriteSave(slot));
        }
        public void SaveThenLeave(Action after)=>StartCoroutine(SaveBeforeLeaving(after));
        IEnumerator SaveBeforeLeaving(Action after)
        {
            yield return null; // Camera preview must render outside an IMGUI/render-pipeline callback.
            Task task;try{Simulation.State.camera=Camera.Capture();UI.CaptureLayout();task=ColonySaves.Save("quicksave",Simulation.State,Simulation.Catalog,PreviewImage());}
            catch(Exception error){UI.ExitSaveFailed(error.Message);yield break;}
            while(!task.IsCompleted)yield return null;
            if(task.IsFaulted){UI.ExitSaveFailed(task.Exception.GetBaseException().Message);yield break;}
            UI.CompleteExit();after?.Invoke();
        }
        IEnumerator WriteSave(string slot)
        {
            try
            {
            yield return null;
            Task task=null;try{Simulation.State.camera=Camera.Capture();UI.CaptureLayout();task=ColonySaves.Save(slot,Simulation.State,Simulation.Catalog,PreviewImage());}catch(Exception error){UI.Toast("Save failed: "+error.Message);yield break;}
            while(!task.IsCompleted)yield return null;UI.Toast(task.IsFaulted?"Save failed: "+task.Exception.GetBaseException().Message:"Saved · "+slot);UI.RefreshSaves();
            }
            finally{pendingWrites--;}
        }
        byte[] PreviewImage()
        {
            var target=RenderTexture.GetTemporary(320,180,16);var previous=Camera.Lens.targetTexture;var activeRT=RenderTexture.active;Texture2D image=null;
            try{Camera.Lens.targetTexture=target;Camera.Lens.Render();RenderTexture.active=target;image=new Texture2D(320,180,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,320,180),0,0);image.Apply();return image.EncodeToPNG();}
            finally{Camera.Lens.targetTexture=previous;RenderTexture.active=activeRT;RenderTexture.ReleaseTemporary(target);if(image)Destroy(image);}
        }
        public void Expand(int x,int z){if(expansionTask!=null&&!expansionTask.IsCompleted)return;StartCoroutine(Expansion(x,z));}
        IEnumerator Expansion(int x,int z)
        {
            RegionOperation operation;try{operation=Simulation.Development.BeginRegion(x,z);}catch(Exception error){UI.Toast(error.Message);yield break;}
            expansionCancellation=new CancellationTokenSource();var token=expansionCancellation.Token;var world=Simulation.World.Surface;int completed=0;
            var task=Task.Run(()=>{var tiles=new SurfaceTileData[36];Parallel.For(0,36,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},i=>{tiles[i]=world.GenerateTile(new Vector2Int(x*6-3+i%6,z*6-3+i/6),token);Interlocked.Increment(ref completed);});return tiles;},token);expansionTask=task;
            while(!task.IsCompleted){ExpansionProgress=Volatile.Read(ref completed)/36f;ExpansionStatus="Surveying adjacent land · "+Mathf.RoundToInt(ExpansionProgress*100)+"%";yield return null;}
            if(task.IsCanceled||token.IsCancellationRequested){operation.active=false;ExpansionStatus="Acquisition cancelled; no credits charged";yield break;}
            if(task.IsFaulted){operation.active=false;ExpansionStatus="Survey failed: "+task.Exception.GetBaseException().Message;UI.Toast(ExpansionStatus);yield break;}
            try{Simulation.Development.CompleteRegion(operation,task.Result);Landscape.Register(task.Result);Camera.SetBounds(Simulation.World.Bounds);ExpansionStatus="Region acquired · 36 km²";UI.Toast(ExpansionStatus);}catch(Exception error){operation.active=false;UI.Toast(error.Message);}
        }
        public void CancelExpansion(){expansionCancellation?.Cancel();}
        public void ReturnToMenu(){if(Saving){UI.Toast("Wait for the current save to finish");return;}SetInteraction(false);ScreenTransition.Travel(Transition,"MainMenu","");}
        void OnApplicationFocus(bool focused){if(!focused)worldCapture=false;}
        void OnDestroy(){expansionCancellation?.Cancel();expansionCancellation?.Dispose();if(Simulation!=null)Simulation.ResourceRemoved-=ResourceRemoved;if(Current==this)Current=null;}
    }
}
