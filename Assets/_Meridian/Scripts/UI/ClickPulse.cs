using UnityEngine;
using UnityEngine.UI;

namespace Meridian
{
    /// <summary>Scene-owned, noninteractive acknowledgement of accepted world clicks.</summary>
    public sealed class ClickPulse : MonoBehaviour
    {
        const int Capacity=4;
        const float Duration=.38f;
        static ClickPulse instance;
        readonly ClickPulseGraphic[] rings=new ClickPulseGraphic[Capacity];
        readonly float[] elapsed=new float[Capacity];
        int next;

        public static void Play(Vector2 screenPoint,bool land)
        {
            if(!instance)
            {
                var root=new GameObject("World Click Feedback",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                instance=root.AddComponent<ClickPulse>();instance.Initialize();
            }
            instance.Show(screenPoint,land);
        }
        public static void Clear()
        {
            if(!instance)return;
            for(int i=0;i<Capacity;i++)if(instance.rings[i])instance.rings[i].gameObject.SetActive(false);
        }
        void Initialize()
        {
            for(int i=0;i<Capacity;i++)
            {
                var go=new GameObject("Click Ring "+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(ClickPulseGraphic));
                var rect=(RectTransform)go.transform;rect.SetParent(transform,false);rect.sizeDelta=new Vector2(100,100);
                rings[i]=go.GetComponent<ClickPulseGraphic>();rings[i].raycastTarget=false;go.SetActive(false);
            }
        }
        void Show(Vector2 point,bool land)
        {
            int slot=next;next=(next+1)%Capacity;elapsed[slot]=0;
            var ring=rings[slot];var rect=ring.rectTransform;
            // Normalized anchors are correct even on the first frame before a new CanvasScaler updates.
            rect.anchorMin=rect.anchorMax=new Vector2(point.x/Mathf.Max(1,Screen.width),point.y/Mathf.Max(1,Screen.height));
            rect.anchoredPosition=Vector2.zero;
            ring.color=land?new Color(1,.718f,.345f,.9f):new Color(.67f,.74f,.79f,.65f);
            ring.SetProgress(0);ring.gameObject.SetActive(true);
        }
        void Update()
        {
            for(int i=0;i<Capacity;i++)if(rings[i]&&rings[i].gameObject.activeSelf)
            {
                elapsed[i]+=Time.unscaledDeltaTime;float t=elapsed[i]/Duration;
                if(t>=1)rings[i].gameObject.SetActive(false);else rings[i].SetProgress(t);
            }
        }
        void OnDestroy(){if(instance==this)instance=null;}

    }
}
