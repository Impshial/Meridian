using UnityEngine;

namespace Meridian
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
    public sealed class PlanetGlobe : MonoBehaviour
    {
        [SerializeField] private Material surfaceMaterial;
        private Mesh generatedMesh;
        private Material generatedMaterial;
        private Texture2D colorMap,normalMap,surfaceMap;
        public PlanetData Data { get; private set; }
        public MeshCollider Collider => GetComponent<MeshCollider>();
        public int TriangleCount => generatedMesh ? generatedMesh.triangles.Length/3 : 0;
        public void Configure(Material material) => surfaceMaterial=material;

        public void Apply(PlanetData data)
        {
            Release(); Data=data;
            var geometry=new SphericalGraph(data.Parameters.meshSubdivisions);
            var vertices=new Vector3[geometry.Directions.Length];
            for(int i=0;i<vertices.Length;i++)vertices[i]=geometry.Directions[i]*(1+Mathf.Max(0,data.Sample(geometry.Directions[i]).Elevation)*data.Parameters.relief);
            generatedMesh=new Mesh {name="Generated Meridian Globe",vertices=vertices,triangles=geometry.Triangles};
            generatedMesh.RecalculateNormals();generatedMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh=generatedMesh;Collider.sharedMesh=generatedMesh;
            colorMap=Map("Planet Color",data,data.ColorMap,false,true);
            normalMap=Map("Planet Object Normals",data,data.NormalMap,true,true);
            surfaceMap=Map("Planet Surface Classification",data,data.SurfaceMap,true,false);
            generatedMaterial=new Material(surfaceMaterial){name="Generated Meridian Surface"};
            generatedMaterial.SetTexture("_ColorMap",colorMap);generatedMaterial.SetTexture("_NormalMap",normalMap);
            generatedMaterial.SetTexture("_SurfaceMap",surfaceMap);
            uint seed=unchecked((uint)data.Seed);
            generatedMaterial.SetVector("_TerrainOffset",new Vector4(seed%997,(seed>>10)%991,(seed>>20)%983,0));
            GetComponent<MeshRenderer>().sharedMaterial=generatedMaterial;
            data.ReleaseAppearanceBuffers();
        }
        static Texture2D Map(string name,PlanetData data,Color32[] pixels,bool linear,bool mipmaps)
        {
            // Every base texel is overwritten and all appearance mips are generated before use.
            var texture=new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,mipmaps,linear,true)
            {name=name,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,
                filterMode=mipmaps?FilterMode.Trilinear:FilterMode.Bilinear,anisoLevel=mipmaps?4:1};
            texture.SetPixels32(pixels);texture.Apply(mipmaps,true);return texture;
        }
        public bool Pick(Ray ray,out RaycastHit hit)
        { hit=default;return Collider.sharedMesh && Collider.Raycast(ray,out hit,100f); }
        public void Release()
        {
            Collider.sharedMesh=null;GetComponent<MeshFilter>().sharedMesh=null;
            GetComponent<MeshRenderer>().sharedMaterial=null;
            if(generatedMesh)Destroy(generatedMesh);if(generatedMaterial)Destroy(generatedMaterial);
            if(colorMap)Destroy(colorMap);if(normalMap)Destroy(normalMap);if(surfaceMap)Destroy(surfaceMap);
            generatedMesh=null;generatedMaterial=null;colorMap=normalMap=surfaceMap=null;Data=null;
        }
        private void OnDestroy()=>Release();
    }
}
