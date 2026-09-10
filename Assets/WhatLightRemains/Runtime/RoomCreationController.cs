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

        private RoomGhostPreview preview;
        private RoomPlacementCandidate candidate;

        public bool IsCreateMode { get; private set; }
        public bool HasValidPreview => candidate.IsValid && preview != null && preview.IsVisible;
        public RoomPlacementCandidate Candidate => candidate;
        public GameObject PreviewObject => preview?.Root;

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
            if (!GameplayInputIsCaptured() || roomLayout == null || previewMaterial == null)
            {
                return false;
            }

            IsCreateMode = true;
            ClearCandidate();
            promptView?.SetCreateMode(true);
            return true;
        }

        public void CancelCreateMode()
        {
            IsCreateMode = false;
            candidate = default;
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
                || !RoomPlacementTargeting.TrySelectSideWall(currentRoom, viewRay, out CubeRoomWall wall)
                || currentRoom.HasDoorway(wall)
                || !roomLayout.TryGetPlacementCandidate(currentRoom, wall, out RoomPlacementCandidate proposed))
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
            return true;
        }

        public bool TryFinalizePlacement(Ray viewRay)
        {
            if (!RefreshTarget(viewRay) || !candidate.IsValid)
            {
                return false;
            }

            CubeRoom sourceRoom = candidate.SourceRoom;
            CubeRoomWall sourceWall = candidate.SourceWall;
            if (!roomLayout.TryPlaceRoom(sourceRoom, sourceWall, out _))
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
        }

        private void EnsurePreview()
        {
            if (preview == null && previewMaterial != null)
            {
                preview = RoomGhostPreview.Create(previewMaterial);
            }
        }

        private void ClearCandidate()
        {
            candidate = default;
            preview?.Hide();
        }

        private void DisposePreview()
        {
            preview?.Dispose();
            preview = null;
        }
    }
}
