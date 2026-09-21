using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meridian
{
    /// <summary>Screen-relative ground survey with smooth tilt, zoom and 45-degree heading targets.</summary>
    public sealed class SurveyCamera : MonoBehaviour
    {
        [SerializeField] private float minimumDistance=95,maximumDistance=1250,initialDistance=730;
        [SerializeField] private float minimumPitch=42,maximumPitch=78,panRate=.65f;
        [SerializeField] private float tiltPerTick=3,tiltSmoothing=12;
        [SerializeField] private float zoomPerTick=.12f;
        [SerializeField] private float rotationSmoothing=12;
        private Camera lens;
        private Rect bounds;
        private Func<float,float,float> height;
        private Func<Vector2,bool> overUI;
        private Func<Vector3,float,bool> canFrame;
        private Func<Vector3,float,Vector3> nearestFrame;
        private Vector3 pivot,initialPivot;
        private float distance,targetDistance,yaw=-25,targetYaw=-25,pitch=57,targetPitch=57;
        private bool enabledInput,middleCapture,focused=true;
        private Vector2 previousPointer;
        private Func<bool> keyboardBlocked;public float Sensitivity=1,ZoomSensitivity=1,TiltSensitivity=1;public bool EdgePan;
        public Camera Lens=>lens;
        public Vector3 Pivot=>pivot;
        public float Heading=>Mathf.Repeat(yaw,360);
        public float Pitch=>pitch;
        public float Distance=>distance;
        public bool IsPanning=>middleCapture;
        // Kept for the placement controller: any captured camera gesture suspends hover positioning.
        public bool IsOrbiting=>middleCapture;
        public void ConfigureColony(Rect area,Func<float,float,float> terrainHeight,Func<Vector2,bool> uiTest,Func<bool> textFocus,Func<Vector3,float,bool> ownership=null,Func<Vector3,float,Vector3> nearest=null)
        {bounds=area;height=terrainHeight;overUI=uiTest;keyboardBlocked=textFocus;canFrame=ownership;nearestFrame=nearest;minimumDistance=28;initialDistance=240;lens.nearClipPlane=.4f;}
        public void SetBounds(Rect area){bounds=area;}
        public void SetHome(Vector3 point){initialPivot=point;}
        public void Focus(Vector3 point){pivot=point;ApplyPose();}
        public Colony.CameraState Capture()=>new Colony.CameraState{pivot=pivot,yaw=yaw,pitch=pitch,distance=distance};
        public void Restore(Colony.CameraState state)
        {pivot=state.pivot;yaw=targetYaw=state.yaw;pitch=targetPitch=Mathf.Clamp(state.pitch,minimumPitch,maximumPitch);distance=targetDistance=Mathf.Clamp(state.distance,minimumDistance,maximumDistance);middleCapture=false;ApplyPose();}

        public void Initialize(Camera camera,Rect area,Vector3 focus,Func<float,float,float> terrainHeight,Func<Vector2,bool> uiTest)
        {
            lens=camera;bounds=area;height=terrainHeight;overUI=uiTest;
            initialPivot=focus;pivot=focus;
            lens.fieldOfView=48;lens.nearClipPlane=2;lens.farClipPlane=7000;
            ResetView();ApplyPose();
        }
        public void SetInteraction(bool value)
        {enabledInput=value;middleCapture=false;if(!value){targetDistance=distance;targetPitch=pitch;targetYaw=yaw;}}
        public void ResetView()
        {
            pivot=initialPivot;
            float supported=SupportedDistance();
            float clearance=Mathf.Min(pivot.x-bounds.xMin,bounds.xMax-pivot.x,pivot.z-bounds.yMin,bounds.yMax-pivot.z)-70;
            float focusDistance=Mathf.Max(Mathf.Min(minimumDistance,supported),clearance/GroundRadius());
            distance=targetDistance=Mathf.Min(initialDistance,supported,focusDistance);
            yaw=targetYaw=-25;pitch=targetPitch=Mathf.Clamp(57,minimumPitch,maximumPitch);middleCapture=false;
        }
        void LateUpdate()
        {
            if(!lens || height==null)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(enabledInput && focused && !(keyboardBlocked?.Invoke()??false))
            {
                bool ui=mouse!=null && (overUI?.Invoke(mouse.position.ReadValue())??false);
                if(mouse!=null)
                {
                    Vector2 point=mouse.position.ReadValue();
                    if(mouse.middleButton.wasPressedThisFrame){middleCapture=!ui;previousPointer=point;}
                    if(middleCapture && mouse.middleButton.isPressed)
                    {
                        Vector2 delta=point-previousPointer;
                        // A fixed screen scale avoids the sideways perspective drift of off-centre plane grabs.
                        float metresPerPixel=2*distance*Mathf.Tan(lens.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Max(1,lens.pixelHeight);
                        float verticalScale=1/Mathf.Max(.1f,-lens.transform.forward.y);
                        Pan(ScreenMotion(new Vector2(-delta.x,-delta.y*verticalScale))*metresPerPixel*Sensitivity);
                    }
                    previousPointer=point;
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
                                targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-ticks*zoomPerTick*ZoomSensitivity),
                                    Mathf.Min(minimumDistance,SupportedDistance()),SupportedDistance());
                            else
                                targetPitch=Mathf.Clamp(targetPitch+ticks*tiltPerTick*TiltSensitivity,minimumPitch,maximumPitch);
                        }
                    }
                }
                if(keyboard!=null)
                {
                    if(keyboard.homeKey.wasPressedThisFrame)ResetView();
                    int turn=(keyboard.eKey.wasPressedThisFrame?1:0)-(keyboard.qKey.wasPressedThisFrame?1:0);
                    targetYaw+=turn*45;
                    float zoom=(keyboard.equalsKey.isPressed||keyboard.numpadPlusKey.isPressed?1:0)-
                        (keyboard.minusKey.isPressed||keyboard.numpadMinusKey.isPressed?1:0);
                    targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-zoom*.9f*Time.unscaledDeltaTime),
                        Mathf.Min(minimumDistance,SupportedDistance()),SupportedDistance());
                    float x=(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1:0);
                    float z=(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1:0);
                    Vector3 motion=ScreenMotion(Vector2.ClampMagnitude(new Vector2(x,z),1));
                    Pan(motion*(distance*panRate*Sensitivity*Mathf.Min(.05f,Time.unscaledDeltaTime)));
                }
                if(EdgePan&&mouse!=null&&!ui&&!middleCapture)
                {var p=mouse.position.ReadValue();var edge=new Vector2(p.x<4?-1:p.x>Screen.width-4?1:0,p.y<4?-1:p.y>Screen.height-4?1:0);Pan(ScreenMotion(edge)*distance*panRate*Sensitivity*Mathf.Min(.05f,Time.unscaledDeltaTime));}
            }
            distance=Mathf.Lerp(distance,targetDistance,1-Mathf.Exp(-10*Time.unscaledDeltaTime));
            pitch=Mathf.Lerp(pitch,targetPitch,1-Mathf.Exp(-tiltSmoothing*Time.unscaledDeltaTime));
            SmoothHeading(Time.unscaledDeltaTime);
            ApplyPose();
        }
        void SmoothHeading(float deltaTime)
        {
            // Keep an unwrapped target: rapid presses retain their direction even past half/full turns.
            yaw=Mathf.Lerp(yaw,targetYaw,1-Mathf.Exp(-rotationSmoothing*deltaTime));
            if(Mathf.Abs(yaw-targetYaw)<.01f)
            {
                yaw=targetYaw;
                float turns=Mathf.Floor(targetYaw/360)*360;yaw-=turns;targetYaw-=turns;
            }
        }
        Vector3 ScreenMotion(Vector2 movement)
        {
            Vector3 right=Vector3.ProjectOnPlane(lens.transform.right,Vector3.up).normalized;
            Vector3 up=Vector3.ProjectOnPlane(lens.transform.forward,Vector3.up).normalized;
            return right*movement.x+up*movement.y;
        }
        void Pan(Vector3 movement)
        {
            float inset=70+GroundRadius()*distance;
            float fraction=1;
            if(movement.x>0)fraction=Mathf.Min(fraction,(bounds.xMax-inset-pivot.x)/movement.x);
            else if(movement.x<0)fraction=Mathf.Min(fraction,(bounds.xMin+inset-pivot.x)/movement.x);
            if(movement.z>0)fraction=Mathf.Min(fraction,(bounds.yMax-inset-pivot.z)/movement.z);
            else if(movement.z<0)fraction=Mathf.Min(fraction,(bounds.yMin+inset-pivot.z)/movement.z);
            // Clip the entire requested segment. Independent X/Z clamps slide along oblique map edges.
            fraction=Mathf.Clamp01(fraction);
            if(canFrame!=null&&!canFrame(pivot+movement*fraction,inset))
            {float lo=0,hi=fraction;for(int i=0;i<20;i++){float mid=(lo+hi)*.5f;if(canFrame(pivot+movement*mid,inset))lo=mid;else hi=mid;}fraction=lo;}
            pivot+=movement*fraction;
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
            if(canFrame!=null)
            {
                float minimumInset=70+GroundRadius()*minimumDistance;
                if(!canFrame(pivot,minimumInset)&&nearestFrame!=null)pivot=nearestFrame(pivot,minimumInset);
                if(!canFrame(pivot,70+GroundRadius()*distance))
                {float lo=minimumDistance,hi=distance;for(int i=0;i<20;i++){float mid=(lo+hi)*.5f;if(canFrame(pivot,70+GroundRadius()*mid))lo=mid;else hi=mid;}distance=lo;targetDistance=Mathf.Min(targetDistance,lo);}
            }
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
