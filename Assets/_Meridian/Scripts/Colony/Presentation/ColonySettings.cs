using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Meridian.Colony
{
    [Serializable] public sealed class ColonyPreferences
    {
        public float master=.7f,music=.35f,effects=.7f,ambience=.6f,cameraSensitivity=1,zoomSensitivity=1,tiltSensitivity=1,uiScale=1;
        public bool edgePan;public int quality=2,width=1920,height=1080,mode=1;
    }
    public static class ColonySettings
    {
        static ColonyPreferences current;public static ColonyPreferences Current=>current??(current=Read());
        static ColonyPreferences Read()
        {
            ColonyPreferences p;try{p=JsonUtility.FromJson<ColonyPreferences>(PlayerPrefs.GetString("Meridian.Preferences",""))??new ColonyPreferences();}catch{p=new ColonyPreferences();}
            p.cameraSensitivity=Mathf.Clamp(p.cameraSensitivity,.2f,3);p.zoomSensitivity=Mathf.Clamp(p.zoomSensitivity,.2f,3);p.tiltSensitivity=Mathf.Clamp(p.tiltSensitivity,.2f,3);p.uiScale=Mathf.Clamp(p.uiScale,.75f,1.5f);
            p.width=Mathf.Clamp(p.width,1024,7680);p.height=Mathf.Clamp(p.height,720,4320);p.mode=Mathf.Clamp(p.mode,0,3);return p;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RestorePreferences()
        {current=null;AudioListener.volume=Current.master;QualitySettings.SetQualityLevel(Mathf.Clamp(Current.quality,0,QualitySettings.names.Length-1),true);if(!Application.isEditor&&PlayerPrefs.HasKey("Meridian.Preferences"))ApplyDisplay();}
        public static void Save(){PlayerPrefs.SetString("Meridian.Preferences",JsonUtility.ToJson(Current));PlayerPrefs.Save();AudioListener.volume=Current.master;QualitySettings.SetQualityLevel(Mathf.Clamp(Current.quality,0,QualitySettings.names.Length-1),true);}
        public static void ApplyDisplay(){Screen.SetResolution(Current.width,Current.height,(FullScreenMode)Current.mode);}
    }
    public sealed class ColonyAudio:MonoBehaviour
    {
        ColonySimulation sim;AudioSource engine,wind,music,effect;AudioClip engineClip,windClip,musicClip,effectClip;
        readonly List<AudioSource> spatial=new List<AudioSource>();AudioClip stepClip,harvestClip,buildClip,warningClip;float soundClock,warningClock;
        public void Initialize(ColonySimulation owner)
        {
            sim=owner;engineClip=Tone("Machinery",73,2,.15f);windClip=Tone("Air circulation",127,3,.1f);musicClip=Tone("Orbital ambience",55,8,.07f);effectClip=Tone("Interface",520,.075f,.12f);
            engine=Source(engineClip,true);wind=Source(windClip,true);music=Source(musicClip,true);effect=Source(effectClip,false);
            stepClip=Tone("Footstep",95,.09f,.35f);harvestClip=Tone("Cutting and extraction",160,.35f,.4f);buildClip=Tone("Assembly tools",310,.2f,.3f);warningClip=Tone("Colony advisory",660,.4f,.2f);
            for(int i=0;i<8;i++){var emitter=new GameObject("Local activity sound");emitter.transform.SetParent(transform,false);var source=emitter.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=35;source.maxDistance=650;source.rolloffMode=AudioRolloffMode.Linear;spatial.Add(source);}
        }
        AudioSource Source(AudioClip clip,bool loop){var a=gameObject.AddComponent<AudioSource>();a.clip=clip;a.loop=loop;a.playOnAwake=false;if(loop)a.Play();return a;}
        static AudioClip Tone(string name,float frequency,float seconds,float amplitude)
        {
            const int rate=22050;var clip=AudioClip.Create(name,(int)(seconds*rate),1,rate,false);var samples=new float[clip.samples];uint noise=17431;
            for(int i=0;i<samples.Length;i++){float t=i/(float)rate;noise=noise*1664525+1013904223;float random=(noise&65535)/32768f-1;float envelope=Mathf.Min(1,t*20,(seconds-t)*20);samples[i]=amplitude*envelope*(Mathf.Sin(t*frequency*Mathf.PI*2)*.45f+Mathf.Sin(t*frequency*1.5f*Mathf.PI*2)*.2f+random*.08f);}
            clip.SetData(samples,0);return clip;
        }
        public void Click(){if(effect){effect.volume=ColonySettings.Current.effects;effect.PlayOneShot(effectClip);}}
        void Update()
        {
            if(sim==null)return;var preferences=ColonySettings.Current;AudioListener.volume=preferences.master;music.volume=preferences.music;wind.volume=preferences.ambience*(.1f+sim.State.environment.storm*.3f);engine.volume=preferences.effects*(sim.State.deployed?Mathf.Clamp01(sim.Networks.PowerDemand/300)*.12f:.35f);
            engine.pitch=sim.State.deployed?.8f:1.2f;
            soundClock-=Time.unscaledDeltaTime;warningClock-=Time.unscaledDeltaTime;
            if(soundClock<=0&&sim.State.speed>0)
            {
                soundClock=.55f;Vector3 camera=ColonyRuntime.Current?.Camera.Pivot??sim.State.setup.landing.Position;int index=0;
                foreach(var actor in sim.SurfaceActors.OrderBy(a=>(a.position-camera).sqrMagnitude).Take(8))
                {
                    if((actor.position-camera).sqrMagnitude>550*550)continue;var job=sim.State.jobs.Find(j=>j.id==actor.job);AudioClip clip=null;
                    if(job?.stage==JobStage.Working)clip=job.kind==JobKind.Harvest?harvestClip:buildClip;
                    else if((actor.kind==ActorKind.Colonist||actor.kind==ActorKind.Visitor)&&!sim.Navigation.Arrived(actor))clip=stepClip;
                    if(clip==null)continue;var source=spatial[index++];source.transform.position=actor.position;source.volume=preferences.effects*.4f;source.PlayOneShot(clip);
                }
                if(warningClock<=0&&sim.SurfaceActors.Any(a=>a.health<50||a.charge<3)){effect.PlayOneShot(warningClip,preferences.effects*.45f);warningClock=30;}
            }
        }
        void OnDestroy(){Destroy(engineClip);Destroy(windClip);Destroy(musicClip);Destroy(effectClip);Destroy(stepClip);Destroy(harvestClip);Destroy(buildClip);Destroy(warningClip);}
    }
}
