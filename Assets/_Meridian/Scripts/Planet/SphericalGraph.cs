using System;
using System.Collections.Generic;
using UnityEngine;

namespace Meridian
{
    /// <summary>Closed, uniformly sampled icosphere. No longitude seam or exceptional polar neighbours.</summary>
    public sealed class SphericalGraph
    {
        public readonly Vector3[] Directions;
        public readonly int[] Triangles;
        public readonly int[][] Neighbours;
        private readonly Face[] roots;

        private sealed class Face
        {
            public int a, b, c;
            public Vector3 ab, bc, ca;
            public Face[] children;
            public float Containment(Vector3 p) => Mathf.Min(Vector3.Dot(ab, p), Mathf.Min(Vector3.Dot(bc, p), Vector3.Dot(ca, p)));
        }

        public SphericalGraph(int subdivisions)
        {
            var vertices = new List<Vector3>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            foreach (var v in new[] { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0), new Vector3(1,-t,0),
                new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t), new Vector3(0,1,-t),
                new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) }) vertices.Add(v.normalized);
            int[] baseFaces = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var cache = new Dictionary<ulong, int>();
            var indices = new List<int>();
            roots = new Face[20];
            int Mid(int a, int b)
            {
                ulong key = ((ulong)(uint)Math.Min(a,b) << 32) | (uint)Math.Max(a,b);
                if (cache.TryGetValue(key, out int found)) return found;
                int i = vertices.Count;
                vertices.Add((vertices[a] + vertices[b]).normalized);
                cache.Add(key, i);
                return i;
            }
            Face Split(int a, int b, int c, int depth)
            {
                var f = new Face { a=a, b=b, c=c, ab=Vector3.Cross(vertices[a],vertices[b]),
                    bc=Vector3.Cross(vertices[b],vertices[c]), ca=Vector3.Cross(vertices[c],vertices[a]) };
                if (depth == 0) { indices.Add(a); indices.Add(b); indices.Add(c); }
                else
                {
                    int ab=Mid(a,b), bc=Mid(b,c), ca=Mid(c,a);
                    f.children = new[] { Split(a,ab,ca,depth-1), Split(b,bc,ab,depth-1),
                        Split(c,ca,bc,depth-1), Split(ab,bc,ca,depth-1) };
                }
                return f;
            }
            for (int i=0;i<20;i++) roots[i]=Split(baseFaces[i*3],baseFaces[i*3+1],baseFaces[i*3+2],subdivisions);
            Directions=vertices.ToArray(); Triangles=indices.ToArray();
            var adjacent=new HashSet<int>[Directions.Length];
            for (int i=0;i<adjacent.Length;i++) adjacent[i]=new HashSet<int>();
            for (int i=0;i<Triangles.Length;i+=3)
            {
                int a=Triangles[i], b=Triangles[i+1], c=Triangles[i+2];
                adjacent[a].Add(b); adjacent[a].Add(c); adjacent[b].Add(a); adjacent[b].Add(c); adjacent[c].Add(a); adjacent[c].Add(b);
            }
            Neighbours=new int[Directions.Length][];
            for (int i=0;i<adjacent.Length;i++) { Neighbours[i]=new int[adjacent[i].Count]; adjacent[i].CopyTo(Neighbours[i]); Array.Sort(Neighbours[i]); }
        }

        public void Locate(Vector3 direction, out int a, out int b, out int c, out Vector3 weights)
        {
            Face Best(Face[] faces)
            {
                Face best=faces[0]; float score=float.NegativeInfinity;
                foreach(var face in faces)
                {
                    float s=face.Containment(direction);
                    if(s>=-1e-8f) return face;
                    if(s>score) {score=s;best=face;}
                }
                return best;
            }
            Face f=Best(roots);
            while(f.children!=null) f=Best(f.children);
            a=f.a; b=f.b; c=f.c;
            Vector3 va=Directions[a], e0=Directions[b]-va, e1=Directions[c]-va;
            Vector3 n=Vector3.Cross(e0,e1);
            Vector3 p=direction*(Vector3.Dot(n,va)/Vector3.Dot(n,direction))-va;
            float d00=Vector3.Dot(e0,e0), d01=Vector3.Dot(e0,e1), d11=Vector3.Dot(e1,e1);
            float d20=Vector3.Dot(p,e0), d21=Vector3.Dot(p,e1), inv=1f/(d00*d11-d01*d01);
            float v=Mathf.Clamp01((d11*d20-d01*d21)*inv), w=Mathf.Clamp01((d00*d21-d01*d20)*inv);
            weights=new Vector3(Mathf.Max(0,1-v-w),v,w); weights/=weights.x+weights.y+weights.z;
        }
    }
}
