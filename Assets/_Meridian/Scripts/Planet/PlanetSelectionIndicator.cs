using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Meridian
{
    /// <summary>An honest offscreen representation of the fixed geographic flag, never a second planted marker.</summary>
    public sealed class PlanetSelectionIndicator : MonoBehaviour
    {
        RectTransform marker;
        TMP_Text farSideLabel;
        Vector2 lastBearing=Vector2.up;
        float bearingAngle=90;

        public static PlanetSelectionIndicator Create()
        {
            var root=new GameObject("Selected Region Edge Indicator",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var component=root.AddComponent<PlanetSelectionIndicator>();component.Initialize();return component;
        }
        void Initialize()
        {
            marker=new GameObject("Amber Selection",typeof(RectTransform)).GetComponent<RectTransform>();
            marker.SetParent(transform,false);marker.sizeDelta=new Vector2(90,88);
            var symbol=new GameObject("Flag",typeof(RectTransform),typeof(CanvasRenderer),typeof(PlanetFlagIndicatorGraphic));
            var symbolRect=(RectTransform)symbol.transform;symbolRect.SetParent(marker,false);
            symbolRect.sizeDelta=new Vector2(60,60);symbolRect.anchoredPosition=new Vector2(0,9);
            var graphic=symbol.GetComponent<PlanetFlagIndicatorGraphic>();graphic.color=new Color32(255,183,88,255);graphic.raycastTarget=false;
            var label=new GameObject("Far Side",typeof(RectTransform),typeof(TextMeshProUGUI));
            var labelRect=(RectTransform)label.transform;labelRect.SetParent(marker,false);
            labelRect.sizeDelta=new Vector2(96,26);labelRect.anchoredPosition=new Vector2(0,-31);
            farSideLabel=label.GetComponent<TMP_Text>();farSideLabel.text="Far side";farSideLabel.fontSize=16;
            farSideLabel.alignment=TextAlignmentOptions.Center;farSideLabel.color=new Color(.78f,.74f,.64f,.9f);farSideLabel.raycastTarget=false;
            var texts=FindObjectsByType<TMP_Text>();
            foreach(var text in texts)if(text!=farSideLabel&&text.font){farSideLabel.font=text.font;break;}
            marker.gameObject.SetActive(false);
        }
        public void Hide(){if(marker)marker.gameObject.SetActive(false);}
        public void Show(Camera camera,Vector3 worldAnchor,Vector3 globeCenter,bool farSide)
        {
            Vector3 offset=worldAnchor-globeCenter;
            Vector2 bearing=new Vector2(Vector3.Dot(offset,camera.transform.right),Vector3.Dot(offset,camera.transform.up));
            // At the exact antipode a projected direction does not exist. Keep the last meaningful bearing.
            // Interpolating an angle (rather than a vector through zero) also prevents a jump when passing it.
            if(bearing.sqrMagnitude>.0025f)
            {
                float target=Mathf.Atan2(bearing.y,bearing.x)*Mathf.Rad2Deg;
                bearingAngle=marker.gameObject.activeSelf?Mathf.LerpAngle(bearingAngle,target,1-Mathf.Exp(-12*Time.unscaledDeltaTime)):target;
                lastBearing=new Vector2(Mathf.Cos(bearingAngle*Mathf.Deg2Rad),Mathf.Sin(bearingAngle*Mathf.Deg2Rad));
            }
            float scale=Mathf.Max(.65f,Screen.height/1080f);
            float left=58*scale,right=Screen.width-58*scale,bottom=158*scale,top=Screen.height-66*scale;
            Vector2 center=new Vector2(Screen.width*.5f,Screen.height*.5f),direction=lastBearing;
            float xDistance=Mathf.Abs(direction.x)<.0001f?float.PositiveInfinity:
                (direction.x>0?right-center.x:left-center.x)/direction.x;
            float yDistance=Mathf.Abs(direction.y)<.0001f?float.PositiveInfinity:
                (direction.y>0?top-center.y:bottom-center.y)/direction.y;
            Vector2 point=center+direction*Mathf.Min(xDistance,yDistance);
            point.x=Mathf.Clamp(point.x,left,right);point.y=Mathf.Clamp(point.y,bottom,top);
            marker.anchorMin=marker.anchorMax=new Vector2(point.x/Mathf.Max(1,Screen.width),point.y/Mathf.Max(1,Screen.height));
            marker.anchoredPosition=Vector2.zero;farSideLabel.gameObject.SetActive(farSide);marker.gameObject.SetActive(true);
        }
    }
}
