using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meridian.Editor
{
    public static class PlanetValidation
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        [MenuItem("Meridian/Validate Planet Saved Assets")]
        public static void SavedAssets()
        {
            MeridianValidation.ValidateSavedScene();
            var scene=EditorSceneManager.OpenScene(PlanetSelectionAuthoring.ScenePath);
            var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
            Require(components.All(c=>c),"Missing planet script.");
            foreach(var component in components)
            {
                var property=new SerializedObject(component).GetIterator();
                while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)
                    Require(property.objectReferenceValue || property.objectReferenceEntityIdValue==default,"Missing reference: "+component.name+"/"+property.propertyPath);
            }
            var buttons=components.OfType<Button>().ToArray();
            Require(buttons.Length==2,"Expected only BACK and CONTINUE.");
            var back=buttons.Single(b=>b.GetComponentInChildren<TMP_Text>().text=="BACK");
            var next=buttons.Single(b=>b.GetComponentInChildren<TMP_Text>().text=="CONTINUE");
            Require(back.onClick.GetPersistentEventCount()==1 && back.onClick.GetPersistentMethodName(0)=="Back","Missing BACK action.");
            Require(!next.interactable && next.onClick.GetPersistentEventCount()==0,"CONTINUE must stay disabled and inert.");
            Require(components.OfType<Camera>().Count()==1 && components.OfType<EventSystem>().Count()==1,"Duplicate camera/EventSystem.");
            Require(Shader.Find("Meridian/Planet Surface")?.isSupported==true,"Planet shader unsupported.");
            EditorSceneManager.OpenScene(MeridianSetup.ScenePath);
            File.WriteAllText("Logs/PlanetSavedAssets.txt","PASS: two saved scenes, controls, references, shader, camera, EventSystem.\n");
        }

        [MenuItem("Meridian/Validate Planet Generation and Gestures")]
        public static async void GenerationAndGestures()
        {
            string path="Logs/PlanetGenerationValidation.txt";File.WriteAllText(path,"RUNNING\n");
            try
            {
                var settings=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset").Snapshot();
                var lines=await Task.Run(()=>CheckGeneration(settings));
                var gesture=new GlobeGesture();
                gesture.Press(Vector2.zero,true,false);gesture.Move(new Vector2(20,0),7);gesture.Move(Vector2.zero,7);
                Require(!gesture.Release(false),"Out-and-back drag selected.");
                gesture.Press(Vector2.zero,true,false);gesture.Move(new Vector2(2,0),7);Require(gesture.Release(false),"Stationary click rejected.");
                gesture.Press(Vector2.zero,true,true);Require(!gesture.Release(false),"UI-owned press selected.");
                gesture.Press(Vector2.zero,false,false);Require(!gesture.Release(false),"Background-owned press selected.");
                gesture.Press(Vector2.zero,true,false);Require(!gesture.Release(true),"Release over UI selected.");
                gesture.Press(Vector2.zero,true,false);gesture.Reset();Require(!gesture.Release(false),"Focus/capture reset selected.");
                lines.Add("PASS: gesture ownership, threshold, out-and-back, UI release and reset.");
                File.WriteAllLines(path,new[]{"PASS"}.Concat(lines));Debug.Log("Meridian generation and gesture validation passed.");
            }
            catch(Exception error){File.WriteAllText(path,"FAIL\n"+error);Debug.LogException(error);}
        }
        static List<string> CheckGeneration(PlanetParameters settings)
        {
            var lines=new List<string>();int[] seeds={73129,18041,90210,42817,61503};string first=null;
            foreach(int seed in seeds)
            {
                var data=PlanetGenerator.Generate(seed,settings);string hash=Hash(data);
                if(first==null)first=hash;else Require(hash!=first,"Different seeds repeated geography.");
                int[] biomes=new int[8],water=new int[4];int mountains=0,lakes=0,rivers=0;
                foreach(var pixel in data.SurfaceMap){water[pixel.r>=128?pixel.g:0]++;if(pixel.r<128)biomes[pixel.b]++;}
                for(int i=0;i<data.Elevation.Length;i++)
                {
                    if(data.Elevation[i]>.32f)mountains++;
                    if(data.Water[i]==PlanetWater.Lake)lakes++;
                    if(data.Water[i]==PlanetWater.River)rivers++;
                    int next=data.Downstream[i];
                    if(next>=0)Require(data.DrainageHeight[i]>data.DrainageHeight[next],"Drainage is cyclic/uphill on the filled surface.");
                }
                Require(data.Graph.Directions.Length==40962,"Unexpected geography budget.");
                Require(data.LandFraction>=.25f && data.LandFraction<=.45f,"Land coverage out of range.");
                foreach(var biome in new[]{PlanetBiome.Forest,PlanetBiome.Desert,PlanetBiome.Snow,PlanetBiome.Plains,PlanetBiome.Rock})
                    Require(biomes[(int)biome]>100,$"Seed {seed} lacks visible {biome}: {biomes[(int)biome]} pixels.");
                Require(water[2]>15 && water[3]>50 && mountains>0,$"Seed {seed} lacks lake/river/mountain examples: {water[2]}/{water[3]}/{mountains}.");
                for(int y=0;y<data.Height;y+=13)
                {
                    var a=data.Sample(PlanetData.Direction(0,(y+.5f)/data.Height));
                    var b=data.Sample(PlanetData.Direction(1,(y+.5f)/data.Height));
                    Require(a.Water==b.Water && Mathf.Abs(a.Elevation-b.Elevation)<.001f,"Longitude seam disagreement.");
                }
                for(int x=1;x<data.Width;x++)
                {
                    Require(data.SurfaceMap[x].Equals(data.SurfaceMap[0]),"South pole mismatch.");
                    Require(data.SurfaceMap[(data.Height-1)*data.Width+x].Equals(data.SurfaceMap[(data.Height-1)*data.Width]),"North pole mismatch.");
                }
                lines.Add($"Seed {seed}: {data.GenerationSeconds:F2}s, land {data.LandFraction:P1}; lake nodes {lakes}, river nodes {rivers}; water pixels {string.Join(",",water)}; biome pixels {string.Join(",",biomes)}; SHA256 {hash}");
            }
            Require(Hash(PlanetGenerator.Generate(seeds[0],settings))==first,"Fixed seed failed exact map reproducibility.");
            var cancelled=new CancellationToken(true);bool stopped=false;
            try{PlanetGenerator.Generate(seeds[0],settings,cancelled);}catch(OperationCanceledException){stopped=true;}
            Require(stopped,"Cancellation ignored.");lines.Add("PASS: exact fixed-seed maps, distinct seeds, drainage, feature coverage, seam/poles, cancellation.");
            return lines;
        }
        static string Hash(PlanetData data)
        {
            using(var sha=SHA256.Create())
            {
                var bytes=new byte[data.SurfaceMap.Length*4+data.ColorMap.Length*4];int i=0;
                foreach(var map in new[]{data.SurfaceMap,data.ColorMap})foreach(var p in map){bytes[i++]=p.r;bytes[i++]=p.g;bytes[i++]=p.b;bytes[i++]=p.a;}
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
            }
        }
        public static void BuildSmoke()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="Builds/Windows/Meridian.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.DetailedBuildReport,extraScriptingDefines=new[]{"MERIDIAN_SMOKE_TEST"}
            });
            File.WriteAllText("Logs/PlanetBuild.txt",$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nSeconds: {report.summary.totalTime.TotalSeconds:F2}\n");
            Require(report.summary.result==BuildResult.Succeeded,"Windows build failed.");
        }
    }
}
