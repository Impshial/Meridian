using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Meridian.Editor
{
    public static class LandingSiteAuthoring
    {
        const string Root="Assets/_Meridian";
        public const string ScenePath=Root+"/Scenes/LandingSiteSelection.unity";
        [MenuItem("Meridian/Author Landing Site Selection")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring.");
            Directory.CreateDirectory(Root+"/Prefabs/Surface");AssetDatabase.Refresh();
            var settings=AssetDatabase.LoadAssetAtPath<SurfaceGenerationSettings>(Root+"/Settings/SurfaceGeneration.asset");
            if(!settings){settings=ScriptableObject.CreateInstance<SurfaceGenerationSettings>();AssetDatabase.CreateAsset(settings,Root+"/Settings/SurfaceGeneration.asset");}
            // Retain the established typeface and existing glyphs while supplying sentence-case UI.
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Art/Fonts/MeridianMenu_SDF.asset");
            font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            string characters=new string(Enumerable.Range(32,95).Select(c=>(char)c).ToArray())+"°²–—·";
            if(!font.TryAddCharacters(characters,out string missing) && !string.IsNullOrEmpty(missing))Debug.LogWarning("Missing survey glyphs: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;EditorUtility.SetDirty(font);

            string transitionPath=Root+"/Prefabs/UI/ScreenTransition.prefab";
            var transitionContents=PrefabUtility.LoadPrefabContents(transitionPath);
            try
            {
                var rootGroup=transitionContents.GetComponent<CanvasGroup>();if(rootGroup)rootGroup.alpha=1;
                var cover=transitionContents.GetComponentInChildren<Image>();
                var fade=cover.GetComponent<CanvasGroup>();if(!fade)fade=cover.gameObject.AddComponent<CanvasGroup>();fade.alpha=0;
                transitionContents.GetComponent<ScreenTransition>().Configure(fade,font);
                PrefabUtility.SaveAsPrefabAsset(transitionContents,transitionPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(transitionContents);}
            var transition=AssetDatabase.LoadAssetAtPath<GameObject>(transitionPath).GetComponent<ScreenTransition>();
            var terrain=Material("SurveyTerrain","Universal Render Pipeline/Terrain/Lit",Color.white);
            var water=Material("SurveyWater","Universal Render Pipeline/Lit",new Color(.065f,.26f,.30f,.82f));
            water.SetFloat("_Surface",1);water.SetFloat("_Blend",0);water.SetFloat("_ZWrite",0);
            water.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);water.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            water.SetFloat("_Smoothness",.72f);water.SetFloat("_Metallic",.05f);
            water.SetOverrideTag("RenderType","Transparent");water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");water.renderQueue=(int)RenderQueue.Transparent;
            EditorUtility.SetDirty(water);
            var ship=new GameObject("Dropship Landing Preview",typeof(DropshipPreview));
            var shipPrefab=PrefabUtility.SaveAsPrefabAsset(ship,Root+"/Prefabs/Surface/DropshipPreview.prefab");UnityEngine.Object.DestroyImmediate(ship);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var controller=new GameObject("Landing Site Selection",typeof(SurfaceSelectionController));
            controller.GetComponent<SurfaceSelectionController>().Configure(settings,transition,shipPrefab.GetComponent<DropshipPreview>(),terrain,water,
                AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/UI/MenuButton.prefab"));
            // Keep a reusable scene root with runtime camera/terrain/UI, whose references are saved.
            PrefabUtility.SaveAsPrefabAssetAndConnect(controller,Root+"/Prefabs/Surface/LandingSiteScreen.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene,ScenePath);
            var planet=EditorSceneManager.OpenScene(PlanetSelectionAuthoring.ScenePath);
            var planetController=UnityEngine.Object.FindAnyObjectByType<PlanetSelectionController>();
            var next=UnityEngine.Object.FindObjectsByType<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text=="CONTINUE");
            while(next.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(next.onClick,0);
            UnityEventTools.AddPersistentListener(next.onClick,planetController.Continue);next.interactable=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(next);
            EditorSceneManager.MarkSceneDirty(planet);EditorSceneManager.SaveScene(planet);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MeridianSetup.ScenePath,true),
                new EditorBuildSettingsScene(PlanetSelectionAuthoring.ScenePath,true),new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(MeridianSetup.ScenePath);
            Debug.Log("Meridian landing selection assets authored; existing menu, globe and water tuning preserved.");
        }
        static Material Material(string name,string shaderName,Color color)
        {
            string path=Root+"/Art/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Missing shader "+shaderName);material=new Material(shader){name=name};AssetDatabase.CreateAsset(material,path);}
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            EditorUtility.SetDirty(material);return material;
        }
    }
}
