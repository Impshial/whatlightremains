using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Presentation-only runtime tuning control for every glass pane. A shader-global override
    /// keeps the generated material asset unchanged and also reaches rooms spawned later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlassOpacityControl : MonoBehaviour
    {
        private static readonly int OpacityOverrideId = Shader.PropertyToID("_WLRGlassOpacityOverride");
        private static readonly int OpacityOverrideEnabledId = Shader.PropertyToID("_WLRGlassOpacityOverrideEnabled");

        [SerializeField] private Slider slider;
        [SerializeField] private Text valueLabel;
        [SerializeField, Range(0f, 1f)] private float minimumOpacity;
        [SerializeField, Range(0f, 1f)] private float maximumOpacity = 1f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.035f;

        private static GlassOpacityControl activeControl;

        public float MinimumOpacity => minimumOpacity;
        public float MaximumOpacity => maximumOpacity;
        public float Opacity => opacity;

        public static bool IsPointerOverControl()
        {
            return activeControl != null
                && Mouse.current != null
                && activeControl.ContainsScreenPoint(Mouse.current.position.ReadValue());
        }

        public void Configure(
            Slider opacitySlider,
            Text opacityValueLabel,
            float minimum,
            float maximum,
            float initialOpacity)
        {
            slider = opacitySlider;
            valueLabel = opacityValueLabel;
            minimumOpacity = Mathf.Clamp01(Mathf.Min(minimum, maximum));
            maximumOpacity = Mathf.Clamp(Mathf.Max(minimum, maximum), minimumOpacity, 1f);
            opacity = Mathf.Clamp(initialOpacity, minimumOpacity, maximumOpacity);
            BindSlider();
            ApplyPresentation();
        }

        public void SetOpacity(float newOpacity)
        {
            opacity = Mathf.Clamp(newOpacity, minimumOpacity, maximumOpacity);
            ApplyPresentation();
        }

        private void Awake()
        {
            BindSlider();
            ApplyPresentation();
        }

        private void OnEnable()
        {
            activeControl = this;
            BindSlider();
            ApplyPresentation();
        }

        private void OnDisable()
        {
            if (activeControl == this)
            {
                activeControl = null;
            }

            if (Application.isPlaying)
            {
                Shader.SetGlobalFloat(OpacityOverrideEnabledId, 0f);
            }
        }

        private void Update()
        {
            if (slider == null
                || Mouse.current == null
                || Cursor.lockState != CursorLockMode.None
                || !Mouse.current.leftButton.isPressed)
            {
                return;
            }

            RectTransform sliderRect = slider.transform as RectTransform;
            Vector2 pointerPosition = Mouse.current.position.ReadValue();
            if (sliderRect == null || !TryGetSliderLocalPoint(pointerPosition, out Vector2 localPoint))
            {
                return;
            }

            float normalized = Mathf.InverseLerp(
                sliderRect.rect.xMin,
                sliderRect.rect.xMax,
                localPoint.x);
            SetOpacity(Mathf.Lerp(minimumOpacity, maximumOpacity, normalized));
        }

        private void OnValidate()
        {
            minimumOpacity = Mathf.Clamp01(minimumOpacity);
            maximumOpacity = Mathf.Clamp(maximumOpacity, minimumOpacity, 1f);
            opacity = Mathf.Clamp(opacity, minimumOpacity, maximumOpacity);
            BindSlider();
            ApplyPresentation();
        }

        private void BindSlider()
        {
            if (slider == null)
            {
                return;
            }

            slider.onValueChanged.RemoveListener(HandleSliderChanged);
            slider.minValue = minimumOpacity;
            slider.maxValue = maximumOpacity;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(opacity);
            slider.onValueChanged.AddListener(HandleSliderChanged);
        }

        private void HandleSliderChanged(float value)
        {
            SetOpacity(value);
        }

        private bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return TryGetSliderLocalPoint(screenPoint, out _);
        }

        private bool TryGetSliderLocalPoint(Vector2 screenPoint, out Vector2 localPoint)
        {
            RectTransform sliderRect = slider != null ? slider.transform as RectTransform : null;
            localPoint = Vector2.zero;
            if (sliderRect == null)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    sliderRect,
                    screenPoint,
                    null,
                    out localPoint)
                && sliderRect.rect.Contains(localPoint);
        }

        private void ApplyPresentation()
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(opacity);
            }

            if (valueLabel != null)
            {
                valueLabel.text = $"Glass opacity  {opacity * 100f:0.0}%   (Esc, then drag)";
            }

            if (Application.isPlaying)
            {
                Shader.SetGlobalFloat(OpacityOverrideId, opacity);
                Shader.SetGlobalFloat(OpacityOverrideEnabledId, 1f);
            }
        }
    }
}
