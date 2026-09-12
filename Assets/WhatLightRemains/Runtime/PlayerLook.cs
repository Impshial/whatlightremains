using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private FirstPersonInput input;
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform pitchCamera;
        [SerializeField] private Camera mainCamera;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Range(-89f, 0f)] private float minimumPitch = -85f;
        [SerializeField, Range(0f, 89f)] private float maximumPitch = 85f;
        [SerializeField, Range(30f, 120f)] private float fieldOfView = 75f;
        [SerializeField] private bool captureCursorOnStart = true;

        private float pitch;

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0f, value);
        }

        public float FieldOfView
        {
            get => fieldOfView;
            set
            {
                fieldOfView = Mathf.Clamp(value, 30f, 120f);
                ApplyFieldOfView();
            }
        }

        public float Pitch => pitch;
        public bool IsCursorCaptured { get; private set; }
        public Transform YawRoot => yawRoot;
        public Transform PitchCamera => pitchCamera;

        public void Configure(
            FirstPersonInput inputSource,
            Transform newYawRoot,
            Transform newPitchCamera,
            Camera newMainCamera)
        {
            input = inputSource;
            yawRoot = newYawRoot;
            pitchCamera = newPitchCamera;
            mainCamera = newMainCamera;
            ReadInitialPitch();
            ApplyFieldOfView();
        }

        public void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            IsCursorCaptured = true;
        }

        public void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            IsCursorCaptured = false;
        }

        private void Awake()
        {
            input ??= GetComponent<FirstPersonInput>();
            yawRoot ??= transform;
            if (mainCamera == null && pitchCamera != null)
            {
                mainCamera = pitchCamera.GetComponent<Camera>();
            }

            ReadInitialPitch();
            ApplyFieldOfView();
        }

        private void Start()
        {
            if (captureCursorOnStart)
            {
                CaptureCursor();
            }
        }

        private void Update()
        {
            if (input == null)
            {
                return;
            }

            if (!IsCursorCaptured || Cursor.lockState != CursorLockMode.Locked || yawRoot == null || pitchCamera == null)
            {
                return;
            }

            Vector2 lookDelta = input.Look * mouseSensitivity;
            yawRoot.localRotation *= Quaternion.AngleAxis(lookDelta.x, Vector3.up);
            pitch = Mathf.Clamp(pitch - lookDelta.y, minimumPitch, maximumPitch);
            pitchCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) IsCursorCaptured = false;
            else if (!PauseMenuController.IsPaused && !WorldMapController.IsMapOpen) CaptureCursor();
        }

        private void ReadInitialPitch()
        {
            if (pitchCamera == null)
            {
                pitch = 0f;
                return;
            }

            float initialPitch = pitchCamera.localEulerAngles.x;
            if (initialPitch > 180f)
            {
                initialPitch -= 360f;
            }

            pitch = Mathf.Clamp(initialPitch, minimumPitch, maximumPitch);
        }

        private void ApplyFieldOfView()
        {
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = fieldOfView;
            }
        }

        private void OnValidate()
        {
            mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
            minimumPitch = Mathf.Clamp(minimumPitch, -89f, 0f);
            maximumPitch = Mathf.Clamp(maximumPitch, 0f, 89f);
            fieldOfView = Mathf.Clamp(fieldOfView, 30f, 120f);
            ApplyFieldOfView();
        }
    }
}
