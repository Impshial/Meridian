using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Meridian.Editor
{
    public static class LandingSiteValidation
    {
        [MenuItem("Meridian/Validate Landing Site Saved Assets")]
        public static void SavedAssets()
        {
            PlanetValidation.SavedAssets();
            var scene=EditorSceneManager.OpenScene(LandingSiteAuthoring.ScenePath);
            var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
            Require(components.All(c=>c),"Missing landing scene script");
            Require(components.OfType<SurfaceSelectionController>().Count()==1,"Expected one landing screen");
            foreach(var component in components)
            {
                var property=new SerializedObject(component).GetIterator();
                while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)
                    Require(property.objectReferenceValue || property.objectReferenceEntityIdValue==default,"Missing landing reference "+property.propertyPath);
            }
            var root=new SerializedObject(components.OfType<SurfaceSelectionController>().Single());
            foreach(string field in new[]{"settings","transitionPrefab","previewPrefab","terrainMaterial","waterMaterial","menuButtonPrefab"})
                Require(root.FindProperty(field).objectReferenceValue,"Unassigned landing reference "+field);
            var settings=(SurfaceGenerationSettings)root.FindProperty("settings").objectReferenceValue;
            var parameters=settings.Snapshot();Require(parameters.heightmapResolution==513 && parameters.tileSize==1000 && parameters.initialTilesPerAxis==6,"Default 6x6 km survey tile budget changed");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Meridian/Art/Fonts/MeridianMenu_SDF.asset");
            Require((ScreenTransition.PlanetLoadingMessage+"Loading Landing Site...Landing site confirmed").All(c=>font.HasCharacter(c)),"Loading/confirmation typography missing glyphs");
            Require(((Material)root.FindProperty("terrainMaterial").objectReferenceValue).shader.isSupported,"Terrain shader unsupported");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/LandingSavedAssets.txt","PASS: 3 saved scenes, selected-region CONTINUE, landing screen/prefab references, 36x1km/513 defaults, loading/confirmation glyphs, shader.\n");
            EditorSceneManager.OpenScene(MeridianSetup.ScenePath);Debug.Log("Meridian landing saved assets passed.");
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

        [MenuItem("Meridian/Validate Survey Camera Bounds")]
        public static void CameraBounds()
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var root=new GameObject("Temporary survey projection check",typeof(Camera),typeof(SurveyCamera));
            try
            {
                var camera=root.GetComponent<Camera>();var view=root.GetComponent<SurveyCamera>();var type=typeof(SurveyCamera);
                void Set(string field,object value)=>type.GetField(field,flags).SetValue(view,value);
                void Apply()=>type.GetMethod("ApplyPose",flags).Invoke(view,null);
                float widest=0;int poses=0;
                float halfSize=AssetDatabase.LoadAssetAtPath<SurfaceGenerationSettings>("Assets/_Meridian/Settings/SurfaceGeneration.asset").Snapshot().initialTilesPerAxis*500;
                foreach(float aspect in new[]{16f/9,4f/3,5f/4})
                {
                    camera.aspect=aspect;view.Initialize(camera,new Rect(-halfSize,-halfSize,2*halfSize,2*halfSize),Vector3.zero,(x,z)=>0,null);
                    float maximum=(float)type.GetMethod("SupportedDistance",flags).Invoke(view,null);if(aspect==16f/9)widest=maximum;
                    foreach(float zoom in new[]{95f,240f,maximum})
                    foreach(float edgeX in new[]{-10000f,10000f})foreach(float edgeZ in new[]{-10000f,10000f})
                    {
                        Set("distance",zoom);Set("targetDistance",zoom);Set("pivot",new Vector3(edgeX,0,edgeZ));Apply();
                        Vector3 anchor=view.Pivot;
                        for(int heading=0;heading<360;heading+=45)for(int tilt=42;tilt<=78;tilt+=6)
                        {
                            Set("yaw",(float)heading);Set("pitch",(float)tilt);Apply();poses++;
                            Require((view.Pivot-anchor).sqrMagnitude<.000001f,"Tilting/turning moved the ground focus at a pan boundary.");
                            Require(Mathf.Abs(view.Distance-zoom)<.001f,"Tilting/turning changed zoom.");
                            var ground=new Plane(Vector3.up,Vector3.zero);
                            for(int x=0;x<=1;x++)for(int y=0;y<=1;y++)
                            {
                                Ray ray=camera.ViewportPointToRay(new Vector3(x,y));
                                Require(ground.Raycast(ray,out float travel),"Survey corner no longer faces the ground.");
                                Vector3 hit=ray.GetPoint(travel);
                                Require(Mathf.Abs(hit.x)<=halfSize && Mathf.Abs(hit.z)<=halfSize,"Survey corner crossed the generated terrain boundary.");
                            }
                        }
                    }
                    // Reset should frame a landing area near the survey edge without moving its focus.
                    var nearEdge=new Vector3(halfSize-350,0,-halfSize+350);
                    view.Initialize(camera,new Rect(-halfSize,-halfSize,2*halfSize,2*halfSize),nearEdge,(x,z)=>0,null);
                    Require(Vector2.Distance(new Vector2(view.Pivot.x,view.Pivot.z),new Vector2(nearEdge.x,nearEdge.z))<.001f,"Reset lost a landing area near the edge.");
                }
                camera.aspect=16f/9;view.Initialize(camera,new Rect(-halfSize,-halfSize,2*halfSize,2*halfSize),Vector3.zero,(x,z)=>0,null);
                Vector3 Motion(Vector2 input)=>(Vector3)type.GetMethod("ScreenMotion",flags).Invoke(view,new object[]{input});
                void Pan(Vector3 motion)=>type.GetMethod("Pan",flags).Invoke(view,new object[]{motion});
                for(int heading=0;heading<360;heading+=25)
                foreach(var axis in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})
                {
                    Set("yaw",(float)heading);Set("pivot",Vector3.zero);Apply();
                    Vector3 motion=Motion(axis),start=view.Pivot;
                    Vector3 screenStart=camera.WorldToScreenPoint(start),screenEnd=camera.WorldToScreenPoint(start+motion*10);
                    Vector2 screenDelta=new Vector2(screenEnd.x-screenStart.x,screenEnd.y-screenStart.y).normalized;
                    Require(Vector2.Dot(screenDelta,axis)>.9999f,"Pan direction does not follow a screen axis.");
                    Pan(motion*10000);Apply();Vector3 edge=view.Pivot,travel=edge-start;travel.y=0;
                    Require(Vector3.Dot(travel.normalized,motion)>.9999f,"Pan slid diagonally along a world boundary.");
                    Pan(motion*100);Apply();Require((view.Pivot-edge).sqrMagnitude<.001f,"Outward pan drifted along an angled boundary.");
                    Pan(-motion*10);Apply();Require(Vector3.Distance(view.Pivot,edge)>9.99f,"Reversing pan at a boundary is delayed.");
                }
                Set("yaw",335f);Set("targetYaw",380f);
                void Smooth(float dt)=>type.GetMethod("SmoothHeading",flags).Invoke(view,new object[]{dt});
                Smooth(1f/60);Require(view.Heading>335 && view.Heading<350,"Q/E heading snapped rather than interpolating.");
                Set("targetYaw",740f);float previous=view.Heading;
                for(int frame=0;frame<90;frame++){Smooth(1f/60);float current=view.Heading;Require(Mathf.DeltaAngle(previous,current)>=-.001f,"Accumulated turns reversed direction.");previous=current;}
                Require(Mathf.Abs(Mathf.DeltaAngle(view.Heading,20))<.001f,"Accumulated heading did not settle at the exact 45-degree target.");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/SurveyCameraBounds.txt",$"PASS: {poses} boundary poses, three aspect ratios/zoom distances, all eight headings and full tilt range; stable ground focus/zoom and ground-facing frustum inside survey. Screen-axis panning stops without diagonal boundary sliding and reverses immediately. Smooth accumulated turns cross 360 degrees and settle exactly. Near-edge initial focus retained. Widest 16:9 distance {widest:F2}m.\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
