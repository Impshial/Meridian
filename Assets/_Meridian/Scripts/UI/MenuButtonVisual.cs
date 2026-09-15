using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Meridian
{
    /// <summary>Shared pointer/keyboard presentation, including a noninteractive disabled state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class MenuButtonVisual : MonoBehaviour, IPointerEnterHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup accent;
        [SerializeField] private Color normalColor = new Color32(225, 224, 219, 255);
        [SerializeField] private Color selectedColor = new Color32(255, 183, 88, 255);
        [SerializeField] private Color pressedColor = new Color32(218, 143, 55, 255);
        [SerializeField] private Color disabledColor = new Color32(93, 98, 99, 255);
        private bool selected;
        private bool pressed;
        private Coroutine submitFeedback;
        private bool wasInteractable;

        private void OnEnable() { wasInteractable = GetComponent<Button>().IsInteractable(); Refresh(); }
        private void Update()
        {
            bool value = GetComponent<Button>().IsInteractable();
            if (value != wasInteractable) { wasInteractable = value; if (!value) selected = pressed = false; Refresh(); }
        }
        private void OnDisable()
        {
            if (submitFeedback != null) StopCoroutine(submitFeedback);
            submitFeedback = null; selected = pressed = false;
        }

        public void Configure(TMP_Text text, CanvasGroup decoration, bool initiallySelected)
        {
            label = text;
            accent = decoration;
            selected = initiallySelected;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (GetComponent<Button>().IsInteractable())
                EventSystem.current?.SetSelectedGameObject(gameObject, eventData);
        }

        public void OnSelect(BaseEventData eventData) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { selected = pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !GetComponent<Button>().IsInteractable()) return;
            pressed = true;
            Refresh();
        }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!GetComponent<Button>().IsInteractable()) return;
            if (submitFeedback != null) StopCoroutine(submitFeedback);
            submitFeedback = StartCoroutine(ShowPress());
        }

        private IEnumerator ShowPress()
        {
            pressed = true;
            Refresh();
            yield return new WaitForSecondsRealtime(0.1f);
            pressed = false;
            Refresh();
            submitFeedback = null;
        }

        private void Refresh()
        {
            if (!GetComponent<Button>().IsInteractable())
            {
                if (label != null) label.color = disabledColor;
                if (accent != null) accent.alpha = 0;
                return;
            }
            if (label != null) label.color = pressed ? pressedColor : selected ? selectedColor : normalColor;
            if (accent != null) accent.alpha = selected ? (pressed ? 0.7f : 1f) : 0f;
        }
    }
}
