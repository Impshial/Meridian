using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Meridian
{
    /// <summary>Pregame orbit camera. The pivot and lens both stay over generated, supported terrain.</summary>
    public sealed class SurveyCamera : MonoBehaviour
    {
        [SerializeField] private float minimumDistance=95,maximumDistance=1250,initialDistance=730;
        [SerializeField] private float minimumPitch=42,maximumPitch=78,panRate=.65f;
        private Camera lens;
        private Rect bounds;
        private Func<float,float,float> height;
        private Func<Vector2,bool> overUI;
        private Vector3 pivot,initialPivot;
        private float distance,targetDistance,yaw=-25,pitch=57;
        private bool enabledInput,middleCapture;
        private Vector2 previous;
        public Camera Lens=>lens;
        public Vector3 Pivot=>pivot;
        public bool IsOrbiting=>middleCapture;

        public void Initialize(Camera camera,Rect area,Vector3 focus,Func<float,float,float> terrainHeight,Func<Vector2,bool> uiTest)
        {
            lens=camera;bounds=area;height=terrainHeight;overUI=uiTest;
            initialPivot=focus;pivot=focus;distance=targetDistance=initialDistance;
            lens.fieldOfView=48;lens.nearClipPlane=2;lens.farClipPlane=7000;
            ApplyPose();
        }
        public void SetInteraction(bool value){enabledInput=value;middleCapture=false;}
        public void ResetView(){pivot=initialPivot;targetDistance=initialDistance;yaw=-25;pitch=57;}
        void LateUpdate()
        {
            if(!lens || height==null)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(enabledInput)
            {
                bool ui=mouse!=null && (overUI?.Invoke(mouse.position.ReadValue())??false);
                if(mouse!=null)
                {
                    Vector2 point=mouse.position.ReadValue();
                    if(mouse.middleButton.wasPressedThisFrame){middleCapture=!ui;previous=point;}
                    if(middleCapture && mouse.middleButton.isPressed)
                    {
                        Vector2 delta=(point-previous)*(1080f/Mathf.Max(1,Screen.height));
                        yaw+=delta.x*.18f;pitch=Mathf.Clamp(pitch-delta.y*.14f,minimumPitch,maximumPitch);
                    }
                    if(!mouse.middleButton.isPressed)middleCapture=false;
                    previous=point;
                    if(!ui)
                    {
                        float divisor=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                            (Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.WindowsEditor)?120f:1f;
                        float ticks=mouse.scroll.ReadValue().y/divisor;
                        if(!float.IsNaN(ticks)&&!float.IsInfinity(ticks))
                            targetDistance=Mathf.Clamp(targetDistance*Mathf.Exp(-Mathf.Clamp(ticks,-20,20)*.12f),minimumDistance,maximumDistance);
                    }
                }
                if(keyboard!=null)
                {
                    if(keyboard.homeKey.wasPressedThisFrame)ResetView();
                    float x=(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1:0);
                    float z=(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1:0);
                    Vector3 motion=Quaternion.Euler(0,yaw,0)*Vector3.ClampMagnitude(new Vector3(x,0,z),1);
                    pivot+=motion*(distance*panRate*Mathf.Min(.05f,Time.unscaledDeltaTime));
                }
            }
            distance=Mathf.Lerp(distance,targetDistance,1-Mathf.Exp(-10*Time.unscaledDeltaTime));
            ApplyPose();
        }
        void ApplyPose()
        {
            Quaternion orientation=Quaternion.Euler(pitch,yaw,0);
            // Reserve the complete ground-facing frustum when panning. A lens over the terrain alone
            // would still expose tile edges while looking toward the edge of the survey.
            GroundExtents(orientation,1,out Vector2 minimum,out Vector2 maximum);
            float safeDistance=Mathf.Min(distance,Mathf.Min((bounds.width-140)/Mathf.Max(.01f,maximum.x-minimum.x),
                (bounds.height-140)/Mathf.Max(.01f,maximum.y-minimum.y)));
            minimum*=safeDistance;maximum*=safeDistance;
            pivot.x=Mathf.Clamp(pivot.x,bounds.xMin+70-minimum.x,bounds.xMax-70-maximum.x);
            pivot.z=Mathf.Clamp(pivot.z,bounds.yMin+70-minimum.y,bounds.yMax-70-maximum.y);
            pivot.y=height(pivot.x,pivot.z)+2;
            Vector3 position=pivot-orientation*Vector3.forward*safeDistance;
            position.x=Mathf.Clamp(position.x,bounds.xMin+20,bounds.xMax-20);
            position.z=Mathf.Clamp(position.z,bounds.yMin+20,bounds.yMax-20);
            position.y=Mathf.Max(position.y,height(position.x,position.z)+24);
            lens.transform.SetPositionAndRotation(position,Quaternion.LookRotation(pivot-position,Vector3.up));
        }
        void GroundExtents(Quaternion orientation,float radius,out Vector2 minimum,out Vector2 maximum)
        {
            Vector3 forward=orientation*Vector3.forward,right=orientation*Vector3.right,up=orientation*Vector3.up;
            Vector3 origin=-forward*radius;float tangent=Mathf.Tan(lens.fieldOfView*.5f*Mathf.Deg2Rad);
            minimum=maximum=new Vector2(origin.x,origin.z);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {
                Vector3 ray=forward+right*(x*tangent*lens.aspect)+up*(y*tangent);
                Vector3 at=origin+ray*(-origin.y/Mathf.Min(-.01f,ray.y));
                minimum=Vector2.Min(minimum,new Vector2(at.x,at.z));maximum=Vector2.Max(maximum,new Vector2(at.x,at.z));
            }
        }
        void OnApplicationFocus(bool focused){if(!focused)middleCapture=false;}
        void OnDisable()=>middleCapture=false;
    }
}
