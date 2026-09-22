using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Meridian.Colony;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Debug=UnityEngine.Debug;

namespace Meridian.Editor
{
    // Editor event harness uses the actual runtime UI component. Native pointer automation is
    // unavailable on the test host; this isolates GUI gesture logic from OS event delivery.
    public sealed class ColonyGUIHarness:EditorWindow
    {
        public ColonyUI target;public float scale=1;void OnGUI(){if(target){scale=(float)typeof(ColonyUI).GetProperty("Scale",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(target);target.SendMessage("OnGUI");}}
    }
    public static partial class ColonyValidation
    {
        static ColonyGUIHarness guiHarness;
        static void OpenGUIHarness(ColonyUI target){guiHarness=ScriptableObject.CreateInstance<ColonyGUIHarness>();guiHarness.target=target;guiHarness.titleContent=new GUIContent("Meridian UI validation");guiHarness.ShowUtility();guiHarness.position=new Rect(40,40,1280,720);target.enabled=false;}
        static void CloseGUIHarness(){if(guiHarness){if(guiHarness.target)guiHarness.target.enabled=true;guiHarness.Close();}guiHarness=null;}
        static readonly List<string> playEvidence=new List<string>();static Mouse uiMouse;static Keyboard uiKeyboard;
        static void PlayNote(string text){playEvidence.Add(text);File.WriteAllText("Logs/ColonyPlayValidation.txt","RUNNING\n"+string.Join("\n",playEvidence));}
        static async Task UIEvent(EventType type,Vector2 point,Vector2 delta=default,int button=0)
        {
            ColonyRuntime.Current?.Camera.SendMessage("OnApplicationFocus",true);
            float scale=Mathf.Min(Screen.width/1920f,Screen.height/1080f)*ColonySettings.Current.uiScale;var pixel=point*scale;
            InputSystem.QueueStateEvent(uiMouse,new MouseState{position=new Vector2(pixel.x,Screen.height-pixel.y),buttons=(ushort)(type==EventType.MouseDown||type==EventType.MouseDrag?1<<button:0)});
            // Supported Editor input queue: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/editorguiutility/queuegameviewinputevent
            if(guiHarness){guiHarness.SendEvent(new Event{type=type,mousePosition=point*guiHarness.scale,delta=delta*guiHarness.scale,button=button});guiHarness.Repaint();await Task.Delay(100);return;}
            var viewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            float gameScale=(float)viewType.GetProperty("gameMouseScale",flags).GetValue(view);var offset=(Vector2)viewType.GetProperty("gameMouseOffset",flags).GetValue(view);
            view.SendEvent(new Event{type=type,mousePosition=pixel/gameScale-offset,delta=delta*scale/gameScale,button=button});view.Repaint();await Task.Delay(100);
        }
        static async Task UIDrag(Vector2 from,Vector2 to)
        {await UIEvent(EventType.MouseDown,from);var last=from;for(int i=1;i<=5;i++){var p=Vector2.Lerp(from,to,i/5f);await UIEvent(EventType.MouseDrag,p,p-last);last=p;}await UIEvent(EventType.MouseUp,to);}
        static async Task UIClick(Vector2 point){await UIEvent(EventType.MouseDown,point);await UIEvent(EventType.MouseUp,point);}
        public static async void MenuInputProbe()
        {
            try
            {
                Application.runInBackground=true;await SizeGame(1920,1080);var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>();menu.OpenSettings();var ui=menu.GetComponent<ColonyUI>();
                var windows=(List<WindowState>)typeof(ColonyUI).GetProperty("Windows",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(ui);var window=windows.First(w=>w.open);window.rect=new Vector4(350,180,480,620);window.dock=0;
                OpenGUIHarness(ui);uiMouse=InputSystem.AddDevice<Mouse>();ColonyUI.ValidationInput=e=>{if(e.type!=EventType.Layout&&e.type!=EventType.Repaint)File.AppendAllText("Logs/ColonyUIEvents.txt",e.type+" "+e.mousePosition+" focus="+Application.isFocused+"\n");};File.WriteAllText("Logs/ColonyUIEvents.txt","Menu input events\n");
                await Task.Delay(500);await UIDrag(new Vector2(430,200),new Vector2(530,250));File.AppendAllText("Logs/ColonyUIEvents.txt","RESULT "+window.rect+"\n");
            }
            catch(Exception error){Debug.LogException(error);}
            finally{CloseGUIHarness();ColonyUI.ValidationInput=null;if(uiMouse!=null)InputSystem.RemoveDevice(uiMouse);uiMouse=null;}
        }
        [MenuItem("Meridian/Colony/Validate Runtime Views and Controls")]
        public static async void PlayAcceptance()
        {
            playEvidence.Clear();PlayNote("Inspecting actual Play Mode UI and rendering");var game=ColonyRuntime.Current;
            try
            {
                Require(game!=null&&!game.Cinematic,"Load a developed fixture first");Application.runInBackground=true;await SizeGame(2560,1440);game.Simulation.State.speed=0;
                OpenGUIHarness(game.UI);
                uiMouse=InputSystem.AddDevice<Mouse>("Colony acceptance pointer");uiKeyboard=InputSystem.AddDevice<Keyboard>("Colony acceptance keyboard");
                ColonyUI.ValidationInput=e=>{if(e.isMouse)File.AppendAllText("Logs/ColonyUIEvents.txt",e.type+" "+e.mousePosition+" focus="+Application.isFocused+"\n");};File.WriteAllText("Logs/ColonyUIEvents.txt","GUI events\n");
                var sim=game.Simulation;sim.State.windows.Clear();ColonySettings.Current.uiScale=1;
                var person=sim.SurfaceActors.First(a=>a.kind==ActorKind.Colonist);var forestry=sim.SurfaceActors.First(a=>a.kind==ActorKind.ForestryBot);var habitat=sim.State.structures.First(b=>b.definition=="habitat");
                var miner=sim.State.structures.FirstOrDefault(b=>b.definition=="miner");if(miner==null){var ore=sim.World.Objects.First(o=>o.Kind==SurfaceObjectKind.Iron);miner=sim.AddStructure("miner",sim.World.Ground(ore.Position),0,true);miner.deposit=ore.Id;}
                var flight=sim.State.flights.First();foreach(var id in new[]{person.id,forestry.id,miner.id,habitat.id,flight.id}){game.UI.Inspect(id);sim.State.windows.Find(w=>w.key==id).pinned=true;}
                Require(sim.State.windows.Count(w=>w.open)==5,"Inspector identity duplication");var window=sim.State.windows.First();foreach(var other in sim.State.windows.Skip(1))other.open=false;
                window.rect=new Vector4(350,180,480,620);window.dock=0;await Task.Delay(500);
                var pivot=game.Camera.Pivot;await UIDrag(new Vector2(430,200),new Vector2(530,250));
                Require(Mathf.Abs(window.rect.x-450)<3&&Mathf.Abs(window.rect.y-230)<3,"Title drag not applied in scaled GUI coordinates: "+window.rect);Require(Vector3.Distance(pivot,game.Camera.Pivot)<.01f,"UI drag moved camera");
                await UIDrag(new Vector2(window.rect.x+window.rect.z-3,window.rect.y+300),new Vector2(window.rect.x+window.rect.z+77,window.rect.y+300));Require(window.rect.z>550,"Right edge resize failed");
                await UIDrag(new Vector2(window.rect.x+80,window.rect.y+20),new Vector2(12,340));Require(window.dock==1,"Left docking failed");
                await UIDrag(new Vector2(180,110),new Vector2(900,380));Require(window.dock==0,"Tab drag did not undock");
                await UIClick(new Vector2(window.rect.x+window.rect.z-80,window.rect.y+20));Require(window.dock==2,"Dock button failed");
                float old=sim.State.dockSizes.y;await UIDrag(new Vector2(1920-old-8,320),new Vector2(1920-old-78,320));Require(sim.State.dockSizes.y>old+50,"Dock splitter failed");
                var second=sim.State.windows[1];second.open=true;second.dock=2;await Task.Delay(200);await UIClick(new Vector2(1920-sim.State.dockSizes.y/4-10,110));
                game.UI.CaptureLayout();string json=JsonUtility.ToJson(sim.State);var copied=ColonySaves.Deserialize(json);Require(JsonUtility.ToJson(copied)==json,"Window layout snapshot differs");
                PlayNote("Five stable inspectors; actual scaled title drag, edge resize, dock, tab undock, dock button, splitter, tab selection and layout capture; UI gesture leaves camera fixed");
                var air=sim.State.structures.Sum(b=>b.air);var position=person.position;game.Visuals.ToggleMaster();game.Visuals.ToggleDome(habitat);game.Visuals.ToggleMaster();
                Require(sim.State.structures.All(b=>b.domeOverride==-1)&&Mathf.Abs(sim.State.structures.Sum(b=>b.air)-air)<.0001f&&person.position==position,"Frames mutated authority beyond visual flags");
                foreach(var w in sim.State.windows)w.open=false;game.UI.Open("Overview");var overview=sim.State.windows.First(w=>w.key=="@Overview");overview.rect=new Vector4(200,150,480,620);overview.dock=0;await Task.Delay(250);
                var camera=game.Camera.Capture();var at=new Vector2(400,400)*Mathf.Min(Screen.width/1920f,Screen.height/1080f);InputSystem.QueueStateEvent(uiMouse,new MouseState{position=new Vector2(at.x,Screen.height-at.y),scroll=new Vector2(0,120)});await Task.Delay(300);
                Require(Mathf.Abs(game.Camera.Pitch-camera.pitch)<.01f,"Wheel over window tilted world");
                game.UI.Confirm("Acceptance modal","Testing captured controls",()=>{});await Task.Delay(150);await UIDrag(new Vector2(260,170),new Vector2(600,230));Require(overview.rect.x==200&&overview.rect.y==150,"Modal allowed title drag");game.UI.DismissModal();
                CloseGUIHarness();foreach(var w in sim.State.windows)w.open=false;await Task.Delay(200);game.Camera.SendMessage("OnApplicationFocus",true);game.Camera.Restore(new CameraState{pivot=sim.State.setup.landing.Position,yaw=0,pitch=57,distance=245});
                InputSystem.QueueStateEvent(uiKeyboard,new KeyboardState(Key.E));await Task.Delay(70);InputSystem.QueueStateEvent(uiKeyboard,new KeyboardState());await Task.Delay(900);Require(Mathf.Abs(Mathf.DeltaAngle(game.Camera.Heading,45))<.3f,"Smooth E heading incorrect");
                game.Camera.SendMessage("OnApplicationFocus",false);Require(!game.Camera.IsPanning,"Focus loss retained camera capture");game.Camera.SendMessage("OnApplicationFocus",true);
                PlayNote("Wheel over UI and modal movement blocked; E settles at 45 degrees; focus-loss capture reset; Frames preserves air, routes and occupants");
                CloseGUIHarness();await SizeGame(1280,1024);game.UI.Open("Overview");await Task.Delay(250);Require(sim.State.windows.Where(w=>w.open&&w.dock==0).All(w=>w.rect.x>=0&&w.rect.y>=0&&w.rect.x+w.rect.z<=Screen.width/(Screen.width/1920f)+1),"Window became unreachable after resolution change");
                await SizeGame(2560,1440);game.Camera.Restore(sim.State.camera);game.UI.Open("Overview");await Task.Delay(500);ScreenCapture.CaptureScreenshot("Captures/Colony-Controls.png");
                File.WriteAllText("Logs/ColonyPlayValidation.txt","PASS\n"+string.Join("\n",playEvidence));Debug.Log("Meridian runtime UI checks passed.");
            }
            catch(Exception error){File.WriteAllText("Logs/ColonyPlayValidation.txt","FAIL\n"+string.Join("\n",playEvidence)+"\n"+error);Debug.LogException(error);ScreenCapture.CaptureScreenshot("Captures/Colony-Controls-Failure.png");}
            finally{CloseGUIHarness();ColonyUI.ValidationInput=null;if(uiMouse!=null)InputSystem.RemoveDevice(uiMouse);if(uiKeyboard!=null)InputSystem.RemoveDevice(uiKeyboard);uiMouse=null;uiKeyboard=null;}
        }
        public static async void MeasureViews()
        {
            var log=new List<string>();var game=ColonyRuntime.Current;
            try
            {
                Require(game!=null,"Load stress fixture first");await SizeGame(2560,1440);Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
                log.Add(SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.systemMemorySize+" MB RAM; Editor 2560 x 1440, 1x simulation, 60 fps target");
                game.Simulation.State.speed=1;foreach(var w in game.Simulation.State.windows)w.open=false;
                foreach(var focus in new[]{new Vector3(550,0,190),new Vector3(6250,0,190),new Vector3(550,0,190)})
                {
                    game.Camera.Restore(new CameraState{pivot=focus,yaw=-25,pitch=57,distance=380});await Task.Delay(12000);while(ShaderUtil.anythingCompiling)await Task.Delay(200);
                    var samples=new List<float>();int lastFrame=-1;void Record(ScriptableRenderContext context,List<Camera> cameras){if(cameras.Contains(game.Camera.Lens)&&Time.frameCount!=lastFrame){lastFrame=Time.frameCount;samples.Add(Time.unscaledDeltaTime*1000);}}
                    RenderPipelineManager.endContextRendering+=Record;try{var watch=Stopwatch.StartNew();while(samples.Count<300&&watch.Elapsed.TotalSeconds<60)await Task.Delay(100);}finally{RenderPipelineManager.endContextRendering-=Record;}
                    Require(samples.Count>=100,"Insufficient rendered frame samples");var sorted=samples.OrderBy(x=>x).ToArray();
                    log.Add("Focus "+focus.x+": "+sorted.Length+" frames, mean "+sorted.Average().ToString("F2")+"ms / p95 "+sorted[(int)(sorted.Length*.95f)].ToString("F2")+"ms / max "+sorted.Last().ToString("F2")+"ms; terrain "+game.Landscape.TerrainCount+", prop batches "+game.Landscape.PropBatchCount+", live entities "+UnityEngine.Object.FindObjectsByType<ColonyPickTarget>().Length+", Unity allocated "+(Profiler.GetTotalAllocatedMemoryLong()/1048576)+"MB, managed "+(GC.GetTotalMemory(false)/1048576)+"MB");
                    Require(game.Landscape.TerrainCount<=49,"Terrain working set exceeded budget");File.WriteAllText("Logs/ColonyFrameValidation.txt","RUNNING\n"+string.Join("\n",log));
                }
                game.Simulation.State.speed=0;game.UI.Open("Overview");ScreenCapture.CaptureScreenshot("Captures/Colony-Stress-1440.png");File.WriteAllText("Logs/ColonyFrameValidation.txt","PASS\n"+string.Join("\n",log));
            }
            catch(Exception error){File.WriteAllText("Logs/ColonyFrameValidation.txt","FAIL\n"+string.Join("\n",log)+"\n"+error);Debug.LogException(error);}
        }
    }
}
