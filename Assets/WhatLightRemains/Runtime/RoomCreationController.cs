using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class RoomCreationController : MonoBehaviour
    {
        [SerializeField] private FirstPersonInput input;
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CubeRoomClusterGenerator roomLayout;
        [SerializeField] private RoomCreationPromptView promptView;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private PlayerRoomTraversal roomTraversal;
        [SerializeField] private RoomDeletionController deletionController;

        private RoomGhostPreview preview;
        private RoomPlacementCandidate candidate;
        private CubeRoom orientationSourceRoom;
        private RoomOrientation previewOrientation = RoomOrientation.Identity;

        public bool IsCreateMode { get; private set; }
        public bool HasValidPreview => candidate.IsValid && preview != null && preview.IsVisible;
        public RoomPlacementCandidate Candidate => candidate;
        public GameObject PreviewObject => preview?.Root;
        public RoomOrientation PreviewOrientation => previewOrientation;

        public void Configure(FirstPersonInput inputSource, PlayerRoomTracker tracker, PlayerLook look, Camera camera, Material material)
        {
            input = inputSource;
            roomTracker = tracker;
            playerLook = look;
            gameplayCamera = camera;
            previewMaterial = material;
        }

        public void Initialize(CubeRoomClusterGenerator layout, RoomCreationPromptView prompt)
        {
            roomLayout = layout;
            promptView = prompt;
            promptView?.SetCreateMode(IsCreateMode);
        }

        public bool EnterCreateMode()
        {
            ResolveDependencies();
            if (!GameplayInputIsCaptured() || roomLayout == null || previewMaterial == null
                || (roomTraversal != null && roomTraversal.IsOwningMovement)
                || (deletionController != null && deletionController.IsDeleteMode))
            {
                return false;
            }

            IsCreateMode = true;
            orientationSourceRoom = null;
            previewOrientation = RoomOrientation.Identity;
            input?.ClearRotationScroll();
            ClearCandidate();
            promptView?.SetCreateMode(true);
            return true;
        }

        public void CancelCreateMode()
        {
            IsCreateMode = false;
            candidate = default;
            orientationSourceRoom = null;
            previewOrientation = RoomOrientation.Identity;
            input?.ClearRotationScroll();
            DisposePreview();
            promptView?.SetCreateMode(false);
        }

        public bool RefreshTarget(Ray viewRay)
        {
            if (!IsCreateMode || !GameplayInputIsCaptured())
            {
                ClearCandidate();
                return false;
            }

            CubeRoom currentRoom = roomTracker != null ? roomTracker.CurrentRoom : null;
            if (currentRoom == null
                || roomLayout == null
                || !roomLayout.IsRegistered(currentRoom)
                || !RoomPlacementTargeting.TryGetFirstBoundary(currentRoom, viewRay, out RoomBoundaryHit hit)
                || !TryConvertTargetFace(hit.Boundary, out CubeRoomFace face))
            {
                ClearCandidate();
                return false;
            }

            EnsureOrientationForRoom(currentRoom);
            if (!roomLayout.TryGetPlacementCandidate(currentRoom, face, previewOrientation, out RoomPlacementCandidate proposed))
            {
                ClearCandidate();
                return false;
            }

            EnsurePreview();
            if (preview == null)
            {
                ClearCandidate();
                return false;
            }

            candidate = proposed;
            preview.Show(candidate);
            UpdateRotationPrompt();
            return true;
        }

        public bool TryFinalizePlacement(Ray viewRay)
        {
            if (!RefreshTarget(viewRay) || !candidate.IsValid)
            {
                return false;
            }

            if (!roomLayout.TryPlaceRoom(candidate, out _))
            {
                ClearCandidate();
                return false;
            }

            CancelCreateMode();
            return true;
        }

        private void Awake()
        {
            ResolveDependencies();
            promptView?.SetCreateMode(false);
        }

        private void Update()
        {
            if (input == null) return;

            if (input.ReleaseCursorPressedThisFrame && IsCreateMode)
            {
                CancelCreateMode();
                return;
            }

            if (input.ToggleCreatePressedThisFrame)
            {
                if (roomTraversal != null && roomTraversal.IsOwningMovement) return;
                if (deletionController != null && deletionController.IsDeleteMode) return;
                if (IsCreateMode) CancelCreateMode();
                else EnterCreateMode();
                return;
            }

            if (!IsCreateMode) return;
            if (!GameplayInputIsCaptured())
            {
                CancelCreateMode();
                return;
            }

            ApplyRotationInput();
            Ray viewRay = GetCenterViewRay();
            RefreshTarget(viewRay);
            if (input.PlaceRoomPressedThisFrame && !GlassOpacityControl.IsPointerOverControl())
            {
                TryFinalizePlacement(viewRay);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelCreateMode();
        }

        private void OnDisable()
        {
            CancelCreateMode();
        }

        private void OnDestroy()
        {
            DisposePreview();
        }

        private Ray GetCenterViewRay()
        {
            if (gameplayCamera != null)
            {
                return new Ray(gameplayCamera.transform.position, gameplayCamera.transform.forward);
            }

            return new Ray(transform.position, transform.forward);
        }

        private bool GameplayInputIsCaptured()
        {
            return playerLook != null
                && playerLook.IsCursorCaptured;
        }

        private void ResolveDependencies()
        {
            input ??= GetComponent<FirstPersonInput>();
            roomTracker ??= GetComponent<PlayerRoomTracker>();
            playerLook ??= GetComponent<PlayerLook>();
            gameplayCamera ??= GetComponentInChildren<Camera>();
            roomLayout ??= FindAnyObjectByType<CubeRoomClusterGenerator>();
            promptView ??= FindAnyObjectByType<RoomCreationPromptView>();
            roomTraversal ??= GetComponent<PlayerRoomTraversal>();
            deletionController ??= GetComponent<RoomDeletionController>();
        }

        private void EnsurePreview()
        {
            if (preview == null && previewMaterial != null)
            {
                preview = RoomGhostPreview.Create(previewMaterial, roomLayout != null ? roomLayout.RoomPrefab : null);
            }
        }

        private void ApplyRotationInput()
        {
            if (input == null || !input.RotateModifierHeld)
            {
                input?.ClearRotationScroll();
                return;
            }

            CubeRoom currentRoom = roomTracker != null ? roomTracker.CurrentRoom : null;
            if (currentRoom == null || roomLayout == null || roomLayout.PrimaryRoom == null) return;
            EnsureOrientationForRoom(currentRoom);
            int steps = input.ConsumeRotationScrollSteps();
            if (steps == 0) return;

            RoomOrientation sourceOrientation = roomLayout.TryGetOrientation(currentRoom, out RoomOrientation registered)
                ? registered
                : RoomOrientation.FromRotation(currentRoom.transform.rotation, roomLayout.PrimaryRoom.transform.rotation);
            Vector3Int sourceLocalAxis = input.AlternateRotationAxisHeld
                ? new Vector3Int(0, 0, 1)
                : Vector3Int.up;
            Vector3Int gridAxis = sourceOrientation.TransformDirection(sourceLocalAxis);
            previewOrientation = previewOrientation.RotateAroundGridAxis(gridAxis, steps);
            ClearCandidate();
        }

        private void EnsureOrientationForRoom(CubeRoom currentRoom)
        {
            if (currentRoom == orientationSourceRoom) return;
            orientationSourceRoom = currentRoom;
            input?.ClearRotationScroll();
            previewOrientation = roomLayout != null && roomLayout.TryGetOrientation(currentRoom, out RoomOrientation registered)
                ? registered
                : roomLayout != null && roomLayout.PrimaryRoom != null
                    ? RoomOrientation.FromRotation(currentRoom.transform.rotation, roomLayout.PrimaryRoom.transform.rotation)
                    : RoomOrientation.Identity;
        }

        private void UpdateRotationPrompt()
        {
            promptView?.SetRotationState(
                IsCreateMode,
                HasValidPreview,
                input != null && input.RotateModifierHeld,
                input != null && input.RotateModifierHeld && input.AlternateRotationAxisHeld);
        }

        private static bool TryConvertTargetFace(RoomBoundaryKind boundary, out CubeRoomFace face)
        {
            switch (boundary)
            {
                case RoomBoundaryKind.West: face = CubeRoomFace.West; return true;
                case RoomBoundaryKind.East: face = CubeRoomFace.East; return true;
                case RoomBoundaryKind.South: face = CubeRoomFace.South; return true;
                case RoomBoundaryKind.North: face = CubeRoomFace.North; return true;
                case RoomBoundaryKind.Ceiling: face = CubeRoomFace.Ceiling; return true;
                default:
                    face = default;
                    return false;
            }
        }

        private void ClearCandidate()
        {
            candidate = default;
            preview?.Hide();
            UpdateRotationPrompt();
        }

        private void DisposePreview()
        {
            preview?.Dispose();
            preview = null;
        }
    }
}
