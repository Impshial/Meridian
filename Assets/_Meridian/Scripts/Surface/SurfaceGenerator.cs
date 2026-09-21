using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Meridian
{
    /// <summary>Shared, order-independent numeric surface sampling. No Unity objects or global random state are touched.</summary>
    public static class SurfaceGenerator
    {
        public const string Version=SurfaceWorldData.GeneratorVersion;

        internal sealed class Geography
        {
            internal readonly int[][] Rivers;
            internal readonly Vector2[] Positions;
            internal readonly float[] LakeLevel;
            internal Geography(PlanetData planet,SurfaceFrame frame,SurfaceParameters settings)
            {
                int count=planet.Graph.Directions.Length;
                Positions=new Vector2[count];Rivers=new int[count][];LakeLevel=new float[count];
                for(int i=0;i<count;i++)Positions[i]=frame.Project(planet.Graph.Directions[i]);
                for(int i=0;i<count;i++)
                {
                    var candidates=new List<int>(7);
                    void Add(int node){if(planet.Water[node]==PlanetWater.River && planet.Downstream[node]>=0)candidates.Add(node);}
                    Add(i);foreach(int neighbour in planet.Graph.Neighbours[i])Add(neighbour);
                    Rivers[i]=candidates.ToArray();
                }
                // A lake is one connected filled basin, with one level shared by every sample and every tile.
                var visited=new bool[count];var queue=new Queue<int>();var component=new List<int>();
                for(int i=0;i<count;i++)
                {
                    if(visited[i] || planet.Water[i]!=PlanetWater.Lake)continue;
                    component.Clear();visited[i]=true;queue.Enqueue(i);float level=float.PositiveInfinity;
                    while(queue.Count>0)
                    {
                        int at=queue.Dequeue();component.Add(at);level=Mathf.Min(level,planet.DrainageHeight[at]);
                        foreach(int next in planet.Graph.Neighbours[at])if(!visited[next] && planet.Water[next]==PlanetWater.Lake)
                        {visited[next]=true;queue.Enqueue(next);}
                    }
                    foreach(int node in component)LakeLevel[node]=level*settings.elevationScale;
                }
            }
        }

        public static SurfaceWorldData Generate(PlanetData planet,Vector3 selectedDirection,SurfaceParameters parameters,CancellationToken cancellation=default,
            SurfaceLoadProgress progress=null,int workerCount=0,ColonySetupRecord saved=null)
        {
            if(planet==null)throw new ArgumentNullException(nameof(planet));
            if(selectedDirection.sqrMagnitude<.5f)throw new ArgumentException("A selected geographic direction is required.");
            cancellation.ThrowIfCancellationRequested();var timer=Stopwatch.StartNew();
            var world=new SurfaceWorldData(planet,saved!=null?saved.surfaceFrame:new SurfaceFrame(selectedDirection,parameters.mappingRadius),parameters);
            if(saved!=null){world.PlainCentre=saved.plainCentre;world.PlainHeight=saved.plainHeight;world.ShapePlain=saved.shapePlain;}else FindPlain(world,cancellation);
            progress?.Report(.02f,"Surveying buildable ground");
            int workers=workerCount>0?Math.Min(workerCount,4):Math.Max(1,Math.Min(4,Environment.ProcessorCount-2));
            var parallel=new ParallelOptions {CancellationToken=cancellation,MaxDegreeOfParallelism=workers};
            // Readiness is measured before allocating terrain arrays, so impossible regions fail cheaply.
            MeasureBuildable(world,cancellation,parallel,progress);
            int count=parameters.initialTilesPerAxis,first=world.MinimumTile;
            var tiles=new SurfaceTileData[count*count];var allObjects=new List<SurfaceObjectData>();int completed=0;
            progress?.Report(.12f,$"Generating terrain: 0 / {tiles.Length} tiles");
            // Each tile reads frozen geography and owns its output. Publish in address order, never completion order.
            Parallel.For(0,tiles.Length,parallel,index=>
            {
                tiles[index]=GenerateTile(world,new Vector2Int(first+index%count,first+index/count),cancellation);
                int done=Interlocked.Increment(ref completed);
                progress?.Report(.12f+.60f*done/tiles.Length,$"Generating terrain: {done} / {tiles.Length} tiles");
            });
            cancellation.ThrowIfCancellationRequested();
            foreach(var tile in tiles)allObjects.AddRange(tile.Objects);
            world.Tiles=tiles;world.Objects=allObjects.ToArray();
            world.TreeGroves=CollectTreeGroves(world,world.Objects);
            var landing=LandingPlacement.Evaluate(world,world.PlainCentre,0);
            if(!landing.Valid)
            {
                // The clearing is fixed geography. Search it, rather than editing terrain under a proposed ship.
                for(int radius=20;radius<=100 && !landing.Valid;radius+=20)for(int k=0;k<8 && !landing.Valid;k++)
                {
                    cancellation.ThrowIfCancellationRequested();float angle=k*Mathf.PI*.25f;
                    landing=LandingPlacement.Evaluate(world,world.PlainCentre+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*radius,k*45);
                }
            }
            if(!landing.Valid)throw new SurfaceSurveyException("This region has no clear, level deployment footprint. Return to the planet and choose another region.");
            world.DefaultLanding=landing.Candidate;timer.Stop();world.GenerationSeconds=timer.Elapsed.TotalSeconds;
            progress?.Report(.74f,"Preparing ground materials");
            return world;
        }

        static void FindPlain(SurfaceWorldData world,CancellationToken token)
        {
            float best=float.NegativeInfinity;Vector2 selected=default;float selectedHeight=0;
            float radius=world.Parameters.plainRadius,limit=world.Bounds.width*.5f-radius-80;
            // Stable coarse search, independent of tile order or the survey camera. Water is never moved or filled.
            for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++)
            {
                token.ThrowIfCancellationRequested();Vector2 centre=new Vector2(x,z)*(limit/4);
                float low=float.PositiveInfinity,high=float.NegativeInfinity,clearance=float.PositiveInfinity,total=0;int samples=0;bool dry=true;
                for(int iz=-3;iz<=3 && dry;iz++)for(int ix=-3;ix<=3;ix++)
                {
                    Vector2 delta=new Vector2(ix,iz)*(radius/3);
                    if(delta.sqrMagnitude>radius*radius)continue;
                    var sample=Sample(world,centre.x+delta.x,centre.y+delta.y);
                    if(!sample.IsLand || sample.WaterDistance<35){dry=false;break;}
                    low=Mathf.Min(low,sample.Height);high=Mathf.Max(high,sample.Height);total+=sample.Height;samples++;
                    clearance=Mathf.Min(clearance,sample.WaterDistance);
                }
                if(!dry)continue;
                float score=Mathf.Min(clearance,300)*.10f-(high-low)*.6f-centre.magnitude*.025f;
                if(score<=best)continue;
                best=score;selected=centre;selectedHeight=total/samples;
            }
            if(float.IsNegativeInfinity(best))throw new SurfaceSurveyException("The selected region is too narrow or divided by water for a safe colony plain. Return to the planet and choose nearby land.");
            world.PlainCentre=selected;world.PlainHeight=selectedHeight+world.Parameters.colonyPlateauHeight;world.ShapePlain=true;
        }

        internal static SurfaceSample Sample(SurfaceWorldData world,float x,float z)
        {
            var planet=world.Planet;var p=world.Parameters;var geo=world.Geography;
            Vector3 direction=world.Frame.Direction(x,z);
            planet.Graph.Locate(direction,out int a,out int b,out int c,out Vector3 weight);
            float Blend(float[] field)=>field[a]*weight.x+field[b]*weight.y+field[c]*weight.z;
            float elevation=Blend(planet.Elevation),moisture=Blend(planet.Moisture),temperature=Blend(planet.Temperature);
            var biome=temperature<.19f || elevation>.68f?PlanetBiome.Snow:elevation>.32f?PlanetBiome.Rock:
                temperature>.42f && moisture<.37f?PlanetBiome.Desert:temperature>.25f && moisture>.48f?PlanetBiome.Forest:PlanetBiome.Plains;

            ReadBoundary(planet,direction,out float signedDistance,out PlanetWater mappedKind);
            signedDistance*=p.mappingRadius;
            float waterLevel=0,coastDistance=10000;
            PlanetWater water=PlanetWater.None;
            if(mappedKind==PlanetWater.Ocean || mappedKind==PlanetWater.Lake)
            {
                coastDistance=signedDistance;
                if(mappedKind==PlanetWater.Lake)
                {
                    float nearest=float.PositiveInfinity;float level=0;
                    void Lake(int node)
                    {
                        if(planet.Water[node]!=PlanetWater.Lake)return;
                        float distance=(geo.Positions[node]-new Vector2(x,z)).sqrMagnitude;
                        if(distance<nearest){nearest=distance;level=geo.LakeLevel[node];}
                    }
                    Lake(a);Lake(b);Lake(c);
                    foreach(int n in planet.Graph.Neighbours[a])Lake(n);
                    foreach(int n in planet.Graph.Neighbours[b])Lake(n);
                    foreach(int n in planet.Graph.Neighbours[c])Lake(n);
                    waterLevel=level;
                }
                if(coastDistance<=0)water=mappedKind;
            }

            float riverDistance=10000,riverLevel=0;
            Vector2 point=new Vector2(x,z);
            void River(int node)
            {
                int next=planet.Downstream[node];Vector2 start=geo.Positions[node],end=geo.Positions[next];
                if(float.IsInfinity(start.x) || float.IsInfinity(end.x))return;
                Vector2 edge=end-start;float t=Mathf.Clamp01(Vector2.Dot(point-start,edge)/Mathf.Max(.01f,edge.sqrMagnitude));
                float width=Mathf.Lerp(p.minimumRiverWidth,p.maximumRiverWidth,Mathf.Clamp01(Mathf.Log(1+planet.Flow[node]/60)/6));
                float distance=(point-(start+edge*t)).magnitude-width*.5f;
                if(distance>=riverDistance)return;
                riverDistance=distance;
                riverLevel=Mathf.Lerp(planet.DrainageHeight[node],planet.DrainageHeight[next],t)*p.elevationScale;
            }
            foreach(int node in geo.Rivers[a])River(node);
            foreach(int node in geo.Rivers[b])River(node);
            foreach(int node in geo.Rivers[c])River(node);
            // Orbital river bands are symbols. Only authoritative drainage routes create physical channels here.
            if(water==PlanetWater.None && riverDistance<coastDistance)
            {waterLevel=riverLevel;if(riverDistance<=0)water=PlanetWater.River;}
            float waterDistance=Mathf.Min(coastDistance,riverDistance);
            float broad=elevation*p.elevationScale;
            // Hundreds-of-metres landforms create readable silhouettes and ramps. Small noisy height variations
            // used to reject otherwise flat-looking footprints, so sub-ship-scale roughness is deliberately tiny.
            float relief=Landforms(world,x,z)*(biome==PlanetBiome.Rock?1.35f:biome==PlanetBiome.Desert?.75f:1);
            float fine=Noise(world.Seed,x/160,z/160,2)*.12f;
            float height=broad+(relief+fine)*Smooth(20,140,waterDistance);
            if(world.ShapePlain)
            {
                float plain=1-Smooth(p.plainRadius,p.plainRadius+p.plainBlend,(point-world.PlainCentre).magnitude);
                plain*=Smooth(20,90,waterDistance);
                float target=world.PlainHeight+Noise(world.Seed,x/180,z/180,4)*.10f;
                height=Mathf.Lerp(height,target,plain);
            }
            // Smaller landforms survive outside the protected building core, including on the wider
            // plateau shoulders. Their compact support leaves level gaps instead of noisy rough ground.
            float coreClearance=world.ShapePlain?Smooth(p.plainRadius,p.plainRadius+40,(point-world.PlainCentre).magnitude):1;
            height+=SmallHills(world,x,z)*coreClearance*Smooth(15,90,waterDistance);
            if(mappedKind==PlanetWater.Ocean || mappedKind==PlanetWater.Lake)
            {
                // Follow the same smoothed mapped shoreline, including a gentle shore profile on its dry side.
                float shoreHeight=waterLevel+Mathf.Max(.05f,coastDistance*.18f);
                height=Mathf.Lerp(shoreHeight,height,Smooth(0,120,coastDistance));
            }
            if(riverDistance<35 && water!=PlanetWater.Ocean && water!=PlanetWater.Lake)
            {
                float bank=riverLevel+Mathf.Max(.05f,riverDistance*.18f);
                height=Mathf.Lerp(bank,height,Smooth(0,35,riverDistance));
            }
            if(water!=PlanetWater.None)height=waterLevel-Mathf.Clamp(-waterDistance*.20f+.8f,.8f,65);
            height=Mathf.Clamp(height,p.minimumTerrainHeight+1,p.minimumTerrainHeight+p.terrainHeightRange-1);
            return new SurfaceSample(height,waterLevel,moisture,temperature,waterDistance,water,biome);
        }

        static void ReadBoundary(PlanetData data,Vector3 direction,out float distance,out PlanetWater candidate)
        {
            Vector2 uv=PlanetData.Coordinates(direction);float x=uv.x*data.Width-.5f,y=uv.y*data.Height-.5f;
            int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
            int At(int xx,int yy)=>Mathf.Clamp(yy,0,data.Height-1)*data.Width+(xx%data.Width+data.Width)%data.Width;
            float r=Mathf.Lerp(Mathf.Lerp(data.SurfaceMap[At(ix,iy)].r,data.SurfaceMap[At(ix+1,iy)].r,x-ix),
                Mathf.Lerp(data.SurfaceMap[At(ix,iy+1)].r,data.SurfaceMap[At(ix+1,iy+1)].r,x-ix),y-iy)/255f;
            candidate=(PlanetWater)Mathf.Clamp(data.SurfaceMap[At(Mathf.FloorToInt(x+.5f),Mathf.FloorToInt(y+.5f))].g,1,3);
            distance=(.5f-r)*(2*PlanetData.BoundaryBand);
        }

        internal static SurfaceTileData GenerateTile(SurfaceWorldData world,Vector2Int address,CancellationToken token)
        {
            var p=world.Parameters;int resolution=p.heightmapResolution,segments=resolution-1;
            float step=p.tileSize/segments;
            var tile=new SurfaceTileData {Address=address,Origin=world.TileOrigin(address),Size=p.tileSize,
                MinHeight=p.minimumTerrainHeight,HeightRange=p.terrainHeightRange,Heights=new float[resolution,resolution]};
            // Integer logical sample coordinates make both copies of a shared border bit-identical.
            int waterResolution=segments/2+1;var waterSamples=new SurfaceSample[waterResolution,waterResolution];tile.WaterClearance=new float[waterResolution,waterResolution];
            for(int z=0;z<resolution;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<resolution;x++)
                {
                    float wx=(address.x*segments+x)*step-world.TileOriginOffset,wz=(address.y*segments+z)*step-world.TileOriginOffset;
                    SurfaceSample sample=Sample(world,wx,wz);tile.Heights[z,x]=(sample.Height-p.minimumTerrainHeight)/p.terrainHeightRange;
                    if((x&1)==0 && (z&1)==0){waterSamples[z/2,x/2]=sample;tile.WaterClearance[z/2,x/2]=sample.WaterDistance;}
                }
            }
            int alpha=p.alphamapResolution;tile.Layers=new float[alpha,alpha,5];
            for(int z=0;z<alpha;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<alpha;x++)
                {
                    float wx=tile.Origin.x+(x+.5f)*p.tileSize/alpha,wz=tile.Origin.y+(z+.5f)*p.tileSize/alpha;
                    SurfaceSample sample=Sample(world,wx,wz);
                    float snow=1-Smooth(.13f,.24f,sample.Temperature),sand=Smooth(.38f,.60f,sample.Temperature)*(1-Smooth(.30f,.43f,sample.Moisture));
                    float slope=world.Slope(wx,wz),rock=Smooth(12,29,slope)*.88f;
                    if(sample.Biome==PlanetBiome.Rock)rock=Mathf.Max(rock,.6f);
                    float soilPatch=Smooth(-.30f,.60f,Noise(world.Seed,wx/85,wz/85,93))*.35f*(1-snow)*(1-sand)*(1-rock);
                    float wet=Mathf.Max((1-Smooth(0,15,sample.WaterDistance))*.8f,soilPatch);
                    float grass=Mathf.Max(.03f,(1-snow)*(1-sand)*(1-rock));
                    float sum=grass+sand+rock+snow+wet;
                    tile.Layers[z,x,0]=grass/sum;tile.Layers[z,x,1]=sand/sum;tile.Layers[z,x,2]=rock/sum;
                    tile.Layers[z,x,3]=snow/sum;tile.Layers[z,x,4]=wet/sum;
                }
            }
            tile.Water=WaterMesh(tile.Origin,p.tileSize,waterSamples,token);
            tile.Objects=Objects(world,address,token);
            return tile;
        }

        struct WaterVertex
        {
            public Vector3 position;
            public float distance;
            public WaterVertex(Vector3 p,float d){position=p;distance=d;}
        }

        static SurfaceWaterMesh WaterMesh(Vector2 origin,float size,SurfaceSample[,] samples,CancellationToken token)
        {
            int n=samples.GetLength(0);float spacing=size/(n-1);var vertices=new List<Vector3>();var triangles=new List<int>();
            var input=new WaterVertex[3];var output=new WaterVertex[4];
            WaterVertex Vertex(int x,int z)
            {
                var sample=samples[z,x];return new WaterVertex(new Vector3(origin.x+x*spacing,sample.WaterHeight+.06f,origin.y+z*spacing),sample.WaterDistance);
            }
            void Add(WaterVertex a,WaterVertex b,WaterVertex c)
            {
                input[0]=a;input[1]=b;input[2]=c;int count=0;
                for(int i=0;i<3;i++)
                {
                    WaterVertex from=input[i],to=input[(i+1)%3];bool insideFrom=from.distance<=0,insideTo=to.distance<=0;
                    if(insideFrom)output[count++]=from;
                    if(insideFrom!=insideTo)
                    {
                        float t=from.distance/(from.distance-to.distance);
                        output[count++]=new WaterVertex(Vector3.Lerp(from.position,to.position,t),0);
                    }
                }
                if(count<3)return;
                int first=vertices.Count;for(int i=0;i<count;i++)vertices.Add(output[i].position);
                for(int i=1;i<count-1;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}
            }
            for(int z=0;z<n-1;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<n-1;x++)
                {
                    var a=Vertex(x,z);var b=Vertex(x+1,z);var c=Vertex(x,z+1);var d=Vertex(x+1,z+1);
                    Add(a,c,b);Add(b,c,d);
                }
            }
            return new SurfaceWaterMesh {Vertices=vertices.ToArray(),Triangles=triangles.ToArray()};
        }

        static SurfaceObjectData[] Objects(SurfaceWorldData world,Vector2Int address,CancellationToken token)
        {
            var result=new List<SurfaceObjectData>();var p=world.Parameters;float cell=p.objectCellSize;
            Vector2 origin=world.TileOrigin(address);
            int minX=Mathf.FloorToInt(origin.x/cell)-1,maxX=Mathf.CeilToInt((origin.x+p.tileSize)/cell)+1;
            int minZ=Mathf.FloorToInt(origin.y/cell)-1,maxZ=Mathf.CeilToInt((origin.y+p.tileSize)/cell)+1;
            for(int z=minZ;z<=maxZ;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=minX;x<=maxX;x++)
                {
                    float wx=(x+.15f+.7f*Random01(world.Seed,x,z,10))*cell,wz=(z+.15f+.7f*Random01(world.Seed,x,z,11))*cell;
                    var owner=world.TileOwner(new Vector2(wx,wz));
                    if(owner!=address || (new Vector2(wx,wz)-world.PlainCentre).sqrMagnitude<90*90)continue;
                    var sample=Sample(world,wx,wz);if(!sample.IsLand || sample.WaterDistance<6 || world.Slope(wx,wz)>34)continue;
                    float roll=Random01(world.Seed,x,z,12);SurfaceObjectKind kind;float scale,radius;
                    if(roll<.0045f)
                    {
                        kind=sample.Biome==PlanetBiome.Snow?SurfaceObjectKind.Ice:Random01(world.Seed,x,z,13)>.5f?SurfaceObjectKind.Iron:SurfaceObjectKind.Copper;
                        scale=Mathf.Lerp(1,1.8f,Random01(world.Seed,x,z,14));radius=2*scale;
                    }
                    else if(roll<.075f || (sample.Biome==PlanetBiome.Rock && roll<.32f))
                    {kind=SurfaceObjectKind.Rock;scale=Mathf.Lerp(1,3,Random01(world.Seed,x,z,14));radius=scale;}
                    else continue;
                    result.Add(new SurfaceObjectData {Id=$"{world.RegionId}:{x}:{z}:{(int)kind}",Owner=owner,Kind=kind,
                        Position=new Vector3(wx,sample.Height,wz),Radius=radius,Scale=scale,Yaw=Random01(world.Seed,x,z,15)*360});
                }
            }
            ScatterTrees(world,address,result,token);
            return result.ToArray();
        }

        static float SmallHills(SurfaceWorldData world,float x,float z)
        {
            float spacing=world.Parameters.smallHillSpacing,result=0;
            int cx=Mathf.FloorToInt(x/spacing),cz=Mathf.FloorToInt(z/spacing);
            for(int iz=cz-1;iz<=cz+1;iz++)for(int ix=cx-1;ix<=cx+1;ix++)
            {
                uint h=Hash(world.Seed,ix,iz,71),k=Hash(world.Seed,ix,iz,72);
                float Unit(uint value)=>(value&1023)/1023f;
                float px=(ix+.5f+(Unit(h)-.5f)*.28f)*spacing,pz=(iz+.5f+(Unit(h>>10)-.5f)*.28f)*spacing;
                float radius=spacing*Mathf.Lerp(.35f,.48f,Unit(h>>20));
                float dx=(x-px)/radius,dz=(z-pz)/(radius*Mathf.Lerp(.80f,1.15f,Unit(k)));
                float q=dx*dx+dz*dz;if(q>=1)continue;
                // Smooth zero slope at the summit and foot; no biome gate, so dry regions all get hills.
                float falloff=1-q;
                result+=world.Parameters.smallHillHeight*Mathf.Lerp(.70f,1.25f,Unit(k>>10))*falloff*falloff;
            }
            return result;
        }

        static float Landforms(SurfaceWorldData world,float x,float z)
        {
            var p=world.Parameters;float spacing=p.landformSpacing;
            int cellX=Mathf.FloorToInt(x/spacing),cellZ=Mathf.FloorToInt(z/spacing);float result=0;
            for(int iz=cellZ-1;iz<=cellZ+1;iz++)for(int ix=cellX-1;ix<=cellX+1;ix++)
            {
                uint h=Hash(world.Seed,ix,iz,41),k=Hash(world.Seed,ix,iz,42);
                float Unit(uint value)=>(value&1023)/1023f;
                float cx=(ix+.5f+(Unit(h)-.5f)*.22f)*spacing,cz=(iz+.5f+(Unit(h>>10)-.5f)*.22f)*spacing;
                float radius=spacing*Mathf.Lerp(.46f,.63f,Unit(h>>20));
                float dx=x-cx,dz=z-cz,squared=dx*dx+dz*dz;
                if(squared>=radius*radius)continue;
                float q=Mathf.Sqrt(squared)/radius;
                float height=p.broadReliefHeight*Mathf.Lerp(.72f,1.20f,Unit(k));
                // Both profiles have zero derivative at their top and foot. Shelves have broad flat summits;
                // intervening hollows remain open, and overlap blends through addition rather than hard ridges.
                bool shelf=Unit(k>>10)<.44f;
                float weight=shelf?1-Smooth(.36f,1,q):1-Smooth(0,1,q);
                result+=height*weight;
            }
            return result;
        }

        const float GroveEdge=1.18f;
        static int GroveSeed(SurfaceWorldData world,Vector2Int cell,int index)=>unchecked((int)Hash(world.Seed,cell.x,cell.y,120+index));

        static void GroveShape(SurfaceWorldData world,Vector2Int cell,int index,out Vector2 centre,out Vector2 axes,out float angle)
        {
            var p=world.Parameters;int seed=GroveSeed(world,cell,index);
            centre=new Vector2(cell.x+Random01(seed,0,0,61),cell.y+Random01(seed,0,0,62))*p.groveSpacing;
            float radius=p.groveRadius*Mathf.Lerp(.70f,1.40f,Random01(seed,0,0,63));
            axes=new Vector2(radius*Mathf.Lerp(1,1.4f,Random01(seed,0,0,65)),radius*Mathf.Lerp(.6f,1,Random01(seed,0,0,66)));
            angle=Random01(seed,0,0,67)*Mathf.PI*2;
        }

        static void ScatterTrees(SurfaceWorldData world,Vector2Int address,List<SurfaceObjectData> result,CancellationToken token)
        {
            var p=world.Parameters;Vector2 origin=world.TileOrigin(address);
            // Cells index an unbounded seeded point process, not one grove per grid square. Each cell
            // can contribute zero to three fully randomized centres, with overlapping irregular outlines.
            float margin=p.groveRadius*2.32f;
            int minX=Mathf.FloorToInt((origin.x-margin)/p.groveSpacing),maxX=Mathf.FloorToInt((origin.x+p.tileSize+margin)/p.groveSpacing);
            int minZ=Mathf.FloorToInt((origin.y-margin)/p.groveSpacing),maxZ=Mathf.FloorToInt((origin.y+p.tileSize+margin)/p.groveSpacing);
            float separation=p.treeSpacing*.42f,separationSquared=separation*separation;
            for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)for(int groupIndex=0;groupIndex<3;groupIndex++)
            {
                token.ThrowIfCancellationRequested();var cell=new Vector2Int(x,z);int seed=GroveSeed(world,cell,groupIndex);
                if(Random01(seed,0,0,64)>.45f)continue;
                GroveShape(world,cell,groupIndex,out Vector2 centre,out Vector2 axes,out float angle);
                float extent=Mathf.Max(axes.x,axes.y)*GroveEdge;
                var nearest=new Vector2(Mathf.Clamp(centre.x,origin.x,origin.x+p.tileSize),Mathf.Clamp(centre.y,origin.y,origin.y+p.tileSize));
                if((nearest-centre).sqrMagnitude>extent*extent)continue;
                var centreSample=Sample(world,centre.x,centre.y);
                if(!centreSample.IsLand || Random01(seed,0,0,68)>=TreePresence(centreSample))continue;
                float cosine=Mathf.Cos(angle),sine=Mathf.Sin(angle);
                int attempts=Mathf.CeilToInt(Mathf.PI*axes.x*axes.y*GroveEdge*GroveEdge/(p.treeSpacing*p.treeSpacing)*1.8f);
                string groupId=GroveId(world,cell,groupIndex);
                var occupied=new Dictionary<Vector2Int,List<Vector2>>();
                for(int i=0;i<attempts;i++)
                {
                    if((i&63)==0)token.ThrowIfCancellationRequested();
                    float radius=Mathf.Sqrt(Random01(seed,i,0,80))*GroveEdge,azimuth=Random01(seed,i,0,81)*Mathf.PI*2;
                    float dx=Mathf.Cos(azimuth)*radius*axes.x,dz=Mathf.Sin(azimuth)*radius*axes.y;
                    Vector2 point=centre+new Vector2(dx*cosine-dz*sine,dx*sine+dz*cosine);
                    float edge=1+Noise(seed,point.x/65,point.y/65,85)*.18f;
                    float strength=(1-Smooth(edge*.5f,edge,radius))*Smooth(-.72f,-.24f,Noise(seed,point.x/40,point.y/40,86));
                    if(Random01(seed,i,0,82)>=strength)continue;
                    // Dart throwing removes coincident trunks without imposing rows. Process the entire
                    // grove before tile ownership/environment clipping so regeneration order cannot alter it.
                    var bucket=new Vector2Int(Mathf.FloorToInt(point.x/separation),Mathf.FloorToInt(point.y/separation));bool crowded=false;
                    for(int iz=-1;iz<=1 && !crowded;iz++)for(int ix=-1;ix<=1 && !crowded;ix++)
                        if(occupied.TryGetValue(bucket+new Vector2Int(ix,iz),out var neighbours))
                            foreach(var other in neighbours)if((other-point).sqrMagnitude<separationSquared){crowded=true;break;}
                    if(crowded)continue;
                    if(!occupied.TryGetValue(bucket,out var members)){members=new List<Vector2>();occupied.Add(bucket,members);}members.Add(point);
                    var owner=world.TileOwner(point);
                    if(owner!=address || (point-world.PlainCentre).sqrMagnitude<90*90)continue;
                    var sample=Sample(world,point.x,point.y);
                    if(!sample.IsLand || sample.WaterDistance<6 || TreePresence(sample)<=0 || world.Slope(point.x,point.y)>34)continue;
                    float growth=Random01(seed,i,0,83),treeRadius=Mathf.Lerp(3,5,growth),height=Mathf.Lerp(12,20,growth);
                    result.Add(new SurfaceObjectData {Id=$"{groupId}:tree:{i}",Owner=owner,Kind=SurfaceObjectKind.Tree,
                        Position=new Vector3(point.x,sample.Height,point.y),Radius=treeRadius,Scale=height/16,Height=height,Yaw=Random01(seed,i,0,84)*360,
                        ResourceGroupId=groupId,ResourceGroupCell=cell,ResourceGroupIndex=groupIndex,WoodAmount=Mathf.RoundToInt(treeRadius*height*.8f)});
                }
            }
        }

        static float TreePresence(SurfaceSample sample)=>sample.Biome==PlanetBiome.Forest?.94f:sample.Biome==PlanetBiome.Plains?.33f:
            sample.Biome==PlanetBiome.Snow && sample.Temperature>.095f && sample.Moisture>.50f?.22f:0;
        static string GroveId(SurfaceWorldData world,Vector2Int cell,int index)=>$"{world.RegionId}:grove:{cell.x}:{cell.y}:{index}";

        /// <summary>Aggregate only available members, retaining each grove's fixed logical anchor across tile boundaries.</summary>
        public static TreeGroveData[] CollectTreeGroves(SurfaceWorldData world,IEnumerable<SurfaceObjectData> objects)
        {
            var groups=new Dictionary<string,TreeGroveData>(StringComparer.Ordinal);
            foreach(var obj in objects)
            {
                if(obj.Kind!=SurfaceObjectKind.Tree || string.IsNullOrEmpty(obj.ResourceGroupId))continue;
                if(!groups.TryGetValue(obj.ResourceGroupId,out var group))
                {
                    GroveShape(world,obj.ResourceGroupCell,obj.ResourceGroupIndex,out Vector2 centre,out Vector2 axes,out float angle);
                    group=new TreeGroveData {Id=obj.ResourceGroupId,Position=new Vector3(centre.x,world.Sample(centre).Height,centre.y),Radius=Mathf.Max(axes.x,axes.y)*GroveEdge+5};
                    groups.Add(obj.ResourceGroupId,group);
                }
                group.TreeCount++;group.WoodAmount+=obj.WoodAmount;
            }
            var ids=new List<string>(groups.Keys);ids.Sort(StringComparer.Ordinal);var result=new TreeGroveData[ids.Count];
            for(int i=0;i<ids.Count;i++)result[i]=groups[ids[i]];
            return result;
        }

        static void MeasureBuildable(SurfaceWorldData world,CancellationToken token,ParallelOptions parallel,SurfaceLoadProgress progress)
        {
            float step=world.Parameters.buildableCellSize;int n=Mathf.CeilToInt(world.Bounds.width/step);
            step=world.Bounds.width/n;var usable=new bool[n*n];var labels=new int[n*n];var sizes=new List<int>();
            int completed=0;
            Parallel.For(0,n,parallel,z=>
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<n;x++)
                {
                    float wx=world.Bounds.xMin+(x+.5f)*step,wz=world.Bounds.yMin+(z+.5f)*step;var sample=world.Sample(wx,wz);
                    // Several metres of shore clearance keep sub-cell channels out of the measured construction area.
                    usable[z*n+x]=sample.IsLand && sample.WaterDistance>step*.75f && world.Slope(wx,wz)<=world.Parameters.buildableSlope;
                }
                int done=Interlocked.Increment(ref completed);
                if(done%10==0)progress?.Report(.02f+.09f*done/n,"Surveying buildable ground");
            });
            var queue=new Queue<int>();int component=0;
            for(int start=0;start<usable.Length;start++)
            {
                if((start&4095)==0)token.ThrowIfCancellationRequested();
                if(!usable[start] || labels[start]!=0)continue;
                component++;int size=0;labels[start]=component;queue.Enqueue(start);
                while(queue.Count>0)
                {
                    if((size&4095)==0)token.ThrowIfCancellationRequested();
                    int at=queue.Dequeue();size++;int x=at%n,z=at/n;
                    void Add(int next){if(usable[next] && labels[next]==0){labels[next]=component;queue.Enqueue(next);}}
                    if(x>0)Add(at-1);if(x<n-1)Add(at+1);if(z>0)Add(at-n);if(z<n-1)Add(at+n);
                }
                sizes.Add(size);
            }
            // Largest all-usable square: every interior cell is verified, not just four corners or a bounding box.
            var squares=new int[n*n];float acceptedArea=0,acceptedInterior=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int i=z*n+x;if(!usable[i])continue;
                int side=x==0 || z==0?1:1+Mathf.Min(Mathf.Min(squares[i-1],squares[i-n]),squares[i-n-1]);squares[i]=side;
                float area=sizes[labels[i]-1]*step*step,interior=side*step;
                if(area>=world.Parameters.minimumBuildableArea && interior>=world.Parameters.minimumInteriorSize && interior>acceptedInterior)
                {acceptedArea=area;acceptedInterior=interior;}
            }
            world.BuildableArea=acceptedArea;world.InteriorClearance=acceptedInterior;
            if(acceptedArea<world.Parameters.minimumBuildableArea || acceptedInterior<world.Parameters.minimumInteriorSize)
                throw new SurfaceSurveyException("Survey found insufficient connected gentle, dry ground for the colony. Return to the planet and choose another region.");
        }

        internal static uint Hash(int seed,int x,int z,int salt)
        {
            uint value=unchecked((uint)seed^(uint)x*374761393u^(uint)z*668265263u^(uint)salt*2246822519u);
            value=(value^(value>>13))*1274126177u;return value^(value>>16);
        }
        static float Random01(int seed,int x,int z,int salt)=>(Hash(seed,x,z,salt)&0xFFFFFF)/16777215f;
        static float Noise(int seed,float x,float z,int salt)
        {
            int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);float tx=x-ix,tz=z-iz;
            tx=tx*tx*tx*(tx*(tx*6-15)+10);tz=tz*tz*tz*(tz*(tz*6-15)+10);
            return Mathf.Lerp(Mathf.Lerp(Random01(seed,ix,iz,salt),Random01(seed,ix+1,iz,salt),tx),
                Mathf.Lerp(Random01(seed,ix,iz+1,salt),Random01(seed,ix+1,iz+1,salt),tx),tz)*2-1;
        }
        static float Smooth(float low,float high,float value){float t=Mathf.Clamp01((value-low)/(high-low));return t*t*(3-2*t);}
    }
}
