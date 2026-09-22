using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Meridian.Colony;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Meridian.Editor
{
    public static partial class ColonyValidation
    {
        public static async void OpenUsabilityFixture()
        {Application.runInBackground=true;await SizeGame(1920,1080);OpenFixture();}
        public static async void CaptureUtilityViews()
        {
            var game=ColonyRuntime.Current;game.Speed(0);FrameColony();
            foreach(var window in game.Simulation.State.windows)window.open=false;
            foreach(int mode in new[]{0,2,1})
            {game.SetUtilityView(mode);await Task.Delay(800);ScreenCapture.CaptureScreenshot("Captures/Colony-Layer-"+mode+".png");await Task.Delay(300);}
            game.SetUtilityView(0);game.UI.Open("Orbit");await Task.Delay(500);ScreenCapture.CaptureScreenshot("Captures/Colony-Orbit-Controls.png");
        }
        [MenuItem("Meridian/Colony/Validate Recovery Camera and Underground")]
        public static async void Usability()
        {
            const string log="Logs/ColonyUsabilityValidation.txt";
            File.WriteAllText(log,"RUNNING\n");Mouse mouse=null;
            var game=ColonyRuntime.Current;
            try
            {
                Require(game!=null&&!game.Cinematic,"Load the colony fixture in Play Mode first");
                game.Speed(0);Application.runInBackground=true;
                var sim=new ColonySimulation(ColonySimulation.Create(game.Simulation.State.setup,game.Simulation.Catalog),game.Simulation.World.Surface,game.Simulation.Catalog);
                sim.FinalizeArrival();var ship=sim.State.structures.First(b=>b.definition=="ship");
                var pile=sim.DropCargo(ship.position+Vector3.right*24,new[]{new Amount(Good.Metal,3.75f),new Amount(Good.Components,.25f)});
                float before=sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Metal));
                sim.Jobs.CollectCargo(pile);int orders=sim.State.jobs.Count(j=>j.source==pile.inventory);
                sim.Jobs.CollectCargo(pile);Require(orders==2&&orders==sim.State.jobs.Count(j=>j.source==pile.inventory),"Cargo collection duplicates reservations or ignores fractional leftovers");
                for(int i=0;i<1800&&(pile.phase!=BuildPhase.Removed||sim.State.jobs.Any(j=>j.source==pile.inventory&&sim.Jobs.Active(j)));i++)sim.Tick(.1f);
                Require(pile.phase==BuildPhase.Removed&&!sim.State.jobs.Any(j=>j.source==pile.inventory&&sim.Jobs.Active(j)),"Machines failed to collect and remove recovered pile");
                Require(Mathf.Abs(before-sim.State.inventories.Sum(i=>sim.Stock.Count(i,Good.Metal)))<.001f,"Cargo cleanup lost material");sim.Stock.Validate();
                var blocked=sim.DropCargo(ship.position+Vector3.right*24,new[]{new Amount(Good.CopperOre,.75f)});var store=sim.Stock.Get(ship.inventory);store.filters.Add(Good.Food);
                sim.Jobs.CollectCargo(blocked);Require(blocked.phase!=BuildPhase.Removed&&blocked.blocker.Contains("Storage full")&&sim.Stock.Count(sim.Stock.Get(blocked.inventory),Good.CopperOre)==.75f,"Filtered storage destroyed or stranded cargo without an explanation");store.filters.Clear();
                sim.Jobs.CollectCargo(blocked);var haul=sim.State.jobs.First(j=>j.source==blocked.inventory);var drone=sim.SurfaceActors.First(a=>a.kind==ActorKind.Drone);var carried=sim.Stock.Get(drone.inventory);
                float picked=sim.Stock.Transfer(sim.Stock.Get(blocked.inventory),carried,Good.CopperOre,.25f,haul.id);sim.Stock.Release(haul.id,true);haul.carrying=true;haul.quantity=picked;haul.actor=drone.id;drone.job=haul.id;
                foreach(var claim in sim.State.reservations.Where(r=>r.job==haul.id&&r.incoming))claim.quantity=picked;
                ColonyCommands.DiscardCargo(sim,blocked);Require(blocked.phase==BuildPhase.Removed&&sim.Stock.Count(carried,Good.CopperOre)==.25f&&sim.Jobs.Active(haul),"Discard destroyed already-loaded cargo or cancelled its delivery");
                var uncollected=sim.DropCargo(ship.position,new[]{new Amount(Good.Metal,.5f)});sim.Jobs.CollectCargo(uncollected);ColonyCommands.DiscardCargo(sim,uncollected);Require(!sim.State.reservations.Any(r=>r.inventory==uncollected.inventory),"Discard left outgoing claims");sim.Stock.Validate();
                File.AppendAllText(log,"PASS: physical collection, fractional leftovers, empty removal, storage filters, repeated planning, discard preserving carriers and releasing claims\n");

                var s=game.Simulation;var utility=s.State.structures.First(b=>b.definition=="cable");
                foreach(var b in s.State.structures.Where(b=>ColonyUtilities.Underground(s.Definition(b))))
                    foreach(var point in ColonyUtilities.Path(s.World,b.position,b.end))Require(Mathf.Abs(s.World.Height(point.x,point.z)-point.y-ColonyUtilities.Depth)<.001f,"Utility leaves buried layer");
                Require(!ColonyCommands.Obstacles(s,"cable",utility.position,0,utility.end).Any(),"Buried line designates surface trees for clearing");
                var shader=Resources.Load<Shader>("Colony/UtilityConnections");Require(shader&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Utility shader unsupported or contains errors");
                game.SetUtilityView(0);Require(!game.Visuals.View(utility.id).activeSelf,"Surface shows buried utility");
                game.SetUtilityView(2);Require(game.Visuals.View(utility.id).activeSelf,"Overlay hides utility");
                var middle=ColonyUtilities.Buried(s.World,(utility.position+utility.end)*.5f);
                Physics.SyncTransforms();Require(game.Visuals.Pick(new Ray(middle+Vector3.up*100,Vector3.down),game.Landscape,out _)==utility.id,"Buried line cannot be picked through terrain");
                var building=s.Structure(utility.from);var port=ColonyUtilities.Port(s,building,s.Structure(utility.to).position,"cable");
                Require(game.Visuals.Pick(new Ray(port+Vector3.up*100,Vector3.down),game.Landscape,out _,"cable")==building.id,"Underground building port cannot be selected");
                game.SetUtilityView(1);Require(game.Landscape.Colliders.All(c=>c.enabled&&!c.GetComponent<Terrain>().drawHeightmap),"Cutaway disabled terrain picking or failed to hide surface");
                game.SetUtilityView(0);Require(game.Landscape.Colliders.All(c=>c.GetComponent<Terrain>().drawHeightmap),"Surface terrain not restored");
                var restored=new ColonySimulation(ColonySaves.Deserialize(JsonUtility.ToJson(s.State)),s.World.Surface,s.Catalog);
                Require(restored.Networks.Connected(utility.from,utility.to,"cable"),"Existing network disconnected during underground migration");
                File.AppendAllText(log,"PASS: buried terrain-following paths, no surface clearing, shader, visibility, line and port picking, terrain collision, existing-save network continuity\n");

                s.State.windows.Clear();game.UI.Inspect(building.id);var inspector=s.State.windows.Single();inspector.dock=2;inspector.pinned=true;
                game.UI.CaptureLayout();game.UI.Inspect(utility.id);Require(s.State.windows.Single(w=>w.key==utility.id).dock==0,"New inspector inherited dock");
                game.UI.Inspect(utility.to);Require(s.State.windows.Count(w=>w.open&&!w.pinned)==1&&inspector.open,"Selection accumulates inspectors or closes pinned view");
                s.State.windows.Clear();await SizeGame(1920,1080);await Task.Delay(300);
                game.Camera.Restore(new CameraState{pivot=s.State.setup.landing.Position,yaw=0,pitch=57,distance=245});game.Camera.SendMessage("OnApplicationFocus",true);
                mouse=InputSystem.AddDevice<Mouse>("Meridian right-drag validation");int clicks=0;Action clicked=()=>clicks++;game.Camera.RightClicked+=clicked;
                Vector2 p=new Vector2(960,540);
                async Task Pointer(Vector2 at,bool pressed)
                {
                    InputSystem.EnableDevice(mouse);mouse.MakeCurrent();game.Camera.SendMessage("OnApplicationFocus",true);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=at,buttons=(ushort)(pressed?2:0)});await Task.Delay(180);
                    File.AppendAllText(log,"POINTER "+at+" press="+pressed+" read="+mouse.rightButton.isPressed+" enabled="+mouse.enabled+" current="+(Mouse.current==mouse)+" ui="+game.UI.OverUI(at)+" text="+game.UI.TextFocused+" modal="+game.UI.Modal+" captured="+game.Camera.IsOrbiting+" heading="+game.Camera.Heading+"\n");
                }
                var pivot=game.Camera.Pivot;float pitch=game.Camera.Pitch;
                await Pointer(p,true);await Pointer(p+Vector2.right*120,true);await Pointer(p+Vector2.right*120,false);await Task.Delay(800);
                Require(Mathf.Abs(Mathf.DeltaAngle(game.Camera.Heading,26.4f))<.4f&&Mathf.Abs(game.Camera.Pitch-pitch)<.01f&&(game.Camera.Pivot-pivot).sqrMagnitude<.001f&&clicks==0,"Right drag result yaw="+game.Camera.Heading+" pitch="+game.Camera.Pitch+" pivotDelta="+(game.Camera.Pivot-pivot).magnitude+" clicks="+clicks+" sensitivity="+game.Camera.Sensitivity);
                await Pointer(p,true);await Pointer(p,false);Require(clicks==1,"Short right click did not cancel");
                await Pointer(p,true);await Pointer(p+Vector2.right*80,true);await Pointer(p,true);await Pointer(p,false);Require(clicks==1,"Return-to-origin drag became click");
                game.UI.Open("Overview");var window=s.State.windows.First(w=>w.key=="@Overview");window.rect=new Vector4(200,180,480,600);window.dock=0;await Task.Delay(300);float heading=game.Camera.Heading;
                await Pointer(new Vector2(350,700),true);await Pointer(new Vector2(1100,540),true);await Pointer(new Vector2(1100,540),false);Require(Mathf.Abs(Mathf.DeltaAngle(heading,game.Camera.Heading))<.1f,"UI-owned right drag rotated world");
                foreach(var w in s.State.windows)w.open=false;await Task.Delay(250);await Pointer(p,true);game.Camera.SendMessage("OnApplicationFocus",false);Require(!game.Camera.IsOrbiting,"Focus loss retained right capture");await Pointer(p,false);game.Camera.SendMessage("OnApplicationFocus",true);game.Camera.RightClicked-=clicked;
                File.AppendAllText(log,"PASS: reusable floating inspector, explicit dock/pin, continuous yaw-only right drag, short click, excursion threshold, UI ownership, focus loss\n");
                FrameColony();foreach(var w in s.State.windows)w.open=false;
                foreach(int mode in new[]{0,2,1}){game.SetUtilityView(mode);await Task.Delay(800);ScreenCapture.CaptureScreenshot("Captures/Colony-Layer-"+mode+".png");await Task.Delay(300);}
                game.SetUtilityView(0);game.UI.Open("Orbit");await Task.Delay(500);ScreenCapture.CaptureScreenshot("Captures/Colony-Orbit-Controls.png");
                File.AppendAllText(log,"PASS: layer and Orbit screenshots captured\nCOMPLETE\n");
            }
            catch(Exception error){File.AppendAllText(log,"FAIL\n"+error);Debug.LogException(error);}
            finally{if(mouse!=null)InputSystem.RemoveDevice(mouse);}
        }
    }
}
