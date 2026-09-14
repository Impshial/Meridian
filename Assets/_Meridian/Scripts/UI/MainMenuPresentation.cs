using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meridian
{
    /// <summary>Initial focus and cursor presentation. No gameplay or menu actions.</summary>
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        [SerializeField] private Button firstSelection;

        public void Configure(Button button) => firstSelection = button;

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (firstSelection != null)
                EventSystem.current?.SetSelectedGameObject(firstSelection.gameObject);
        }
    }
}
