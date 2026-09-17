using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Meridian
{
    public sealed class SurfaceSelectionController : MonoBehaviour,ISetupDestination,ISetupLoading
    {
        [SerializeField] private SurfaceGenerationSettings settings;
        [SerializeField] private ScreenTransition transitionPrefab;
        [SerializeField] private DropshipPreview previewPrefab;
        [SerializeField] private Material terrainMaterial,waterMaterial;
        [SerializeField] private GameObject menuButtonPrefab;
        private SurfaceLandscape landscape;
        private SurfacePresentation presentation;
        private SurveyCamera surveyCamera;
        private DropshipPreview preview;
        private CancellationTokenSource cancellation;
        private readonly GlobeGesture gesture=new GlobeGesture();
        private SetupSession session;
        private Vector2 previewPosition;
        private float heading,nextEvaluation;
        private PlacementResult placement;
        private bool interaction,locked,hasPosition,confirmed;
        private readonly SurfaceLoadProgress loadingProgress=new SurfaceLoadProgress();
        public SurfaceLoadProgress.Snapshot LoadingProgress=>loadingProgress.Current;
        public bool IsReady {get;private set;}
        public bool GenerationFailed {get;private set;}
        public string FailureMessage {get;private set;}
        public SurfaceWorldData World {get;private set;}
        public SurveyCamera Viewing=>surveyCamera;
        public DropshipPreview Preview=>preview;
        public bool CandidateLocked=>locked;
        public PlacementResult CurrentPlacement=>placement;

        public void Configure(SurfaceGenerationSettings config,ScreenTransition transition,DropshipPreview ship,Material terrain,Material water,GameObject buttonPrefab)
        {settings=config;transitionPrefab=transition;previewPrefab=ship;terrainMaterial=terrain;waterMaterial=water;menuButtonPrefab=buttonPrefab;}
        void Awake()
        {
            presentation=gameObject.AddComponent<SurfacePresentation>();presentation.Initialize(menuButtonPrefab,this);
            SetInteraction(false);ScreenTransition.Reveal(transitionPrefab,"Loading Landing Site...");
        }
        IEnumerator Start()
        {
            Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            session=SetupSession.Current;
            if(!session || session.Planet==null || session.Selection==null)
            {Fail("Choose a land region on the planet before surveying a landing site.");yield break;}
            cancellation=new CancellationTokenSource();CancellationToken token=cancellation.Token;int revision=session.RegionRevision;
            if(session.Surface!=null)World=session.Surface;
            else
            {
                PlanetData planet=session.Planet;Vector3 direction=session.Selection.localDirection;
                SurfaceParameters parameters=settings.Snapshot();
                Debug.Log($"Meridian survey started: seed {planet.Seed}, direction {direction.ToString("F6")}, {parameters.initialTilesPerAxis}x{parameters.initialTilesPerAxis} tiles.");
                var task=Task.Run(()=>SurfaceGenerator.Generate(planet,direction,parameters,token,loadingProgress),token);
                _=task.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
                while(!task.IsCompleted)yield return null;
                if(token.IsCancellationRequested || !session || revision!=session.RegionRevision)yield break;
                if(task.IsCanceled){Fail("The landing survey was cancelled. Return to the planet and try again.");yield break;}
                if(task.IsFaulted)
                {
                    Exception error=task.Exception?.GetBaseException();
                    Fail(error is SurfaceSurveyException?error.Message:"The landing survey could not finish. Return to the planet and select another region.",error);yield break;
                }
                World=task.Result;session.SetSurface(World);
                Debug.Log($"Meridian survey numerical data ready: {World.GenerationSeconds:F2}s.");
            }
            landscape=new GameObject("Generated Surface Landscape").AddComponent<SurfaceLandscape>();landscape.transform.SetParent(transform,false);
            // Catch nested coroutine errors as well as generation errors: never leave an overlay waiting forever.
            var stack=new Stack<IEnumerator>();stack.Push(landscape.Build(World,terrainMaterial,waterMaterial,loadingProgress));
            while(stack.Count>0)
            {
                bool moved=false;object current=null;Exception failure=null;
                try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}
                catch(Exception error){failure=error;}
                if(failure!=null){Fail("The landscape could not be prepared. Return to the planet and try again.",failure);yield break;}
                if(token.IsCancellationRequested || !session || revision!=session.RegionRevision)yield break;
                if(!moved){stack.Pop();continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            try
            {
                PrepareView();
                presentation.AddSurveyMarkers(World);presentation.Ready();
            }
            catch(Exception error){Fail("The survey controls could not be prepared. Return to the planet and try again.",error);}
            if(GenerationFailed)yield break;
            loadingProgress.Report(.99f,"Finishing the survey view");
            // Render the new camera under the cover before declaring readiness. Fresh terrain shader
            // variants compile asynchronously in the Editor; exposing them early can reveal empty ground.
            yield return null;yield return null;
#if UNITY_EDITOR
            while(UnityEditor.ShaderUtil.anythingCompiling)yield return null;
#endif
            if(token.IsCancellationRequested || !session || revision!=session.RegionRevision)yield break;
            IsReady=true;UpdatePresentation();
            loadingProgress.Report(1,"Survey ready");
            Debug.Log($"Meridian landing survey ready: seed {World.Seed}, {World.Version}, {World.Tiles.Length} tiles, {World.Parameters.heightmapResolution} heights/tile, {World.BuildableArea:F0} m² connected buildable area, {World.GenerationSeconds:F2}s numeric generation.");
        }
        void PrepareView()
        {
            var camera=new GameObject("Colony Survey Camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(transform,false);camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.18f,.20f);camera.allowHDR=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            surveyCamera=camera.gameObject.AddComponent<SurveyCamera>();
            LandingCandidate initial=session.Candidate??World.DefaultLanding;
            surveyCamera.Initialize(camera,World.Bounds,initial.logicalPosition,World.GroundHeight,presentation.OverUI);
            var light=new GameObject("Surface daylight",typeof(Light)).GetComponent<Light>();light.transform.SetParent(transform,false);
            light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(32,-52,0);light.intensity=1.35f;
            light.color=new Color(1,.95f,.86f);light.shadows=LightShadows.Soft;light.shadowStrength=.65f;
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.30f,.35f,.40f);
            RenderSettings.fog=false;
            preview=Instantiate(previewPrefab,transform);preview.gameObject.SetActive(true);preview.Initialize(World.Parameters,waterMaterial);
            heading=initial.yaw;previewPosition=new Vector2(initial.logicalPosition.x,initial.logicalPosition.z);hasPosition=true;
            locked=session.Candidate!=null;confirmed=session.LandingConfirmed;
            placement=LandingPlacement.Evaluate(World,previewPosition,heading);ShowPreview();
        }
        public void SetInteraction(bool enabled)
        {
            interaction=enabled;gesture.Reset();if(surveyCamera)surveyCamera.SetInteraction(enabled);
            if(presentation)presentation.SetInteraction(enabled,locked&&placement.Valid&&!confirmed);
            if(!enabled)ClickPulse.Clear();
        }
        public void CancelLoading()
        {
            cancellation?.Cancel();StopAllCoroutines();SetInteraction(false);
        }
        public void BackToPlanet()
        {
            if(!IsReady || !interaction || ScreenTransition.Active)return;
            SetInteraction(false);ScreenTransition.Travel(transitionPrefab,"PlanetSelection","Returning to Exo-planet...");
        }
        public void ConfirmLanding()
        {
            if(!IsReady || !interaction || !locked || confirmed || ScreenTransition.Active)return;
            placement=LandingPlacement.Evaluate(World,previewPosition,heading);
            if(placement.Valid){session.ConfirmLanding(placement.Candidate);confirmed=true;}
            ShowPreview();UpdatePresentation();
        }
        void Update()
        {
            if(!IsReady)return;
            presentation.UpdateMarkers(surveyCamera.Lens,landscape);
            if(!interaction || confirmed)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(keyboard!=null)
            {
                if(keyboard.escapeKey.wasPressedThisFrame){ClearCandidate();}
                if(keyboard.rKey.wasPressedThisFrame && hasPosition)
                {
                    heading=Mathf.Repeat(heading+(keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed?-45:45),360);
                    EvaluatePreview();
                    if(locked)session.SetCandidate(placement.Valid?placement.Candidate:new LandingCandidate{surfaceVersion=World.Version,planetSeed=World.Seed,regionDirection=World.Frame.anchor,
                        logicalPosition=new Vector3(previewPosition.x,World.GroundHeight(previewPosition.x,previewPosition.y),previewPosition.y),yaw=heading,geographicDirection=World.Frame.Direction(previewPosition.x,previewPosition.y)});
                }
            }
            if(mouse==null)return;
            Vector2 point=mouse.position.ReadValue();bool ui=presentation.OverUI(point);
            if(mouse.rightButton.wasPressedThisFrame && !ui)ClearCandidate();
            bool hitGround=landscape.Pick(surveyCamera.Lens.ScreenPointToRay(point),out var hit);
            if(!locked && !ui && !surveyCamera.IsOrbiting)
            {
                if(hitGround)
                {
                    Vector2 position=new Vector2(hit.point.x,hit.point.z);
                    if(!hasPosition || ((position-previewPosition).sqrMagnitude>.04f && Time.unscaledTime>=nextEvaluation))
                    {previewPosition=position;hasPosition=true;EvaluatePreview();}
                }
                else {hasPosition=false;preview.Hide();placement=new PlacementResult(false,"Point at ground within the survey area");UpdatePresentation();}
            }
            if(mouse.leftButton.wasPressedThisFrame)gesture.Press(point,hitGround,ui);
            if(gesture.Captured)
            {
                gesture.Move(point,7*Mathf.Max(.5f,Screen.height/1080f));
                if(mouse.leftButton.wasReleasedThisFrame)
                {
                    if(gesture.Release(ui) && hitGround)
                    {
                        Vector2 at=new Vector2(hit.point.x,hit.point.z);PlacementResult result=LandingPlacement.Evaluate(World,at,heading);
                        if(result.Valid)
                        {
                            previewPosition=at;hasPosition=true;placement=result;locked=true;session.SetCandidate(result.Candidate);
                            ClickPulse.Play(point,true);ShowPreview();UpdatePresentation();
                        }
                        else if(!locked){placement=result;previewPosition=at;hasPosition=true;ShowPreview();UpdatePresentation();}
                    }
                }
                else if(!mouse.leftButton.isPressed)gesture.Reset();
            }
        }
        void ClearCandidate()
        {if(confirmed)return;locked=false;session.SetCandidate(null);gesture.Reset();UpdatePresentation();}
        void EvaluatePreview()
        {nextEvaluation=Time.unscaledTime+.09f;placement=LandingPlacement.Evaluate(World,previewPosition,heading);ShowPreview();UpdatePresentation();}
        void ShowPreview()
        {
            if(!hasPosition)return;
            Vector3 position=placement.Valid?placement.Candidate.logicalPosition:new Vector3(previewPosition.x,World.GroundHeight(previewPosition.x,previewPosition.y)+.5f,previewPosition.y);
            preview.Present(position,heading,placement.Valid,confirmed);
        }
        void UpdatePresentation()
        {
            presentation.SetPlacement(locked,placement.Valid,confirmed,placement.Reason);
            presentation.SetInteraction(interaction,locked&&placement.Valid&&!confirmed);
        }
        void Fail(string message,Exception error=null)
        {FailureMessage=message;GenerationFailed=true;SetInteraction(false);Debug.LogWarning("Meridian survey: "+message+(error!=null?"\n"+error:""));}
        void OnApplicationFocus(bool focused){if(!focused)gesture.Reset();}
        void OnDestroy()
        {cancellation?.Cancel();cancellation?.Dispose();cancellation=null;gesture.Reset();ClickPulse.Clear();World=null;}
    }
}
