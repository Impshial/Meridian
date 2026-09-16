using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meridian
{
    /// <summary>Pregame ground survey: grab to pan, wheel to tilt, Shift + wheel to zoom, and discrete heading changes.</summary>
    public sealed class SurveyCamera : MonoBehaviour
    {
        [SerializeField] private float minimumDistance=95,maximumDistance=1250,initialDistance=730;
        [SerializeField] private float minimumPitch=42,maximumPitch=78,panRate=.65f;
        [SerializeField] private float tiltPerTick=3,tiltSmoothing=12;
        [SerializeField] private float zoomPerTick=.12f;
        private Camera lens;
        private Rect bounds;
        private Func<float,float,float> height;
        private Func<Vector2,bool> overUI;
        private Vector3 pivot,initialPivot;
        private float distance,targetDistance,yaw=-25,pitch=57,targetPitch=57;
        private bool enabledInput,middleCapture,focused=true;
        private Plane grabPlane;
        private Vector3 grabbedPoint;
        public Camera Lens=>lens;
        public Vector3 Pivot=>pivot;
        public float Heading=>yaw;
        public float Pitch=>pitch;
        public float Distance=>distance;
        public bool IsPanning=>middleCapture;
        // Kept for the placement controller: any captured camera gesture suspends hover positioning.
        public bool IsOrbiting=>middleCapture;

        public void Initialize(Camera camera,Rect area,Vector3 focus,Func<float,float,float> terrainHeight,Func<Vector2,bool> uiTest)
        {
            lens=camera;bounds=area;height=terrainHeight;overUI=uiTest;
            initialPivot=focus;pivot=focus;
            lens.fieldOfView=48;lens.nearClipPlane=2;lens.farClipPlane=7000;
            ResetView();ApplyPose();
        }
        public void SetInteraction(bool value)
        {enabledInput=value;middleCapture=false;if(!value){targetDistance=distance;targetPitch=pitch;}}
        public void ResetView()
        {
            pivot=initialPivot;
            float supported=SupportedDistance();
            float clearance=Mathf.Min(pivot.x-bounds.xMin,bounds.xMax-pivot.x,pivot.z-bounds.yMin,bounds.yMax-pivot.z)-70;
            float focusDistance=Mathf.Max(Mathf.Min(minimumDistance,supported),clearance/GroundRadius());
            distance=targetDistance=Mathf.Min(initialDistance,supported,focusDistance);
            yaw=-25;pitch=targetPitch=Mathf.Clamp(57,minimumPitch,maximumPitch);middleCapture=false;
        }
        void LateUpdate()
        {
            if(!lens || height==null)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(enabledInput && focused)
            {
                bool ui=mouse!=null && (overUI?.Invoke(mouse.position.ReadValue())??false);
                if(mouse!=null)
                {
                    Vector2 point=mouse.position.ReadValue();
                    if(mouse.middleButton.wasPressedThisFrame)middleCapture=!ui && BeginGrab(point);
                    if(middleCapture && mouse.middleButton.isPressed)
                    {
                        Ray ray=lens.ScreenPointToRay(point);
                        if(grabPlane.Raycast(ray,out float travel))
                        {
                            Vector3 movement=grabbedPoint-ray.GetPoint(travel);movement.y=0;pivot+=movement;
                        }
                    }
                    if(!mouse.middleButton.isPressed)middleCapture=false;
                    if(!ui)
                    {
                        float divisor=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                            (Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.WindowsEditor)?120f:1f;
                        float ticks=mouse.scroll.ReadValue().y/divisor;
                        if(!float.IsNaN(ticks)&&!float.IsInfinity(ticks))
                        {
                            ticks=Mathf.Clamp(ticks,-20,20);
                            bool zoom=keyboard!=null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                            if(zoom)
                                targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-ticks*zoomPerTick),
                                    Mathf.Min(minimumDistance,SupportedDistance()),SupportedDistance());
                            else
                                targetPitch=Mathf.Clamp(targetPitch+ticks*tiltPerTick,minimumPitch,maximumPitch);
                        }
                    }
                }
                if(keyboard!=null)
                {
                    if(keyboard.homeKey.wasPressedThisFrame)ResetView();
                    int turn=(keyboard.eKey.wasPressedThisFrame?1:0)-(keyboard.qKey.wasPressedThisFrame?1:0);
                    yaw=Mathf.Repeat(yaw+turn*45,360);
                    float zoom=(keyboard.equalsKey.isPressed||keyboard.numpadPlusKey.isPressed?1:0)-
                        (keyboard.minusKey.isPressed||keyboard.numpadMinusKey.isPressed?1:0);
                    targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-zoom*.9f*Time.unscaledDeltaTime),
                        Mathf.Min(minimumDistance,SupportedDistance()),SupportedDistance());
                    float x=(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1:0);
                    float z=(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1:0);
                    Vector3 motion=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(x,0,z),1);
                    pivot+=motion*(distance*panRate*Mathf.Min(.05f,Time.unscaledDeltaTime));
                }
            }
            distance=Mathf.Lerp(distance,targetDistance,1-Mathf.Exp(-10*Time.unscaledDeltaTime));
            pitch=Mathf.Lerp(pitch,targetPitch,1-Mathf.Exp(-tiltSmoothing*Time.unscaledDeltaTime));
            Vector3 requestedPivot=pivot;
            ApplyPose();
            // At a survey boundary, discard unreachable drag travel so reversing direction responds immediately.
            if(middleCapture && mouse!=null && (new Vector2(pivot.x-requestedPivot.x,pivot.z-requestedPivot.z)).sqrMagnitude>.0001f)
            {
                Ray ray=lens.ScreenPointToRay(mouse.position.ReadValue());
                if(grabPlane.Raycast(ray,out float travel))grabbedPoint=ray.GetPoint(travel);
            }
        }
        bool BeginGrab(Vector2 screenPoint)
        {
            Ray ray=lens.ScreenPointToRay(screenPoint);float planeHeight=pivot.y-2;
            if(Physics.Raycast(ray,out RaycastHit hit,lens.farClipPlane,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.collider is TerrainCollider)
                planeHeight=hit.point.y;
            grabPlane=new Plane(Vector3.up,new Vector3(0,planeHeight,0));
            if(!grabPlane.Raycast(ray,out float travel))return false;
            grabbedPoint=ray.GetPoint(travel);return true;
        }
        float SupportedDistance()
        {
            if(!lens)return initialDistance;
            // Reserve the same footprint for every tilt and heading, plus some pan travel at the widest view.
            float halfSpan=Mathf.Max(1,Mathf.Min(bounds.width,bounds.height)*.5f-70);
            return Mathf.Max(1,Mathf.Min(maximumDistance,halfSpan*.85f/GroundRadius()));
        }
        void ApplyPose()
        {
            Quaternion orientation=Quaternion.Euler(pitch,yaw,0);
            targetDistance=Mathf.Min(targetDistance,SupportedDistance());distance=Mathf.Min(distance,SupportedDistance());
            float safeDistance=distance;
            // This inset depends on zoom/aspect, never on the current pitch or heading. A point reached
            // overhead remains valid when tilting or turning at an edge, so ApplyPose cannot relocate it.
            float inset=70+GroundRadius()*safeDistance;
            pivot.x=Mathf.Clamp(pivot.x,bounds.xMin+inset,bounds.xMax-inset);
            pivot.z=Mathf.Clamp(pivot.z,bounds.yMin+inset,bounds.yMax-inset);
            pivot.y=height(pivot.x,pivot.z)+2;
            Vector3 position=pivot-orientation*Vector3.forward*safeDistance;
            // The footprint includes the lens, so the pivot constraint already keeps its X/Z in bounds.
            // Independently clamping the lens here would change camera heading without a Q/E keypress.
            position.y=Mathf.Max(position.y,height(position.x,position.z)+24);
            lens.transform.SetPositionAndRotation(position,Quaternion.LookRotation(pivot-position,Vector3.up));
        }
        float GroundRadius()
        {
            // On the ground plane, the far corner at minimum pitch has the greatest distance from
            // the pivot. Its swept circle encloses every allowed tilt and any Q/E heading.
            float angle=minimumPitch*Mathf.Deg2Rad,sine=Mathf.Sin(angle),cosine=Mathf.Cos(angle);
            float tangent=Mathf.Tan(lens.fieldOfView*.5f*Mathf.Deg2Rad);
            float denominator=Mathf.Max(.01f,sine-cosine*tangent);
            var corner=new Vector2(lens.aspect*tangent*sine,tangent)/denominator;
            return Mathf.Max(cosine,corner.magnitude);
        }
        void OnApplicationFocus(bool value){focused=value;if(!value)middleCapture=false;}
        void OnDisable()=>middleCapture=false;
    }
}
