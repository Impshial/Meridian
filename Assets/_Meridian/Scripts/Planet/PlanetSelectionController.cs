using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Meridian
{
    public sealed class PlanetSelectionController : MonoBehaviour,ISetupDestination
    {
        [SerializeField] private PlanetGenerationSettings settings;
        [SerializeField] private PlanetGlobe globe;
        [SerializeField] private PlanetFlag flagPrefab;
        [SerializeField] private PlanetViewingInput viewing;
        [SerializeField] private ScreenTransition transitionPrefab;
        [SerializeField] private CanvasGroup controls;
        [SerializeField] private Button backButton,continueButton;
        private CancellationTokenSource cancellation;
        private PlanetFlag flag;
        public PlanetSelection Selection {get;private set;}
        public PlanetData Data=>globe.Data;
        public PlanetGlobe Globe=>globe;
        public PlanetFlag Flag=>flag;
        public PlanetViewingInput Viewing=>viewing;
        public bool IsReady {get;private set;}
        public bool GenerationFailed {get;private set;}
        public string FailureMessage {get;private set;}
        private SetupSession session;

        public void Configure(PlanetGenerationSettings config,PlanetGlobe planet,PlanetFlag marker,PlanetViewingInput input,
            ScreenTransition transition,CanvasGroup ui,Button back,Button next)
        {settings=config;globe=planet;flagPrefab=marker;viewing=input;transitionPrefab=transition;controls=ui;backButton=back;continueButton=next;}
        void Awake()
        {
            session=SetupSession.Ensure();controls.alpha=0;SetInteraction(false);
            ScreenTransition.Reveal(transitionPrefab,session.Planet==null?"Loading Exo-planet...":"Returning to Exo-planet...");
        }
        IEnumerator Start()
        {
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            yield return null;
            if(session.Planet!=null && session.Visuals!=null)
            {
                try
                {
                    globe.Attach(session.Planet,session.Visuals);Selection=session.Selection;
                    viewing.RestoreView(session.Record.globeRotation,session.Record.globeFraming);
                    if(Selection!=null)PlantFlag(session.Record.regionSurfaceAnchor,Selection.localDirection);
                    controls.alpha=1;IsReady=true;
                }
                catch(Exception error){Fail(error);}
                yield break;
            }
            cancellation=new CancellationTokenSource();
            int seed=settings.overrideSeed?settings.developmentSeed:BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
            PlanetParameters parameters=settings.Snapshot();CancellationToken token=cancellation.Token;
            Task<PlanetData> task=Task.Run(()=>PlanetGenerator.Generate(seed,parameters,token),token);
            _=task.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
            while(!task.IsCompleted)yield return null;
            if(token.IsCancellationRequested || session!=SetupSession.Current)yield break;
            if(task.IsFaulted || task.IsCanceled)
            {
                Fail((Exception)task.Exception??new InvalidOperationException("Planet generation was cancelled."));yield break;
            }
            try
            {
                globe.Apply(task.Result);
                session.SetPlanet(task.Result,globe.TransferOwnership());
                // Present a useful land-bearing hemisphere; orientation is deterministic for a fixed seed.
                int best=0;float score=float.NegativeInfinity;
                for(int i=0;i<Data.Graph.Directions.Length;i++) if(Data.Water[i]==PlanetWater.None && Data.Elevation[i]>.045f && Data.Sample(Data.Graph.Directions[i]).IsLand)
                {
                    float s=Data.Moisture[i]-Mathf.Abs(Data.Graph.Directions[i].y)*.4f-Mathf.Abs(Data.Elevation[i]-.13f);
                    if(s>score){score=s;best=i;}
                }
                globe.transform.rotation=Quaternion.FromToRotation(Data.Graph.Directions[best],Vector3.back);
                session.SaveGlobeView(globe.transform.rotation,viewing.CurrentFraming);
                controls.alpha=1;IsReady=true;
                Debug.Log($"Meridian planet ready: seed {seed}, {Data.Version}, {Data.Graph.Directions.Length} samples, {globe.TriangleCount} triangles, {Data.Width}x{Data.Height} maps, {Data.GenerationSeconds:F2}s generation.");
            }
            catch(Exception error){Fail(error);}
        }
        public void SetInteraction(bool enabled)
        {viewing.SetInteraction(enabled);backButton.interactable=enabled;controls.interactable=enabled;continueButton.interactable=enabled&&IsReady&&Selection!=null;}
        public void Back(){if(!IsReady || !backButton.interactable || ScreenTransition.Active)return;SetInteraction(false);ScreenTransition.Travel(transitionPrefab,"MainMenu","Returning to menu...");}
        public void Continue()
        {
            if(!IsReady || Selection==null || !continueButton.interactable || ScreenTransition.Active)return;
            session.SaveGlobeView(globe.transform.rotation,viewing.CurrentFraming);
            SetInteraction(false);ScreenTransition.Travel(transitionPrefab,"LandingSiteSelection","Loading Landing Site...");
        }
        public bool Select(Vector2 screenPoint)
        {
            if(!IsReady || !viewing.InteractionEnabled || ScreenTransition.Active || viewing.OverUI(screenPoint))return false;
            if(!globe.Pick(viewing.ViewingCamera.ScreenPointToRay(screenPoint),out RaycastHit hit))return false;
            Vector3 local=globe.transform.InverseTransformPoint(hit.point),direction=local.normalized;
            bool land=Data.Sample(direction).IsLand;
            ClickPulse.Play(screenPoint,land);viewing.CenterOn(direction);
            if(!land)return true;
            Selection=new PlanetSelection(Data,direction);
            session.SelectRegion(Selection,local);PlantFlag(local,direction);
            continueButton.interactable=true;return true;
        }
        void PlantFlag(Vector3 local,Vector3 direction)
        {
            if(!flag)flag=Instantiate(flagPrefab,globe.transform);
            flag.ConfigureCamera(viewing.ViewingCamera);flag.Plant(globe.transform,local,direction);
        }
        void Fail(Exception error)
        {
            GenerationFailed=true;FailureMessage="The planet could not be prepared. Return to the menu and try again.";
            Debug.LogError("Meridian planet generation failed: "+error);
        }
        void OnDestroy()
        {
            cancellation?.Cancel();cancellation?.Dispose();cancellation=null;
            ClickPulse.Clear();Selection=null;if(flag)Destroy(flag.gameObject);
        }
    }
}
