using System;
using UnityEngine;

namespace Meridian
{
    [Serializable]
    public sealed class ColonySetupRecord
    {
        public string recordVersion="meridian-setup-1";
        public int seed;
        public string planetVersion;
        public PlanetParameters planetParameters;
        public PlanetSelection region;
        public string regionId;
        public Vector3 regionSurfaceAnchor;
        public Vector2 longitudeLatitude;
        public Quaternion globeRotation=Quaternion.identity;
        public float globeFraming=.63f;
        public string surfaceVersion;
        public SurfaceParameters surfaceParameters;
        public SurfaceFrame surfaceFrame;
        public Vector2Int[] surveyTiles;
        public LandingCandidate landing;
        public bool landingConfirmed;
        public Vector2 plainCentre;public float plainHeight;public bool shapePlain;
    }

    /// <summary>Logical setup and one planet render cache. Scene objects never live here.</summary>
    public sealed class SetupSession : MonoBehaviour
    {
        public static SetupSession Current {get;private set;}
        public ColonySetupRecord Record {get;private set;}=new ColonySetupRecord();
        public PlanetData Planet {get;private set;}
        public PlanetRenderResources Visuals {get;private set;}
        public PlanetSelection Selection=>Record.region;
        public SurfaceWorldData Surface {get;private set;}
        public LandingCandidate Candidate=>Record.landing;
        public bool LandingConfirmed=>Record.landingConfirmed;
        public int RegionRevision {get;private set;}
        public bool HasGlobeView {get;private set;}

        public static SetupSession Ensure()
        {
            if(!Current)new GameObject("Colony Setup Session",typeof(SetupSession));
            return Current;
        }
        public static void BeginNew(){Colony.ColonyLoadPipeline.ClearPending();Ensure().Clear();}
        public static void End(){Colony.ColonyLoadPipeline.ClearPending();if(Current){Current.Clear();Destroy(Current.gameObject);Current=null;}}
        void Awake()
        {
            if(Current && Current!=this){Destroy(gameObject);return;}
            Current=this;DontDestroyOnLoad(gameObject);
        }
        public void SetPlanet(PlanetData data,PlanetRenderResources visuals)
        {
            Visuals?.Dispose();Planet=data;Visuals=visuals;
            Record.seed=data.Seed;Record.planetVersion=data.Version;Record.planetParameters=data.Parameters;
        }
        public void SelectRegion(PlanetSelection selection,Vector3 surfaceAnchor)
        {
            // Clicking the exact same region does not discard a confirmed landing on review.
            bool changed=Selection==null || (Selection.localDirection-selection.localDirection).sqrMagnitude>1e-12f;
            Record.region=selection;
            Record.regionSurfaceAnchor=surfaceAnchor;
            Record.regionId=FormattableString.Invariant($"{selection.seed}:{selection.localDirection.x:R},{selection.localDirection.y:R},{selection.localDirection.z:R}");
            var d=selection.localDirection;
            Record.longitudeLatitude=new Vector2(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,Mathf.Asin(Mathf.Clamp(d.y,-1,1))*Mathf.Rad2Deg);
            if(!changed)return;
            RegionRevision++;Surface=null;Record.landing=null;Record.landingConfirmed=false;
            Record.surfaceVersion=null;Record.surfaceFrame=default;Record.surfaceParameters=default;Record.surveyTiles=null;
        }
        public void SaveGlobeView(Quaternion rotation,float framing)
        {Record.globeRotation=rotation;Record.globeFraming=framing;HasGlobeView=true;}
        public void SetSurface(SurfaceWorldData data)
        {
            Surface=data;Record.surfaceParameters=data.Parameters;Record.surfaceFrame=data.Frame;
            Record.surfaceVersion=SurfaceGenerator.Version;
            Record.plainCentre=data.PlainCentre;Record.plainHeight=data.PlainHeight;Record.shapePlain=data.ShapePlain;
            Record.surveyTiles=new Vector2Int[data.Tiles.Length];
            for(int i=0;i<data.Tiles.Length;i++)Record.surveyTiles[i]=data.Tiles[i].Address;
        }
        public void SetCandidate(LandingCandidate candidate)
        {if(!LandingConfirmed)Record.landing=candidate;}
        public void ConfirmLanding(LandingCandidate candidate)
        {Record.landing=candidate;Record.landingConfirmed=true;Visuals?.Dispose();Visuals=null;Planet?.ReleaseAppearanceBuffers();}
        public void Restore(ColonySetupRecord record,PlanetData planet,SurfaceWorldData surface)
        {Clear();Record=record;Planet=planet;Surface=surface;HasGlobeView=true;}
        void Clear()
        {
            RegionRevision++;Visuals?.Dispose();Visuals=null;Planet=null;Surface=null;
            Record=new ColonySetupRecord();HasGlobeView=false;
        }
        void OnDestroy(){Clear();if(Current==this)Current=null;}
    }
}
