using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    [Serializable] public sealed class ColonyGeneralLayout
    {public Vector4 docks=new Vector4(380,380,250,250);public List<WindowState> windows=new List<WindowState>();}
    public sealed partial class ColonyUI
    {
        string movingWindow,resizingWindow;Vector2 gestureStart;Rect gestureRect;int resizeEdges,dockPreview;bool movedWindow;
        ColonyGeneralLayout generalLayout;
        void ReadGeneralLayout()
        {
            try{generalLayout=JsonUtility.FromJson<ColonyGeneralLayout>(PlayerPrefs.GetString("Meridian.WindowLayout",""));}catch{generalLayout=null;}
            if(generalLayout==null||generalLayout.windows==null)generalLayout=new ColonyGeneralLayout();
            if(Windows.Count==0)DockSizes=generalLayout.docks;
        }
        void RestoreGeometry(WindowState window)
        {
            var saved=generalLayout?.windows.Find(w=>w.key==(window.key.StartsWith("@")?window.key:"$inspector"));
            if(saved!=null){window.rect=saved.rect;window.dock=Mathf.Clamp(saved.dock,0,4);if(window.dock==0&&!window.key.StartsWith("@")){int count=Windows.Count(w=>w.open&&!w.key.StartsWith("@"));window.rect.x+=count%5*30;window.rect.y+=count%5*24;}}
        }
        void SaveGeneralLayout()
        {
            if(generalLayout==null)return;generalLayout.docks=DockSizes;
            foreach(var window in Windows)
            {
                string key=window.key.StartsWith("@")?window.key:"$inspector";var saved=generalLayout.windows.Find(w=>w.key==key);
                if(saved==null){saved=new WindowState{key=key};generalLayout.windows.Add(saved);}saved.rect=window.rect;saved.dock=window.dock;
            }
            PlayerPrefs.SetString("Meridian.WindowLayout",JsonUtility.ToJson(generalLayout));PlayerPrefs.Save();
        }
        void ResetWindowLayout()
        {Windows.Clear();generalLayout=new ColonyGeneralLayout();DockSizes=generalLayout.docks;PlayerPrefs.DeleteKey("Meridian.WindowLayout");PlayerPrefs.Save();CancelWindowGesture();if(runtime)Open("Overview");else Open("Settings");}
        void CancelWindowGesture(){movingWindow=resizingWindow=null;resizeEdges=dockPreview=resizeId=splitDock=0;pointerCapture=false;}
        void StartMove(WindowState window,Vector2 point)
        {
            if(!GUI.enabled||Event.current.button!=0)return;
            movingWindow=window.key;gestureStart=point;gestureRect=new Rect(window.rect.x,window.rect.y,window.rect.z,window.rect.w);movedWindow=false;pointerCapture=true;activeWindow=window.key;Event.current.Use();
        }
        void WindowTitleGesture(WindowState window,Vector2 size,bool docked)
        {
            if(!GUI.enabled||Event.current.type!=EventType.MouseDown||Event.current.button!=0)return;
            Vector2 local=Event.current.mousePosition;
            if(!docked)
            {
                int edges=(local.x<7?1:local.x>size.x-7?2:0)|(local.y<6?4:local.y>size.y-7?8:0);
                if(local.x>size.x-24&&local.y>size.y-24)edges=2|8;
                if(edges!=0){resizingWindow=window.key;resizeEdges=edges;gestureStart=local+new Vector2(window.rect.x,window.rect.y);gestureRect=new Rect(window.rect.x,window.rect.y,window.rect.z,window.rect.w);resizeId=WindowID(window.key);pointerCapture=true;Event.current.Use();return;}
            }
            if(new Rect(8,7,size.x-135,31).Contains(local))
            {var area=docked?DockRect(window.dock):new Rect(window.rect.x,window.rect.y,window.rect.z,window.rect.w);StartMove(window,local+area.position+(docked?Vector2.up*39:Vector2.zero));}
        }
        int DockAt(Vector2 point)=>point.x<42?1:point.x>Width-42?2:point.y<118?3:point.y>Height-100?4:0;
        void HandleWindowGestures()
        {
            if(!GUI.enabled){CancelWindowGesture();return;}var e=Event.current;Vector2 point=e.mousePosition;
            var window=Windows.Find(w=>w.key==(resizingWindow??movingWindow));if(window==null){movingWindow=resizingWindow=null;return;}
            if(e.type==EventType.MouseDrag&&e.button==0)
            {
                Vector2 delta=point-gestureStart;
                if(resizingWindow!=null)
                {
                    var r=gestureRect;
                    if((resizeEdges&1)!=0)r.xMin=Mathf.Min(gestureRect.xMin+delta.x,gestureRect.xMax-330);
                    if((resizeEdges&2)!=0)r.xMax=Mathf.Max(gestureRect.xMax+delta.x,gestureRect.xMin+330);
                    if((resizeEdges&4)!=0)r.yMin=Mathf.Min(gestureRect.yMin+delta.y,gestureRect.yMax-230);
                    if((resizeEdges&8)!=0)r.yMax=Mathf.Max(gestureRect.yMax+delta.y,gestureRect.yMin+230);
                    r=Clamp(r);window.rect=new Vector4(r.x,r.y,r.width,r.height);
                }
                else if(delta.sqrMagnitude>36||movedWindow)
                {
                    if(window.dock!=0){window.dock=0;gestureRect=new Rect(point.x-200,point.y-22,Mathf.Max(400,gestureRect.width),Mathf.Max(350,gestureRect.height));gestureStart=point;delta=Vector2.zero;}
                    movedWindow=true;var r=Clamp(new Rect(gestureRect.position+delta,gestureRect.size));window.rect=new Vector4(r.x,r.y,r.width,r.height);dockPreview=DockAt(point);
                }
                e.Use();
            }
            if(e.rawType==EventType.MouseUp&&e.button==0)
            {if(movingWindow!=null&&movedWindow&&dockPreview>0)Dock(window,dockPreview);SaveGeneralLayout();CancelWindowGesture();e.Use();}
        }
        void DrawDockPreview()
        {
            if(dockPreview==0||movingWindow==null)return;var rect=DockRect(dockPreview);var old=GUI.color;GUI.color=new Color(1,.69f,.27f,.28f);GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;GUI.Label(new Rect(rect.x+20,rect.y+12,rect.width-40,36),"Release to dock",accent);
        }
    }
}
