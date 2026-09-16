using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meridian
{
    /// <summary>Menu focus and the entry point into a fresh planet visit.</summary>
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        [SerializeField] private Button firstSelection;
        [SerializeField] private ScreenTransition transitionPrefab;

        public void Configure(Button button) => firstSelection = button;
        public void ConfigureTransition(ScreenTransition transition) => transitionPrefab = transition;
        public void OpenPlanetSelection()
        {
            if(ScreenTransition.Active)return;
            SetupSession.BeginNew();
            ScreenTransition.Travel(transitionPrefab,"PlanetSelection","Loading Exo-planet...");
        }

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (firstSelection != null)
                EventSystem.current?.SetSelectedGameObject(firstSelection.gameObject);
        }
    }
}
