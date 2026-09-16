using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Meridian.Editor
{
    /// <summary>Explicit milestone authoring. Only creates this milestone's assets and patches its menu entry.</summary>
    public static class PlanetSelectionAuthoring
    {
        const string Root="Assets/_Meridian";
        public const string ScenePath=Root+"/Scenes/PlanetSelection.unity";
        [MenuItem("Meridian/Author Planet Selection")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring.");
            foreach(string folder in new[]{"Prefabs/Planet","Prefabs/UI","Settings","Art/Materials","Art/Meshes"})Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var settings=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>(Root+"/Settings/PlanetGeneration.asset");
            if(!settings){settings=ScriptableObject.CreateInstance<PlanetGenerationSettings>();AssetDatabase.CreateAsset(settings,Root+"/Settings/PlanetGeneration.asset");}
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");
            int planetLayer=LayerMask.NameToLayer("PlanetSurface");
            if(planetLayer<0)
            {
                for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {layers.GetArrayElementAtIndex(i).stringValue="PlanetSurface";planetLayer=i;break;}
                if(planetLayer<0)throw new InvalidOperationException("No free interaction layer.");
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            var surface=Material("PlanetSurface","Meridian/Planet Surface",Color.white);
            var amber=Material("FlagAmber","Universal Render Pipeline/Unlit",new Color32(255,183,88,255));
            var pole=Material("FlagPole","Universal Render Pipeline/Lit",new Color32(220,218,199,255));
            var transition=CreateTransition();
            var flag=CreateFlag(amber,pole);

            var prototype=new GameObject("PlanetGlobe",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider),typeof(PlanetGlobe));
            prototype.layer=planetLayer;prototype.GetComponent<PlanetGlobe>().Configure(surface);
            var renderer=prototype.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var globePrefab=PrefabUtility.SaveAsPrefabAsset(prototype,Root+"/Prefabs/Planet/PlanetGlobe.prefab");
            UnityEngine.Object.DestroyImmediate(prototype);
            var globe=((GameObject)PrefabUtility.InstantiatePrefab(globePrefab,scene)).GetComponent<PlanetGlobe>();

            var cameraObject=new GameObject("Fixed Planet Camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,0,-4);camera.transform.rotation=Quaternion.identity;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.fieldOfView=45;
            camera.allowHDR=false;camera.allowMSAA=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var light=new GameObject("Fixed Orbital Key",typeof(Light)).GetComponent<Light>();
            light.type=LightType.Directional;light.color=new Color(1,.97f,.91f);light.intensity=1.1f;light.shadows=LightShadows.None;
            light.transform.rotation=Quaternion.Euler(25,-25,0);
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.15f,.17f,.20f);

            var ui=Rect("Planet Controls",null);ui.gameObject.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=ui.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            ui.gameObject.AddComponent<GraphicRaycaster>();var group=ui.gameObject.AddComponent<CanvasGroup>();
            var band=Rect("Bottom Controls",ui);band.anchorMin=Vector2.zero;band.anchorMax=new Vector2(1,0);
            band.pivot=new Vector2(.5f,0);band.sizeDelta=new Vector2(0,94);
            var back=Button("BACK",band,false);var next=Button("CONTINUE",band,true);next.interactable=false;
            back.navigation=new Navigation{mode=Navigation.Mode.None};next.navigation=new Navigation{mode=Navigation.Mode.None};
            Events(ui);
            var controller=new GameObject("Planet Selection",typeof(PlanetSelectionController),typeof(PlanetViewingInput));
            var selection=controller.GetComponent<PlanetSelectionController>();var viewing=controller.GetComponent<PlanetViewingInput>();
            viewing.Configure(camera,globe,selection);
            selection.Configure(settings,globe,flag,viewing,transition,group,back,next);
            UnityEventTools.AddPersistentListener(back.onClick,selection.Back);
            UnityEventTools.AddPersistentListener(next.onClick,selection.Continue);
            EditorSceneManager.SaveScene(scene,ScenePath);

            string menuPath=Root+"/Prefabs/UI/MainMenu.prefab";
            var menu=PrefabUtility.LoadPrefabContents(menuPath);
            try
            {
                var presentation=menu.GetComponent<MainMenuPresentation>();presentation.ConfigureTransition(transition);
                var entry=menu.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text=="NEW COLONY");
                while(entry.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(entry.onClick,0);
                UnityEventTools.AddPersistentListener(entry.onClick,presentation.OpenPlanetSelection);
                PrefabUtility.SaveAsPrefabAsset(menu,menuPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(menu);}
            var buildScenes=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(MeridianSetup.ScenePath,true),new EditorBuildSettingsScene(ScenePath,true)};
            if(File.Exists(LandingSiteAuthoring.ScenePath))buildScenes.Add(new EditorBuildSettingsScene(LandingSiteAuthoring.ScenePath,true));
            EditorBuildSettings.scenes=buildScenes.ToArray();
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(MeridianSetup.ScenePath);
            Debug.Log("Meridian planet selection assets authored and saved.");
        }
        static Material Material(string name,string shaderName,Color color)
        {
            string path=Root+"/Art/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Missing shader "+shaderName);
                material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);
            }
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            EditorUtility.SetDirty(material);return material;
        }
        static ScreenTransition CreateTransition()
        {
            var root=Rect("Screen Transition",null);var canvas=root.gameObject.AddComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var black=Rect("Black Cover",root);Stretch(black);black.gameObject.AddComponent<Image>().color=Color.black;
            var group=black.gameObject.AddComponent<CanvasGroup>();group.alpha=0;
            root.gameObject.AddComponent<ScreenTransition>().Configure(group,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Art/Fonts/MeridianMenu_SDF.asset"));
            var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"/Prefabs/UI/ScreenTransition.prefab");
            UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab.GetComponent<ScreenTransition>();
        }
        static PlanetFlag CreateFlag(Material amber,Material pole)
        {
            var root=new GameObject("Landing Flag",typeof(PlanetFlag));
            var visual=new GameObject("Planting Visual").transform;visual.SetParent(root.transform,false);
            var mast=GameObject.CreatePrimitive(PrimitiveType.Cylinder);mast.name="Pole";mast.transform.SetParent(visual,false);
            mast.transform.localPosition=Vector3.up*.5f;mast.transform.localScale=new Vector3(.028f,.5f,.028f);
            UnityEngine.Object.DestroyImmediate(mast.GetComponent<Collider>());mast.GetComponent<MeshRenderer>().sharedMaterial=pole;
            string path=Root+"/Art/Meshes/FlagPennant.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                mesh=new Mesh{name="Double Sided Pennant"};AssetDatabase.CreateAsset(mesh,path);
            }
            mesh.Clear();
            mesh.vertices=new[]{new Vector3(0,.94f,0),new Vector3(.62f,.78f,.05f),new Vector3(.20f,.64f,-.10f),new Vector3(0,.59f,0)};
            mesh.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var pennant=new GameObject("Amber Pennant",typeof(MeshFilter),typeof(MeshRenderer));pennant.transform.SetParent(visual,false);
            pennant.GetComponent<MeshFilter>().sharedMesh=mesh;pennant.GetComponent<MeshRenderer>().sharedMaterial=amber;
            root.GetComponent<PlanetFlag>().Configure(visual);root.SetActive(false);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Planet/LandingFlag.prefab");
            UnityEngine.Object.DestroyImmediate(root);return prefab.GetComponent<PlanetFlag>();
        }
        static Button Button(string title,Transform parent,bool right)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/UI/MenuButton.prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=title;
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(right?1:0,0);
            rect.pivot=new Vector2(right?1:0,.5f);rect.anchoredPosition=new Vector2(right?-65:65,55);
            var label=go.GetComponentInChildren<TMP_Text>();label.text=title;
            // Local contrast when the enlarged globe passes behind these overlay controls.
            var face=go.GetComponent<Image>();face.color=new Color(0,0,0,.62f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(face);
            go.GetComponent<MenuButtonVisual>().Configure(label,go.GetComponentInChildren<CanvasGroup>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            return go.GetComponent<Button>();
        }
        static void Events(Transform parent)
        {
            var go=new GameObject("EventSystem",typeof(EventSystem));go.SetActive(false);go.transform.SetParent(parent,false);
            var module=go.AddComponent<InputSystemUIInputModule>();
            module.point=module.move=module.leftClick=module.rightClick=module.middleClick=null;
            module.scrollWheel=module.submit=module.cancel=module.trackedDeviceOrientation=module.trackedDevicePosition=null;
            var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root+"/Input/MenuUI.inputactions");module.actionsAsset=actions;
            InputActionReference Ref(string name)=>AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(actions)).OfType<InputActionReference>().First(r=>r.action.name==name);
            module.point=Ref("Point");module.leftClick=Ref("Click");module.move=Ref("Navigate");module.submit=Ref("Submit");
            module.deselectOnBackgroundClick=false;go.SetActive(true);
        }
        static RectTransform Rect(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(parent)rect.SetParent(parent,false);return rect;}
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
