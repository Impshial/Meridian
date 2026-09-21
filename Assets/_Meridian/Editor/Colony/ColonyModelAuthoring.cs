using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Meridian.Editor
{
    public static class ColonyModelAuthoring
    {
        [MenuItem("Meridian/Colony/Import Supplied Models")]
        public static void Import()
        {
            string output="Assets/_Meridian/Resources/Colony/Models";Directory.CreateDirectory(output);AssetDatabase.Refresh();
            string[] names={"ConstructionBot","MiningDrill","HabitatDome","PowerMachine","StorageModule","IndustrialRobot"};
            foreach(string name in names)
            {
                string directory="Assets/_Meridian/Art/Imported/"+name;string file=Directory.GetFiles(directory,"*.fbx",SearchOption.AllDirectories).Single().Replace('\\','/');
                var importer=(ModelImporter)AssetImporter.GetAtPath(file);importer.isReadable=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                var textures=Directory.GetFiles(directory,"*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jpeg",StringComparison.OrdinalIgnoreCase)).ToArray();
                Texture2D Texture(string key,bool normal=false)
                {
                    string path=textures.FirstOrDefault(p=>Path.GetFileName(p).IndexOf(key,StringComparison.OrdinalIgnoreCase)>=0)?.Replace('\\','/');if(path==null)return null;
                    var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;t.maxTextureSize=2048;t.sRGBTexture=!normal&&key=="basecolor";t.mipmapEnabled=true;t.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                string materialPath=output+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
                mat.SetTexture("_BaseMap",Texture("basecolor"));var normalMap=Texture("normal",true);mat.SetTexture("_BumpMap",normalMap);if(normalMap)mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.7f);mat.SetFloat("_Metallic",.3f);mat.SetFloat("_Smoothness",.35f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);
                var root=new GameObject(name);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(file));model.transform.SetParent(root.transform,false);
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=new Bounds();bool first=true;foreach(var renderer in renderers){if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);renderer.sharedMaterials=Enumerable.Repeat(mat,renderer.sharedMaterials.Length).ToArray();}
                float scale=1/Mathf.Max(.001f,bounds.size.x,bounds.size.z);model.transform.localScale*=scale;model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
                foreach(var collider in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
                var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,renderers)});lod.RecalculateBounds();PrefabUtility.SaveAsPrefabAsset(root,output+"/"+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();Debug.Log("Meridian: six supplied models normalized with PBR color/normal materials.");
        }
    }
}
