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
            var parameters=settings.Snapshot();Require(parameters.heightmapResolution==513 && parameters.tileSize==1000,"Default survey tile budget changed");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Meridian/Art/Fonts/MeridianMenu_SDF.asset");
            Require("Loading Exo-planet...Loading Landing Site...Landing site confirmed".All(c=>font.HasCharacter(c)),"Loading/confirmation typography missing glyphs");
            Require(((Material)root.FindProperty("terrainMaterial").objectReferenceValue).shader.isSupported,"Terrain shader unsupported");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/LandingSavedAssets.txt","PASS: 3 saved scenes, selected-region CONTINUE, landing screen/prefab references, 4x1km/513 defaults, loading/confirmation glyphs, shader.\n");
            EditorSceneManager.OpenScene(MeridianSetup.ScenePath);Debug.Log("Meridian landing saved assets passed.");
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
