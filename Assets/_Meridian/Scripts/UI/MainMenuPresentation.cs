using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Meridian.Colony;

namespace Meridian
{
    /// <summary>Menu focus and the entry point into a fresh planet visit.</summary>
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        [SerializeField] private Button firstSelection;
        [SerializeField] private ScreenTransition transitionPrefab;
        private ColonyUI colonyUI;

        public void Configure(Button button) => firstSelection = button;
        public void ConfigureTransition(ScreenTransition transition) => transitionPrefab = transition;
        public void OpenPlanetSelection()
        {
            if(ScreenTransition.Active)return;
            SetupSession.BeginNew();
            ScreenTransition.Travel(transitionPrefab,"PlanetSelection",ScreenTransition.PlanetLoadingMessage);
        }
        public void ContinueColony(){if(!ScreenTransition.Active)colonyUI.ContinueLatest();}
        public void LoadColony(){if(!ScreenTransition.Active)colonyUI.Open("Load");}
        public void OpenSettings(){if(!ScreenTransition.Active)colonyUI.Open("Settings");}
        public void QuitGame()
        {
            if(ScreenTransition.Active)return;
            colonyUI.Confirm("Quit Meridian?","Your saved colonies will remain available next time.",()=>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            });
        }

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            colonyUI=gameObject.AddComponent<ColonyUI>();colonyUI.Initialize(null,transitionPrefab);
            foreach(var button in GetComponentsInChildren<Button>())
                if(button.name=="CONTINUE")button.interactable=colonyUI.HasSave;
            AudioListener.volume=ColonySettings.Current.master;
            if (firstSelection != null)
                EventSystem.current?.SetSelectedGameObject(firstSelection.gameObject);
        }
        private void Update()
        {
            if(!colonyUI)return;bool available=!colonyUI.HasOpenWindows&&!colonyUI.Modal&&!ScreenTransition.Active;
            foreach(var button in GetComponentsInChildren<Button>())button.interactable=available&&(button.name!="CONTINUE"||colonyUI.HasSave);
            if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true){if(!colonyUI.DismissModal()&&colonyUI.HasOpenWindows)colonyUI.Escape();}
        }
    }
}
