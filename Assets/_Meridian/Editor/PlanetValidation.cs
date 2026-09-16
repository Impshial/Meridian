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
            Require(buttons.All(b=>b.GetComponentInChildren<CanvasGroup>().alpha==0),"Saved planet controls contain a hover bracket before pointer entry.");
            var back=buttons.Single(b=>b.GetComponentInChildren<TMP_Text>().text=="BACK");
            var next=buttons.Single(b=>b.GetComponentInChildren<TMP_Text>().text=="CONTINUE");
            Require(back.onClick.GetPersistentEventCount()==1 && back.onClick.GetPersistentMethodName(0)=="Back","Missing BACK action.");
            Require(!next.interactable && next.onClick.GetPersistentEventCount()==1 && next.onClick.GetPersistentMethodName(0)=="Continue","CONTINUE must start disabled and invoke the landing survey when selected.");
            Require(components.OfType<Camera>().Count()==1 && components.OfType<EventSystem>().Count()==1,"Duplicate camera/EventSystem.");
            Require(Shader.Find("Meridian/Planet Surface")?.isSupported==true,"Planet shader unsupported.");
            var viewing=components.OfType<PlanetViewingInput>().Single();
            var zoom=new SerializedObject(viewing);
            Require(Mathf.Approximately(zoom.FindProperty("initialFraming").floatValue,.63f) &&
                Mathf.Approximately(zoom.FindProperty("minimumFraming").floatValue,.45f) &&
                Mathf.Approximately(zoom.FindProperty("maximumFraming").floatValue,2.1f),"Saved zoom must be 45% / 63% / 210%, including cropped close views.");
            Require(Mathf.Approximately(zoom.FindProperty("zoomPerTick").floatValue,.1f),"Saved proportional wheel rate changed.");
            foreach(var button in buttons)Require(button.GetComponent<Image>().color.a>=.5f,"Planet control needs local contrast over enlarged terrain.");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Meridian/Art/Materials/PlanetSurface.mat");
            foreach(string property in new[]{"_RippleStrength","_WaterDetailScale","_WaterSpeed","_WaterRoughness","_GlintStrength","_DepthContribution"})
                Require(material.HasProperty(property),"Missing water tuning property: "+property);
            var generation=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset");
            Require(generation.mapWidth==4096 && generation.Snapshot().mapWidth==4096,"Saved terrain maps must actually generate at 4096x2048.");
            EditorSceneManager.OpenScene(MeridianSetup.ScenePath);
            File.WriteAllText("Logs/PlanetSavedAssets.txt","PASS: three-scene flow, controls, references, shader, camera, EventSystem; selection-gated CONTINUE; 45/63/210% zoom, wheel rate, local contrast, six water controls; saved 4096x2048 terrain maps.\n");
        }

        [MenuItem("Meridian/Validate Regional Zoom Projection")]
        public static void ZoomProjection()
        {
            // Exercise the actual runtime projection function, including its authored defaults.
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var cameraObject=new GameObject("Projection validation camera",typeof(Camera));
            var globeObject=new GameObject("Projection validation globe",typeof(PlanetGlobe));
            var inputObject=new GameObject("Projection validation input",typeof(PlanetViewingInput));
            try
            {
                var camera=cameraObject.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-4);
                var input=inputObject.GetComponent<PlanetViewingInput>();input.Configure(camera,globeObject.GetComponent<PlanetGlobe>(),null);
                var type=typeof(PlanetViewingInput);
                Require(Mathf.Approximately((float)type.GetField("maximumFraming",flags).GetValue(input),2.1f),"New components retained obsolete close limit.");
                foreach(float framing in new[]{.45f,.63f,2.1f})
                {
                    type.GetField("targetFraming",flags).SetValue(input,framing);
                    type.GetField("currentFraming",flags).SetValue(input,framing);
                    type.GetMethod("ApplyFraming",flags).Invoke(input,null);
                    float measured=1.005f/Mathf.Sqrt(16-1.005f*1.005f)/Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
                    Require(Mathf.Abs(measured-framing)<.0001f,"Runtime projection does not reach requested diameter: "+framing);
                }
                Require(camera.transform.position==new Vector3(0,0,-4) && camera.transform.rotation==Quaternion.identity && globeObject.transform.localScale==Vector3.one,"Zoom moved camera or scaled globe.");
                File.WriteAllText("Logs/PlanetZoomValidation.txt","PASS: runtime projection reaches 45%, 63%, 210%; new component defaults; fixed camera transform and globe scale.\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(inputObject);UnityEngine.Object.DestroyImmediate(globeObject);UnityEngine.Object.DestroyImmediate(cameraObject);}
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
                var bytes=new byte[65536];int i=0;
                foreach(var map in new[]{data.SurfaceMap,data.ColorMap,data.NormalMap})foreach(var p in map)
                {
                    bytes[i++]=p.r;bytes[i++]=p.g;bytes[i++]=p.b;bytes[i++]=p.a;
                    if(i==bytes.Length){sha.TransformBlock(bytes,0,i,null,0);i=0;}
                }
                sha.TransformFinalBlock(bytes,0,i);
                return BitConverter.ToString(sha.Hash).Replace("-","");
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
