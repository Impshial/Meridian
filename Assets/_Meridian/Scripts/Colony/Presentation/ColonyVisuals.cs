using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed class ColonyVisuals:MonoBehaviour
    {
        ColonySimulation sim;readonly Dictionary<string,GameObject> views=new Dictionary<string,GameObject>();readonly Dictionary<string,BuildPhase> phases=new Dictionary<string,BuildPhase>();
        readonly Dictionary<string,GameObject> flightViews=new Dictionary<string,GameObject>();MaterialPropertyBlock tint;float refresh;
        GameObject cinematic,selection,preview;LineRenderer outline;public string Selected;public bool Arriving=>cinematic;
        public Vector3 ArrivalPosition=>cinematic?cinematic.transform.position:sim.State.setup.landing.Position;
        public void Initialize(ColonySimulation owner){sim=owner;tint=new MaterialPropertyBlock();Sync();}
        public void BeginArrival(){cinematic=ColonyModels.Dropship();cinematic.transform.SetParent(transform,false);cinematic.transform.SetPositionAndRotation(sim.State.setup.landing.Position+Vector3.up*200,Quaternion.Euler(0,sim.State.setup.landing.yaw,0));}
        public void Arrival(float t)
        {
            if(!cinematic)return;float descent=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.68f));cinematic.transform.position=sim.State.setup.landing.Position+Vector3.up*(1-descent)*200;
            Boosters(cinematic,t<.75f,Time.unscaledTime);var ramp=cinematic.transform.Find("Ramp");if(ramp)ramp.localRotation=Quaternion.Euler(Mathf.Lerp(85,8,Mathf.InverseLerp(.72f,.92f,t)),0,0);
        }
        public void FinishArrival(){if(cinematic)Destroy(cinematic);cinematic=null;Sync();}
        public GameObject View(string id)=>id!=null&&views.TryGetValue(id,out var view)?view:null;
        public void Sync()
        {
            var camera=ColonyRuntime.Current?.Camera;bool Near(Vector3 p,string id)=>camera==null||id==Selected||(p-camera.Pivot).sqrMagnitude<Mathf.Pow(Mathf.Max(900,camera.Distance*2.2f),2);int created=0;
            foreach(var b in sim.State.structures)
            {
                if(b.phase==BuildPhase.Removed||!Near(sim.Definition(b).link?(b.position+b.end)*.5f:b.position,b.id)){if(views.TryGetValue(b.id,out var dead)){Destroy(dead);views.Remove(b.id);phases.Remove(b.id);}continue;}
                if(!views.TryGetValue(b.id,out var view)){if(created++>=64)continue;view=ColonyModels.Structure(b,sim.Definition(b),sim.World);view.transform.SetParent(transform,true);views.Add(b.id,view);}
                var exterior=view.transform.Find("Exterior");if(exterior)exterior.gameObject.SetActive(!Frames(b));
                var interior=view.transform.Find("Interior");if(interior)interior.gameObject.SetActive(Frames(b));
                var frame=view.transform.Find("Structural frame");if(frame)frame.gameObject.SetActive(Frames(b));
                if(!phases.TryGetValue(b.id,out var previous)||previous!=b.phase)
                {
                    phases[b.id]=b.phase;bool complete=b.phase==BuildPhase.Complete;
                    foreach(var r in view.GetComponentsInChildren<Renderer>(true)){tint.Clear();if(!complete)tint.SetColor("_BaseColor",new Color(.45f,.65f,.66f));r.SetPropertyBlock(tint);}
                }
                if(b.phase==BuildPhase.Construction)view.transform.localScale=new Vector3(1,Mathf.Lerp(.2f,1,b.progress),1);else view.transform.localScale=Vector3.one;
            }
            foreach(var a in sim.State.actors)
            {
                if(a.location!=PersonLocation.Arrived||!Near(a.position,a.id)){if(views.TryGetValue(a.id,out var old)){Destroy(old);views.Remove(a.id);}continue;}
                if(!views.ContainsKey(a.id)){if(created++>=64)continue;var view=ColonyModels.Actor(a);view.transform.SetParent(transform,false);views.Add(a.id,view);}
            }
        }
        public bool Frames(StructureState b)=>b.domeOverride>=0?b.domeOverride==1:sim.State.masterFrames;
        public void ToggleMaster(){sim.State.masterFrames=!sim.State.masterFrames;foreach(var b in sim.State.structures)b.domeOverride=-1;Sync();}
        public void ToggleDome(StructureState b){b.domeOverride=Frames(b)?0:1;Sync();}
        void LateUpdate()
        {
            if(sim==null)return;refresh-=Time.unscaledDeltaTime;if(refresh<=0){Sync();refresh=.4f;}
            foreach(var a in sim.SurfaceActors)
            {
                if(!views.TryGetValue(a.id,out var view))continue;float hover=a.kind==ActorKind.Drone?2.6f:0;
                bool sleeping=a.intention=="sleep"&&sim.Navigation.Arrived(a);view.transform.SetPositionAndRotation(a.position+Vector3.up*(hover+(sleeping?.72f:0)),Quaternion.Euler(sleeping?90:0,a.yaw,0));
                bool walking=!sim.Navigation.Arrived(a)&&sim.State.speed>0;float angle=walking?Mathf.Sin((float)sim.State.time*8)*28:0;
                foreach(string limb in new[]{"Left leg","Right leg","Left arm","Right arm"}){var part=view.transform.Find(limb);if(part)part.localRotation=Quaternion.Euler(limb.StartsWith("Left")?angle:-angle,0,0);}
                var helmet=view.transform.Find("EVA helmet");if(helmet)helmet.gameObject.SetActive(a.eva);
            }
            foreach(var b in sim.State.structures.Where(b=>b.definition=="wind")){var rotor=View(b.id)?.transform.Find("Rotor");if(rotor&&sim.Operating(b))rotor.Rotate(Vector3.forward,Time.unscaledDeltaTime*sim.State.speed*80*sim.State.environment.wind);}
            foreach(var f in sim.State.flights)
            {
                bool visible=f.phase==FlightPhase.Descending||f.phase==FlightPhase.Disembarking||f.phase==FlightPhase.Departing||f.phase==FlightPhase.Returning||f.phase==FlightPhase.Preparing&&(f.kind==FlightKind.Export||f.kind==FlightKind.Departure);
                if(!visible){if(flightViews.TryGetValue(f.id,out var old)){Destroy(old);flightViews.Remove(f.id);}continue;}
                if(!flightViews.TryGetValue(f.id,out var ship)){ship=ColonyModels.Dropship();ship.transform.SetParent(transform,false);ship.transform.localScale=Vector3.one*.55f;ship.AddComponent<ColonyPickTarget>().id=f.id;var collider=ship.AddComponent<BoxCollider>();collider.center=Vector3.up*3;collider.size=new Vector3(14,7,26);flightViews.Add(f.id,ship);}
                ship.transform.SetPositionAndRotation(f.position,Quaternion.Euler(0,sim.Structure(f.pad)?.yaw??0,0));Boosters(ship,f.phase==FlightPhase.Descending||f.phase==FlightPhase.Departing||f.phase==FlightPhase.Returning,(float)sim.State.time);
            }
            Selection();
        }
        static void Boosters(GameObject ship,bool active,float time)
        {foreach(Transform t in ship.transform)if(t.name=="Booster"){t.gameObject.SetActive(active);t.localScale=new Vector3(1.1f,2.2f+Mathf.Sin(time*23)*.3f,1.1f);}}
        void Selection()
        {
            if(Selected==null){if(selection)selection.SetActive(false);return;}
            Vector3 position=sim.Position(Selected);var b=sim.Structure(Selected);float radius=b!=null?sim.Definition(b).size.magnitude*.55f:sim.World.Object(Selected)?.Radius+1??2;
            if(!selection){selection=new GameObject("Selected footprint");selection.transform.SetParent(transform,false);outline=ColonyModels.Line(selection.transform,"Selection",new Vector3[48],ColonyModels.Amber,.12f,true);}
            selection.SetActive(true);selection.transform.position=position+Vector3.up*.7f;for(int i=0;i<48;i++){float a=i*Mathf.PI/24;outline.SetPosition(i,new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius));}
        }
        public void Preview(string definition,Vector3 point,float yaw,Vector3 end,bool valid)
        {
            if(preview)Destroy(preview);preview=null;if(definition==null)return;var d=sim.Catalog.Building(definition);preview=new GameObject("Construction footprint");preview.transform.SetParent(transform,false);preview.transform.SetPositionAndRotation(point,Quaternion.Euler(0,yaw,0));
            var mat=valid?ColonyModels.Material("Placement valid",new Color(.24f,.9f,.67f)):ColonyModels.Material("Placement blocked",new Color(1,.3f,.2f));
            if(d.link){preview.transform.rotation=Quaternion.identity;ColonyModels.Line(preview.transform,"Connection",new[]{Vector3.up, end-point+Vector3.up},mat,.3f);}
            else
            {
                float x=d.size.x*.5f,z=d.size.y*.5f;ColonyModels.Line(preview.transform,"Footprint",new[]{new Vector3(-x,.8f,-z),new Vector3(x,.8f,-z),new Vector3(x,.8f,z),new Vector3(-x,.8f,z)},mat,.2f,true);
                foreach(int sign in new[]{-1,1})ColonyModels.Line(preview.transform,"Height",new[]{new Vector3(sign*x,.8f,-z),new Vector3(sign*x,d.height,-z)},mat,.1f);
                ColonyModels.Line(preview.transform,"Door clearance",new[]{new Vector3(-1.5f,.85f,z),new Vector3(-1.5f,.85f,z+3),new Vector3(1.5f,.85f,z+3),new Vector3(1.5f,.85f,z)},mat,.15f);
            }
        }
        public string Pick(Ray ray,SurfaceLandscape landscape,out Vector3 ground)
        {
            ground=default;string fallback=null;float best=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(ray,10000).OrderBy(h=>h.distance))
            {
                var entity=hit.collider.GetComponentInParent<ColonyPickTarget>();if(!entity)continue;var b=sim.Structure(entity.id);
                if(b!=null&&sim.Definition(b).sealedModule&&Frames(b)){fallback=entity.id;continue;}
                best=hit.distance;fallback=entity.id;break;
            }
            if(landscape.Pick(ray,out var terrain))
            {
                ground=terrain.point;var point=ground;if(terrain.distance+1<best&&fallback==null)
                {var obj=sim.World.Nearby(point,14).Where(o=>ColonyWorld.XZ(o.Position-point).magnitude<o.Radius+2).OrderBy(o=>Vector3.SqrMagnitude(o.Position-point)).FirstOrDefault();if(obj!=null)fallback=obj.Id;}
            }
            return fallback;
        }
        void OnDestroy(){ColonyModels.ReleaseMaterials();}
    }
}
