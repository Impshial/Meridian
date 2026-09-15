using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Meridian
{
    public sealed class ScreenTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        [SerializeField,Range(.1f,1f)] private float duration=.5f;
        public static ScreenTransition Active {get;private set;}
        public void Configure(CanvasGroup group)=>overlay=group;
        void Awake(){Active=this;DontDestroyOnLoad(gameObject);}
        public static void Travel(ScreenTransition prefab,string destination)
        {
            if(Active)return;
            var runner=Instantiate(prefab);runner.StartCoroutine(runner.ChangeScene(destination));
        }
        public static void Reveal(ScreenTransition prefab)
        {
            if(Active)return;
            var runner=Instantiate(prefab);runner.overlay.alpha=1;runner.StartCoroutine(runner.FinishEntry());
        }
        IEnumerator ChangeScene(string destination)
        {
            var current=FindAnyObjectByType<PlanetSelectionController>();if(current)current.SetInteraction(false);
            yield return Fade(0,1);
            var operation=SceneManager.LoadSceneAsync(destination,LoadSceneMode.Single);
            while(!operation.isDone)yield return null;
            yield return FinishEntry();
        }
        IEnumerator FinishEntry()
        {
            // Allow the loaded scene's Start to begin generation before inspecting readiness.
            yield return null;
            var planet=FindAnyObjectByType<PlanetSelectionController>();
            if(planet)
            {
                while(planet && !planet.IsReady && !planet.GenerationFailed)yield return null;
                if(planet && planet.GenerationFailed)
                {
                    var load=SceneManager.LoadSceneAsync("MainMenu");while(!load.isDone)yield return null;planet=null;
                }
            }
            yield return Fade(1,0);
            // Do not carry the menu's submit or a held pointer into a new interaction context.
            while((Mouse.current?.leftButton.isPressed??false) || (Keyboard.current?.enterKey.isPressed??false) ||
                (Keyboard.current?.spaceKey.isPressed??false) || (Gamepad.current?.buttonSouth.isPressed??false))yield return null;
            if(planet)planet.SetInteraction(true);
            Destroy(gameObject);
        }
        IEnumerator Fade(float from,float to)
        {
            overlay.alpha=from;float elapsed=0;
            while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;overlay.alpha=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,elapsed/duration));yield return null;}
            overlay.alpha=to;
        }
        void OnDestroy(){if(Active==this)Active=null;}
    }
}
