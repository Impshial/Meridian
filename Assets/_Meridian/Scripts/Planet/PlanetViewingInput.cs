using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Meridian
{
    public sealed class PlanetViewingInput : MonoBehaviour
    {
        [SerializeField] private Camera viewingCamera;
        [SerializeField] private PlanetGlobe globe;
        [SerializeField] private PlanetSelectionController selection;
        [SerializeField,Range(.05f,.6f)] private float rotationSensitivity=.22f;
        [SerializeField,Range(3,15)] private float dragThreshold=7;
        [SerializeField,Tooltip("Projected full globe diameter / viewport height. Close views intentionally crop the globe.")]
        private float initialFraming=.63f,minimumFraming=.45f,maximumFraming=2.1f;
        [SerializeField,Tooltip("Logarithmic magnification per normalized wheel tick.")]
        private float zoomPerTick=.10f,zoomSmoothing=12;
        [SerializeField,Range(.5f,.8f)] private float centeringDuration=.65f;
        private readonly GlobeGesture gesture=new GlobeGesture();
        private readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        private Vector2 previous;
        private float targetFraming,currentFraming;
        private bool interactionEnabled;
        private bool centering;
        private float centeringElapsed;
        private Quaternion centeringStart,centeringTarget;
        public bool InteractionEnabled => interactionEnabled;
        public Camera ViewingCamera=>viewingCamera;
        public bool IsDragging=>gesture.Captured&&gesture.Dragged;
        public bool IsCentering=>centering;
        public float CurrentFraming=>currentFraming;
        public void Configure(Camera camera,PlanetGlobe planet,PlanetSelectionController controller)
        {viewingCamera=camera;globe=planet;selection=controller;}
        void Awake(){targetFraming=currentFraming=initialFraming;}
        public void SetInteraction(bool value)
        {
            interactionEnabled=value;gesture.Reset();
            if(!value){targetFraming=currentFraming;CancelCentering();ClickPulse.Clear();}
        }
        public void RestoreView(Quaternion rotation,float framing)
        {
            CancelCentering();gesture.Reset();globe.transform.rotation=rotation;
            targetFraming=currentFraming=Mathf.Clamp(framing,minimumFraming,maximumFraming);ApplyFraming();
        }
        public void CenterOn(Vector3 localDirection)
        {
            if(!globe || !viewingCamera || localDirection.sqrMagnitude<.00001f)return;
            Vector3 from=globe.transform.TransformDirection(localDirection.normalized).normalized;
            Vector3 to=(viewingCamera.transform.position-globe.transform.position).normalized;
            Vector3 axis=Vector3.Cross(from,to);
            Quaternion delta;
            if(axis.sqrMagnitude<1e-12f && Vector3.Dot(from,to)<0)
            {
                // The antipode has infinitely many shortest paths. Choose a stable camera-relative one.
                axis=Vector3.Cross(from,viewingCamera.transform.up);
                if(axis.sqrMagnitude<1e-8f)axis=Vector3.Cross(from,viewingCamera.transform.right);
                delta=Quaternion.AngleAxis(180,axis.normalized);
            }
            else delta=Quaternion.FromToRotation(from,to);
            centeringStart=globe.transform.rotation;centeringTarget=delta*centeringStart;
            centeringElapsed=0;centering=true;
        }
        public void CancelCentering()=>centering=false;
        public bool OverUI(Vector2 point)
        {
            if(!EventSystem.current)return false;
            uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},uiHits);
            return uiHits.Count>0;
        }
        void Update()
        {
            ApplyFraming();
            var mouse=Mouse.current;
            if(!interactionEnabled || mouse==null){gesture.Reset();return;}
            Vector2 point=mouse.position.ReadValue();bool ui=OverUI(point);
            if(mouse.leftButton.wasPressedThisFrame)
            {
                gesture.Press(point,globe.Pick(viewingCamera.ScreenPointToRay(point),out _),ui);previous=point;
                if(gesture.Captured)CancelCentering();
            }
            if(gesture.Captured)
            {
                gesture.Move(point,dragThreshold*Mathf.Max(.5f,Screen.height/1080f));
                if(mouse.leftButton.isPressed && gesture.Dragged)
                {
                    CancelCentering();
                    Vector2 delta=(point-previous)*(1080f/Mathf.Max(1,Screen.height))*rotationSensitivity;
                    globe.transform.rotation=Quaternion.AngleAxis(-delta.x,viewingCamera.transform.up)*
                        Quaternion.AngleAxis(delta.y,viewingCamera.transform.right)*globe.transform.rotation;
                }
                previous=point;
                if(mouse.leftButton.wasReleasedThisFrame && gesture.Release(ui))selection.Select(point);
                else if(!mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame)gesture.Reset();
            }
            if(centering)
            {
                centeringElapsed+=Time.unscaledDeltaTime;
                float t=Mathf.Clamp01(centeringElapsed/Mathf.Max(.01f,centeringDuration));
                globe.transform.rotation=Quaternion.Slerp(centeringStart,centeringTarget,t*t*(3-2*t));
                if(t>=1){globe.transform.rotation=centeringTarget;centering=false;}
            }
            if(!ui)
            {
                // Input System 1.20 defaults to normalized +/-1 ticks. Native Windows mode uses +/-120.
                float divisor=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                    (Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.WindowsEditor)?120f:1f;
                float ticks=mouse.scroll.ReadValue().y/divisor;
                if(!float.IsNaN(ticks)&&!float.IsInfinity(ticks))
                    targetFraming=Mathf.Clamp(targetFraming*Mathf.Exp(Mathf.Clamp(ticks,-20,20)*zoomPerTick),minimumFraming,maximumFraming);
            }
        }
        void ApplyFraming()
        {
            if(!viewingCamera||!globe)return;
            float max=Mathf.Max(.2f,maximumFraming),min=Mathf.Clamp(minimumFraming,.2f,max);
            targetFraming=Mathf.Clamp(targetFraming,min,max);
            currentFraming=Mathf.Clamp(Mathf.Lerp(currentFraming,targetFraming,1-Mathf.Exp(-zoomSmoothing*Time.unscaledDeltaTime)),min,max);
            float distance=Vector3.Distance(viewingCamera.transform.position,globe.transform.position);
            // Unit sphere plus maximum relief. UI/flag clearance must not limit regional zoom.
            const float envelope=1.005f;
            float tangent=envelope/Mathf.Sqrt(distance*distance-envelope*envelope);
            viewingCamera.fieldOfView=2*Mathf.Atan(tangent/currentFraming)*Mathf.Rad2Deg;
        }
        void OnApplicationFocus(bool focused){if(!focused){gesture.Reset();CancelCentering();}}
        void OnDisable(){gesture.Reset();CancelCentering();ClickPulse.Clear();}
    }
}
