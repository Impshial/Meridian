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
            var parameters=settings.Snapshot();Require(parameters.heightmapResolution==513 && parameters.tileSize==1000 && parameters.initialTilesPerAxis==3,"Default 9 km2 survey tile budget changed");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Meridian/Art/Fonts/MeridianMenu_SDF.asset");
            Require((ScreenTransition.PlanetLoadingMessage+"Loading Landing Site...Landing site confirmed").All(c=>font.HasCharacter(c)),"Loading/confirmation typography missing glyphs");
            Require(((Material)root.FindProperty("terrainMaterial").objectReferenceValue).shader.isSupported,"Terrain shader unsupported");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/LandingSavedAssets.txt","PASS: 3 saved scenes, selected-region CONTINUE, landing screen/prefab references, 9x1km/513 defaults, loading/confirmation glyphs, shader.\n");
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
                    float maximum=view.Distance;if(aspect==16f/9)widest=maximum;
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
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/SurveyCameraBounds.txt",$"PASS: {poses} boundary poses, three aspect ratios/zoom distances, all eight headings and full tilt range; stable ground focus/zoom and ground-facing frustum inside survey. Near-edge initial focus retained. Widest 16:9 distance {widest:F2}m.\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
