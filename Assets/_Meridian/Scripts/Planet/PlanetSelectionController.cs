using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Meridian
{
    public sealed class PlanetSelectionController : MonoBehaviour
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

        public void Configure(PlanetGenerationSettings config,PlanetGlobe planet,PlanetFlag marker,PlanetViewingInput input,
            ScreenTransition transition,CanvasGroup ui,Button back,Button next)
        {settings=config;globe=planet;flagPrefab=marker;viewing=input;transitionPrefab=transition;controls=ui;backButton=back;continueButton=next;}
        void Awake()
        {
            controls.alpha=0;SetInteraction(false);continueButton.interactable=false;
            ScreenTransition.Reveal(transitionPrefab);
        }
        IEnumerator Start()
        {
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            cancellation=new CancellationTokenSource();
            int seed=settings.overrideSeed?settings.developmentSeed:BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0);
            PlanetParameters parameters=settings.Snapshot();CancellationToken token=cancellation.Token;
            Task<PlanetData> task=Task.Run(()=>PlanetGenerator.Generate(seed,parameters,token),token);
            while(!task.IsCompleted)yield return null;
            if(token.IsCancellationRequested)yield break;
            if(task.IsFaulted || task.IsCanceled)
            {
                GenerationFailed=true;Debug.LogError("Planet generation failed; returning to MainMenu. "+task.Exception);yield break;
            }
            try
            {
                globe.Apply(task.Result);
                // Present a useful land-bearing hemisphere; orientation is deterministic for a fixed seed.
                int best=0;float score=float.NegativeInfinity;
                for(int i=0;i<Data.Graph.Directions.Length;i++) if(Data.Water[i]==PlanetWater.None && Data.Elevation[i]>.045f && Data.Sample(Data.Graph.Directions[i]).IsLand)
                {
                    float s=Data.Moisture[i]-Mathf.Abs(Data.Graph.Directions[i].y)*.4f-Mathf.Abs(Data.Elevation[i]-.13f);
                    if(s>score){score=s;best=i;}
                }
                globe.transform.rotation=Quaternion.FromToRotation(Data.Graph.Directions[best],Vector3.back);
                controls.alpha=1;IsReady=true;
                Debug.Log($"Meridian planet ready: seed {seed}, {Data.Version}, {Data.Graph.Directions.Length} samples, {globe.TriangleCount} triangles, {Data.Width}x{Data.Height} maps, {Data.GenerationSeconds:F2}s generation.");
            }
            catch(Exception error){GenerationFailed=true;Debug.LogException(error);}
        }
        public void SetInteraction(bool enabled)
        {viewing.SetInteraction(enabled);backButton.interactable=enabled;controls.interactable=enabled;continueButton.interactable=false;}
        public void Back(){if(!IsReady || !backButton.interactable || ScreenTransition.Active)return;SetInteraction(false);ScreenTransition.Travel(transitionPrefab,"MainMenu");}
        public bool Select(Vector2 screenPoint)
        {
            if(!IsReady || !viewing.InteractionEnabled || viewing.OverUI(screenPoint))return false;
            if(!globe.Pick(viewing.ViewingCamera.ScreenPointToRay(screenPoint),out RaycastHit hit))return false;
            Vector3 local=globe.transform.InverseTransformPoint(hit.point),direction=local.normalized;
            if(!Data.Sample(direction).IsLand)return false;
            Selection=new PlanetSelection(Data,direction);
            if(!flag)flag=Instantiate(flagPrefab,globe.transform);
            flag.Plant(globe.transform,local,direction);return true;
        }
        void OnDestroy()
        {
            cancellation?.Cancel();cancellation?.Dispose();cancellation=null;
            Selection=null;if(flag)Destroy(flag.gameObject);
        }
    }
}
