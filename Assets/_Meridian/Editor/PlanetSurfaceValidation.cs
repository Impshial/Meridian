using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Meridian.Editor
{
    public static class PlanetSurfaceValidation
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}

        [MenuItem("Meridian/Validate Terrain Resolution and Boundaries")]
        public static async void Validate()
        {
            const string path="Logs/PlanetSurfaceValidation.txt";File.WriteAllText(path,"RUNNING\n");
            try
            {
                var asset=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset");
                var defaults=ScriptableObject.CreateInstance<PlanetGenerationSettings>();
                Require(defaults.mapWidth==4096,"New settings restore low-resolution maps.");
                defaults.mapWidth=8192;Require(defaults.Snapshot().mapWidth==8192,"8K is silently clamped.");
                UnityEngine.Object.DestroyImmediate(defaults);
                CheckPatchEdges();
                var parameters=asset.Snapshot();
                string result=await Task.Run(()=>CheckMaps(parameters));
                File.WriteAllText(path,"PASS\n"+result);Debug.Log("Terrain resolution and boundary validation passed.");
            }
            catch(Exception error){File.WriteAllText(path,"FAIL\n"+error);Debug.LogException(error);}
        }

        static void CheckPatchEdges()
        {
            var method=typeof(PlanetGenerator).GetMethod("CurvedField",BindingFlags.Static|BindingFlags.NonPublic);
            var ab=new Vector3(.02f,.01f,0);var ga=new Vector3(40,-10,0);var gb=new Vector3(-20,30,0);
            for(int i=0;i<=20;i++)
            {
                var weights=new Vector3(i/20f,1-i/20f,0);
                float Sample(float third,Vector3 ac)=>(float)method.Invoke(null,new object[]{-.1f,.1f,third,ga,gb,Vector3.zero,ab,ac,weights});
                float a=Sample(-2,new Vector3(0,.02f,.01f)),b=Sample(3,new Vector3(0,-.02f,-.01f));
                Require(Mathf.Abs(a-b)<1e-6f,"Cubic boundary discontinuity at a shared graph edge.");
                Require(a>=-.100001f && a<=.100001f,"Cubic edge invented an extremum.");
            }
        }

        static string CheckMaps(PlanetParameters settings)
        {
            var high=PlanetGenerator.Generate(73129,settings);
            var coarse=settings;coarse.mapWidth=1024;var low=PlanetGenerator.Generate(73129,coarse);
            Require(high.Width==4096 && high.Height==2048,"Actual maps did not reach the 4K baseline.");
            foreach(var pair in new[]{(high.Elevation,low.Elevation),(high.Moisture,low.Moisture),(high.Temperature,low.Temperature),(high.DrainageHeight,low.DrainageHeight),(high.Flow,low.Flow)})
                Require(pair.Item1.SequenceEqual(pair.Item2),"Resolution changed physical/climate/drainage fields.");
            Require(high.Downstream.SequenceEqual(low.Downstream)&&high.Water.SequenceEqual(low.Water),"Resolution changed river routes/basins.");
            int transitions=0;var freezingLevels=new bool[256];float minimumNormal=2,maximumNormal=0;
            for(int i=0;i<high.SurfaceMap.Length;i++)
            {
                var p=high.SurfaceMap[i];Require(p.g>=1&&p.g<=3&&p.b<=7,"Invalid categorical ID.");
                if(p.r>32&&p.r<223)transitions++;
                if(p.r>=128)freezingLevels[p.a]=true;
                if((i&127)==0)
                {
                    var encoded=high.NormalMap[i];var n=new Vector3(encoded.r/255f*2-1,encoded.g/255f*2-1,encoded.b/255f*2-1);
                    minimumNormal=Mathf.Min(minimumNormal,n.magnitude);maximumNormal=Mathf.Max(maximumNormal,n.magnitude);
                    Vector3 direction=PlanetData.Direction((i%high.Width+.5f)/high.Width,(i/high.Width+.5f)/high.Height);
                    Require(Mathf.Abs(high.Sample(direction).Elevation-low.Sample(direction).Elevation)<1e-6f,"Appearance resolution changed placement elevation.");
                }
            }
            Require(transitions>1000,"Boundary field is still binary.");
            Require(freezingLevels.Count(value=>value)>240,"Freezing transition does not use the available precision.");
            Require(minimumNormal>.985f&&maximumNormal<1.015f,"Invalid encoded normals.");
            for(int i=0;i<high.Downstream.Length;i++)if(high.Water[i]==PlanetWater.River)
                Require(PlanetGenerator.RiverHalfWidth(high.Flow[i])>=2*Mathf.PI/1024*.9f,"River widths shrank with higher resolution.");
            using(var cancel=new CancellationTokenSource())
            {
                cancel.CancelAfter(100);bool stopped=false;
                try{PlanetGenerator.Generate(73129,settings,cancel.Token);}catch(OperationCanceledException){stopped=true;}
                Require(stopped,"Worker ignored cancellation.");
            }
            high.ReleaseAppearanceBuffers();Require(high.ColorMap==null&&high.NormalMap==null&&high.SurfaceMap!=null,"Upload staging buffers retained or selection discarded.");
            return $"4K/new defaults/8K clamp; shared cubic edge continuity; graph/climate/drainage invariance; placement elevations; {transitions} continuous boundary texels; normalized normals [{minimumNormal:F4},{maximumNormal:F4}]; resolution-independent rivers; cancellation; staging release.\n4K numeric generation: {high.GenerationSeconds:F2}s; 1K: {low.GenerationSeconds:F2}s.\n";
        }
    }
}
