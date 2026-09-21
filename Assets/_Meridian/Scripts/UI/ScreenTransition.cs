using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Meridian
{
    public sealed class ScreenTransition : MonoBehaviour
    {
        public const string PlanetLoadingMessage="Getting you an exo-planet...";
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField,Range(.1f,1f)] private float duration=.5f;
        private CanvasGroup presentation;
        private TMP_Text message,details;
        private RectTransform indicator;
        private Button recovery;
        private string recoveryScene;
        private bool recovering,failed;
        private ISetupDestination loadingDestination;
        private float loadingStarted,nextProgressUpdate;
        private int loggedProgress=-1;
        public static ScreenTransition Active {get;private set;}
        public string Message=>message?message.text:string.Empty;
        public void Configure(CanvasGroup group,TMP_FontAsset typography=null){overlay=group;font=typography;}
        void Awake()
        {
            if(Active && Active!=this){Destroy(gameObject);return;}
            Active=this;DontDestroyOnLoad(gameObject);BuildPresentation();
        }
        public static void Travel(ScreenTransition prefab,string destination,string loadingMessage=null)
        {
            if(Active)return;
            var runner=Instantiate(prefab);
            runner.SetMessage(loadingMessage??(destination=="PlanetSelection"?PlanetLoadingMessage:string.Empty));
            runner.StartCoroutine(runner.ChangeScene(destination));
        }
        public static void Reveal(ScreenTransition prefab,string loadingMessage=null)
        {
            if(Active)return;
            var runner=Instantiate(prefab);runner.overlay.alpha=1;
            runner.SetMessage(loadingMessage??PlanetLoadingMessage);runner.StartCoroutine(runner.FinishEntry());
        }
        static ISetupDestination Destination()=>FindObjectsByType<MonoBehaviour>().OfType<ISetupDestination>().FirstOrDefault();
        IEnumerator ChangeScene(string destination)
        {
            Destination()?.SetInteraction(false);ClickPulse.Clear();
            // Give the independently visible message a rendered frame before scene/resource work.
            Canvas.ForceUpdateCanvases();yield return null;
            yield return Fade(overlay.alpha,1);
            AsyncOperation operation=null;System.Exception failure=null;
            try{operation=SceneManager.LoadSceneAsync(destination,LoadSceneMode.Single);}
            catch(System.Exception error){failure=error;}
            if(failure!=null || operation==null)
            {Debug.LogError("Meridian scene transition failed: "+failure);ShowFailure("The next scene could not be opened. Please return and try again.");yield break;}
            while(!operation.isDone)yield return null;
            if(destination=="MainMenu")SetupSession.End();
            yield return FinishEntry();
        }
        IEnumerator FinishEntry()
        {
            yield return null;
            var destination=loadingDestination=Destination();
            while(destination!=null && !destination.IsReady && !destination.GenerationFailed)yield return null;
            if(destination!=null && destination.GenerationFailed)
            {
                destination.SetInteraction(false);
                string reason=destination.FailureMessage;
                ShowFailure(string.IsNullOrWhiteSpace(reason)?"This region could not be prepared. Please choose another location.":reason);
                yield break;
            }
            recovery.gameObject.SetActive(false);details.gameObject.SetActive(false);
            yield return Fade(1,0);
            while(HeldInput())yield return null;
            // Keep the destination input gated until the overlay has actually retired.
            float elapsed=0;while(elapsed<.15f){elapsed+=Time.unscaledDeltaTime;presentation.alpha=1-elapsed/.15f;yield return null;}
            destination?.SetInteraction(true);Destroy(gameObject);
        }
        public static bool HeldInput()=>
            (Mouse.current?.leftButton.isPressed??false)||(Mouse.current?.rightButton.isPressed??false)||(Mouse.current?.middleButton.isPressed??false)||
            (Keyboard.current?.enterKey.isPressed??false)||(Keyboard.current?.spaceKey.isPressed??false)||(Gamepad.current?.buttonSouth.isPressed??false);
        void ShowFailure(string reason)
        {
            (loadingDestination as ISetupLoading)?.CancelLoading();
            failed=true;recovering=false;recovery.interactable=true;overlay.alpha=1;presentation.alpha=1;
            bool restoring=SetupSession.Current && SetupSession.Current.LandingConfirmed;
            bool hasPlanet=!restoring && SceneManager.GetActiveScene().name!="PlanetSelection" && SetupSession.Current && SetupSession.Current.Planet!=null;
            recoveryScene=hasPlanet?"PlanetSelection":"MainMenu";
            message.text=restoring?"Unable to restore colony":hasPlanet?"Survey unavailable":"Unable to load planet";
            details.text=reason;details.gameObject.SetActive(true);indicator.gameObject.SetActive(false);
            recovery.GetComponentInChildren<TMP_Text>().text=hasPlanet?"BACK TO PLANET":"RETURN TO MENU";
            recovery.gameObject.SetActive(true);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(recovery.gameObject);
        }
        public void Recover()
        {
            bool canCancel=loadingDestination is ISetupLoading && !loadingDestination.IsReady;
            if(recovering || (!failed && !canCancel))return;
            (loadingDestination as ISetupLoading)?.CancelLoading();StopAllCoroutines();
            recovering=true;failed=false;recovery.interactable=false;
            recovery.gameObject.SetActive(false);details.gameObject.SetActive(false);
            indicator.gameObject.SetActive(true);SetMessage(recoveryScene=="PlanetSelection"?"Returning to Exo-planet...":"Returning to menu...");
            StartCoroutine(ChangeScene(recoveryScene));
        }
        void SetMessage(string text)
        {
            message.text=text;presentation.alpha=string.IsNullOrEmpty(text)?0:1;
            loadingStarted=Time.realtimeSinceStartup;loadingDestination=null;loggedProgress=-1;
        }
        void Update()
        {
            if(failed)return;
            if(indicator)indicator.localRotation=Quaternion.Euler(0,0,-Time.unscaledTime*100);
            float now=Time.realtimeSinceStartup;
            // Watch the entire transition, including scene activation and input release, independently
            // of the coroutine that may be waiting. A live spinner must never conceal an unbounded wait.
            if(now-loadingStarted>180)
            {
                StopAllCoroutines();loadingDestination=Destination();
                Debug.LogError($"Meridian loading timed out in {SceneManager.GetActiveScene().name}: {details.text}");
                ShowFailure("Loading took too long. Return to the planet or menu and try again.");return;
            }
            if(now<nextProgressUpdate)return;nextProgressUpdate=now+.2f;
            if(loadingDestination is Object old && !old)loadingDestination=null;
            if(loadingDestination==null)loadingDestination=Destination();
            if(!recovering && loadingDestination is ISetupLoading loading && !loadingDestination.IsReady && !loadingDestination.GenerationFailed)
            {
                var state=loading.LoadingProgress;
                int percent=Mathf.FloorToInt(state.Fraction*100);
                details.text=$"{state.Detail}  ({percent}%)";details.gameObject.SetActive(true);
                if(percent/10>loggedProgress){loggedProgress=percent/10;Debug.Log($"Meridian loading: {details.text}; {now-loadingStarted:F1}s elapsed.");}
                bool restoring=SetupSession.Current && SetupSession.Current.LandingConfirmed;
                recoveryScene=restoring?"MainMenu":"PlanetSelection";recovery.GetComponentInChildren<TMP_Text>().text=restoring?"RETURN TO MENU":"BACK TO PLANET";
                recovery.interactable=true;recovery.gameObject.SetActive(true);
            }
        }
        IEnumerator Fade(float from,float to)
        {
            overlay.alpha=from;float elapsed=0;
            while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;overlay.alpha=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,elapsed/duration));yield return null;}
            overlay.alpha=to;
        }
        void BuildPresentation()
        {
            // Upgrade an older prefab without making its label inherit the fading black alpha.
            if(overlay.transform==transform)
            {
                var black=GetComponentInChildren<Image>();overlay.alpha=1;
                overlay=black.GetComponent<CanvasGroup>();if(!overlay)overlay=black.gameObject.AddComponent<CanvasGroup>();overlay.alpha=0;
            }
            GetComponent<Canvas>().sortingOrder=30000;
            var scaler=GetComponent<CanvasScaler>();if(!scaler)scaler=gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var shield=Rect("Input Shield",transform);shield.anchorMin=Vector2.zero;shield.anchorMax=Vector2.one;shield.sizeDelta=Vector2.zero;
            shield.gameObject.AddComponent<Image>().color=Color.clear;
            var root=Rect("Loading Presentation",transform);root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.sizeDelta=Vector2.zero;
            presentation=root.gameObject.AddComponent<CanvasGroup>();
            message=Label("Loading Message",root,new Vector2(0,24),28,new Vector2(1300,80));
            details=Label("Recovery Detail",root,new Vector2(0,-65),20,new Vector2(1050,110));details.textWrappingMode=TextWrappingModes.Normal;details.gameObject.SetActive(false);
            indicator=Rect("Activity Indicator",root);indicator.sizeDelta=new Vector2(20,20);indicator.anchoredPosition=new Vector2(0,-130);
            var graphic=indicator.gameObject.AddComponent<Image>();graphic.color=new Color32(255,183,88,255);graphic.raycastTarget=false;
            var cutout=Rect("Indicator Centre",indicator);cutout.sizeDelta=new Vector2(14,14);var cut=cutout.gameObject.AddComponent<Image>();cut.color=Color.black;cut.raycastTarget=false;
            var buttonRect=Rect("Recovery",root);buttonRect.sizeDelta=new Vector2(380,70);buttonRect.anchoredPosition=new Vector2(0,-180);
            var face=buttonRect.gameObject.AddComponent<Image>();face.color=new Color(.12f,.14f,.15f,1);
            recovery=buttonRect.gameObject.AddComponent<Button>();recovery.targetGraphic=face;recovery.onClick.AddListener(Recover);
            var label=Label("Label",buttonRect,Vector2.zero,22,new Vector2(370,65));label.color=new Color32(255,183,88,255);
            recovery.gameObject.SetActive(false);
        }
        TMP_Text Label(string name,Transform parent,Vector2 position,float size,Vector2 dimensions)
        {
            var rect=Rect(name,parent);rect.anchoredPosition=position;rect.sizeDelta=dimensions;
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();if(font)text.font=font;
            text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.color=new Color32(230,228,219,255);text.raycastTarget=false;
            text.gameObject.AddComponent<Shadow>().effectColor=new Color(0,0,0,.85f);return text;
        }
        static RectTransform Rect(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);return rect;}
        void OnDestroy(){if(Active==this)Active=null;}
    }
}
