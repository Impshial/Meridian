using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meridian
{
    /// <summary>Main-thread scene resources. Logical generation never owns these Unity objects.</summary>
    public sealed class SurfaceLandscape : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        private readonly Dictionary<Vector2Int,Terrain> terrains=new Dictionary<Vector2Int,Terrain>();
        private readonly List<TerrainCollider> colliders=new List<TerrainCollider>();
        private readonly Dictionary<int,Material> propMaterials=new Dictionary<int,Material>();
        public IReadOnlyList<TerrainCollider> Colliders=>colliders;
        public IEnumerator Build(SurfaceWorldData world,Material terrainMaterial,Material waterMaterial)
        {
            TerrainLayer[] layers=GroundLayers();
            foreach(var tile in world.Tiles)
            {
                var data=Own(new TerrainData{name=$"Survey Terrain {tile.Address.x},{tile.Address.y}",heightmapResolution=tile.Heights.GetLength(0),
                    alphamapResolution=tile.Layers.GetLength(0),baseMapResolution=512,size=new Vector3(tile.Size,tile.HeightRange,tile.Size)});
                data.terrainLayers=layers;data.SetHeights(0,0,tile.Heights);yield return null;
                data.SetAlphamaps(0,0,tile.Layers);
                var go=Terrain.CreateTerrainGameObject(data);go.name=data.name;go.transform.SetParent(transform,false);
                go.transform.localPosition=new Vector3(tile.Origin.x,tile.MinHeight,tile.Origin.y);
                var terrain=go.GetComponent<Terrain>();terrain.materialTemplate=terrainMaterial;terrain.heightmapPixelError=3;
                terrain.basemapDistance=2000;terrain.drawInstanced=true;terrain.allowAutoConnect=false;
                terrain.shadowCastingMode=ShadowCastingMode.On;terrain.Flush();
                terrains.Add(tile.Address,terrain);colliders.Add(go.GetComponent<TerrainCollider>());
                if(tile.Water!=null && tile.Water.Vertices.Length>0)
                {
                    var mesh=Own(new Mesh{name=$"Survey water {tile.Address}",indexFormat=IndexFormat.UInt32});
                    mesh.vertices=tile.Water.Vertices;mesh.triangles=tile.Water.Triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                    var water=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));water.transform.SetParent(transform,false);
                    water.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=water.GetComponent<MeshRenderer>();renderer.sharedMaterial=waterMaterial;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                }
                yield return null;
            }
            foreach(var pair in terrains)
            {
                Terrain At(int x,int z)=>terrains.TryGetValue(pair.Key+new Vector2Int(x,z),out var t)?t:null;
                pair.Value.SetNeighbors(At(-1,0),At(0,1),At(1,0),At(0,-1));
            }
            yield return BuildProps(world,waterMaterial);
            Physics.SyncTransforms();
        }
        public bool Pick(Ray ray,out RaycastHit hit)
        {
            hit=default;float distance=float.PositiveInfinity;bool found=false;
            foreach(var collider in colliders)if(collider && collider.Raycast(ray,out var candidate,10000) && candidate.distance<distance)
            {found=true;distance=candidate.distance;hit=candidate;}
            return found;
        }
        TerrainLayer[] GroundLayers()
        {
            var colors=new[]{new Color(.25f,.32f,.16f),new Color(.56f,.46f,.29f),new Color(.31f,.32f,.30f),new Color(.79f,.83f,.82f),new Color(.25f,.22f,.15f)};
            var names=new[]{"Grass and forest soil","Dry sand","Exposed stone","Snow","Damp earth"};
            var result=new TerrainLayer[5];
            for(int layer=0;layer<5;layer++)
            {
                const int n=128;var tex=Own(new Texture2D(n,n,TextureFormat.RGBA32,true,false){name="Survey "+names[layer],wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4});
                var pixels=new Color32[n*n];
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)
                {
                    // Integer-period harmonics make these small ground textures exactly repeatable.
                    float a=x*2*Mathf.PI/n,b=z*2*Mathf.PI/n;
                    uint hash=(uint)(x*73856093 ^ z*19349663 ^ layer*83492791);hash^=hash>>13;hash*=1274126177u;
                    float grain=(hash&255)/255f-.5f;
                    float variation=Mathf.Sin(a*13+Mathf.Sin(b*5))*.035f+Mathf.Cos(b*17+Mathf.Cos(a*7))*.025f+grain*.07f;
                    pixels[z*n+x]=colors[layer]*Mathf.Clamp(1+variation*3,.7f,1.3f);
                }
                tex.SetPixels32(pixels);tex.Apply(true,true);
                var terrainLayer=Own(new TerrainLayer{name=names[layer],diffuseTexture=tex,tileSize=new Vector2(10,10),smoothness=layer==3?.15f:.04f,metallic=0});
                result[layer]=terrainLayer;
            }
            return result;
        }
        IEnumerator BuildProps(SurfaceWorldData world,Material template)
        {
            Color[] colors={new Color(.16f,.26f,.12f),new Color(.30f,.28f,.23f),new Color(.35f,.31f,.28f),new Color(.48f,.29f,.15f),new Color(.60f,.79f,.82f),new Color(.24f,.17f,.10f)};
            for(int i=0;i<colors.Length;i++)
            {
                var mat=Own(new Material(template){name="Survey prop "+i});
                mat.SetColor("_BaseColor",colors[i]);mat.SetFloat("_Surface",0);mat.SetFloat("_ZWrite",1);
                mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType","Opaque");mat.renderQueue=(int)RenderQueue.Geometry;mat.SetFloat("_Smoothness",i==4?.42f:.12f);
                propMaterials[i]=mat;
            }
            var batches=new Dictionary<Vector3Int,Geometry>();int processed=0;
            foreach(var item in world.Objects)
            {
                int kind=(int)item.Kind;var key=new Vector3Int(Mathf.FloorToInt(item.Position.x/160),Mathf.FloorToInt(item.Position.z/160),kind);
                if(!batches.TryGetValue(key,out var geometry)){geometry=new Geometry();batches.Add(key,geometry);}
                Quaternion rotation=Quaternion.Euler(0,item.Yaw,0);
                if(item.Kind==SurfaceObjectKind.Tree)
                {
                    float radius=Mathf.Max(.5f,item.Radius),height=item.Height>0?item.Height:radius*4.2f;
                    geometry.Cone(item.Position+Vector3.up*height*.17f,radius,height*.72f,rotation,7);
                    geometry.Cone(item.Position+Vector3.up*height*.48f,radius*.72f,height*.55f,rotation,7);
                    var trunkKey=new Vector3Int(key.x,key.y,5);
                    if(!batches.TryGetValue(trunkKey,out var trunk)){trunk=new Geometry();batches.Add(trunkKey,trunk);}
                    trunk.Cone(item.Position,radius*.14f,height*.6f,rotation,5);
                }
                else geometry.Rock(item.Position,Mathf.Max(.7f,item.Radius),rotation,item.Kind==SurfaceObjectKind.Ice);
                if(++processed%350==0)yield return null;
            }
            foreach(var batch in batches)
            {
                var mesh=Own(new Mesh{name=$"Survey props {batch.Key}",indexFormat=IndexFormat.UInt32});
                mesh.SetVertices(batch.Value.Vertices);mesh.SetTriangles(batch.Value.Triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=propMaterials[batch.Key.z];
                renderer.shadowCastingMode=ShadowCastingMode.On;
                if(++processed%8==0)yield return null;
            }
        }
        T Own<T>(T resource)where T:UnityEngine.Object{owned.Add(resource);return resource;}
        void OnDestroy(){foreach(var resource in owned)if(resource)Destroy(resource);owned.Clear();terrains.Clear();colliders.Clear();}
        sealed class Geometry
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<int> Triangles=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c){int start=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);Triangles.Add(start);Triangles.Add(start+1);Triangles.Add(start+2);}
            public void Cone(Vector3 p,float radius,float height,Quaternion rotation,int segments)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;
                    Triangle(p+rotation*new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius),p+rotation*new Vector3(Mathf.Sin(b)*radius,0,Mathf.Cos(b)*radius),p+Vector3.up*height);
                }
            }
            public void Rock(Vector3 p,float radius,Quaternion rotation,bool ice)
            {
                const int n=7;Vector3 top=p+Vector3.up*radius*(ice?1.65f:.9f);
                for(int i=0;i<n;i++)
                {
                    float a=i*2*Mathf.PI/n,b=(i+1)*2*Mathf.PI/n;
                    Vector3 v=p+rotation*new Vector3(Mathf.Sin(a)*radius,Mathf.Sin(i*17)*radius*.18f,Mathf.Cos(a)*radius*.8f);
                    Vector3 w=p+rotation*new Vector3(Mathf.Sin(b)*radius,Mathf.Sin((i+1)*17)*radius*.18f,Mathf.Cos(b)*radius*.8f);
                    Triangle(v,w,top);Triangle(v,p-Vector3.up*radius*.3f,w);
                }
            }
        }
    }
}
