using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WhatLightRemains.Runtime
{
    public enum RoomLightShadowResolutionTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    [DisallowMultipleComponent]
    public sealed class CubeRoomLighting : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer[] stripRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] doorwayFrameRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Light[] supportingLights = System.Array.Empty<Light>();
        [SerializeField] private Light[] doorwayFrameLights = System.Array.Empty<Light>();
        [SerializeField] private Color stripColor = Color.white;
        [SerializeField, Min(0.001f)] private float stripWidth = 0.07f;
        [SerializeField, Min(0f)] private float emissionIntensity = 6f;
        [SerializeField, Range(0f, 1f)] private float doorwayFramePowerRatio = 0.5f;
        [SerializeField, Min(0f)] private float supportingLightIntensity = 0.85f;
        [SerializeField, Min(0.01f)] private float supportingLightRange = 12f;
        [SerializeField, Range(1f, 179f)] private float supportingLightSpotAngle = 170f;
        [SerializeField, Range(0f, 179f)] private float supportingLightInnerSpotAngle = 160f;
        [SerializeField] private LightShadows supportingLightShadows = LightShadows.None;
        [SerializeField] private RoomLightShadowResolutionTier supportingLightShadowResolution = RoomLightShadowResolutionTier.Low;
        [SerializeField] private bool isLightingEnabled = true;
        [SerializeField, HideInInspector] private bool roomPowerOn = true;

        private MaterialPropertyBlock propertyBlock;

        public Color StripColor
        {
            get => stripColor;
            set
            {
                stripColor = value;
                ApplySettings();
            }
        }

        public float StripWidth
        {
            get => stripWidth;
            set
            {
                stripWidth = Mathf.Max(0.001f, value);
                ApplySettings();
            }
        }

        public float EmissionIntensity
        {
            get => emissionIntensity;
            set
            {
                emissionIntensity = Mathf.Max(0f, value);
                ApplySettings();
            }
        }

        public float SupportingLightIntensity
        {
            get => supportingLightIntensity;
            set
            {
                supportingLightIntensity = Mathf.Max(0f, value);
                ApplySettings();
            }
        }

        public float DoorwayFrameEmissionIntensity
        {
            get => emissionIntensity * doorwayFramePowerRatio;
            set
            {
                doorwayFramePowerRatio = emissionIntensity > 0.0001f
                    ? Mathf.Clamp01(value / emissionIntensity)
                    : 0f;
                ApplySettings();
            }
        }

        public float DoorwayFramePowerRatio
        {
            get => doorwayFramePowerRatio;
            set
            {
                doorwayFramePowerRatio = Mathf.Clamp01(value);
                ApplySettings();
            }
        }

        public float SupportingLightRange
        {
            get => supportingLightRange;
            set
            {
                supportingLightRange = Mathf.Max(0.01f, value);
                ApplySettings();
            }
        }

        public float SupportingLightSpotAngle
        {
            get => supportingLightSpotAngle;
            set
            {
                supportingLightSpotAngle = Mathf.Clamp(value, 1f, 179f);
                supportingLightInnerSpotAngle = Mathf.Min(supportingLightInnerSpotAngle, supportingLightSpotAngle);
                ApplySettings();
            }
        }

        public float SupportingLightInnerSpotAngle
        {
            get => supportingLightInnerSpotAngle;
            set
            {
                supportingLightInnerSpotAngle = Mathf.Clamp(value, 0f, supportingLightSpotAngle);
                ApplySettings();
            }
        }

        public LightShadows SupportingLightShadows
        {
            get => supportingLightShadows;
            set
            {
                supportingLightShadows = value;
                ApplySettings();
            }
        }

        public RoomLightShadowResolutionTier SupportingLightShadowResolution
        {
            get => supportingLightShadowResolution;
            set
            {
                supportingLightShadowResolution = value;
                ApplySettings();
            }
        }

        public bool IsLightingEnabled => isLightingEnabled;
        public bool IsEffectivelyLit => isLightingEnabled && roomPowerOn;

        public void Configure(Renderer[] strips, Light[] lights)
        {
            Configure(strips, lights, null, null);
        }

        public void Configure(Renderer[] strips, Light[] lights, Renderer[] doorwayFrames)
        {
            Configure(strips, lights, doorwayFrames, null);
        }

        public void Configure(Renderer[] strips, Light[] lights, Renderer[] doorwayFrames, Light[] doorwayLights)
        {
            stripRenderers = strips ?? System.Array.Empty<Renderer>();
            supportingLights = lights ?? System.Array.Empty<Light>();
            doorwayFrameRenderers = doorwayFrames ?? System.Array.Empty<Renderer>();
            doorwayFrameLights = doorwayLights ?? System.Array.Empty<Light>();
            ApplySettings();
        }

        public void ApplySettings()
        {
            stripRenderers ??= System.Array.Empty<Renderer>();
            doorwayFrameRenderers ??= System.Array.Empty<Renderer>();
            supportingLights ??= System.Array.Empty<Light>();
            doorwayFrameLights ??= System.Array.Empty<Light>();
            propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < stripRenderers.Length; i++)
            {
                Renderer strip = stripRenderers[i];
                if (strip == null)
                {
                    continue;
                }

                ApplyStripWidth(strip.transform);
                ApplyEmission(strip, IsEffectivelyLit ? emissionIntensity : 0f);
                strip.enabled = true;
            }

            for (int i = 0; i < doorwayFrameRenderers.Length; i++)
            {
                Renderer frame = doorwayFrameRenderers[i];
                if (frame != null)
                {
                    // Wall-boundary state owns visibility. Lighting only changes luminance so
                    // disabling a room does not accidentally close or reveal a doorway.
                    ApplyEmission(frame, IsEffectivelyLit ? emissionIntensity * doorwayFramePowerRatio : 0f);
                }
            }

            for (int i = 0; i < supportingLights.Length; i++)
            {
                Light supportingLight = supportingLights[i];
                if (supportingLight == null)
                {
                    continue;
                }

                ApplyLightSettings(supportingLight, supportingLightIntensity);
                supportingLight.enabled = IsEffectivelyLit;
            }


            for (int i = 0; i < doorwayFrameLights.Length; i++)
            {
                Light doorwayLight = doorwayFrameLights[i];
                if (doorwayLight == null) continue;
                ApplyLightSettings(doorwayLight, supportingLightIntensity * doorwayFramePowerRatio);
                doorwayLight.enabled = IsEffectivelyLit && DoorwayLightOwnsActivePassage(doorwayLight);
            }
        }

        public void SetLightingEnabled(bool enabled)
        {
            isLightingEnabled = enabled;
            ApplySettings();
        }

        public void SetPowerState(bool on)
        {
            roomPowerOn = on;
            ApplySettings();
        }

        private void Awake()
        {
            ApplySettings();
        }

        private void OnValidate()
        {
            stripWidth = Mathf.Max(0.001f, stripWidth);
            emissionIntensity = Mathf.Max(0f, emissionIntensity);
            doorwayFramePowerRatio = Mathf.Clamp01(doorwayFramePowerRatio);
            supportingLightIntensity = Mathf.Max(0f, supportingLightIntensity);
            supportingLightRange = Mathf.Max(0.01f, supportingLightRange);
            supportingLightSpotAngle = Mathf.Clamp(supportingLightSpotAngle, 1f, 179f);
            supportingLightInnerSpotAngle = Mathf.Clamp(
                supportingLightInnerSpotAngle,
                0f,
                supportingLightSpotAngle);
            ApplySettings();
        }

        private void ApplyLightSettings(Light light, float intensity)
        {
            light.color = stripColor;
            light.intensity = Mathf.Max(0f, intensity);
            light.range = supportingLightRange;
            light.shadows = supportingLightShadows;
            if (light.type == LightType.Spot)
            {
                light.spotAngle = supportingLightSpotAngle;
                light.innerSpotAngle = Mathf.Min(supportingLightInnerSpotAngle, supportingLightSpotAngle);
            }
            if (Application.isPlaying
                && light.TryGetComponent(out UniversalAdditionalLightData additionalData))
            {
                additionalData.additionalLightsShadowResolutionTier =
                    (int)supportingLightShadowResolution;
            }
        }

        private static bool DoorwayLightOwnsActivePassage(Light doorwayLight)
        {
            CubeRoomWallBoundary boundary = doorwayLight.GetComponentInParent<CubeRoomWallBoundary>();
            if (boundary != null) return boundary.HasDoorway && boundary.OwnsBoundary && !boundary.UsesExternalGeometry;
            CubeRoomCeilingBoundary ceiling = doorwayLight.GetComponentInParent<CubeRoomCeilingBoundary>();
            return ceiling != null && ceiling.HasPassage && ceiling.OwnsBoundary && !ceiling.UsesExternalGeometry;
        }

        private void ApplyEmission(Renderer renderer, float intensity)
        {
            Color visibleColor = new Color(stripColor.r, stripColor.g, stripColor.b, 1f);
            Color emissionColor = visibleColor * Mathf.Max(0f, intensity);
            emissionColor.a = 1f;
            renderer.GetPropertyBlock(propertyBlock);
            // Set URP and legacy color properties so instance-specific emission remains robust
            // if the generated core material changes shader later.
            propertyBlock.SetColor(BaseColorId, emissionColor);
            propertyBlock.SetColor(ColorId, emissionColor);
            propertyBlock.SetColor(EmissionColorId, emissionColor);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyStripWidth(Transform stripTransform)
        {
            Vector3 scale = stripTransform.localScale;
            Vector3 absoluteScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            if (absoluteScale.x >= absoluteScale.y && absoluteScale.x >= absoluteScale.z)
            {
                scale.y = SignedWidth(scale.y);
                scale.z = SignedWidth(scale.z);
            }
            else if (absoluteScale.y >= absoluteScale.z)
            {
                scale.x = SignedWidth(scale.x);
                scale.z = SignedWidth(scale.z);
            }
            else
            {
                scale.x = SignedWidth(scale.x);
                scale.y = SignedWidth(scale.y);
            }

            stripTransform.localScale = scale;
        }

        private float SignedWidth(float currentValue)
        {
            return currentValue < 0f ? -stripWidth : stripWidth;
        }
    }
}
