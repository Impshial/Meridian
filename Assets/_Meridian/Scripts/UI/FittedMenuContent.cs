using UnityEngine;

namespace Meridian
{
    /// <summary>Fits one design surface, including the artwork and controls, inside the viewport.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FittedMenuContent : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Vector2 designSize = new Vector2(1920f, 1080.5742f);

        public void Configure(RectTransform target, Vector2 size)
        {
            content = target;
            designSize = size;
            Fit();
        }

        private void OnEnable() => Fit();
        private void OnRectTransformDimensionsChange() => Fit();
        private void OnValidate() => Fit();

        private void Fit()
        {
            if (content == null || designSize.x <= 0f || designSize.y <= 0f)
                return;

            var viewport = ((RectTransform)transform).rect.size;
            float scale = Mathf.Min(viewport.x / designSize.x, viewport.y / designSize.y);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = designSize;
            content.localScale = Vector3.one * Mathf.Max(0f, scale);
        }
    }
}
