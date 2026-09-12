using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class RoomDeletionController : MonoBehaviour
    {
        [SerializeField] private FirstPersonInput input;
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CubeRoomClusterGenerator roomLayout;
        [SerializeField] private RoomCreationPromptView promptView;
        [SerializeField] private RoomCreationController creationController;
        [SerializeField] private PlayerRoomTraversal roomTraversal;

        private RoomDeletionHighlight highlight;

        public bool IsDeleteMode { get; private set; }
        public CubeRoom FocusedRoom { get; private set; }
        public GameObject HighlightObject => highlight?.Root;

        public void Configure(FirstPersonInput inputSource, PlayerRoomTracker tracker, PlayerLook look,
            Camera camera, RoomCreationController creation, PlayerRoomTraversal traversal)
        {
            input = inputSource;
            roomTracker = tracker;
            playerLook = look;
            gameplayCamera = camera;
            creationController = creation;
            roomTraversal = traversal;
        }

        public void Initialize(CubeRoomClusterGenerator layout, RoomCreationPromptView prompt)
        {
            roomLayout = layout;
            promptView = prompt;
            promptView?.SetDeleteMode(IsDeleteMode);
        }

        public bool EnterDeleteMode()
        {
            ResolveDependencies();
            if (!GameplayInputIsCaptured() || roomLayout == null
                || (roomTraversal != null && roomTraversal.IsOwningMovement))
            {
                return false;
            }

            if (creationController != null && creationController.IsCreateMode)
                creationController.CancelCreateMode();
            roomTraversal?.ClearTargeting();
            EnsureHighlight();
            if (highlight == null) return false;

            IsDeleteMode = true;
            ClearFocus();
            promptView?.SetDeleteMode(true);
            return true;
        }

        public void CancelDeleteMode()
        {
            IsDeleteMode = false;
            ClearFocus();
            promptView?.SetDeleteMode(false);
        }

        public bool RefreshTarget(Ray viewRay)
        {
            if (!IsDeleteMode || !GameplayInputIsCaptured())
            {
                ClearFocus();
                return false;
            }

            CubeRoom currentRoom = roomTracker != null ? roomTracker.CurrentRoom : null;
            if (currentRoom == null || roomLayout == null || !roomLayout.IsRegistered(currentRoom)
                || !RoomPlacementTargeting.TryGetFirstBoundary(currentRoom, viewRay, out RoomBoundaryHit hit)
                || !TryConvertTargetFace(hit.Boundary, out CubeRoomFace face))
            {
                ClearFocus();
                return false;
            }

            CubeRoom target = currentRoom.GetConnectedRoom(face);
            if (target == null || target == currentRoom || !roomLayout.CanDeleteRoom(target))
            {
                ClearFocus();
                return false;
            }

            SetFocus(target);
            return true;
        }

        public bool TryDeleteFocusedRoom()
        {
            CubeRoom target = FocusedRoom;
            if (!IsDeleteMode || target == null || roomLayout == null || !roomLayout.TryDeleteRoom(target))
                return false;

            ClearFocus();
            return true;
        }

        private void Awake()
        {
            ResolveDependencies();
            promptView?.SetDeleteMode(false);
        }

        private void Update()
        {
            if (input == null) return;
            if (WorldMapController.IsMapOpen)
            {
                if (IsDeleteMode) CancelDeleteMode();
                return;
            }

            if (input.EscapePressedThisFrame && IsDeleteMode)
            {
                CancelDeleteMode();
                return;
            }

            if (input.ToggleDeletePressedThisFrame)
            {
                if (!IsDeleteMode) EnterDeleteMode();
                return;
            }

            if (!IsDeleteMode) return;
            if (!GameplayInputIsCaptured() || (roomTraversal != null && roomTraversal.IsOwningMovement))
            {
                CancelDeleteMode();
                return;
            }

            RefreshTarget(GetCenterViewRay());
            if (input.DeleteRoomPressedThisFrame)
                TryDeleteFocusedRoom();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelDeleteMode();
        }

        private void OnDisable() => CancelDeleteMode();

        private void OnDestroy()
        {
            highlight?.Dispose();
            highlight = null;
        }

        private Ray GetCenterViewRay()
        {
            return gameplayCamera != null
                ? new Ray(gameplayCamera.transform.position, gameplayCamera.transform.forward)
                : new Ray(transform.position, transform.forward);
        }

        private bool GameplayInputIsCaptured()
        {
            return playerLook != null && playerLook.IsCursorCaptured;
        }

        private void ResolveDependencies()
        {
            input ??= GetComponent<FirstPersonInput>();
            roomTracker ??= GetComponent<PlayerRoomTracker>();
            playerLook ??= GetComponent<PlayerLook>();
            gameplayCamera ??= GetComponentInChildren<Camera>();
            roomLayout ??= FindAnyObjectByType<CubeRoomClusterGenerator>();
            promptView ??= FindAnyObjectByType<RoomCreationPromptView>();
            creationController ??= GetComponent<RoomCreationController>();
            roomTraversal ??= GetComponent<PlayerRoomTraversal>();
        }

        private void EnsureHighlight()
        {
            if (highlight == null)
                highlight = RoomDeletionHighlight.Create();
        }

        private void SetFocus(CubeRoom room)
        {
            if (FocusedRoom == room)
            {
                promptView?.SetDeleteTarget(room != null);
                return;
            }

            FocusedRoom = room;
            if (room == null)
            {
                highlight?.Hide();
                promptView?.SetDeleteTarget(false);
                return;
            }

            EnsureHighlight();
            highlight?.Show(room);
            promptView?.SetDeleteTarget(true);
        }

        private void ClearFocus() => SetFocus(null);

        private static bool TryConvertTargetFace(RoomBoundaryKind boundary, out CubeRoomFace face)
        {
            switch (boundary)
            {
                case RoomBoundaryKind.West: face = CubeRoomFace.West; return true;
                case RoomBoundaryKind.East: face = CubeRoomFace.East; return true;
                case RoomBoundaryKind.South: face = CubeRoomFace.South; return true;
                case RoomBoundaryKind.North: face = CubeRoomFace.North; return true;
                case RoomBoundaryKind.Floor: face = CubeRoomFace.Floor; return true;
                case RoomBoundaryKind.Ceiling: face = CubeRoomFace.Ceiling; return true;
                default: face = default; return false;
            }
        }
    }
}
