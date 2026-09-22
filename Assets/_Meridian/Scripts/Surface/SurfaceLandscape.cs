using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meridian
{
    /// <summary>Terrain and resource rendering with an explicitly bounded gameplay working set.</summary>
    public sealed class SurfaceLandscape : MonoBehaviour
    {
        sealed class TileView {public Terrain terrain;public GameObject water;public TerrainData data;public Mesh mesh;}
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<Vector2Int,TileView> terrains=new Dictionary<Vector2Int,TileView>();
        readonly List<TerrainCollider> colliders=new List<TerrainCollider>();
        readonly Dictionary<int,Material> propMaterials=new Dictionary<int,Material>();
        readonly Dictionary<Vector3Int,List<SurfaceObjectData>> propSources=new Dictionary<Vector3Int,List<SurfaceObjectData>>();
        readonly Dictionary<Vector3Int,GameObject> propViews=new Dictionary<Vector3Int,GameObject>();
        readonly Dictionary<Vector2Int,SurfaceTileData> numerical=new Dictionary<Vector2Int,SurfaceTileData>();
        readonly HashSet<Vector3Int> dirtyProps=new HashSet<Vector3Int>();
        TerrainLayer[] layers;Material ground,water;Func<string,bool> removed;
        bool synchronizing,surfaceVisible=true;public int TerrainCount=>terrains.Count;public int PropBatchCount=>propViews.Count;
        public IReadOnlyList<TerrainCollider> Colliders=>colliders;
        public IEnumerator Build(SurfaceWorldData world,Material terrainMaterial,Material waterMaterial,SurfaceLoadProgress progress=null)
        {
            ground=terrainMaterial;water=waterMaterial;
            progress?.Report(.74f,"Preparing ground materials");
            yield return SurfaceGroundTextures.Create(resource=>owned.Add(resource),created=>layers=created,(done,total)=>progress?.Report(.74f+.08f*done/total,$"Preparing ground materials: {done} / {total}"));
            MakePropMaterials();Register(world.Tiles);int count=0;
            foreach(var tile in world.Tiles){yield return Upload(tile);count++;progress?.Report(.82f+.10f*count/world.Tiles.Length,$"Preparing landscape: {count} / {world.Tiles.Length} tiles");}
            Neighbors();count=0;
            foreach(var key in propSources.Keys){RebuildProp(key);if(++count%8==0){progress?.Report(.92f+.06f*count/Mathf.Max(1,propSources.Count),"Preparing forests and resources");yield return null;}}
            Physics.SyncTransforms();
        }
        public void Register(IEnumerable<SurfaceTileData> tiles)
        {
            foreach(var tile in tiles)
            {
                if(numerical.ContainsKey(tile.Address))continue;numerical.Add(tile.Address,tile);
                foreach(var item in tile.Objects)
                {
                    var key=Key(item);if(!propSources.TryGetValue(key,out var list)){list=new List<SurfaceObjectData>();propSources.Add(key,list);}list.Add(item);dirtyProps.Add(key);
                    if(item.Kind==SurfaceObjectKind.Tree){key.z=5;if(!propSources.TryGetValue(key,out list)){list=new List<SurfaceObjectData>();propSources.Add(key,list);}list.Add(item);dirtyProps.Add(key);}
                }
            }
        }
        public void SetSurfaceVisible(bool value)
        {
            surfaceVisible=value;
            foreach(var view in terrains.Values){view.terrain.drawHeightmap=value;if(view.water)view.water.SetActive(value);}
            foreach(var view in propViews.Values)view.SetActive(value);
        }
        public void SetRemovalQuery(Func<string,bool> query,bool refreshExisting=true){removed=query;if(refreshExisting)foreach(var key in propViews.Keys)dirtyProps.Add(key);}
        public void ResourceChanged(SurfaceObjectData item){if(item==null)return;var key=Key(item);dirtyProps.Add(key);if(item.Kind==SurfaceObjectKind.Tree){key.z=5;dirtyProps.Add(key);}}
        static Vector3Int Key(SurfaceObjectData item)=>new Vector3Int(Mathf.FloorToInt(item.Position.x/160),Mathf.FloorToInt(item.Position.z/160),(int)item.Kind);
        public void Maintain(Vector3 focus)
        {
            int done=0;foreach(var key in dirtyProps.ToArray()){if(propViews.ContainsKey(key))RebuildProp(key);dirtyProps.Remove(key);if(++done>=4)break;}
            if(!synchronizing)StartCoroutine(WorkingSet(focus));
        }
        IEnumerator WorkingSet(Vector3 focus)
        {
            synchronizing=true;var wanted=numerical.Values.OrderBy(t=>Vector2.SqrMagnitude(t.Origin+Vector2.one*t.Size*.5f-new Vector2(focus.x,focus.z))).Take(49).ToArray();
            var addresses=new HashSet<Vector2Int>(wanted.Select(t=>t.Address));
            foreach(var key in terrains.Keys.Where(k=>!addresses.Contains(k)).ToArray())Unload(key);
            foreach(var tile in wanted)if(!terrains.ContainsKey(tile.Address))yield return Upload(tile);
            Neighbors();int processed=0;
            bool Visible(Vector3Int key)=>new Vector2(key.x*160+80-focus.x,key.y*160+80-focus.z).sqrMagnitude<2200*2200;
            foreach(var key in propViews.Keys.Where(k=>!Visible(k)).ToArray())RemoveProp(key);
            foreach(var key in propSources.Keys)if(Visible(key)&&!propViews.ContainsKey(key)){RebuildProp(key);if(++processed%8==0)yield return null;}
            synchronizing=false;
        }
        IEnumerator Upload(SurfaceTileData tile)
        {
            var data=Own(new TerrainData{name=$"Meridian Terrain {tile.Address.x},{tile.Address.y}",heightmapResolution=tile.Heights.GetLength(0),alphamapResolution=tile.Layers.GetLength(0),baseMapResolution=512,size=new Vector3(tile.Size,tile.HeightRange,tile.Size)});
            data.terrainLayers=layers;data.SetHeights(0,0,tile.Heights);yield return null;data.SetAlphamaps(0,0,tile.Layers);
            var go=Terrain.CreateTerrainGameObject(data);go.name=data.name;go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(tile.Origin.x,tile.MinHeight,tile.Origin.y);
            var terrain=go.GetComponent<Terrain>();terrain.drawHeightmap=surfaceVisible;terrain.materialTemplate=ground;terrain.heightmapPixelError=3;terrain.basemapDistance=2000;terrain.drawInstanced=true;terrain.allowAutoConnect=false;terrain.shadowCastingMode=ShadowCastingMode.On;terrain.Flush();
            var view=new TileView{terrain=terrain,data=data};terrains.Add(tile.Address,view);colliders.Add(go.GetComponent<TerrainCollider>());
            if(tile.Water!=null&&tile.Water.Vertices.Length>0)
            {
                var mesh=Own(new Mesh{name="Surface water "+tile.Address,indexFormat=IndexFormat.UInt32});mesh.vertices=tile.Water.Vertices;mesh.triangles=tile.Water.Triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                var waterObject=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));waterObject.transform.SetParent(transform,false);waterObject.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=waterObject.GetComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;view.mesh=mesh;view.water=waterObject;waterObject.SetActive(surfaceVisible);
            }
            yield return null;
        }
        void Unload(Vector2Int key){var view=terrains[key];colliders.Remove(view.terrain.GetComponent<TerrainCollider>());Destroy(view.terrain.gameObject);if(view.water)Destroy(view.water);Release(view.data);Release(view.mesh);terrains.Remove(key);}
        void Neighbors()
        {foreach(var pair in terrains){Terrain At(int x,int z)=>terrains.TryGetValue(pair.Key+new Vector2Int(x,z),out var v)?v.terrain:null;pair.Value.terrain.SetNeighbors(At(-1,0),At(0,1),At(1,0),At(0,-1));}}
        public bool Pick(Ray ray,out RaycastHit hit)
        {hit=default;float distance=float.PositiveInfinity;bool found=false;foreach(var collider in colliders)if(collider&&collider.Raycast(ray,out var candidate,10000)&&candidate.distance<distance){found=true;distance=candidate.distance;hit=candidate;}return found;}
        void MakePropMaterials()
        {
            Color[] colors={new Color(.16f,.26f,.12f),new Color(.30f,.28f,.23f),new Color(.35f,.31f,.28f),new Color(.48f,.29f,.15f),new Color(.60f,.79f,.82f),new Color(.24f,.17f,.10f)};
            for(int i=0;i<colors.Length;i++){var mat=Own(new Material(water){name="Surface resource "+i});mat.SetColor("_BaseColor",colors[i]);mat.SetFloat("_Surface",0);mat.SetFloat("_ZWrite",1);mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Opaque");mat.renderQueue=(int)RenderQueue.Geometry;mat.SetFloat("_Smoothness",i==4?.42f:.12f);propMaterials[i]=mat;}
        }
        void RebuildProp(Vector3Int key)
        {
            RemoveProp(key);if(!propSources.TryGetValue(key,out var source))return;var geometry=new Geometry();
            foreach(var item in source)
            {
                if(removed?.Invoke(item.Id)??false)continue;Quaternion rotation=Quaternion.Euler(0,item.Yaw,0);
                if(item.Kind==SurfaceObjectKind.Tree)
                {
                    float radius=Mathf.Max(.5f,item.Radius),height=item.Height>0?item.Height:radius*4.2f;
                    if(key.z==5)geometry.Cone(item.Position,radius*.14f,height*.6f,rotation,5);
                    else{geometry.Cone(item.Position+Vector3.up*height*.17f,radius,height*.72f,rotation,7);geometry.Cone(item.Position+Vector3.up*height*.48f,radius*.72f,height*.55f,rotation,7);}
                }
                else geometry.Rock(item.Position,Mathf.Max(.7f,item.Radius),rotation,item.Kind==SurfaceObjectKind.Ice);
            }
            if(geometry.Vertices.Count==0)return;
            var mesh=Own(new Mesh{name="Surface resource batch "+key,indexFormat=IndexFormat.UInt32});mesh.SetVertices(geometry.Vertices);mesh.SetTriangles(geometry.Triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=propMaterials[key.z];go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.On;propViews.Add(key,go);go.SetActive(surfaceVisible);
        }
        void RemoveProp(Vector3Int key){if(!propViews.TryGetValue(key,out var go))return;Release(go.GetComponent<MeshFilter>().sharedMesh);Destroy(go);propViews.Remove(key);}
        T Own<T>(T resource)where T:UnityEngine.Object{owned.Add(resource);return resource;}
        void Release(UnityEngine.Object value){if(!value)return;owned.Remove(value);Destroy(value);}
        void OnDestroy(){StopAllCoroutines();foreach(var resource in owned)if(resource)Destroy(resource);owned.Clear();terrains.Clear();colliders.Clear();}
        sealed class Geometry
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();public readonly List<int> Triangles=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c){int i=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);Triangles.Add(i);Triangles.Add(i+1);Triangles.Add(i+2);}
            public void Cone(Vector3 p,float radius,float height,Quaternion rotation,int segments)
            {for(int i=0;i<segments;i++){float a=i*2*Mathf.PI/segments,b=(i+1)*2*Mathf.PI/segments;Triangle(p+rotation*new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius),p+rotation*new Vector3(Mathf.Sin(b)*radius,0,Mathf.Cos(b)*radius),p+Vector3.up*height);}}
            public void Rock(Vector3 p,float radius,Quaternion rotation,bool ice)
            {const int n=7;Vector3 top=p+Vector3.up*radius*(ice?1.65f:.9f);for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n,b=(i+1)*2*Mathf.PI/n;Vector3 v=p+rotation*new Vector3(Mathf.Sin(a)*radius,Mathf.Sin(i*17)*radius*.18f,Mathf.Cos(a)*radius*.8f),w=p+rotation*new Vector3(Mathf.Sin(b)*radius,Mathf.Sin((i+1)*17)*radius*.18f,Mathf.Cos(b)*radius*.8f);Triangle(v,w,top);Triangle(v,p-Vector3.up*radius*.3f,w);}}
        }
    }
}
