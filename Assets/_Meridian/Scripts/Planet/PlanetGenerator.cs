using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
            public float Detail(Vector3 p,int octaves,out Vector3 gradient)
            {
                float value=0,weight=.56f,sum=0,frequency=1;gradient=Vector3.zero;
                for(int octave=0;octave<octaves;octave++)
                {
                    int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y),z=Mathf.FloorToInt(p.z);
                    Vector3 f=new Vector3(p.x-x,p.y-y,p.z-z);
                    float S(float t)=>t*t*t*(t*(t*6-15)+10);
                    float D(float t)=>30*t*t*(t-1)*(t-1);
                    float u=S(f.x),v=S(f.y),w=S(f.z);
                    float a=At(x,y,z),b=At(x+1,y,z),c=At(x,y+1,z),d=At(x+1,y+1,z);
                    float e=At(x,y,z+1),g=At(x+1,y,z+1),h=At(x,y+1,z+1),j=At(x+1,y+1,z+1);
                    float x0=Mathf.Lerp(a,b,u),x1=Mathf.Lerp(c,d,u),x2=Mathf.Lerp(e,g,u),x3=Mathf.Lerp(h,j,u);
                    float y0=Mathf.Lerp(x0,x1,v),y1=Mathf.Lerp(x2,x3,v);
                    value+=Mathf.Lerp(y0,y1,w)*weight;
                    gradient+=new Vector3(Mathf.Lerp(Mathf.Lerp(b-a,d-c,v),Mathf.Lerp(g-e,j-h,v),w)*D(f.x),
                        Mathf.Lerp(x1-x0,x3-x2,w)*D(f.y),(y1-y0)*D(f.z))*(weight*frequency);
                    sum+=weight;p=p*2.07f+new Vector3(17.1f,9.2f,5.7f);weight*=.49f;frequency*=2.07f;
                }
                gradient/=sum;return value/sum;
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

        // Geographic half-width is independent of appearance resolution. Preserve the original
        // 1024-wide overview's minimum rather than shrinking rivers as maps become larger.
        public static float RiverHalfWidth(float flow)=>Mathf.Max(.0018f+Mathf.Min(.0024f,Mathf.Log(1+flow/60)*.0007f),2*Mathf.PI/1024*.9f);

        static Vector3 FaceGradient(Vector3 edge0,Vector3 edge1,float delta0,float delta1)
        {
            Vector3 n=Vector3.Cross(edge0,edge1);
            return (Vector3.Cross(edge1,n)*delta0+Vector3.Cross(n,edge0)*delta1)/Mathf.Max(1e-12f,n.sqrMagnitude);
        }

        // Shared vertex derivatives define matching cubic edge curves on neighbouring faces.
        // Bounded controls preserve node extrema and prevent invented islands/lakes from overshoot.
        static float CurvedField(float a,float b,float c,Vector3 ga,Vector3 gb,Vector3 gc,Vector3 ab,Vector3 ac,Vector3 weights)
        {
            float minimum=Mathf.Min(a,Mathf.Min(b,c)),maximum=Mathf.Max(a,Mathf.Max(b,c));
            if(maximum-minimum<1e-7f)return a;
            float Clamp(float value)=>Mathf.Clamp(value,minimum,maximum);
            float Edge(float value,float start,float end)=>Mathf.Clamp(value,Mathf.Min(start,end),Mathf.Max(start,end));
            float aab=Edge(a+Vector3.Dot(ga,ab)/3,a,b),aac=Edge(a+Vector3.Dot(ga,ac)/3,a,c);
            float abb=Edge(b-Vector3.Dot(gb,ab)/3,a,b),bbc=Edge(b+Vector3.Dot(gb,ac-ab)/3,b,c);
            float acc=Edge(c-Vector3.Dot(gc,ac)/3,a,c),bcc=Edge(c+Vector3.Dot(gc,ab-ac)/3,b,c);
            float centre=Clamp((aab+aac+abb+bbc+acc+bcc)*.25f-(a+b+c)/6);
            float u=weights.x,v=weights.y,w=weights.z;
            return a*u*u*u+b*v*v*v+c*w*w*w+3*(aab*u*u*v+aac*u*u*w+abb*u*v*v+bbc*v*v*w+acc*u*w*w+bcc*v*w*w)+6*centre*u*v*w;
        }

        static void Bake(PlanetData d,Noise noise,Vector3 offset,CancellationToken cancellation)
        {
            int w=d.Width,h=d.Height;
            var graph=d.Graph;
            var gradients=new Vector3[graph.Directions.Length];
            var lakeGradients=new Vector3[gradients.Length];
            var temperatureGradients=new Vector3[gradients.Length];
            var weightsAtNodes=new int[gradients.Length];
            for(int f=0;f<graph.Triangles.Length;f+=3)
            {
                int a=graph.Triangles[f],b=graph.Triangles[f+1],c=graph.Triangles[f+2];
                Vector3 gradient=FaceGradient(graph.Directions[b]-graph.Directions[a],graph.Directions[c]-graph.Directions[a],d.Elevation[b]-d.Elevation[a],d.Elevation[c]-d.Elevation[a]);
                gradients[a]+=gradient;gradients[b]+=gradient;gradients[c]+=gradient;
                float Lake(int i)=>d.Water[i]==PlanetWater.Lake?1:0;
                Vector3 lakeGradient=FaceGradient(graph.Directions[b]-graph.Directions[a],graph.Directions[c]-graph.Directions[a],Lake(b)-Lake(a),Lake(c)-Lake(a));
                lakeGradients[a]+=lakeGradient;lakeGradients[b]+=lakeGradient;lakeGradients[c]+=lakeGradient;
                Vector3 temperatureGradient=FaceGradient(graph.Directions[b]-graph.Directions[a],graph.Directions[c]-graph.Directions[a],d.Temperature[b]-d.Temperature[a],d.Temperature[c]-d.Temperature[a]);
                temperatureGradients[a]+=temperatureGradient;temperatureGradients[b]+=temperatureGradient;temperatureGradients[c]+=temperatureGradient;
                weightsAtNodes[a]++;weightsAtNodes[b]++;weightsAtNodes[c]++;
            }
            // Shared vertex gradients remove hard face-normal changes without modifying elevation.
            for(int i=0;i<gradients.Length;i++){gradients[i]/=weightsAtNodes[i];lakeGradients[i]/=weightsAtNodes[i];temperatureGradients[i]/=weightsAtNodes[i];}
            var riverCandidates=new int[gradients.Length][];
            for(int i=0;i<gradients.Length;i++)
            {
                var candidates=new List<int>();
                if(d.Water[i]==PlanetWater.River && d.Downstream[i]>=0)candidates.Add(i);
                foreach(int neighbour in graph.Neighbours[i])if(d.Water[neighbour]==PlanetWater.River && d.Downstream[neighbour]>=0)candidates.Add(neighbour);
                riverCandidates[i]=candidates.ToArray();
            }
            var options=new ParallelOptions {CancellationToken=cancellation,MaxDegreeOfParallelism=Math.Min(8,Math.Max(1,Environment.ProcessorCount/2))};
            Parallel.For(0,h,options,y=>
            {
                // Sample exact poles on the outer rows. All operations are continuous in 3D.
                float v=y==0?0:y==h-1?1:(y+.5f)/h;
                for(int x=0;x<w;x++)
                {
                    if((x&255)==0)cancellation.ThrowIfCancellationRequested();
                    int index=y*w+x;Vector3 dir=PlanetData.Direction((x+.5f)/w,v);
                    graph.Locate(dir,out int a,out int b,out int c,out Vector3 weights);
                    float Interpolate(float[] field)=>field[a]*weights.x+field[b]*weights.y+field[c]*weights.z;
                    Vector3 edge0=graph.Directions[b]-graph.Directions[a],edge1=graph.Directions[c]-graph.Directions[a];
                    float elevation=CurvedField(d.Elevation[a],d.Elevation[b],d.Elevation[c],gradients[a],gradients[b],gradients[c],edge0,edge1,weights);
                    float moisture=Interpolate(d.Moisture);
                    float temperature=CurvedField(d.Temperature[a],d.Temperature[b],d.Temperature[c],temperatureGradients[a],temperatureGradients[b],temperatureGradients[c],edge0,edge1,weights);
                    float la=d.Water[a]==PlanetWater.Lake?1:0,lb=d.Water[b]==PlanetWater.Lake?1:0,lc=d.Water[c]==PlanetWater.Lake?1:0;
                    float lake=CurvedField(la,lb,lc,lakeGradients[a],lakeGradients[b],lakeGradients[c],edge0,edge1,weights);
                    float oceanDistance=-elevation/Mathf.Max(.0001f,FaceGradient(edge0,edge1,d.Elevation[b]-d.Elevation[a],d.Elevation[c]-d.Elevation[a]).magnitude);
                    float lakeDistance=(lake-.48f)/Mathf.Max(.0001f,FaceGradient(edge0,edge1,lb-la,lc-la).magnitude);
                    float riverDistance=-1;
                    void RiversAt(int vertex)
                    {
                        foreach(int node in riverCandidates[vertex])
                        {
                            Vector3 p=graph.Directions[node],edge=graph.Directions[d.Downstream[node]]-p;
                            float t=Mathf.Clamp01(Vector3.Dot(dir-p,edge)/edge.sqrMagnitude);
                            riverDistance=Mathf.Max(riverDistance,RiverHalfWidth(d.Flow[node])-(dir-(p+edge*t).normalized).magnitude);
                        }
                    }
                    RiversAt(a);RiversAt(b);RiversAt(c);
                    float signedWater=Mathf.Max(oceanDistance,Mathf.Max(lakeDistance,riverDistance));
                    // IDs describe the nearest water boundary even just outside it, so antialiasing
                    // can shade a dry-side fringe correctly without interpolating categorical bytes.
                    PlanetWater type=oceanDistance>=0?PlanetWater.Ocean:lakeDistance>=0?PlanetWater.Lake:riverDistance>=0?PlanetWater.River:
                        signedWater==oceanDistance?PlanetWater.Ocean:signedWater==lakeDistance?PlanetWater.Lake:PlanetWater.River;
                    var biome=Biome(elevation,temperature,moisture);
                    float fine=noise.Detail(dir*160+offset,3,out Vector3 fineGradient),detail=noise.Detail(dir*55+offset,3,out Vector3 detailGradient);
                    Color plains=new Color(.32f,.38f,.19f),forest=new Color(.105f,.235f,.135f),desert=new Color(.66f,.52f,.31f);
                    Color rock=new Color(.39f,.37f,.31f),snow=new Color(.84f,.89f,.89f);
                    float vegetation=Smooth(.41f,.60f,moisture)*Smooth(.20f,.36f,temperature);
                    Color color=Color.Lerp(plains,forest,vegetation);
                    color=Color.Lerp(color,desert,(1-Smooth(.30f,.43f,moisture))*Smooth(.36f,.55f,temperature));
                    float rocky=Smooth(.20f,.42f,elevation),snowy=Mathf.Max(1-Smooth(.12f,.23f,temperature),Smooth(.57f,.74f,elevation));
                    color=Color.Lerp(color,rock,rocky);color=Color.Lerp(color,snow,snowy);vegetation*=(1-rocky)*(1-snowy);
                    color*=1+fine*.17f+detail*.15f;
                    color.a=Mathf.Clamp01(-elevation*6);
                    d.ColorMap[index]=color;
                    byte boundary=(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f+signedWater/(2*PlanetData.BoundaryBand))*255);
                    // Allocate all 8 temperature bits to the visible freezing interval. Encoding
                    // the entire climate range left only ~20 levels here and banded polar water.
                    byte freezing=(byte)Mathf.RoundToInt(Mathf.InverseLerp(.08f,.16f,temperature)*255);
                    d.SurfaceMap[index]=new Color32(boundary,(byte)type,(byte)biome,freezing);
                    Vector3 slope=(gradients[a]*weights.x+gradients[b]*weights.y+gradients[c]*weights.z)*.14f;
                    slope+=Vector3.ClampMagnitude((detailGradient*(55*.018f)+fineGradient*(160*.008f))*.14f,.22f);
                    slope-=dir*Vector3.Dot(dir,slope);
                    Vector3 normal=(dir-slope).normalized;
                    // Continuous land normals extend under the shoreline coverage blend. No height
                    // discontinuity at water, finite-difference resolution gain, or polar basis.
                    d.NormalMap[index]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,vegetation);
                }
            });
        }
    }
}
