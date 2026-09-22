using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meridian.Colony
{
    public sealed class ColonyUtilityMarker:MonoBehaviour {public string type;public bool port;public int side;}
    public sealed partial class ColonyVisuals
    {
        readonly Dictionary<string,Material> utilityMaterials=new Dictionary<string,Material>();
        GameObject utilityFloor;Mesh floorMesh;Vector3 floorCenter;float floorSpan;
        public bool UtilitiesVisible=>sim.State.utilityView!=0;
        Material UtilityMaterial(string type)
        {
            if(utilityMaterials.TryGetValue(type,out var material))return material;
            material=new Material(Resources.Load<Shader>("Colony/UtilityConnections")){name="Underground "+type};
            material.SetColor("_Color",type=="pipe"?new Color(.24f,.77f,1):type=="floor"?new Color(.045f,.075f,.095f):new Color(1,.65f,.20f));
            material.renderQueue=type=="floor"?(int)RenderQueue.Geometry:(int)RenderQueue.Transparent+20;
            material.SetFloat("_Grid",type=="floor"?1:0);material.SetFloat("_ZTest",type=="floor"?(float)CompareFunction.LessEqual:(float)CompareFunction.Always);
            utilityMaterials.Add(type,material);return material;
        }
        GameObject UtilityLink(StructureState b)
        {
            var root=new GameObject(b.name);root.AddComponent<ColonyPickTarget>().id=b.id;root.AddComponent<ColonyUtilityMarker>().type=b.definition;
            var path=ColonyUtilities.Path(sim.World,b.position,b.end);
            ColonyModels.Line(root.transform,"Buried service line",path,UtilityMaterial(b.definition),.5f);
            // Colliders belong to the service layer and never enter the walking graph.
            for(int i=1;i<path.Length;i++)
            {
                var segment=new GameObject("Service pick segment");segment.transform.SetParent(root.transform,false);
                segment.transform.SetPositionAndRotation((path[i-1]+path[i])*.5f,Quaternion.LookRotation(path[i]-path[i-1]));
                var box=segment.AddComponent<BoxCollider>();box.size=new Vector3(1.6f,1.6f,Vector3.Distance(path[i-1],path[i]));box.isTrigger=true;
            }
            return root;
        }
        void AddUtilityPorts(GameObject root,StructureState b)
        {
            if(b.definition=="cargo"||sim.Definition(b).link)return;
            var group=new GameObject("Underground service ports");group.transform.SetParent(root.transform,false);
            foreach(var type in new[]{"cable","pipe"})for(int side=0;side<4;side++)
            {
                var port=GameObject.CreatePrimitive(type=="pipe"?PrimitiveType.Sphere:PrimitiveType.Cube);port.name=type=="pipe"?"Water port · -3 m":"Power port · -3 m";
                port.transform.SetParent(group.transform,true);port.transform.position=ColonyUtilities.PortOnSide(sim,b,side,type);port.transform.localScale=Vector3.one*1.1f;
                port.GetComponent<Renderer>().sharedMaterial=UtilityMaterial(type);port.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                port.GetComponent<Collider>().isTrigger=true;var marker=port.AddComponent<ColonyUtilityMarker>();marker.type=type;marker.port=true;marker.side=side;
            }
        }
        void UtilityVisibility(GameObject view,StructureState b)
        {
            if(ColonyUtilities.Underground(sim.Definition(b))){view.SetActive(UtilitiesVisible);return;}
            var ports=view.transform.Find("Underground service ports");if(ports){ports.gameObject.SetActive(UtilitiesVisible);if(UtilitiesVisible)foreach(var marker in ports.GetComponentsInChildren<ColonyUtilityMarker>())marker.transform.position=ColonyUtilities.PortOnSide(sim,b,marker.side,marker.type);}
        }
        string PickUtility(Ray ray,string type)
        {
            if(!UtilitiesVisible)return null;
            foreach(var hit in Physics.RaycastAll(ray,10000,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance))
            {
                var marker=hit.collider.GetComponentInParent<ColonyUtilityMarker>();
                if(!marker||type!=null&&(!marker.port||marker.type!=type)||type==null&&marker.port)continue;
                var target=hit.collider.GetComponentInParent<ColonyPickTarget>();if(target)return target.id;
            }
            return null;
        }
        void UpdateUtilityFloor()
        {
            bool cutaway=sim.State.utilityView==1;
            if(utilityFloor)utilityFloor.SetActive(cutaway);if(!cutaway)return;
            var camera=ColonyRuntime.Current?.Camera;if(!camera)return;
            var center=camera.Pivot;float span=Mathf.Ceil(Mathf.Max(800,camera.Distance*5)/100)*100;
            if(utilityFloor&&(center-floorCenter).sqrMagnitude<2500&&Mathf.Abs(span-floorSpan)<50)return;
            floorCenter=center;floorSpan=span;
            if(!utilityFloor){utilityFloor=new GameObject("Underground survey grid",typeof(MeshFilter),typeof(MeshRenderer));utilityFloor.transform.SetParent(transform,false);utilityFloor.GetComponent<Renderer>().sharedMaterial=UtilityMaterial("floor");floorMesh=new Mesh{name="Underground survey floor"};utilityFloor.GetComponent<MeshFilter>().sharedMesh=floorMesh;}
            const int n=48;var vertices=new Vector3[(n+1)*(n+1)];var triangles=new int[n*n*6];
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){var p=center+new Vector3((x/(float)n-.5f)*span,0,(z/(float)n-.5f)*span);vertices[z*(n+1)+x]=sim.World.Ground(p)-Vector3.up*9;}
            int k=0;for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=z*(n+1)+x;triangles[k++]=i;triangles[k++]=i+n+1;triangles[k++]=i+1;triangles[k++]=i+1;triangles[k++]=i+n+1;triangles[k++]=i+n+2;}
            floorMesh.Clear();floorMesh.vertices=vertices;floorMesh.triangles=triangles;floorMesh.RecalculateBounds();
        }
        void ReleaseUtilities(){foreach(var material in utilityMaterials.Values)if(material)Destroy(material);if(floorMesh)Destroy(floorMesh);}
    }
}
