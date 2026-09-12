using UnityEngine;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    /// <summary>Passive circular gauge. Presentation holds no independently mutable stat.</summary>
    [DisallowMultipleComponent]
    public sealed class VitalGaugeView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;

        public Image FillImage => fillImage;

        public void Configure(Image fill)
        {
            fillImage = fill;
        }

        public void ShowValue(float current, float maximum)
        {
            if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(current / maximum);
        }
    }
}
