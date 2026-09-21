using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Meridian.Colony
{
    /// <summary>Presentation follows the saved calendar/weather; it never advances simulation.</summary>
    public sealed class ColonyEnvironment:MonoBehaviour
    {
        ColonyRuntime runtime;Light sun;Color oldAmbient,oldFog;bool oldFogEnabled;float oldFogDensity;
        readonly List<GameObject> dust=new List<GameObject>();readonly List<LineRenderer> work=new List<LineRenderer>();
        Material dustMaterial;float arrival;bool landing;
        public void Initialize(ColonyRuntime owner)
        {
            runtime=owner;sun=GetComponentsInChildren<Light>().FirstOrDefault(l=>l.type==LightType.Directional);
            oldAmbient=RenderSettings.ambientLight;oldFog=RenderSettings.fogColor;oldFogEnabled=RenderSettings.fog;oldFogDensity=RenderSettings.fogDensity;
            dustMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Landing dust",renderQueue=3000};
            dustMaterial.SetFloat("_Surface",1);dustMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);dustMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);dustMaterial.SetFloat("_ZWrite",0);dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");dustMaterial.SetOverrideTag("RenderType","Transparent");dustMaterial.color=new Color(.47f,.42f,.30f,.16f);
            for(int i=0;i<24;i++){var puff=ColonyModels.Part(transform,"Ground-proximity dust",PrimitiveType.Sphere,Vector3.zero,Vector3.one,dustMaterial);puff.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;puff.SetActive(false);dust.Add(puff);}
            for(int i=0;i<8;i++){var beam=ColonyModels.Line(transform,"Active work sparks",new Vector3[2],ColonyModels.Amber,.06f);beam.useWorldSpace=true;beam.gameObject.SetActive(false);work.Add(beam);}
        }
        public void Arrival(float fraction){arrival=fraction;landing=true;}
        public void FinishArrival(){landing=false;foreach(var puff in dust)puff.SetActive(false);}
        void LateUpdate()
        {
            if(!runtime||runtime.Simulation==null)return;var sim=runtime.Simulation;var env=sim.State.environment;
            float day=(float)(sim.State.time/sim.Day%1),solar=Mathf.Sin((day-.25f)*Mathf.PI*2),daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.1f,.45f,solar));
            if(runtime.Cinematic)daylight=1;
            if(sun){sun.transform.rotation=Quaternion.Euler(runtime.Cinematic?32:Mathf.Lerp(15,65,Mathf.Abs(solar)),day*360-52,0);sun.intensity=Mathf.Lerp(.65f,1.45f,daylight)*(1-env.storm*.35f);sun.color=Color.Lerp(new Color(.67f,.78f,1f),new Color(1,.94f,.83f),daylight);}
            RenderSettings.ambientLight=Color.Lerp(new Color(.36f,.41f,.50f),new Color(.40f,.43f,.46f),daylight)*(1-env.storm*.15f);
            float restoration=Mathf.Clamp01((env.atmosphere-env.atmosphereStart)/Mathf.Max(1,100-env.atmosphereStart));
            var clearSky=Color.Lerp(new Color(.27f,.31f,.32f),new Color(.24f,.39f,.53f),restoration);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=Mathf.Lerp(.00008f,.00032f,env.storm);RenderSettings.fogColor=Color.Lerp(new Color(.09f,.13f,.20f),clearSky,daylight);runtime.Camera.Lens.backgroundColor=RenderSettings.fogColor;
            if(landing)
            {
                bool active=arrival>.48f&&arrival<.96f;float spread=Mathf.Clamp01((arrival-.48f)/.48f);
                for(int i=0;i<dust.Count;i++){var puff=dust[i];puff.SetActive(active);if(!active)continue;float angle=i*2.399963f;float radius=4+spread*(12+i%5);Vector3 p=sim.State.setup.landing.Position+new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);p.y=sim.World.Height(p.x,p.z)+.3f+spread;puff.transform.position=p;puff.transform.localScale=new Vector3(4+spread*5,.6f+spread*1.5f,3+spread*5);}
                dustMaterial.color=new Color(.47f,.42f,.30f,.16f*Mathf.Sin(spread*Mathf.PI));
            }
            int shown=0;foreach(var job in sim.State.jobs.Where(j=>j.stage==JobStage.Working&&j.actor!=null).OrderBy(j=>(sim.Position(j.actor)-runtime.Camera.Pivot).sqrMagnitude))
            {
                if(shown>=work.Count||sim.State.speed==0)break;var actor=sim.Actor(job.actor);if(actor==null||(actor.position-runtime.Camera.Pivot).sqrMagnitude>350*350)continue;
                var line=work[shown++];line.gameObject.SetActive(true);Vector3 target=job.kind==JobKind.Harvest?sim.Position(job.target)+Vector3.up:actor.position+Quaternion.Euler(0,actor.yaw,0)*Vector3.forward*2+Vector3.up*.7f;
                line.SetPosition(0,actor.position+Vector3.up*1.8f);line.SetPosition(1,target+Vector3.up*Mathf.Sin((float)sim.State.time*27)*.2f);
            }
            for(int i=shown;i<work.Count;i++)work[i].gameObject.SetActive(false);
        }
        void OnDestroy(){if(dustMaterial)Destroy(dustMaterial);RenderSettings.ambientLight=oldAmbient;RenderSettings.fogColor=oldFog;RenderSettings.fog=oldFogEnabled;RenderSettings.fogDensity=oldFogDensity;}
    }
}
