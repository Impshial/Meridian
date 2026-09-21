using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Meridian
{
    /// <summary>Small, noninteractive survey annotations plus the two established menu-style actions.</summary>
    public sealed class SurfacePresentation : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform root;
        private CanvasGroup group;
        private TMP_FontAsset font;
        private TMP_Text instruction,status;
        private readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        private readonly List<Marker> markers=new List<Marker>();
        public Button BackButton {get;private set;}
        public Button LandButton {get;private set;}
        public void Initialize(GameObject buttonPrefab,SurfaceSelectionController controller)
        {
            root=new GameObject("Landing Survey UI",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(transform,false);
            canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();group=root.gameObject.AddComponent<CanvasGroup>();group.alpha=0;
            font=buttonPrefab?buttonPrefab.GetComponentInChildren<TMP_Text>(true)?.font:TMP_Settings.defaultFontAsset;
            var shade=Rect("Control backdrop",root);shade.anchorMin=Vector2.zero;shade.anchorMax=new Vector2(1,0);shade.pivot=new Vector2(.5f,0);shade.sizeDelta=new Vector2(0,146);
            var panel=shade.gameObject.AddComponent<Image>();panel.color=new Color(.012f,.018f,.021f,.90f);panel.raycastTarget=false;
            BackButton=Button("BACK TO PLANET",buttonPrefab,false);LandButton=Button("LAND HERE",buttonPrefab,true);
            BackButton.onClick.AddListener(controller.BackToPlanet);LandButton.onClick.AddListener(controller.ConfirmLanding);
            instruction=Label("Placement instruction",root,19,TextAlignmentOptions.Center);
            Layout(instruction.rectTransform,new Vector2(.5f,0),new Vector2(0,104),new Vector2(1700,32));
            instruction.text="Choose clear ground  ·  R / Shift+R turn  ·  Right click / Esc clear";
            status=Label("Placement status",root,20,TextAlignmentOptions.Center);
            Layout(status.rectTransform,new Vector2(.5f,0),new Vector2(0,53),new Vector2(880,52));
            var title=Label("Survey title",root,27,TextAlignmentOptions.TopLeft);
            Layout(title.rectTransform,new Vector2(0,1),new Vector2(44,-35),new Vector2(1100,45),new Vector2(0,1));
            title.text="L A N D I N G   S U R V E Y";title.color=new Color(1,.75f,.43f);
            var controls=Label("Camera controls",root,16,TextAlignmentOptions.TopLeft);
            Layout(controls.rectTransform,new Vector2(0,1),new Vector2(46,-85),new Vector2(1700,50),new Vector2(0,1));
            controls.text="Middle-drag / WASD move   ·   Wheel tilt   ·   Shift + wheel zoom   ·   Q / E rotate 45°   ·   Home reset";
            controls.color=new Color(.81f,.85f,.86f);
            if(!EventSystem.current)
            {
                var events=new GameObject("Surface EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        public void Ready(){group.alpha=1;}
        public void HideForColony(){if(root)root.gameObject.SetActive(false);}
        public void SetInteraction(bool enabled,bool confirm)
        {group.interactable=enabled;group.blocksRaycasts=enabled;BackButton.interactable=enabled;LandButton.interactable=enabled&&confirm;}
        public void SetPlacement(bool locked,bool valid,bool confirmed,string reason)
        {
            if(confirmed)
            {
                status.text="Landing site confirmed";status.color=new Color(1,.75f,.43f);
                instruction.text="Site secured for arrival. Continue surveying, or return to the planet to review.";
            }
            else
            {
                instruction.text="Choose clear ground  ·  R / Shift+R turn  ·  Right click / Esc clear";
                status.text=valid?(locked?"Position locked — ready to land":"Click to lock this landing position"):reason;
                status.color=valid?new Color(.54f,.91f,.73f):new Color(1,.64f,.53f);
            }
        }
        public bool OverUI(Vector2 point)
        {
            if(!EventSystem.current)return false;
            uiHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},uiHits);
            return uiHits.Count>0;
        }
        public void AddSurveyMarkers(SurfaceWorldData world)
        {
            if(world.TreeGroves!=null)
            {
                var groves=new List<TreeGroveData>(world.TreeGroves);
                groves.Sort((a,b)=>(a.Position-world.DefaultLanding.Position).sqrMagnitude.CompareTo((b.Position-world.DefaultLanding.Position).sqrMagnitude));
                for(int i=0;i<Mathf.Min(12,groves.Count);i++)
                {
                    var grove=groves[i];
                    AddMarker(grove.Position+Vector3.up*24,$"TIMBER · {grove.WoodAmount:N0}",new Color(.73f,.91f,.53f));
                }
            }
            int deposits=0;
            foreach(var item in world.Objects)
            {
                if(item.Kind!=SurfaceObjectKind.Iron && item.Kind!=SurfaceObjectKind.Copper && item.Kind!=SurfaceObjectKind.Ice)continue;
                AddMarker(item.Position+Vector3.up*(item.Radius+4),item.Kind.ToString().ToUpperInvariant(),new Color(.99f,.75f,.39f));
                if(++deposits>=18)break;
            }
            foreach(var tile in world.Tiles)
            {
                if(tile.Water==null || tile.Water.Vertices.Length==0)continue;
                Vector3 p=tile.Water.Vertices[tile.Water.Vertices.Length/2]+Vector3.up*4;
                AddMarker(p,"~ WATER",new Color(.48f,.83f,.92f));
            }
        }
        public void UpdateMarkers(Camera camera,SurfaceLandscape landscape)
        {
            foreach(var marker in markers)
            {
                Vector3 view=camera.WorldToViewportPoint(marker.Position);
                bool visible=view.z>0 && view.x>.02f && view.x<.98f && view.y>.17f && view.y<.86f;
                if(visible)
                {
                    Vector3 delta=marker.Position-camera.transform.position;
                    if(landscape.Pick(new Ray(camera.transform.position,delta.normalized),out var hit) && hit.distance<delta.magnitude-8)visible=false;
                }
                marker.Label.gameObject.SetActive(visible);
                if(visible)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root,camera.WorldToScreenPoint(marker.Position),null,out Vector2 point);
                    marker.Label.rectTransform.anchoredPosition=point;
                }
            }
        }
        void AddMarker(Vector3 position,string title,Color color)
        {
            var label=Label(title,root,14,TextAlignmentOptions.Center);label.text=title;label.color=color;
            label.outlineWidth=.22f;label.outlineColor=new Color(0,0,0,.8f);
            label.rectTransform.sizeDelta=new Vector2(180,24);markers.Add(new Marker{Position=position,Label=label});
        }
        Button Button(string title,GameObject prefab,bool right)
        {
            GameObject go=prefab?Instantiate(prefab,root):new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));
            if(!prefab)go.transform.SetParent(root,false);go.name=title;
            var rect=(RectTransform)go.transform;Layout(rect,new Vector2(right?1:0,0),new Vector2(right?-44:44,54),new Vector2(360,58),new Vector2(right?1:0,.5f));
            var label=go.GetComponentInChildren<TMP_Text>();if(!label)label=Label(title,go.transform,22,TextAlignmentOptions.Center);
            label.text=title;label.characterSpacing=4;label.fontSize=22;
            var button=go.GetComponent<Button>();button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick=new Button.ButtonClickedEvent();
            var visual=go.GetComponent<MenuButtonVisual>();if(visual)visual.Configure(label,go.GetComponentInChildren<CanvasGroup>());
            return button;
        }
        TMP_Text Label(string name,Transform parent,float size,TextAlignmentOptions alignment)
        {
            var label=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();label.transform.SetParent(parent,false);
            label.font=font;label.fontSize=size;label.alignment=alignment;label.color=new Color(.88f,.88f,.85f);label.raycastTarget=false;
            label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        static void Layout(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size,Vector2? pivot=null)
        {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot??new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
        sealed class Marker{public Vector3 Position;public TMP_Text Label;}
    }
}
