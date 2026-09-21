using System;
using UnityEditor;
using UnityEngine;

namespace Meridian.Editor
{
    public static class ColonyBuildSupport
    {
        // Runtime-generated Terrains still require a serialized Terrain in a build scene:
        // https://docs.unity3d.com/6000.5/Documentation/Manual/terrain-Runtime.html
        [MenuItem("Meridian/Colony/Ensure Runtime Terrain Build Support")]
        public static void Ensure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first");
            const string path="Assets/_Meridian/Settings/TerrainBuildSupport.asset";
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if(!data){data=new TerrainData{name="Runtime terrain build support",heightmapResolution=33,size=Vector3.one};AssetDatabase.CreateAsset(data,path);}
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Meridian/Art/Materials/SurveyTerrain.mat");material.enableInstancing=true;EditorUtility.SetDirty(material);
            const string prefab="Assets/_Meridian/Prefabs/Surface/LandingSiteScreen.prefab";var root=PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var child=root.transform.Find("Runtime Terrain Build Support");
                if(!child){child=new GameObject("Runtime Terrain Build Support").transform;child.SetParent(root.transform,false);}
                child.gameObject.SetActive(false);var terrain=child.GetComponent<Terrain>();if(!terrain)terrain=child.gameObject.AddComponent<Terrain>();terrain.terrainData=data;terrain.materialTemplate=material;terrain.drawInstanced=true;
                PrefabUtility.SaveAsPrefabAsset(root,prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void Validate()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Meridian/Prefabs/Surface/LandingSiteScreen.prefab");
            var terrain=prefab.GetComponentInChildren<Terrain>(true);
            if(!terrain||terrain.gameObject.activeSelf||!terrain.terrainData||!terrain.materialTemplate.enableInstancing)
                throw new InvalidOperationException("The inactive runtime-terrain build support or instancing material is missing. Run Meridian > Colony > Ensure Runtime Terrain Build Support.");
        }
    }
}
