using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meridian.Colony
{
    public sealed class ColonyPickTarget:MonoBehaviour {public string id;}
    public sealed class ColonyModelResources:MonoBehaviour
    {public readonly List<Object> owned=new List<Object>();void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);}}
    /// <summary>Imported hero assets combined with a coherent modular kit for foundations, interiors and spacecraft.</summary>
    public static class ColonyModels
    {
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        public static Material Material(string name,Color color,float metal=.15f,float smooth=.35f)
        {
            if(materials.TryGetValue(name,out var found)&&found)return found;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Meridian "+name,enableInstancing=true};mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",smooth);materials[name]=mat;return mat;
        }
        public static Material White=>Material("Ceramic",new Color(.73f,.77f,.77f),.32f,.46f);
        public static Material Dark=>Material("Graphite",new Color(.055f,.085f,.10f),.65f,.36f);
        public static Material Metal=>Material("Titanium",new Color(.32f,.42f,.44f),.78f,.52f);
        public static Material Amber=>Material("Safety amber",new Color(.98f,.55f,.12f),.2f,.35f);
        public static Material Green=>Material("Garden",new Color(.22f,.44f,.2f),0,.18f);
        public static Material Blue=>Material("Blue glass",new Color(.10f,.33f,.43f),.6f,.85f);
        public static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 at,Vector3 scale,Material material,Quaternion? rotation=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;go.transform.localRotation=rotation??Quaternion.identity;go.GetComponent<Renderer>().sharedMaterial=material;Object.Destroy(go.GetComponent<Collider>());return go;
        }
        public static GameObject Imported(string key,Transform parent,Vector3 at,Vector3 dimensions)
        {
            var prefab=Resources.Load<GameObject>("Colony/Models/"+key);if(!prefab)return null;
            var go=Object.Instantiate(prefab,parent);go.name=key;go.transform.localPosition=Vector3.zero;
            Bounds bounds=new Bounds();bool first=true;
            foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var box=filter.sharedMesh.bounds;for(int z=-1;z<=1;z+=2)for(int y=-1;y<=1;y+=2)for(int x=-1;x<=1;x+=2)
                {var corner=go.transform.InverseTransformPoint(filter.transform.TransformPoint(box.center+Vector3.Scale(box.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(corner,Vector3.zero);first=false;}else bounds.Encapsulate(corner);}
            }
            // Authoring normalizes horizontal size; retain model proportions within the requested footprint.
            float max=Mathf.Max(bounds.size.x,bounds.size.z);float s=Mathf.Min(dimensions.x,dimensions.z)/Mathf.Max(.001f,max);if(bounds.size.y*s>dimensions.y)s=dimensions.y/Mathf.Max(.001f,bounds.size.y);
            go.transform.localScale=Vector3.one*s;go.transform.localPosition=at;return go;
        }
        public static LineRenderer Line(Transform parent,string name,IEnumerable<Vector3> points,Material material,float width,bool loop=false)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=material;line.startWidth=line.endWidth=width;line.loop=loop;var array=points.ToArray();line.positionCount=array.Length;line.SetPositions(array);line.numCornerVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;return line;
        }
        public static GameObject Structure(StructureState b,BuildingDefinition d,ColonyWorld world=null)
        {
            var root=new GameObject(b.name);root.transform.SetPositionAndRotation(b.position,Quaternion.Euler(0,b.yaw,0));root.AddComponent<ColonyPickTarget>().id=b.id;
            var physics=root.AddComponent<BoxCollider>();physics.center=Vector3.up*d.height*.5f;physics.size=new Vector3(d.size.x,d.height,d.size.y);
            if(d.link){Object.Destroy(physics);Link(root,b,d);return root;}
            root.transform.position+=Vector3.up*b.foundation;
            var baseGroup=new GameObject("Foundation and ports");baseGroup.transform.SetParent(root.transform,false);
            if(world!=null)for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
            {var local=new Vector3(x*d.size.x*.42f,0,z*d.size.y*.42f);var at=root.transform.TransformPoint(local);float depth=Mathf.Max(.15f,at.y-world.Height(at.x,at.z));Part(baseGroup.transform,"Adjustable foundation pier",PrimitiveType.Cylinder,local-Vector3.up*depth*.5f,new Vector3(.65f,depth*.5f,.65f),Metal);}
            Part(baseGroup.transform,"Foundation",PrimitiveType.Cube,new Vector3(0,.22f,0),new Vector3(d.size.x+.7f,.6f,d.size.y+.7f),Dark);
            for(int i=0;i<4;i++){float angle=i*90;var q=Quaternion.Euler(0,angle,0);var direction=q*Vector3.forward;float distance=(i%2==0?d.size.y:d.size.x)*.5f;Part(baseGroup.transform,"Connection port",PrimitiveType.Cube,direction*distance+Vector3.up*1.5f,new Vector3(2.8f,2.8f,.45f),White,q);Part(baseGroup.transform,"Port light",PrimitiveType.Cube,direction*(distance+.3f)+Vector3.up*2.5f,new Vector3(1.5f,.14f,.12f),Amber,q);}
            if(b.definition=="ship"){var ship=Dropship();ship.transform.SetParent(root.transform,false);ship.transform.localPosition=Vector3.up*.5f;Object.Destroy(physics);var hull=root.AddComponent<BoxCollider>();hull.center=new Vector3(0,4,0);hull.size=new Vector3(14,8,26);}
            else if(d.sealedModule&&b.definition!="airlock")Dome(root,b,d);
            else if(b.definition=="apron"||b.definition=="spaceport")
            {
                var corners=new[]{new Vector3(-d.size.x*.42f,.57f,-d.size.y*.42f),new Vector3(d.size.x*.42f,.57f,-d.size.y*.42f),new Vector3(d.size.x*.42f,.57f,d.size.y*.42f),new Vector3(-d.size.x*.42f,.57f,d.size.y*.42f)};Line(root.transform,"Apron landing lights",corners,Amber,.2f,true);
                Part(root.transform,"Landing H",PrimitiveType.Cube,new Vector3(0,.55f,0),new Vector3(5,.04f,.6f),White);foreach(int x in new[]{-1,1})Part(root.transform,"Landing H side",PrimitiveType.Cube,new Vector3(x*2.5f,.55f,0),new Vector3(.6f,.04f,5),White);
                var vestibule=new GameObject("Exterior");vestibule.transform.SetParent(root.transform,false);float length=d.size.y*.5f;
                foreach(int side in new[]{-1,1})Part(vestibule.transform,"Enclosed passenger gangway",PrimitiveType.Cube,new Vector3(side*1.5f,2,length*.5f),new Vector3(.2f,3,length),White);
                Part(vestibule.transform,"Passenger gangway roof",PrimitiveType.Cube,new Vector3(0,3.5f,length*.5f),new Vector3(3.2f,.2f,length),Blue);
            }
            else if(Imported(d.model,root.transform,new Vector3(0,.55f,0),new Vector3(d.size.x*.86f,d.height,d.size.y*.86f))==null)Equipment(root,b,d);
            Combine(baseGroup.transform);return root;
        }
        static void Dome(GameObject root,StructureState b,BuildingDefinition d)
        {
            var exterior=new GameObject("Exterior");exterior.transform.SetParent(root.transform,false);
            if(!Imported("HabitatDome",exterior.transform,new Vector3(0,.5f,0),new Vector3(d.size.x,d.height,d.size.y)))Part(exterior.transform,"Dome shell",PrimitiveType.Sphere,new Vector3(0,.9f,0),new Vector3(d.size.x,d.height*2,d.size.y),White);
            var frame=new GameObject("Structural frame");frame.transform.SetParent(root.transform,false);
            for(int rib=0;rib<8;rib++){var points=new List<Vector3>();float bearing=rib*Mathf.PI/4;for(int i=0;i<=16;i++){float a=i*Mathf.PI/16;points.Add(new Vector3(Mathf.Cos(a)*Mathf.Cos(bearing)*d.size.x*.47f,.7f+Mathf.Sin(a)*d.height,Mathf.Cos(a)*Mathf.Sin(bearing)*d.size.y*.47f));}Line(frame.transform,"Pressure rib",points,Metal,.14f);}
            var rim=new List<Vector3>();for(int i=0;i<64;i++){float a=i*Mathf.PI/32;rim.Add(new Vector3(Mathf.Cos(a)*d.size.x*.47f,.7f,Mathf.Sin(a)*d.size.y*.47f));}Line(frame.transform,"Base pressure ring",rim,White,.25f,true);
            var inside=new GameObject("Interior");inside.transform.SetParent(root.transform,false);
            Part(inside.transform,"Interior floor",PrimitiveType.Cylinder,new Vector3(0,.64f,0),new Vector3(d.size.x*.92f,.09f,d.size.y*.92f),Material("Floor",new Color(.25f,.29f,.30f),.1f,.2f));
            string purpose=b.definition;int count=purpose=="habitat"||purpose=="lodge"?8:purpose=="greenhouse"||purpose=="garden"?8:6;
            for(int i=0;i<count;i++)
            {
                float x=(i%2==0?-1:1)*d.size.x*.25f,z=((i/2)-1.5f)*d.size.y*.18f;var p=new Vector3(x,1,z);
                if(purpose=="habitat"||purpose=="lodge")
                {Part(inside.transform,"Bed frame",PrimitiveType.Cube,p,new Vector3(2,.5f,2.8f),Metal);Part(inside.transform,"Mattress",PrimitiveType.Cube,p+Vector3.up*.32f,new Vector3(1.8f,.3f,2.6f),White);Part(inside.transform,"Blanket",PrimitiveType.Cube,p+new Vector3(0,.49f,-.35f),new Vector3(1.85f,.08f,1.7f),Blue);}
                else if(purpose=="greenhouse"||purpose=="garden"||purpose=="field")
                {Part(inside.transform,"Grow tray",PrimitiveType.Cube,p,new Vector3(2.8f,.6f,2),White);for(int k=0;k<6;k++)Part(inside.transform,"Crop",PrimitiveType.Sphere,p+new Vector3((k%3-1)*.7f,.7f,k/3*.65f-.3f),new Vector3(.6f,.6f,.6f),Green);}
                else if(purpose=="canteen"||purpose=="restaurant"||purpose=="lounge")
                {Part(inside.transform,"Table",PrimitiveType.Cylinder,p+Vector3.up*.4f,new Vector3(1.8f,.1f,1.8f),White);Part(inside.transform,"Table pedestal",PrimitiveType.Cylinder,p,new Vector3(.25f,.45f,.25f),Metal);for(int k=-1;k<=1;k+=2)Part(inside.transform,"Seat",PrimitiveType.Cube,p+new Vector3(k*1.2f,0,0),new Vector3(.8f,.5f,.8f),Blue);}
                else if(purpose=="warehouse")
                {Part(inside.transform,"Storage rack",PrimitiveType.Cube,p+Vector3.up,new Vector3(2,.14f,2),Metal);for(int k=0;k<3;k++)Part(inside.transform,"Cargo bins",PrimitiveType.Cube,p+Vector3.up*k*.55f,new Vector3(1.8f,.5f,1.6f),k%2==0?White:Amber);}
                else
                {Part(inside.transform,purpose=="clinic"?"Treatment station":"Work station",PrimitiveType.Cube,p,new Vector3(2.4f,.9f,1.6f),White);Part(inside.transform,"Instrument console",PrimitiveType.Cube,p+new Vector3(0,.85f,.5f),new Vector3(1.8f,.8f,.14f),Dark,Quaternion.Euler(-18,0,0));Part(inside.transform,"Display",PrimitiveType.Cube,p+new Vector3(0,.88f,.38f),new Vector3(1.5f,.55f,.05f),Blue,Quaternion.Euler(-18,0,0));}
            }
            Combine(inside.transform);
        }
        static void Equipment(GameObject root,StructureState b,BuildingDefinition d)
        {
            var group=new GameObject("Equipment");group.transform.SetParent(root.transform,false);
            if(b.definition=="solar")
            {for(int i=-1;i<=1;i++){Part(group.transform,"Panel support",PrimitiveType.Cylinder,new Vector3(i*d.size.x*.3f,1,0),new Vector3(.35f,1,.35f),Metal);Part(group.transform,"Solar array",PrimitiveType.Cube,new Vector3(i*d.size.x*.3f,2.6f,0),new Vector3(d.size.x*.29f,.2f,d.size.y*.86f),Blue,Quaternion.Euler(-18,0,0));for(int k=-2;k<=2;k++)Part(group.transform,"Panel conductor",PrimitiveType.Cube,new Vector3(i*d.size.x*.3f,2.8f,k*d.size.y*.15f),new Vector3(d.size.x*.29f,.025f,.035f),Metal,Quaternion.Euler(-18,0,0));}}
            else if(b.definition=="wind")
            {Part(group.transform,"Turbine tower",PrimitiveType.Cylinder,new Vector3(0,d.height*.43f,0),new Vector3(1,d.height*.43f,1),White);var rotor=new GameObject("Rotor");rotor.transform.SetParent(root.transform,false);rotor.transform.localPosition=new Vector3(0,d.height*.84f,0);for(int i=0;i<3;i++){var q=Quaternion.Euler(0,0,i*120);Part(rotor.transform,"Turbine blade",PrimitiveType.Cube,q*Vector3.up*3,new Vector3(.45f,6,.2f),White,q);}}
            else if(b.definition=="tank"||b.definition=="battery")
            {for(int i=-1;i<=1;i++)Part(group.transform,"Containment cylinder",PrimitiveType.Cylinder,new Vector3(i*d.size.x*.27f,d.height*.5f,0),new Vector3(d.size.x*.24f,d.height*.5f,d.size.y*.6f),i==0?White:Metal);}
            else if(b.definition=="park"||b.definition=="field")
            {for(int i=0;i<10;i++)Part(group.transform,b.definition=="park"?"Scenic planting":"Outdoor grow row",PrimitiveType.Cube,new Vector3((i%2-.5f)*d.size.x*.5f,.9f,(i/2-2)*d.size.y*.15f),new Vector3(d.size.x*.35f,.6f,d.size.y*.08f),Green);}
            else
            {Part(group.transform,"Machine housing",PrimitiveType.Cube,new Vector3(0,d.height*.35f,0),new Vector3(d.size.x*.72f,d.height*.7f,d.size.y*.7f),White);for(int i=-1;i<=1;i++)Part(group.transform,"Heat exchanger",PrimitiveType.Cube,new Vector3(i*d.size.x*.2f,d.height*.55f,-d.size.y*.37f),new Vector3(.25f,d.height*.7f,.7f),Dark);Part(group.transform,"Control interface",PrimitiveType.Cube,new Vector3(0,1.5f,d.size.y*.36f),new Vector3(1.2f,.8f,.2f),Blue);}
            Combine(group.transform);
        }
        static void Link(GameObject root,StructureState b,BuildingDefinition d)
        {
            root.transform.rotation=Quaternion.identity;Vector3 delta=b.end-b.position;float length=delta.magnitude;var q=Quaternion.LookRotation(delta.normalized==Vector3.zero?Vector3.forward:delta.normalized);
            if(b.definition=="corridor")
            {
                var exterior=new GameObject("Exterior");exterior.transform.SetParent(root.transform,false);
                foreach(int sign in new[]{-1,1})Part(exterior.transform,"Sealed side wall",PrimitiveType.Cube,delta*.5f+q*Vector3.right*sign*1.58f+Vector3.up*1.5f,new Vector3(.2f,3,length),White,q);
                Part(exterior.transform,"Observation roof",PrimitiveType.Cube,delta*.5f+Vector3.up*3,new Vector3(3.3f,.2f,length),Blue,q);
                Part(root.transform,"Passage floor",PrimitiveType.Cube,delta*.5f-Vector3.up*.1f,new Vector3(3.3f,.2f,length),Metal,q);
                for(float x=2;x<length;x+=5){var p=delta.normalized*x;Line(root.transform,"Bulkhead frame",new[]{p+q*Vector3.left*1.65f,p+q*Vector3.left*1.65f+Vector3.up*3.1f,p+q*Vector3.right*1.65f+Vector3.up*3.1f,p+q*Vector3.right*1.65f},White,.15f);}
                var boundary=new GameObject("Passage physical boundary");boundary.transform.SetParent(root.transform,false);boundary.transform.localPosition=delta*.5f+Vector3.up*1.5f;boundary.transform.localRotation=q;var col=boundary.AddComponent<BoxCollider>();col.size=new Vector3(3.3f,3,length);
            }
            else Line(root.transform,b.definition,new[]{Vector3.up*.35f,delta+Vector3.up*.35f},b.definition=="pipe"?Blue:Amber,b.definition=="pipe"?.3f:.16f);
        }
        public static GameObject Dropship()
        {
            var root=new GameObject("Expedition dropship");var fixedParts=new GameObject("Spaceframe");fixedParts.transform.SetParent(root.transform,false);
            ShipHull(fixedParts.transform);
            Part(fixedParts.transform,"Cockpit glazing",PrimitiveType.Sphere,new Vector3(0,4.65f,7.2f),new Vector3(5.8f,1.7f,5.8f),Blue);
            for(int i=0;i<5;i++)
            {
                Part(fixedParts.transform,"Dorsal hull plate",PrimitiveType.Cube,new Vector3(0,6.08f,-5.8f+i*2.5f),new Vector3(5.8f,.14f,2.2f),i==3?Dark:White);
                foreach(int side in new[]{-1,1}){Part(fixedParts.transform,"Cargo module reinforcement",PrimitiveType.Cube,new Vector3(side*4.44f,3.8f,-6+i*2.5f),new Vector3(.1f,2.4f,.22f),Metal);Part(fixedParts.transform,"Engine ventilation slot",PrimitiveType.Cube,new Vector3(side*5.2f,4.23f,-4+i*1.8f),new Vector3(1.5f,.12f,.3f),Dark);}
            }
            for(int sign=-1;sign<=1;sign+=2)
            {
                Part(fixedParts.transform,"Engine nacelle",PrimitiveType.Capsule,new Vector3(sign*5.2f,2.5f,0),new Vector3(3.5f,8.5f,3.5f),Metal,Quaternion.Euler(90,0,0));
                Part(fixedParts.transform,"Engine cowling",PrimitiveType.Cylinder,new Vector3(sign*5.2f,2.5f,-7.6f),new Vector3(2.7f,.7f,2.7f),Dark,Quaternion.Euler(90,0,0));
                foreach(int z in new[]{-1,1})
                {Part(fixedParts.transform,"Landing strut",PrimitiveType.Cylinder,new Vector3(sign*5,1,z*7),new Vector3(.45f,1.2f,.45f),Metal);Part(fixedParts.transform,"Landing foot",PrimitiveType.Cube,new Vector3(sign*5,.1f,z*7),new Vector3(2.5f,.3f,2),Dark);
                 Part(fixedParts.transform,"Ventral thruster nozzle",PrimitiveType.Cylinder,new Vector3(sign*3,.9f,z*5),new Vector3(1.7f,.5f,1.7f),Dark);
                 var flame=Part(root.transform,"Booster",PrimitiveType.Cylinder,new Vector3(sign*3,-1.2f,z*5),new Vector3(1.1f,2,1.1f),Material("Exhaust",new Color(.35f,.72f,1),0,.2f));flame.SetActive(false);}
                Part(fixedParts.transform,"Safety rail",PrimitiveType.Cube,new Vector3(sign*4.5f,5.3f,0),new Vector3(.2f,.2f,13),Amber);
            }
            Part(fixedParts.transform,"Cargo bay shutters",PrimitiveType.Cube,new Vector3(0,3,-8.1f),new Vector3(6.8f,3.8f,.25f),Dark);
            var ramp=Part(root.transform,"Ramp",PrimitiveType.Cube,new Vector3(0,.55f,-10.2f),new Vector3(6,.2f,5),Metal,Quaternion.Euler(8,0,0));Combine(fixedParts.transform);return root;
        }
        static void ShipHull(Transform parent)
        {
            var go=new GameObject("Faceted cargo fuselage",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            float[] z={-8,-6,4.5f,8.3f,11.4f},width={3.4f,4.4f,4.4f,3.5f,1.1f},height={1.8f,2.55f,2.55f,1.8f,.6f};
            Vector2[] section={new Vector2(-.72f,1),new Vector2(.72f,1),new Vector2(1,.55f),new Vector2(1,-.65f),new Vector2(.7f,-1),new Vector2(-.7f,-1),new Vector2(-1,-.65f),new Vector2(-1,.55f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Vector3 Point(int ring,int i)=>new Vector3(section[i%8].x*width[ring],3.6f+section[i%8].y*height[ring],z[ring]);
            void Triangle(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
            for(int ring=0;ring<z.Length-1;ring++)for(int i=0;i<8;i++){Triangle(Point(ring,i),Point(ring+1,i),Point(ring,i+1));Triangle(Point(ring,i+1),Point(ring+1,i),Point(ring+1,i+1));}
            for(int i=1;i<7;i++){Triangle(Point(0,0),Point(0,i),Point(0,i+1));Triangle(Point(4,0),Point(4,i+1),Point(4,i));}
            var mesh=new Mesh{name="Meridian cargo hull"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=White;
            // The combined spaceframe copies this geometry; destroy the source mesh with its source object.
            go.AddComponent<ColonyModelResources>().owned.Add(mesh);
        }
        public static GameObject Actor(ActorState a)
        {
            var root=new GameObject(a.name);root.AddComponent<ColonyPickTarget>().id=a.id;
            var collider=root.AddComponent<CapsuleCollider>();collider.radius=.7f;collider.height=2;collider.center=Vector3.up;
            if(a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor)
            {
                Color[] colors={new Color(.83f,.51f,.18f),new Color(.35f,.66f,.31f),new Color(.85f,.87f,.9f),new Color(.38f,.55f,.86f),new Color(.62f,.45f,.77f),new Color(.67f,.28f,.29f)};var uniform=Material("Uniform "+a.profession,colors[(int)a.profession],.05f,.2f);
                Part(root.transform,"Torso",PrimitiveType.Capsule,new Vector3(0,1.05f,0),new Vector3(.65f,.48f,.38f),uniform);
                var head=Part(root.transform,"Head",PrimitiveType.Sphere,new Vector3(0,1.78f,0),Vector3.one*.38f,Material("Skin",new Color(.66f,.47f,.34f),0,.15f));
                Part(root.transform,"Visor",PrimitiveType.Cube,new Vector3(0,1.8f,.19f),new Vector3(.27f,.1f,.045f),Dark);
                foreach(int s in new[]{-1,1}){Part(root.transform,s<0?"Left leg":"Right leg",PrimitiveType.Capsule,new Vector3(s*.17f,.43f,0),new Vector3(.22f,.43f,.24f),Dark);Part(root.transform,s<0?"Left arm":"Right arm",PrimitiveType.Capsule,new Vector3(s*.42f,1.03f,0),new Vector3(.19f,.38f,.21f),uniform);}
                var suit=Part(root.transform,"EVA helmet",PrimitiveType.Sphere,new Vector3(0,1.77f,0),Vector3.one*.52f,Blue);suit.SetActive(a.eva);
                Part(root.transform,"Pack",PrimitiveType.Cube,new Vector3(0,1.2f,-.27f),new Vector3(.45f,.65f,.25f),White);
            }
            else if(a.kind==ActorKind.Drone)
            {
                Part(root.transform,"Cargo drone chassis",PrimitiveType.Cube,new Vector3(0,1.2f,0),new Vector3(1.8f,.7f,1.5f),White);Part(root.transform,"Cargo container",PrimitiveType.Cube,new Vector3(0,.6f,0),new Vector3(1.35f,.65f,1.2f),Amber);
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1}){Part(root.transform,"Lift ring",PrimitiveType.Cylinder,new Vector3(x*1.2f,1.4f,z*.9f),new Vector3(.75f,.09f,.75f),Dark);Part(root.transform,"Lift turbine",PrimitiveType.Cylinder,new Vector3(x*1.2f,1.5f,z*.9f),new Vector3(.53f,.03f,.53f),Metal);}
            }
            else if(!Imported(a.kind==ActorKind.ForestryBot?"IndustrialRobot":"ConstructionBot",root.transform,Vector3.zero,new Vector3(2.8f,3,2.8f)))
            {Part(root.transform,"Robot chassis",PrimitiveType.Cube,new Vector3(0,1,0),new Vector3(2,1.2f,2),White);Part(root.transform,"Working arm",PrimitiveType.Capsule,new Vector3(0,2,0),new Vector3(.35f,1,.35f),Amber);}
            if(a.kind==ActorKind.ForestryBot)Part(root.transform,"Timber grapple",PrimitiveType.Cube,new Vector3(0,.9f,1.4f),new Vector3(2.4f,.3f,.7f),Amber);
            return root;
        }
        public static void Combine(Transform group)
        {
            var filters=group.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>()!=null).ToArray();if(filters.Length<2)return;
            var resources=group.gameObject.AddComponent<ColonyModelResources>();var byMaterial=filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial).ToArray();
            foreach(var set in byMaterial)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Combined modular furniture"};mesh.CombineMeshes(set.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=group.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);resources.owned.Add(mesh);
                var batch=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));batch.transform.SetParent(group,false);batch.GetComponent<MeshFilter>().sharedMesh=mesh;batch.GetComponent<MeshRenderer>().sharedMaterial=set.Key;
            }
            foreach(var f in filters)Object.Destroy(f.gameObject);
        }
        public static void ReleaseMaterials(){foreach(var material in materials.Values)if(material)Object.Destroy(material);materials.Clear();}
    }
}
