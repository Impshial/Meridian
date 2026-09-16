using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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

        public static SurfaceWorldData Generate(PlanetData planet,Vector3 selectedDirection,SurfaceParameters parameters,CancellationToken cancellation=default)
        {
            if(planet==null)throw new ArgumentNullException(nameof(planet));
            if(selectedDirection.sqrMagnitude<.5f)throw new ArgumentException("A selected geographic direction is required.");
            cancellation.ThrowIfCancellationRequested();var timer=Stopwatch.StartNew();
            var world=new SurfaceWorldData(planet,new SurfaceFrame(selectedDirection,parameters.mappingRadius),parameters);
            FindPlain(world,cancellation);
            // Readiness is measured before allocating the four large terrain arrays, so impossible regions fail cheaply.
            MeasureBuildable(world,cancellation);
            var tiles=new SurfaceTileData[4];var allObjects=new List<SurfaceObjectData>();int index=0;
            for(int z=-1;z<=0;z++)for(int x=-1;x<=0;x++)
            {
                cancellation.ThrowIfCancellationRequested();
                var tile=GenerateTile(world,new Vector2Int(x,z),cancellation);tiles[index++]=tile;allObjects.AddRange(tile.Objects);
            }
            world.Tiles=tiles;world.Objects=allObjects.ToArray();
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
            return world;
        }

        static void FindPlain(SurfaceWorldData world,CancellationToken token)
        {
            float best=float.NegativeInfinity;Vector2 selected=default;float selectedHeight=0;
            float radius=world.Parameters.plainRadius,limit=world.Parameters.tileSize-radius-80;
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
            world.PlainCentre=selected;world.PlainHeight=selectedHeight;world.ShapePlain=true;
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
            float undulation=Noise(world.Seed,x/230,z/230,1)*(biome==PlanetBiome.Rock?22:8);
            float fine=Noise(world.Seed,x/75,z/75,2)*.65f+Noise(world.Seed,x/24,z/24,3)*.18f;
            float height=broad+(undulation+fine)*Smooth(10,100,waterDistance);
            if(world.ShapePlain)
            {
                float plain=1-Smooth(p.plainRadius,p.plainRadius+p.plainBlend,(point-world.PlainCentre).magnitude);
                plain*=Smooth(20,90,waterDistance);
                float target=world.PlainHeight+Noise(world.Seed,x/90,z/90,4)*.35f+Noise(world.Seed,x/30,z/30,5)*.12f;
                height=Mathf.Lerp(height,target,plain);
            }
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
            var tile=new SurfaceTileData {Address=address,Origin=new Vector2(address.x*p.tileSize,address.y*p.tileSize),Size=p.tileSize,
                MinHeight=p.minimumTerrainHeight,HeightRange=p.terrainHeightRange,Heights=new float[resolution,resolution]};
            // Integer logical sample coordinates make both copies of a shared border bit-identical.
            int waterResolution=segments/2+1;var waterSamples=new SurfaceSample[waterResolution,waterResolution];
            for(int z=0;z<resolution;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<resolution;x++)
                {
                    float wx=(address.x*segments+x)*step,wz=(address.y*segments+z)*step;
                    SurfaceSample sample=Sample(world,wx,wz);tile.Heights[z,x]=(sample.Height-p.minimumTerrainHeight)/p.terrainHeightRange;
                    if((x&1)==0 && (z&1)==0)waterSamples[z/2,x/2]=sample;
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
                    float slope=world.Slope(wx,wz),rock=Smooth(22,43,slope);
                    if(sample.Biome==PlanetBiome.Rock)rock=Mathf.Max(rock,.6f);
                    float wet=(1-Smooth(0,15,sample.WaterDistance))*.8f;
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
            int minX=Mathf.FloorToInt(address.x*p.tileSize/cell)-1,maxX=Mathf.CeilToInt((address.x+1)*p.tileSize/cell)+1;
            int minZ=Mathf.FloorToInt(address.y*p.tileSize/cell)-1,maxZ=Mathf.CeilToInt((address.y+1)*p.tileSize/cell)+1;
            for(int z=minZ;z<=maxZ;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=minX;x<=maxX;x++)
                {
                    float wx=(x+.15f+.7f*Random01(world.Seed,x,z,10))*cell,wz=(z+.15f+.7f*Random01(world.Seed,x,z,11))*cell;
                    var owner=new Vector2Int(Mathf.FloorToInt(wx/p.tileSize),Mathf.FloorToInt(wz/p.tileSize));
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
                    else if((sample.Biome==PlanetBiome.Forest && roll<.62f) || (sample.Biome==PlanetBiome.Plains && roll<.11f))
                    {kind=SurfaceObjectKind.Tree;scale=Mathf.Lerp(.75f,1.45f,Random01(world.Seed,x,z,14));radius=2*scale;}
                    else continue;
                    result.Add(new SurfaceObjectData {Id=$"{world.RegionId}:{x}:{z}:{(int)kind}",Owner=owner,Kind=kind,
                        Position=new Vector3(wx,sample.Height,wz),Radius=radius,Scale=scale,Yaw=Random01(world.Seed,x,z,15)*360});
                }
            }
            return result.ToArray();
        }

        static void MeasureBuildable(SurfaceWorldData world,CancellationToken token)
        {
            float step=world.Parameters.buildableCellSize;int n=Mathf.CeilToInt(world.Bounds.width/step);
            step=world.Bounds.width/n;var usable=new bool[n*n];var labels=new int[n*n];var sizes=new List<int>();
            for(int z=0;z<n;z++)
            {
                token.ThrowIfCancellationRequested();
                for(int x=0;x<n;x++)
                {
                    float wx=world.Bounds.xMin+(x+.5f)*step,wz=world.Bounds.yMin+(z+.5f)*step;var sample=world.Sample(wx,wz);
                    // Several metres of shore clearance keep sub-cell channels out of the measured construction area.
                    usable[z*n+x]=sample.IsLand && sample.WaterDistance>step*.75f && world.Slope(wx,wz)<=world.Parameters.buildableSlope;
                }
            }
            var queue=new Queue<int>();int component=0;
            for(int start=0;start<usable.Length;start++)
            {
                if(!usable[start] || labels[start]!=0)continue;
                component++;int size=0;labels[start]=component;queue.Enqueue(start);
                while(queue.Count>0)
                {
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
