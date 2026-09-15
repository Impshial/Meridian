using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Meridian
{
    /// <summary>Pure numeric generation; safe on a worker. Unity objects are created only by PlanetGlobe.</summary>
    public static class PlanetGenerator
    {
        private sealed class Noise
        {
            readonly uint seed;
            public Noise(int seed) { this.seed=unchecked((uint)seed); }
            float At(int x,int y,int z)
            {
                uint h=unchecked(seed ^ (uint)x*374761393u ^ (uint)y*668265263u ^ (uint)z*2246822519u);
                h=(h^(h>>13))*1274126177u; h^=h>>16;
                return (h&0xffffff)/8388607.5f-1;
            }
            public float Value(Vector3 p)
            {
                int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);
                float Smooth(float f)=>f*f*f*(f*(f*6-15)+10);
                float a=Smooth(p.x-x),b=Smooth(p.y-y),c=Smooth(p.z-z);
                return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(At(x,y,z),At(x+1,y,z),a),Mathf.Lerp(At(x,y+1,z),At(x+1,y+1,z),a),b),
                    Mathf.Lerp(Mathf.Lerp(At(x,y,z+1),At(x+1,y,z+1),a),Mathf.Lerp(At(x,y+1,z+1),At(x+1,y+1,z+1),a),b),c);
            }
            public float Fractal(Vector3 p,int octaves=5)
            {
                float value=0,weight=.56f,sum=0;
                for(int i=0;i<octaves;i++) {value+=Value(p)*weight;sum+=weight;p=p*2.07f+new Vector3(17.1f,9.2f,5.7f);weight*=.49f;}
                return value/sum;
            }
        }

        public static PlanetData Generate(int seed,PlanetParameters settings,CancellationToken cancellation=default)
        {
            var watch=Stopwatch.StartNew();
            var graph=new SphericalGraph(settings.geographySubdivisions);
            var data=new PlanetData(seed,settings,graph);
            var noise=new Noise(seed);
            var random=new System.Random(seed);
            var offset=new Vector3((float)random.NextDouble()*100,(float)random.NextDouble()*100,(float)random.NextDouble()*100);
            int count=graph.Directions.Length;
            float target=Mathf.Lerp(settings.minimumLand,settings.maximumLand,(float)random.NextDouble());
            var continents=new float[count];
            for(int i=0;i<count;i++)
            {
                if((i&1023)==0)cancellation.ThrowIfCancellationRequested();
                Vector3 d=graph.Directions[i],p=d*(2.15f*settings.continentScale)+offset;
                Vector3 warp=new Vector3(noise.Fractal(p+Vector3.right*31,3),noise.Fractal(p+Vector3.up*47,3),noise.Fractal(p+Vector3.forward*83,3));
                continents[i]=noise.Fractal(p+warp*.8f,6)+noise.Value(d*1.2f+offset*2)*.18f;
            }
            var sorted=(float[])continents.Clone(); Array.Sort(sorted);
            float sea=sorted[Mathf.Clamp((int)(count*(1-target)),0,count-1)];
            for(int i=0;i<count;i++)
            {
                Vector3 d=graph.Directions[i],p=d+offset;
                float h=continents[i]-sea;
                float inland=Mathf.Clamp01(h*9);
                float ridges=Mathf.Pow(1-Mathf.Abs(noise.Fractal(d*15+offset,3)),7);
                float range=Smooth(.02f,.3f,noise.Fractal(d*4.7f+offset+Vector3.one*62,3));
                // Lowlands remain broad and gently varying. Ranges occupy coherent interior belts.
                data.Elevation[i]=h<=0?h*1.6f:Mathf.Clamp01(h*h*1.4f+inland*ridges*range*.72f+inland*.022f);
                data.Moisture[i]=Mathf.Clamp01(.56f+noise.Fractal(d*3.8f+offset+Vector3.one*93)*.85f
                    -Mathf.Exp(-Mathf.Pow((Mathf.Abs(d.y)-.43f)/.19f,2))*.23f);
                data.Temperature[i]=Mathf.Clamp01(1.02f-Mathf.Pow(Mathf.Abs(d.y),1.35f)*1.16f-data.Elevation[i]*.55f+noise.Fractal(p*5,3)*.09f);
            }
            Drain(data,cancellation);
            if(!MarkLakes(data))
            {
                // Deterministic basin repair in a low inland region, followed by the same drainage solution.
                int centre=-1;float score=float.NegativeInfinity;
                for(int i=0;i<count;i++) if(data.Elevation[i]>.04f && data.Elevation[i]<.18f)
                {
                    float s=data.Moisture[i]-data.Elevation[i];
                    if(s>score){score=s;centre=i;}
                }
                if(centre>=0)
                    for(int i=0;i<count;i++)
                    {
                        float distance=(graph.Directions[i]-graph.Directions[centre]).magnitude;
                        if(distance<.10f) data.Elevation[i]=Mathf.Max(.006f,data.Elevation[i]-.12f*Mathf.Pow(1-distance/.10f,2));
                    }
                Drain(data,cancellation); MarkLakes(data);
            }
            int land=0;
            for(int i=0;i<count;i++)
            {
                if(data.Water[i]==PlanetWater.None && data.Flow[i]>60*(count/40962f)/settings.riverDensity && data.Downstream[i]>=0)
                    data.Water[i]=PlanetWater.River;
                data.Biomes[i]=Biome(data.Elevation[i],data.Temperature[i],data.Moisture[i]);
                if(data.Elevation[i]>0)land++;
            }
            // Ensure climate examples without isolated random-color triangles: adjust broad climate patches.
            EnsureClimate(data,PlanetBiome.Forest,cancellation);
            EnsureClimate(data,PlanetBiome.Desert,cancellation);
            EnsureClimate(data,PlanetBiome.Snow,cancellation);
            data.LandFraction=(float)land/count;
            Bake(data,noise,offset,cancellation);
            watch.Stop(); data.GenerationSeconds=watch.Elapsed.TotalSeconds;
            return data;
        }

        static PlanetBiome Biome(float h,float temperature,float moisture)
        {
            if(temperature<.19f || h>.68f)return PlanetBiome.Snow;
            if(h>.32f)return PlanetBiome.Rock;
            if(temperature>.42f && moisture<.37f)return PlanetBiome.Desert;
            if(temperature>.25f && moisture>.48f)return PlanetBiome.Forest;
            return PlanetBiome.Plains;
        }
        static float Smooth(float a,float b,float value) {float t=Mathf.Clamp01((value-a)/(b-a));return t*t*(3-2*t);}

        static void EnsureClimate(PlanetData d,PlanetBiome biome,CancellationToken cancellation)
        {
            int found=0,centre=-1;float best=float.NegativeInfinity;
            for(int i=0;i<d.Elevation.Length;i++)
            {
                if(d.Elevation[i]<=0 || d.Water[i]==PlanetWater.Lake)continue;
                if(d.Biomes[i]==biome)found++;
                float latitude=Mathf.Abs(d.Graph.Directions[i].y);
                float score=biome==PlanetBiome.Snow?latitude+d.Elevation[i]:biome==PlanetBiome.Desert?-Mathf.Abs(latitude-.4f)-d.Moisture[i]:d.Moisture[i]-latitude*.4f;
                if(score>best){best=score;centre=i;}
            }
            if(found>=d.Elevation.Length*.004f || centre<0)return;
            for(int i=0;i<d.Elevation.Length;i++)
            {
                if((i&1023)==0)cancellation.ThrowIfCancellationRequested();
                float distance=(d.Graph.Directions[i]-d.Graph.Directions[centre]).magnitude;
                float weight=1-Smooth(.13f,.35f,distance);
                if(biome==PlanetBiome.Snow)d.Temperature[i]=Mathf.Lerp(d.Temperature[i],.05f,weight);
                else
                {
                    d.Temperature[i]=Mathf.Lerp(d.Temperature[i],.7f,weight);
                    d.Moisture[i]=Mathf.Lerp(d.Moisture[i],biome==PlanetBiome.Forest?.8f:.15f,weight);
                }
                d.Biomes[i]=Biome(d.Elevation[i],d.Temperature[i],d.Moisture[i]);
            }
        }

        // Stable priority flood: each land node drains to an earlier, lower filled-surface node.
        static void Drain(PlanetData d,CancellationToken cancellation)
        {
            int n=d.Elevation.Length;
            var heap=new List<int>();var visited=new bool[n];var order=new List<int>(n);
            bool Less(int a,int b)=>d.DrainageHeight[a]<d.DrainageHeight[b] || (d.DrainageHeight[a]==d.DrainageHeight[b] && a<b);
            void Push(int id)
            {
                int i=heap.Count;heap.Add(id);
                while(i>0){int p=(i-1)/2;if(!Less(id,heap[p]))break;heap[i]=heap[p];i=p;}heap[i]=id;
            }
            int Pop()
            {
                int result=heap[0],last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);
                if(heap.Count>0)
                {
                    int i=0;
                    while(i*2+1<heap.Count){int child=i*2+1;if(child+1<heap.Count&&Less(heap[child+1],heap[child]))child++;
                        if(!Less(heap[child],last))break;heap[i]=heap[child];i=child;}heap[i]=last;
                }
                return result;
            }
            for(int i=0;i<n;i++)
            {
                d.Downstream[i]=-1;d.Flow[i]=.35f+d.Moisture[i];d.Water[i]=PlanetWater.None;
                if(d.Elevation[i]<=0){visited[i]=true;d.DrainageHeight[i]=0;d.Water[i]=PlanetWater.Ocean;Push(i);}
            }
            while(heap.Count>0)
            {
                int i=Pop();order.Add(i);if((order.Count&1023)==0)cancellation.ThrowIfCancellationRequested();
                foreach(int next in d.Graph.Neighbours[i]) if(!visited[next])
                {
                    visited[next]=true;d.Downstream[next]=i;
                    d.DrainageHeight[next]=Mathf.Max(d.Elevation[next],d.DrainageHeight[i]+.000001f);Push(next);
                }
            }
            for(int j=order.Count-1;j>=0;j--){int i=order[j];if(d.Downstream[i]>=0)d.Flow[d.Downstream[i]]+=d.Flow[i];}
        }

        static bool MarkLakes(PlanetData d)
        {
            var visited=new bool[d.Elevation.Length];var basins=new List<List<int>>();
            for(int i=0;i<visited.Length;i++)
            {
                if(visited[i] || d.Elevation[i]<=0 || d.DrainageHeight[i]-d.Elevation[i]<.006f)continue;
                var basin=new List<int>();var queue=new Queue<int>();queue.Enqueue(i);visited[i]=true;
                while(queue.Count>0)
                {
                    int j=queue.Dequeue();basin.Add(j);
                    foreach(int k in d.Graph.Neighbours[j]) if(!visited[k] && d.Elevation[k]>0 && d.DrainageHeight[k]-d.Elevation[k]>.003f)
                    {visited[k]=true;queue.Enqueue(k);}
                }
                if(basin.Count>=6 && basin.Count<visited.Length*.015f)basins.Add(basin);
            }
            basins.Sort((a,b)=>b.Count!=a.Count?b.Count.CompareTo(a.Count):a[0].CompareTo(b[0]));
            for(int j=0;j<Math.Min(12,basins.Count);j++) foreach(int i in basins[j])d.Water[i]=PlanetWater.Lake;
            return basins.Count>0;
        }

        static void Bake(PlanetData d,Noise noise,Vector3 offset,CancellationToken cancellation)
        {
            int w=d.Width,h=d.Height;
            var heightForNormals=new float[w*h];
            for(int y=0;y<h;y++)
            {
                cancellation.ThrowIfCancellationRequested();
                // The outer rows sample the actual pole, giving one consistent value at every longitude.
                float v=y==0?0:y==h-1?1:(y+.5f)/h;
                for(int x=0;x<w;x++)
                {
                    int index=y*w+x;Vector3 dir=PlanetData.Direction((x+.5f)/w,v);
                    d.Graph.Locate(dir,out int a,out int b,out int c,out Vector3 weights);
                    float Interpolate(float[] field)=>field[a]*weights.x+field[b]*weights.y+field[c]*weights.z;
                    float elevation=Interpolate(d.Elevation),moisture=Interpolate(d.Moisture),temperature=Interpolate(d.Temperature);
                    float lake=(d.Water[a]==PlanetWater.Lake?weights.x:0)+(d.Water[b]==PlanetWater.Lake?weights.y:0)+(d.Water[c]==PlanetWater.Lake?weights.z:0);
                    PlanetWater water=elevation<=0?PlanetWater.Ocean:lake>.48f?PlanetWater.Lake:PlanetWater.None;
                    bool RiverNear(int node)
                    {
                        int end=d.Downstream[node];
                        if(end<0 || d.Water[node]!=PlanetWater.River)return false;
                        Vector3 p=d.Graph.Directions[node],q=d.Graph.Directions[end],edge=q-p;
                        float t=Mathf.Clamp01(Vector3.Dot(dir-p,edge)/edge.sqrMagnitude);
                        float width=.0018f+Mathf.Min(.0024f,Mathf.Log(1+d.Flow[node]/60)*.0007f);
                        // A sub-texel river breaks into disconnected blue dots when viewed from orbit.
                        width=Mathf.Max(width,2*Mathf.PI/d.Width*.9f);
                        return (dir-(p+edge*t).normalized).sqrMagnitude<width*width;
                    }
                    if(water==PlanetWater.None && (RiverNear(a)||RiverNear(b)||RiverNear(c)))water=PlanetWater.River;
                    var biome=Biome(elevation,temperature,moisture);
                    float fine=noise.Fractal(dir*160+offset,4),detail=noise.Fractal(dir*55+offset,4);
                    Color plains=new Color(.32f,.38f,.19f),forest=new Color(.105f,.235f,.135f),desert=new Color(.66f,.52f,.31f);
                    Color rock=new Color(.39f,.37f,.31f),snow=new Color(.84f,.89f,.89f);
                    Color color=Color.Lerp(plains,forest,Smooth(.41f,.60f,moisture)*Smooth(.20f,.36f,temperature));
                    color=Color.Lerp(color,desert,(1-Smooth(.30f,.43f,moisture))*Smooth(.36f,.55f,temperature));
                    color=Color.Lerp(color,rock,Smooth(.20f,.42f,elevation));
                    color=Color.Lerp(color,snow,Mathf.Max(1-Smooth(.12f,.23f,temperature),Smooth(.57f,.74f,elevation)));
                    color*=1+fine*.17f+detail*.15f;
                    color.a=water==PlanetWater.Ocean?Mathf.Clamp01(-elevation*6):0;
                    d.ColorMap[index]=color;
                    d.MapElevation[index]=elevation;d.MapMoisture[index]=moisture;
                    d.SurfaceMap[index]=new Color32(water==PlanetWater.None?(byte)0:(byte)255,(byte)water,(byte)biome,(byte)(Mathf.Clamp01(temperature)*255));
                    heightForNormals[index]=water!=PlanetWater.None?0:elevation+detail*.018f+fine*.008f;
                }
            }
            // Object-space normals avoid a tangent singularity at the poles; shader rotates them with the globe.
            for(int y=0;y<h;y++)
            {
                cancellation.ThrowIfCancellationRequested();
                for(int x=0;x<w;x++)
                {
                    int i=y*w+x;float v=y==0?0:y==h-1?1:(y+.5f)/h;
                    Vector3 dir=PlanetData.Direction((x+.5f)/w,v);
                    Vector3 east=Vector3.Cross(Vector3.up,dir).normalized,north=Vector3.Cross(dir,east);
                    float dx=(heightForNormals[y*w+(x+1)%w]-heightForNormals[y*w+(x+w-1)%w])*.07f;
                    float dy=(heightForNormals[Math.Min(h-1,y+1)*w+x]-heightForNormals[Math.Max(0,y-1)*w+x])*.07f;
                    // Clamp near-polar detail instead of amplifying longitude gradients by 1/cos(latitude).
                    Vector3 normal=(dir-east*dx*w/(2*Mathf.PI)-north*dy*h/Mathf.PI).normalized;
                    if(y==0 || y==h-1 || d.SurfaceMap[i].r>0)normal=dir;
                    d.NormalMap[i]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);
                }
            }
        }
    }
}
