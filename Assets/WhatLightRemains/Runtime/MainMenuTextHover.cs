using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MainMenuTextHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Text label;
        [SerializeField] private Color normalColor = new(0.90f, 0.87f, 0.78f, 0.82f);
        [SerializeField] private Color hoverColor = new(0.98f, 0.96f, 0.90f, 1f);
        [SerializeField, Min(1f)] private float hoverScale = 1.025f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.12f;

        private bool highlighted;
        private float blend;

        public void Configure(Text text, Color restingColor, Color highlightedColor,
            float scale, float duration)
        {
            label = text;
            normalColor = restingColor;
            hoverColor = highlightedColor;
            hoverScale = Mathf.Max(1f, scale);
            transitionDuration = Mathf.Max(0.01f, duration);
            ApplyVisuals();
        }

        public void OnPointerEnter(PointerEventData eventData) => highlighted = true;
        public void OnPointerExit(PointerEventData eventData) => highlighted = false;
        public void OnSelect(BaseEventData eventData) => highlighted = true;
        public void OnDeselect(BaseEventData eventData) => highlighted = false;

        private void Awake()
        {
            label ??= GetComponent<Text>();
            ApplyVisuals();
        }

        private void Update()
        {
            float target = highlighted ? 1f : 0f;
            blend = Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / transitionDuration);
            ApplyVisuals();
        }

        private void OnDisable()
        {
            highlighted = false;
            blend = 0f;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (label == null) return;
            label.color = Color.Lerp(normalColor, hoverColor, blend);
            label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, hoverScale, blend);
        }
    }
}
