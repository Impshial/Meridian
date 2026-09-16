using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Meridian.Editor
{
    /// <summary>One focused numeric smoke check. Larger biome and visual acceptance passes are intentionally manual.</summary>
    public static class SurfaceGenerationValidation
    {
        static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}

        [MenuItem("Meridian/Validate Surface Generation")]
        public static async void Validate()
        {
            const string path="Logs/SurfaceGenerationValidation.txt";
            Directory.CreateDirectory("Logs");File.WriteAllText(path,"RUNNING\n");
            try
            {
                var planetAsset=AssetDatabase.LoadAssetAtPath<PlanetGenerationSettings>("Assets/_Meridian/Settings/PlanetGeneration.asset");
                var surfaceAsset=AssetDatabase.LoadAssetAtPath<SurfaceGenerationSettings>("Assets/_Meridian/Settings/SurfaceGeneration.asset");
                bool temporary=surfaceAsset==null;if(temporary)surfaceAsset=ScriptableObject.CreateInstance<SurfaceGenerationSettings>();
                var planetParameters=planetAsset.Snapshot();planetParameters.mapWidth=1024;
                var surfaceParameters=surfaceAsset.Snapshot();if(temporary)UnityEngine.Object.DestroyImmediate(surfaceAsset);
                string report=await Task.Run(()=>Check(planetParameters,surfaceParameters));
                File.WriteAllText(path,"PASS\n"+report);Debug.Log("Surface generation numeric checks passed.");
            }
            catch(Exception error){File.WriteAllText(path,"FAIL\n"+error);Debug.LogException(error);}
        }

        static string Check(PlanetParameters planetSettings,SurfaceParameters settings)
        {
            var planet=PlanetGenerator.Generate(73129,planetSettings);planet.ReleaseAppearanceBuffers();
            Vector3 direction=default;float best=float.NegativeInfinity;
            // Pick one representative, broad inland region from authoritative samples, rather than relying on a brittle node ID.
            for(int i=0;i<planet.Graph.Directions.Length;i+=13)
            {
                if(planet.Water[i]!=PlanetWater.None || planet.Biomes[i]!=PlanetBiome.Forest)continue;
                var at=planet.Graph.Directions[i];var frame=new SurfaceFrame(at,settings.mappingRadius);bool dry=true;
                for(int z=-1;z<=1 && dry;z++)for(int x=-1;x<=1;x++)if(!planet.Sample(frame.Direction(x*1000,z*1000)).IsLand){dry=false;break;}
                if(!dry)continue;
                float score=planet.Moisture[i]-planet.Elevation[i];if(score<=best)continue;
                best=score;direction=at;
            }
            Require(direction.sqrMagnitude>.9f,"Fixed seed has no representative inland forest region.");
            var world=SurfaceGenerator.Generate(planet,direction,settings);
            Require(world.Tiles.Length==4 && world.Tiles.All(t=>t.Heights.GetLength(0)==settings.heightmapResolution),"Wrong initial terrain budget.");
            Require(world.BuildableArea>=settings.minimumBuildableArea && world.InteriorClearance>=settings.minimumInteriorSize,"Region bypassed measured flat-ground requirements.");
            float maximumAddedRelief=0,minimumHeight=float.PositiveInfinity,maximumHeight=float.NegativeInfinity;int visibleSlopes=0,gentleSamples=0,drySamples=0;
            for(float z=world.Bounds.yMin+25;z<world.Bounds.yMax;z+=50)for(float x=world.Bounds.xMin+25;x<world.Bounds.xMax;x+=50)
            {
                var sample=world.Sample(x,z);if(!sample.IsLand || sample.WaterDistance<140)continue;
                minimumHeight=Mathf.Min(minimumHeight,sample.Height);maximumHeight=Mathf.Max(maximumHeight,sample.Height);
                float slope=world.Slope(x,z);drySamples++;
                if(slope>12)visibleSlopes++;
                if(slope<=settings.buildableSlope)gentleSamples++;
                if((new Vector2(x,z)-world.PlainCentre).magnitude<settings.plainRadius+settings.plainBlend)continue;
                float sharedHeight=planet.Sample(world.Frame.Direction(x,z)).Elevation*settings.elevationScale;
                maximumAddedRelief=Mathf.Max(maximumAddedRelief,sample.Height-sharedHeight);
            }
            Require(maximumAddedRelief>settings.broadReliefHeight*.5f && maximumAddedRelief<settings.broadReliefHeight*2+settings.smallHillHeight*2,
                "Regional relief is missing or exceeds the gentler landform budget.");
            Require(gentleSamples>drySamples*.6f,"Representative inland region no longer has predominantly gentle terrain.");
            var trees=world.Objects.Where(o=>o.Kind==SurfaceObjectKind.Tree).ToArray();
            Require(trees.Length>4000 && world.TreeGroves.Length>1,"Default forest region is missing its denser timber groves.");
            Require(trees.All(t=>!string.IsNullOrEmpty(t.ResourceGroupId) && t.WoodAmount>0 && t.Radius>=3 && t.Radius<=5 && t.Height>=12 && t.Height<=20),"Tree dimensions or timber resource data are invalid.");
            Require(world.TreeGroves.Sum(g=>g.TreeCount)==trees.Length && world.TreeGroves.Sum(g=>g.WoodAmount)==trees.Sum(t=>t.WoodAmount),"Timber aggregate does not match its member trees.");
            Vector2 centre=new Vector2(world.DefaultLanding.logicalPosition.x,world.DefaultLanding.logicalPosition.z);
            for(int angle=0;angle<360;angle+=45)Require(LandingPlacement.Evaluate(world,centre,angle).Valid,"Default clearing cannot contain the rotated full footprint.");
            Require(!LandingPlacement.Evaluate(world,new Vector2(world.Bounds.xMax-2,centre.y),45).Valid,"Placement accepts a footprint outside the generated area.");
            var blocker=world.Objects.OrderBy(o=>(new Vector2(o.Position.x,o.Position.z)-world.PlainCentre).sqrMagnitude).FirstOrDefault();
            Require(blocker!=null,"Representative forest has no landing obstructions.");
            var blocked=LandingPlacement.Evaluate(world,new Vector2(blocker.Position.x,blocker.Position.z),0);
            Require(!blocked.Valid && blocked.Reason.Contains("obstructs"),"Obstacle rejection was masked by a different placement failure: "+blocked.Reason);

            SurfaceTileData Tile(int x,int z)=>world.Tiles.First(t=>t.Address==new Vector2Int(x,z));
            void EastBorder(SurfaceTileData left,SurfaceTileData right)
            {
                int n=left.Heights.GetLength(0);for(int z=0;z<n;z++)Require(left.Heights[z,n-1]==right.Heights[z,0],"East/west terrain border changed.");
            }
            void NorthBorder(SurfaceTileData south,SurfaceTileData north)
            {
                int n=south.Heights.GetLength(0);for(int x=0;x<n;x++)Require(south.Heights[n-1,x]==north.Heights[0,x],"North/south terrain border changed.");
            }
            EastBorder(Tile(-1,-1),Tile(0,-1));EastBorder(Tile(-1,0),Tile(0,0));
            NorthBorder(Tile(-1,-1),Tile(-1,0));NorthBorder(Tile(0,-1),Tile(0,0));
            var east=world.GenerateTile(new Vector2Int(1,0));
            var southeast=world.GenerateTile(new Vector2Int(1,-1));
            var repeated=world.GenerateTile(new Vector2Int(0,0));
            EastBorder(Tile(0,0),east);EastBorder(Tile(0,-1),southeast);NorthBorder(southeast,east);
            var original=Tile(0,0);int resolution=settings.heightmapResolution;
            for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)Require(original.Heights[z,x]==repeated.Heights[z,x],"Generation order altered an established height.");
            Require(original.Objects.Length==repeated.Objects.Length,"Regeneration altered object count.");
            for(int i=0;i<original.Objects.Length;i++)Require(original.Objects[i].Id==repeated.Objects[i].Id && original.Objects[i].Position==repeated.Objects[i].Position &&
                original.Objects[i].ResourceGroupId==repeated.Objects[i].ResourceGroupId && original.Objects[i].WoodAmount==repeated.Objects[i].WoodAmount && original.Objects[i].Height==repeated.Objects[i].Height,
                "Object identity, position, grove membership or timber quantity depends on generation order.");
            var ids=new HashSet<string>();
            foreach(var item in world.Objects.Concat(east.Objects).Concat(southeast.Objects))Require(ids.Add(item.Id),"Tile ownership duplicated a vegetation or resource object.");
            var extendedGroves=SurfaceGenerator.CollectTreeGroves(world,world.Objects.Concat(east.Objects).Concat(southeast.Objects));
            foreach(var grove in world.TreeGroves)
            {
                var expanded=extendedGroves.First(g=>g.Id==grove.Id);
                Require(expanded.Position==grove.Position && expanded.WoodAmount>=grove.WoodAmount,"Neighbor generation moved a timber grove or lost existing wood.");
            }
            Require(world.Tiles.Length==4,"Development neighbor generation mutated the active survey.");
            foreach(var pole in new[]{Vector3.up,Vector3.down,Vector3.back})
            {
                var frame=new SurfaceFrame(pole,settings.mappingRadius);var p=new Vector2(135,-417);var back=frame.Project(frame.Direction(p.x,p.y));
                Require((p-back).magnitude<.005f && Mathf.Abs(Vector3.Dot(frame.east,frame.north))<.00001f,"Polar/seam frame is unstable.");
            }
            using(var source=new CancellationTokenSource())
            {
                source.Cancel();bool cancelled=false;try{world.GenerateTile(new Vector2Int(3,0),source.Token);}catch(OperationCanceledException){cancelled=true;}
                Require(cancelled,"Surface tile generation ignored cancellation.");
            }
            return $"Seed 73129; validation-only 1024x512 planet maps, production surface {settings.heightmapResolution}x{settings.heightmapResolution} per tile.\n"+
                $"Region direction {direction}; {world.GenerationSeconds:F2}s initial numeric generation; {world.BuildableArea:F0} m2 connected buildable land; {world.InteriorClearance:F0}m fully usable square.\n"+
                $"{world.Objects.Length} stable objects; 4 active tiles plus 2 development-only neighbors; repeated tile matches all heights and object IDs/positions.\n"+
                $"Dry terrain height range {minimumHeight:F1}–{maximumHeight:F1}m; added relief up to {maximumAddedRelief:F1}m; {visibleSlopes} sampled slopes above 12 degrees; {gentleSamples}/{drySamples} samples at or below {settings.buildableSlope} degrees.\n"+
                $"{trees.Length} trees in {world.TreeGroves.Length} timber groves; {world.TreeGroves.Sum(g=>g.WoodAmount)} available wood units.\n"+
                "PASS: initial borders, extension borders/corner, regeneration after different tile order, unique ownership, timber aggregation and stable grove anchors, gentle relief, pole/seam projection, full footprint at 8 headings, bounds/obstruction rejection, cancellation.\n";
        }
    }
}
