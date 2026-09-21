using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meridian.Colony
{
    /// <summary>One reusable window system for entity inspectors and management panels. Stable world/entity bindings are saved.</summary>
    public sealed partial class ColonyUI:MonoBehaviour
    {
        ColonyRuntime runtime;ColonySimulation sim;ColonyCatalog catalog;ScreenTransition transition;
        readonly List<WindowState> menuWindows=new List<WindowState>();readonly Dictionary<string,Vector2> scroll=new Dictionary<string,Vector2>();
        readonly Dictionary<string,int> ids=new Dictionary<string,int>();readonly Dictionary<int,string> dockTabs=new Dictionary<int,string>();readonly Dictionary<string,Rect> drawn=new Dictionary<string,Rect>();
        readonly List<Texture2D> textures=new List<Texture2D>();readonly Dictionary<string,Texture2D> previews=new Dictionary<string,Texture2D>();
        GUISkin skin;GUIStyle title,muted,accent,warning,small,card;int nextID=100,resizeId,splitDock;Vector2 resizeStart;Rect resizeOriginal;Vector4 menuDock=new Vector4(380,380,250,250);
        string toast,modalTitle,modalText;float toastUntil;Action confirmation;string activeWindow;bool pointerCapture;Rect hoverDomeRect;string hoverDome;float hoverUntil;
        Action exitAction;bool exitSaving,clearTextFocus;string exitError,bringFront;
        public bool TextFocused{get;private set;}public bool Modal=>confirmation!=null||exitAction!=null||displayDeadline>0||ColonyLoadPipeline.Active!=null;public bool IsMenu=>runtime==null;
        public bool HasOpenWindows=>Windows.Any(w=>w.open);
        float Scale=>Mathf.Max(.6f,Mathf.Min(Screen.width/1920f,Screen.height/1080f))*ColonySettings.Current.uiScale;
        float Width=>Screen.width/Scale;float Height=>Screen.height/Scale;
        List<WindowState> Windows=>sim!=null?sim.State.windows:menuWindows;
        Vector4 DockSizes{get=>sim!=null?sim.State.dockSizes:menuDock;set{if(sim!=null)sim.State.dockSizes=value;else menuDock=value;}}
        public void Initialize(ColonyRuntime owner,ScreenTransition screenTransition)
        {runtime=owner;sim=owner?.Simulation;catalog=sim?.Catalog??ColonyCatalog.Load();transition=screenTransition;ReadGeneralLayout();if(sim!=null){runtime.Visuals.Selected=sim.State.selected;foreach(var w in Windows)if(w.world!=sim.State.worldId)w.open=false;}RefreshSaves();}
        public void Toast(string message){toast=message;toastUntil=Time.unscaledTime+7;}
        public void CaptureLayout(){if(sim!=null)sim.State.selected=runtime.Selected;SaveGeneralLayout();}
        public void Open(string panel)=>Show("@"+panel,panel);
        public void Inspect(string entity)=>Show(entity,entity);
        void Show(string key,string entity)
        {
            var window=Windows.Find(w=>w.key==key);if(window==null){int count=Windows.Count(w=>w.open);window=new WindowState{key=key,entity=entity,world=sim?.State.worldId,rect=new Vector4(150+count%5*36,145+count%5*28,key=="@Build"?520:480,620)};RestoreGeometry(window);Windows.Add(window);}window.open=true;activeWindow=key;if(window.dock>0)dockTabs[window.dock]=key;
            bringFront=key;if(key=="@Save"||key=="@Load")RefreshSaves();runtime?.Audio.Click();
        }
        public bool OverUI(Vector2 point)
        {
            if(Modal)return true;var p=new Vector2(point.x/Scale,(Screen.height-point.y)/Scale);
            if(pointerCapture||resizeId!=0||splitDock!=0||movingWindow!=null||resizingWindow!=null)return true;
            if(!IsMenu&&(p.y<88||p.y>Height-74||runtime.Cinematic))return true;
            if(hoverDome!=null&&hoverDomeRect.Contains(p))return true;return drawn.Values.Any(r=>r.Contains(p));
        }
        public void Confirm(string heading,string message,Action action){modalTitle=heading;modalText=message;confirmation=action;CancelWindowGesture();TextFocused=false;clearTextFocus=true;}
        public bool DismissModal(){if(exitAction!=null){if(!exitSaving)exitAction=null;return true;}if(confirmation==null)return false;confirmation=null;return true;}
        public void AskExit(Action action){exitAction=action;exitError=null;exitSaving=false;}
        public void ExitSaveFailed(string error){exitSaving=false;exitError="Save failed: "+error;}
        public void CompleteExit(){exitAction=null;exitSaving=false;}
        public void Escape(){if(resizeId!=0||splitDock!=0){resizeId=splitDock=0;return;}var last=Windows.LastOrDefault(w=>w.open);if(last!=null){last.open=false;return;}Open("Pause");if(sim!=null&&sim.State.speed>0)runtime.TogglePause();}
        public void Quickload()
        {RefreshSaves();var quick=saves.FirstOrDefault(s=>s.slot=="quicksave"&&s.Valid&&!s.backup);if(quick==null){Toast("No valid quicksave is available");return;}Confirm("Load quicksave?","Unsaved progress will be replaced after this save has been validated.",()=>Load(quick));}
        void Update()
        {
            PreparePortrait();
            if(sim?.State.failed==true&&!Windows.Any(w=>w.key=="@Recovery"&&w.open))Open("Recovery");
            if(Mouse.current!=null){if(Mouse.current.leftButton.wasPressedThisFrame||Mouse.current.middleButton.wasPressedThisFrame)pointerCapture=OverUI(Mouse.current.position.ReadValue());if(!Mouse.current.leftButton.isPressed&&!Mouse.current.middleButton.isPressed)pointerCapture=false;}
            if(runtime&&runtime.Simulation.State.deployed&&!Modal&&Mouse.current!=null)
            {
                var pointer=Mouse.current.position.ReadValue();if(!OverUI(pointer))
                {
                    string hit=runtime.Visuals.Pick(runtime.Camera.Lens.ScreenPointToRay(pointer),runtime.Landscape,out _);var building=sim.Structure(hit);
                    if(building!=null&&sim.Definition(building).sealedModule&&!sim.Definition(building).link)
                    {hoverDome=building.id;hoverUntil=Time.unscaledTime+1;var position=runtime.Camera.Lens.WorldToScreenPoint(building.position+Vector3.up*(sim.Definition(building).height+2));hoverDomeRect=new Rect(position.x/Scale-72,(Screen.height-position.y)/Scale-24,144,30);}
                }
                else if(hoverDomeRect.Contains(new Vector2(pointer.x/Scale,(Screen.height-pointer.y)/Scale)))hoverUntil=Time.unscaledTime+1;
                if(Time.unscaledTime>hoverUntil)hoverDome=null;
            }
            if(displayDeadline>0&&Time.unscaledTime>displayDeadline)RevertDisplay();
        }
        void Skin()
        {
            if(skin)return;skin=Instantiate(GUI.skin);skin.font=Font.CreateDynamicFontFromOSFont("Segoe UI",17);skin.label.fontSize=17;skin.label.normal.textColor=new Color(.86f,.9f,.9f);skin.label.wordWrap=true;
            Texture2D ColorTexture(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();textures.Add(t);return t;}
            var panel=ColorTexture(new Color(.025f,.045f,.055f,.98f));var face=ColorTexture(new Color(.10f,.15f,.18f));var hover=ColorTexture(new Color(.20f,.28f,.31f));var selected=ColorTexture(new Color(.47f,.29f,.11f));
            skin.window=new GUIStyle(skin.box){normal={background=panel,textColor=new Color(1,.73f,.36f)},padding=new RectOffset(12,12,12,12),border=new RectOffset(1,1,1,1)};
            foreach(var style in new[]{skin.button,skin.toggle,skin.textField,skin.textArea}){style.fontSize=16;style.normal.textColor=new Color(.9f,.93f,.93f);style.normal.background=face;style.hover.background=hover;style.active.background=selected;style.focused.background=hover;style.padding=new RectOffset(10,10,7,7);style.margin=new RectOffset(3,3,3,3);}
            skin.button.wordWrap=true;skin.toggle.onNormal.background=selected;skin.toggle.onHover.background=selected;skin.textField.wordWrap=false;skin.verticalScrollbar.fixedWidth=12;
            title=new GUIStyle(skin.label){fontSize=23,fontStyle=FontStyle.Bold};accent=new GUIStyle(skin.label){normal={textColor=new Color(1,.73f,.36f)},fontStyle=FontStyle.Bold};muted=new GUIStyle(skin.label){normal={textColor=new Color(.58f,.69f,.73f)},fontSize=15};small=new GUIStyle(skin.label){fontSize=14};warning=new GUIStyle(skin.label){normal={textColor=new Color(1,.58f,.39f)}};card=new GUIStyle(skin.box){normal={background=face},padding=new RectOffset(10,10,9,9),margin=new RectOffset(2,2,5,5)};
        }
        void OnGUI()
        {
#if UNITY_EDITOR
            ValidationInput?.Invoke(new Event(Event.current));
#endif
            if(catalog==null||ScreenTransition.Active)return;Skin();var old=GUI.skin;var matrix=GUI.matrix;bool oldEnabled=GUI.enabled;GUI.enabled=!Modal;GUI.skin=skin;GUI.matrix=Matrix4x4.Scale(Vector3.one*Scale);drawn.Clear();
            if(clearTextFocus){GUI.FocusControl(null);clearTextFocus=false;}
            HandleWindowGestures();
            if(runtime)
            {
                if(runtime.Cinematic){GUI.Box(new Rect(0,Height-112,Width,112),GUIContent.none);GUI.Label(new Rect(40,Height-92,Width-300,35),"M E R I D I A N  /  EXPEDITION ARRIVAL",title);GUI.Label(new Rect(40,Height-48,Width-330,35),runtime.SaveQueued?"Quicksave queued for the completed landing checkpoint.":"Machines and supplies first. Your colonists remain safely in orbital cryosleep.",muted);if(GUI.Button(new Rect(Width-220,Height-77,175,45),"Skip arrival  [Esc]"))runtime.FinishArrival();GUI.matrix=matrix;GUI.skin=old;GUI.enabled=oldEnabled;return;}
                HUD();
                if(runtime.HarvestRectangle is Rect drag){var rect=new Rect(drag.x/Scale,(Screen.height-drag.yMax)/Scale,drag.width/Scale,drag.height/Scale);GUI.Box(rect,GUIContent.none);}
                if(hoverDome!=null&&!Modal){var b=sim.Structure(hoverDome);if(b!=null&&GUI.Button(hoverDomeRect,new GUIContent(runtime.Visuals.Frames(b)?"Show exterior":"See interior","Toggle this dome's exterior. Structure and air remain unchanged.")))runtime.Visuals.ToggleDome(b);}
            }
            var floating=Windows.Where(w=>w.open&&w.dock==0).ToArray();
            string pointerWindow=floating.LastOrDefault(w=>new Rect(w.rect.x,w.rect.y,w.rect.z,w.rect.w).Contains(Event.current.mousePosition))?.key;
            var dockEvent=Event.current.type;bool coverDock=Event.current.isMouse&&pointerWindow!=null;if(coverDock)Event.current.type=EventType.Ignore;
            for(int dock=1;dock<=4;dock++)DrawDock(dock);
            if(coverDock)Event.current.type=dockEvent;
            foreach(var window in floating)DrawFloating(window,pointerWindow);
            if(bringFront!=null){var window=Windows.Find(w=>w.key==bringFront);if(window!=null){Windows.Remove(window);Windows.Add(window);}bringFront=null;}
            DrawDockPreview();
            TextFocused=!string.IsNullOrEmpty(GUI.GetNameOfFocusedControl())&&GUI.GetNameOfFocusedControl().StartsWith("text:");
            if(toast!=null&&Time.unscaledTime<toastUntil){GUI.Box(new Rect(Width*.5f-390,Height-130,780,48),GUIContent.none);GUI.Label(new Rect(Width*.5f-375,Height-123,750,40),toast,accent);}
            GUI.enabled=true;
            if(exitAction!=null)
            {
                GUI.Box(new Rect(0,0,Width,Height),GUIContent.none);GUILayout.BeginArea(new Rect(Width*.5f-285,Height*.5f-140,570,280),skin.window);GUILayout.Label("LEAVE THIS COLONY?",title);GUILayout.Label("Save your current progress before leaving, or discard changes since your latest save.");if(exitError!=null)GUILayout.Label(exitError,warning);GUILayout.FlexibleSpace();GUI.enabled=!exitSaving;GUILayout.BeginHorizontal();if(GUILayout.Button("Cancel"))exitAction=null;if(GUILayout.Button("Discard and leave")){var action=exitAction;exitAction=null;Safe(action);}if(GUILayout.Button(exitSaving?"Saving…":"Save and leave")){exitSaving=true;runtime.SaveThenLeave(exitAction);}GUILayout.EndHorizontal();GUI.enabled=true;GUILayout.EndArea();
            }
            if(confirmation!=null)
            {
                GUI.Box(new Rect(0,0,Width,Height),GUIContent.none);GUILayout.BeginArea(new Rect(Width*.5f-280,Height*.5f-135,560,270),skin.window);GUILayout.Label(modalTitle,title);GUILayout.Space(12);GUILayout.Label(modalText);GUILayout.FlexibleSpace();GUILayout.BeginHorizontal();if(GUILayout.Button("Cancel"))confirmation=null;if(GUILayout.Button("Confirm")){var action=confirmation;confirmation=null;Safe(action);}GUILayout.EndHorizontal();GUILayout.EndArea();
            }
            if(displayDeadline>0){GUI.Box(new Rect(Width*.5f-260,80,520,85),GUIContent.none);GUI.Label(new Rect(Width*.5f-245,90,490,25),"Keep this display mode? Reverting in "+Mathf.CeilToInt(displayDeadline-Time.unscaledTime)+"s");if(GUI.Button(new Rect(Width*.5f-240,125,225,30),"Keep")){displayDeadline=0;ColonySettings.Save();}if(GUI.Button(new Rect(Width*.5f+5,125,225,30),"Revert"))RevertDisplay();}
            if(!string.IsNullOrEmpty(GUI.tooltip)&&!Modal){var p=Event.current.mousePosition;GUI.Box(new Rect(Mathf.Min(p.x+15,Width-350),Mathf.Min(p.y+20,Height-60),340,50),GUI.tooltip);}
            GUI.matrix=matrix;GUI.skin=old;GUI.enabled=oldEnabled;
        }
        int WindowID(string key){if(!ids.TryGetValue(key,out var id)){id=nextID++;ids.Add(key,id);}return id;}
#if UNITY_EDITOR
        public static Action<Event> ValidationInput;
#endif
        string Title(WindowState w)
        {if(w.key.StartsWith("@"))return w.key.Substring(1);var region=sim?.State.regions.Find(r=>r.id==w.entity);if(region!=null)return "Region "+region.x+", "+region.z;return sim?.Structure(w.entity)?.name??sim?.Actor(w.entity)?.name??sim?.State.flights.Find(f=>f.id==w.entity)?.name??sim?.World.Object(w.entity)?.Kind.ToString()??"Entity unavailable";}
        Rect Clamp(Rect r){r.width=Mathf.Clamp(r.width,330,Width-30);r.height=Mathf.Clamp(r.height,230,Height-115);r.x=Mathf.Clamp(r.x,0,Width-r.width);r.y=Mathf.Clamp(r.y,IsMenu?25:90,Height-r.height-12);return r;}
        void DrawFloating(WindowState w,string pointerWindow)
        {
            var r=Clamp(new Rect(w.rect.x,w.rect.y,w.rect.z,w.rect.w));int id=WindowID(w.key);drawn[w.key]=r;
            // Groups keep pointer ownership in this UI, including while dragging beyond a window.
            // Suppress mouse events for obscured windows; only the top window under the pointer owns them.
            var type=Event.current.type;bool blocked=Event.current.isMouse&&pointerWindow!=w.key;
            if(blocked)Event.current.type=EventType.Ignore;
            GUI.BeginGroup(r,GUIContent.none,skin.window);WindowBody(w,r.size,false);GUI.EndGroup();
            if(blocked)Event.current.type=type;
            w.rect=new Vector4(r.x,r.y,r.width,r.height);
        }
        void Dock(WindowState w,int dock){w.dock=dock;dockTabs[dock]=w.key;}
        Rect DockRect(int zone)
        {
            var s=DockSizes;bool left=Windows.Any(w=>w.open&&w.dock==1),right=Windows.Any(w=>w.open&&w.dock==2);float x=left?s.x+8:8,width=Width-x-(right?s.y+8:8);
            if(zone==1)return new Rect(8,94,s.x,Height-174);if(zone==2)return new Rect(Width-s.y-8,94,s.y,Height-174);if(zone==3)return new Rect(x,94,width,s.z);return new Rect(x,Height-80-s.w,width,s.w);
        }
        void DrawDock(int zone)
        {
            var tabs=Windows.Where(w=>w.open&&w.dock==zone).ToArray();if(tabs.Length==0)return;var r=DockRect(zone);GUI.Box(r,GUIContent.none,skin.window);
            if(!dockTabs.TryGetValue(zone,out var selected)||!tabs.Any(w=>w.key==selected))selected=dockTabs[zone]=tabs[0].key;
            float tabWidth=(r.width-14)/tabs.Length;for(int i=0;i<tabs.Length;i++)
            {var tab=tabs[i];var tabRect=new Rect(r.x+7+i*tabWidth,r.y+5,tabWidth-3,32);if(GUI.enabled&&Event.current.type==EventType.MouseDown&&tabRect.Contains(Event.current.mousePosition)){selected=dockTabs[zone]=tab.key;StartMove(tab,Event.current.mousePosition);}GUI.Toggle(tabRect,tab.key==selected,new GUIContent(Title(tab),"Drag this tab into the play area to undock"),skin.button);}
            var activeTab=tabs.First(w=>w.key==selected);drawn[activeTab.key]=r;GUILayout.BeginArea(new Rect(r.x,r.y+39,r.width,r.height-39));WindowBody(activeTab,new Vector2(r.width,r.height-39),true);GUILayout.EndArea();
            Rect edge=zone==1?new Rect(r.xMax-3,r.y,6,r.height):zone==2?new Rect(r.x-3,r.y,6,r.height):zone==3?new Rect(r.x,r.yMax-3,r.width,6):new Rect(r.x,r.y-3,r.width,6);GUI.Box(edge,GUIContent.none);
            if(GUI.enabled&&Event.current.type==EventType.MouseDown&&edge.Contains(Event.current.mousePosition)){splitDock=zone;Event.current.Use();}
            if(GUI.enabled&&splitDock==zone&&Event.current.type==EventType.MouseDrag){var s=DockSizes;float delta=zone<=2?Event.current.delta.x:Event.current.delta.y;if(zone==2||zone==4)delta=-delta;s[zone-1]=Mathf.Clamp(s[zone-1]+delta,zone<=2?330:230,zone<=2?Width*.4f:Height*.4f);DockSizes=s;Event.current.Use();}
            if(Event.current.rawType==EventType.MouseUp)splitDock=0;
        }
        void WindowBody(WindowState window,Vector2 size,bool docked)
        {
            if(GUI.enabled&&Event.current.type==EventType.MouseDown){activeWindow=window.key;bringFront=window.key;}
            WindowTitleGesture(window,size,docked);
            GUI.Label(new Rect(14,7,size.x-145,31),Title(window),accent);
            if(GUI.Button(new Rect(size.x-42,6,31,29),"x",small)){window.open=false;return;}
            if(GUI.Button(new Rect(size.x-112,6,63,29),docked?"Float":"Dock")){if(docked){window.dock=0;window.rect=new Vector4(Width*.5f-240,150,480,600);}else Dock(window,2);}
            GUILayout.BeginArea(new Rect(10,42,size.x-20,size.y-56));var position=scroll.TryGetValue(window.key,out var old)?old:Vector2.zero;position=GUILayout.BeginScrollView(position);scroll[window.key]=position;
            try{if(window.key.StartsWith("@"))Panel(window.key.Substring(1));else Entity(window.entity);}catch(Exception error){GUILayout.Label("This view could not refresh: "+error.Message,warning);}
            GUILayout.EndScrollView();GUILayout.EndArea();
            if(!docked)
            {
                var grip=new Rect(size.x-22,size.y-22,20,20);GUI.Label(grip,"◢",muted);
            }
        }
        void Row(string label,string value){GUILayout.BeginHorizontal();GUILayout.Label(label,muted,GUILayout.Width(145));GUILayout.Label(value);GUILayout.EndHorizontal();}
        void Header(string text){GUILayout.Space(10);GUILayout.Label(text,accent);}
        bool Button(string text,Action action,bool enabled=true,string tooltip=null)
        {bool before=GUI.enabled;GUI.enabled=before&&enabled;bool clicked=GUILayout.Button(new GUIContent(text,tooltip??""));GUI.enabled=before;if(clicked){runtime?.Audio.Click();Safe(action);}return clicked;}
        void Safe(Action action){try{action?.Invoke();}catch(Exception error){Toast(error.Message);Debug.LogWarning("Meridian command: "+error.Message);}}
        string Text(string key,string value){GUI.SetNextControlName("text:"+key);return GUILayout.TextField(value??"");}
        void Bar(string name,float value,float max=100)
        {GUILayout.Label(name+"   "+value.ToString("0.0")+(max==100?"%":" / "+max.ToString("0")),small);var rect=GUILayoutUtility.GetRect(10,5,GUILayout.ExpandWidth(true));GUI.Box(rect,GUIContent.none);var fill=rect;fill.width*=Mathf.Clamp01(value/Mathf.Max(.001f,max));var color=GUI.color;GUI.color=new Color(.88f,.62f,.26f);GUI.DrawTexture(fill,Texture2D.whiteTexture);GUI.color=color;}
        void OnApplicationFocus(bool focused){if(!focused){CancelWindowGesture();TextFocused=false;}}
        void OnDestroy(){SaveGeneralLayout();ReleasePortraits();foreach(var t in textures)if(t)Destroy(t);foreach(var p in previews.Values)if(p)Destroy(p);if(skin){if(skin.font)Destroy(skin.font);Destroy(skin);}}
    }
}
