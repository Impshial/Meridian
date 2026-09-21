using System;
using System.Reflection;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Meridian.Colony;

namespace Meridian.Editor
{
    public static partial class ColonyValidation
    {
        static async Task SizeGame(int width,int height)
        {
            const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var assembly=typeof(EditorWindow).Assembly;var type=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(type);var sizes=singleton.GetProperty("instance",all).GetValue(null);
            var group=type.GetMethod("GetGroup",all).Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),all,null,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"Meridian colony validation"},null);group.GetType().GetMethod("AddCustomSize",all).Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount",all).Invoke(group,null);var viewType=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);viewType.GetProperty("selectedSizeIndex",all).SetValue(view,count-1);view.Focus();view.Repaint();await Task.Delay(500);
            Require(Screen.width==width&&Screen.height==height,"Requested Game view size not active");
        }
        public static async void View1440()
        {
            try{Application.runInBackground=true;await SizeGame(2560,1440);FrameColony();var game=ColonyRuntime.Current;game.Simulation.State.speed=0;await Task.Delay(500);Capture();}catch(Exception error){UnityEngine.Debug.LogException(error);}
        }
        public static async void ViewInteriors()
        {
            try
            {
                var game=ColonyRuntime.Current;foreach(var w in game.Simulation.State.windows)w.open=false;
                var habitat=game.Simulation.State.structures.First(b=>b.definition=="habitat");game.Camera.Restore(new CameraState{pivot=habitat.position,yaw=-25,pitch=60,distance=58});
                if(!game.Visuals.Frames(habitat))game.Visuals.ToggleDome(habitat);game.UI.Inspect(habitat.id);await Task.Delay(500);ScreenCapture.CaptureScreenshot("Captures/Colony-Interiors.png");
            }catch(Exception error){UnityEngine.Debug.LogException(error);}
        }
    }
}
