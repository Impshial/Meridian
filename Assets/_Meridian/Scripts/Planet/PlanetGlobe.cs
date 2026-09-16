using UnityEngine;

namespace Meridian
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
    public sealed class PlanetGlobe : MonoBehaviour
    {
        [SerializeField] private Material surfaceMaterial;
        private PlanetRenderResources resources;
        private bool ownsResources;
        public PlanetData Data { get; private set; }
        public MeshCollider Collider => GetComponent<MeshCollider>();
        public int TriangleCount => resources?.TriangleCount??0;
        public void Configure(Material material) => surfaceMaterial=material;

        public void Apply(PlanetData data)
        {
            Release();
            var created=new PlanetRenderResources();
            try{created.Create(data,surfaceMaterial);Attach(data,created);ownsResources=true;}
            catch{created.Dispose();throw;}
        }
        public void Attach(PlanetData data,PlanetRenderResources cached)
        {
            Release();Data=data;resources=cached;ownsResources=false;
            GetComponent<MeshFilter>().sharedMesh=cached.Mesh;Collider.sharedMesh=cached.Mesh;
            GetComponent<MeshRenderer>().sharedMaterial=cached.Material;
        }
        public PlanetRenderResources TransferOwnership(){ownsResources=false;return resources;}
        public bool Pick(Ray ray,out RaycastHit hit)
        { hit=default;return Collider.sharedMesh && Collider.Raycast(ray,out hit,100f); }
        public void Release()
        {
            Collider.sharedMesh=null;GetComponent<MeshFilter>().sharedMesh=null;
            GetComponent<MeshRenderer>().sharedMaterial=null;
            if(ownsResources)resources?.Dispose();
            resources=null;ownsResources=false;Data=null;
        }
        private void OnDestroy()=>Release();
    }

    /// <summary>One session-owned GPU cache; views attach without duplicating the 4K maps.</summary>
    public sealed class PlanetRenderResources : System.IDisposable
    {
        public Mesh Mesh {get;private set;}
        public Material Material {get;private set;}
        public int TriangleCount {get;private set;}
        private Texture2D colorMap,normalMap,surfaceMap;
        public void Create(PlanetData data,Material template)
        {
            var geometry=new SphericalGraph(data.Parameters.meshSubdivisions);
            var vertices=new Vector3[geometry.Directions.Length];
            for(int i=0;i<vertices.Length;i++)vertices[i]=geometry.Directions[i]*(1+Mathf.Max(0,data.Sample(geometry.Directions[i]).Elevation)*data.Parameters.relief);
            Mesh=new Mesh {name="Generated Meridian Globe",vertices=vertices,triangles=geometry.Triangles};
            TriangleCount=geometry.Triangles.Length/3;
            Mesh.RecalculateNormals();Mesh.RecalculateBounds();
            colorMap=Map("Planet Color",data,data.ColorMap,false,true);
            normalMap=Map("Planet Object Normals",data,data.NormalMap,true,true);
            surfaceMap=Map("Planet Surface Classification",data,data.SurfaceMap,true,false);
            Material=new Material(template){name="Generated Meridian Surface"};
            Material.SetTexture("_ColorMap",colorMap);Material.SetTexture("_NormalMap",normalMap);
            Material.SetTexture("_SurfaceMap",surfaceMap);
            uint seed=unchecked((uint)data.Seed);
            Material.SetVector("_TerrainOffset",new Vector4(seed%997,(seed>>10)%991,(seed>>20)%983,0));
            data.ReleaseAppearanceBuffers();
        }
        static Texture2D Map(string name,PlanetData data,Color32[] pixels,bool linear,bool mipmaps)
        {
            // Every base texel is overwritten and all appearance mips are generated before use.
            var texture=new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,mipmaps,linear,true)
            {name=name,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,
                filterMode=mipmaps?FilterMode.Trilinear:FilterMode.Bilinear,anisoLevel=mipmaps?4:1};
            try{texture.SetPixels32(pixels);texture.Apply(mipmaps,true);return texture;}
            catch{UnityEngine.Object.Destroy(texture);throw;}
        }
        public void Dispose()
        {
            if(Mesh)UnityEngine.Object.Destroy(Mesh);if(Material)UnityEngine.Object.Destroy(Material);
            if(colorMap)UnityEngine.Object.Destroy(colorMap);if(normalMap)UnityEngine.Object.Destroy(normalMap);if(surfaceMap)UnityEngine.Object.Destroy(surfaceMap);
            Mesh=null;Material=null;colorMap=normalMap=surfaceMap=null;TriangleCount=0;
        }
    }
}
