using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Presents the live room geometry through a dedicated orbit camera. The map pauses gameplay,
    /// owns the cursor while visible, and never duplicates or simplifies authored room details.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-300)]
    public sealed class WorldMapController : MonoBehaviour
    {
        private enum OrbitAxis
        {
            None,
            Horizontal,
            Vertical,
        }

        [SerializeField] private FirstPersonInput input;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private RoomCreationController creationController;
        [SerializeField] private RoomDeletionController deletionController;
        [SerializeField] private PlayerRoomTraversal traversal;
        [SerializeField] private CubeRoomClusterGenerator roomLayout;
        [SerializeField] private GameObject mapRoot;
        [SerializeField] private GameObject mapOverlayRoot;
        [SerializeField] private Camera mapCamera;
        [SerializeField] private Transform playerMarker;
        [SerializeField] private RoomCreationPromptView promptView;
        [SerializeField] private GameObject gameplayReticle;
        [SerializeField] private GameObject[] gameplayHudElements;
        [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.16f;
        [SerializeField, Min(0.0001f)] private float panSensitivity = 0.0015f;
        [SerializeField, Range(0.01f, 0.5f)] private float zoomFractionPerStep = 0.14f;
        [SerializeField, Min(1f)] private float minimumDistance = 6f;
        [SerializeField, Min(5f)] private float maximumDistance = 300f;
        [SerializeField, Min(1f)] private float initialPlayerDistance = 20f;
        [SerializeField, Min(0.01f)] private float centerTransitionDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float levelTransitionDuration = 0.16f;
        [SerializeField, Min(0f)] private float orbitAxisLockThreshold = 2f;
        [SerializeField, Min(0f)] private float orbitAxisSwitchThreshold = 4f;
        [SerializeField, Range(1f, 2f)] private float orbitAxisSwitchDominance = 1.2f;

        private bool[] hudActiveStates;
        private Vector3 mapUp = Vector3.up;
        private Vector3 orbitOffsetDirection = new Vector3(0f, 0.819152f, -0.573576f);
        private Vector3 orbitCameraUp = new Vector3(0f, 0.573576f, 0.819152f);
        private Vector3 openingFocusPoint;
        private Vector3 openingMapUp;
        private Vector3 openingOrbitOffsetDirection;
        private Vector3 openingOrbitCameraUp;
        private float openingDistance;
        private bool hasOpeningView;
        private bool isCenterTransitioning;
        private Vector3 centerTransitionStart;
        private Vector3 centerTransitionTarget;
        private float centerTransitionElapsed;
        private bool isLevelTransitioning;
        private Vector3 levelTransitionStartUp;
        private Vector3 levelTransitionTargetUp;
        private float levelTransitionElapsed;
        private OrbitAxis orbitAxis;
        private Vector2 orbitGestureTravel;
        private float orbitAxisSwitchTravel;
        private static int escapeCloseFrame = -1;

        public static bool IsMapOpen { get; private set; }
        public static bool ClosedByEscapeThisFrame => escapeCloseFrame == Time.frameCount;
        public bool IsOpen => IsMapOpen && mapRoot != null && mapRoot.activeSelf;
        public Camera MapCamera => mapCamera;
        public GameObject MapRoot => mapRoot;
        public GameObject MapOverlayRoot => mapOverlayRoot;
        public Transform PlayerMarker => playerMarker;
        public GameObject GameplayReticle => gameplayReticle;
        public Vector3 FocusPoint { get; private set; }
        public Vector3 ClusterCenter { get; private set; }
        public Vector3 PrimaryRoomCenter { get; private set; }
        public float Distance { get; private set; }
        public bool IsCenterTransitioning => isCenterTransitioning;
        public bool IsLevelTransitioning => isLevelTransitioning;
        public CubeRoom LevelReferenceRoom { get; private set; }

        public void Configure(GameObject worldRoot, GameObject overlayRoot, Camera camera,
            Transform marker, GameObject[] hudElements, GameObject gameplayReticleObject = null)
        {
            mapRoot = worldRoot;
            mapOverlayRoot = overlayRoot;
            mapCamera = camera;
            playerMarker = marker;
            gameplayHudElements = hudElements;
            gameplayReticle = gameplayReticleObject;
            SetMapPresentation(false);
        }

        public bool OpenMap()
        {
            ResolveDependencies();
            if (IsMapOpen || PauseMenuController.IsPaused || input == null || playerLook == null
                || mapRoot == null || mapOverlayRoot == null || mapCamera == null
                || (traversal != null && traversal.IsOwningMovement)) return false;

            creationController?.CancelCreateMode();
            deletionController?.CancelDeleteMode();
            InitializeViewFromPlayer();
            CaptureOpeningView();
            EnsureReticles();
            CacheAndHideGameplayHud();
            if (gameplayReticle != null) gameplayReticle.SetActive(false);
            promptView?.SetMapMode(true);
            IsMapOpen = true;
            Time.timeScale = 0f;
            playerLook.ReleaseCursor();
            SetMapPresentation(true);
            UpdatePlayerMarker();
            ApplyCameraTransform();
            return true;
        }

        public void CloseMap(bool closedByEscape = false)
        {
            if (!IsMapOpen) return;
            if (closedByEscape) escapeCloseFrame = Time.frameCount;
            SetMapPresentation(false);
            promptView?.SetMapMode(false);
            RestoreGameplayHud();
            if (gameplayReticle != null) gameplayReticle.SetActive(true);
            IsMapOpen = false;
            CancelCenterTransition();
            CancelLevelTransition();
            EndOrbitDrag();
            Time.timeScale = 1f;
            playerLook?.CaptureCursor();
        }

        public void ApplyOrbitDelta(Vector2 pointerDelta)
        {
            if (mapCamera == null || pointerDelta.sqrMagnitude <= Mathf.Epsilon) return;

            if (Mathf.Abs(pointerDelta.x) > Mathf.Epsilon)
            {
                Quaternion yawRotation = Quaternion.AngleAxis(
                    pointerDelta.x * orbitSensitivity, orbitCameraUp);
                orbitOffsetDirection = (yawRotation * orbitOffsetDirection).normalized;
                orbitCameraUp = yawRotation * orbitCameraUp;
            }
            if (Mathf.Abs(pointerDelta.y) > Mathf.Epsilon)
            {
                Vector3 pitchAxis = Vector3.Cross(orbitCameraUp, -orbitOffsetDirection).normalized;
                if (pitchAxis.sqrMagnitude < 0.5f) pitchAxis = mapCamera.transform.right;
                Quaternion pitchRotation = Quaternion.AngleAxis(-pointerDelta.y * orbitSensitivity, pitchAxis);
                orbitOffsetDirection = (pitchRotation * orbitOffsetDirection).normalized;
                orbitCameraUp = pitchRotation * orbitCameraUp;
            }
            NormalizeOrbitBasis();
            ApplyCameraTransform();
        }

        public void ApplyPanDelta(Vector2 pointerDelta)
        {
            if (mapCamera == null) return;
            float scale = Mathf.Max(minimumDistance, Distance) * panSensitivity;
            FocusPoint -= (mapCamera.transform.right * pointerDelta.x
                + mapCamera.transform.up * pointerDelta.y) * scale;
            ApplyCameraTransform();
        }

        public void ApplyZoomSteps(int steps)
        {
            if (steps == 0) return;
            float factor = steps > 0 ? 1f - zoomFractionPerStep : 1f + zoomFractionPerStep;
            for (int index = 0; index < Mathf.Abs(steps); index++) Distance *= factor;
            Distance = Mathf.Clamp(Distance, minimumDistance, maximumDistance);
            ApplyCameraTransform();
        }

        public void FocusClusterCenterPreservingCamera()
        {
            if (mapCamera == null) return;
            SetFocusPreservingCamera(ClusterCenter);
        }

        public void FocusPlayerPreservingCamera()
        {
            if (mapCamera == null || input == null) return;
            SetFocusPreservingCamera(input.transform.position);
        }

        public void CenterOnPlayer()
        {
            if (!IsOpen || input == null) return;
            CancelLevelTransition();
            centerTransitionStart = FocusPoint;
            centerTransitionTarget = input.transform.position;
            centerTransitionElapsed = 0f;
            isCenterTransitioning = Vector3.Distance(centerTransitionStart, centerTransitionTarget) > 0.001f;
            if (!isCenterTransitioning)
            {
                FocusPoint = centerTransitionTarget;
                ApplyCameraTransform();
            }
        }

        public void FocusPrimaryRoomPreservingCamera()
        {
            if (mapCamera == null) return;
            RefreshClusterBounds(input != null ? input.transform.position : FocusPoint);
            SetFocusPreservingCamera(PrimaryRoomCenter);
        }

        public void ResetViewToPlayer()
        {
            ResetViewToOpening();
        }

        public void ResetViewToOpening()
        {
            if (!IsOpen || !hasOpeningView) return;
            CancelCenterTransition();
            CancelLevelTransition();
            FocusPoint = openingFocusPoint;
            mapUp = openingMapUp;
            orbitOffsetDirection = openingOrbitOffsetDirection;
            orbitCameraUp = openingOrbitCameraUp;
            Distance = openingDistance;
            UpdatePlayerMarker();
            ApplyCameraTransform();
        }

        public void LevelCurrentView()
        {
            if (!IsOpen || mapCamera == null || input == null) return;
            CancelCenterTransition();
            CancelLevelTransition();
            LevelReferenceRoom = FindRoomAtMapCenter();
            if (LevelReferenceRoom == null) return;
            mapUp = LevelReferenceRoom.RoomUp.normalized;

            Vector3 viewForward = -orbitOffsetDirection;
            Vector3 cardinalUp = Vector3.ProjectOnPlane(mapUp, viewForward);
            if (cardinalUp.sqrMagnitude < 0.001f)
            {
                cardinalUp = Vector3.ProjectOnPlane(LevelReferenceRoom.transform.forward, viewForward);
            }
            if (cardinalUp.sqrMagnitude < 0.001f)
                cardinalUp = Vector3.ProjectOnPlane(LevelReferenceRoom.transform.right, viewForward);
            cardinalUp.Normalize();

            levelTransitionStartUp = orbitCameraUp.normalized;
            levelTransitionTargetUp = Vector3.Dot(levelTransitionStartUp, cardinalUp) >= 0f
                ? cardinalUp
                : -cardinalUp;
            levelTransitionElapsed = 0f;
            isLevelTransitioning = Vector3.Angle(levelTransitionStartUp, levelTransitionTargetUp) > 0.01f;
            if (!isLevelTransitioning)
            {
                orbitCameraUp = levelTransitionTargetUp;
                NormalizeOrbitBasis();
                ApplyCameraTransform();
            }
        }

        public void LevelViewToPlayer() => LevelCurrentView();

        public void AdvanceLevelTransition(float unscaledDeltaTime)
        {
            if (!isLevelTransitioning) return;
            levelTransitionElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float duration = Mathf.Max(0.01f, levelTransitionDuration);
            float t = Mathf.Clamp01(levelTransitionElapsed / duration);
            float eased = t * t * (3f - 2f * t);
            orbitCameraUp = Vector3.Slerp(levelTransitionStartUp, levelTransitionTargetUp, eased).normalized;
            if (t >= 1f)
            {
                orbitCameraUp = levelTransitionTargetUp;
                isLevelTransitioning = false;
            }
            NormalizeOrbitBasis();
            ApplyCameraTransform();
        }

        public void AdvanceCenterTransition(float unscaledDeltaTime)
        {
            if (!isCenterTransitioning) return;
            centerTransitionElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float duration = Mathf.Max(0.01f, centerTransitionDuration);
            float t = Mathf.Clamp01(centerTransitionElapsed / duration);
            float eased = t * t * (3f - 2f * t);
            FocusPoint = Vector3.LerpUnclamped(centerTransitionStart, centerTransitionTarget, eased);
            if (t >= 1f)
            {
                FocusPoint = centerTransitionTarget;
                isCenterTransitioning = false;
            }
            ApplyCameraTransform();
        }

        private void Awake()
        {
            IsMapOpen = false;
            ResolveDependencies();
            SetMapPresentation(false);
        }

        private void Start()
        {
            EnsureReticles();
        }

        private void Update()
        {
            ResolveDependencies();
            if (input == null) return;
            if (input.ToggleMapPressedThisFrame)
            {
                if (IsOpen) CloseMap(); else OpenMap();
                return;
            }
            if (!IsOpen) return;
            AdvanceCenterTransition(Time.unscaledDeltaTime);
            AdvanceLevelTransition(Time.unscaledDeltaTime);
            if (input.EscapePressedThisFrame)
            {
                CloseMap(true);
                return;
            }
            if (input.ResetMapPressedThisFrame)
            {
                ResetViewToOpening();
                return;
            }
            if (input.CenterMapPressedThisFrame)
            {
                CenterOnPlayer();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 delta = mouse.delta.ReadValue();
            if (mouse.middleButton.isPressed)
            {
                if (mouse.middleButton.wasPressedThisFrame)
                {
                    CancelCenterTransition();
                    CancelLevelTransition();
                }
                ApplyPanDelta(delta);
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                EndOrbitDrag();
                LevelCurrentView();
            }
            else if (mouse.leftButton.isPressed)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    CancelLevelTransition();
                    BeginOrbitDrag();
                }
                ApplyOrbitDragDelta(delta);
            }
            else EndOrbitDrag();

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) ApplyZoomSteps(scroll > 0f ? 1 : -1);
            UpdatePlayerMarker();
        }

        private void OnDisable()
        {
            if (!IsMapOpen) return;
            SetMapPresentation(false);
            promptView?.SetMapMode(false);
            RestoreGameplayHud();
            if (gameplayReticle != null) gameplayReticle.SetActive(true);
            IsMapOpen = false;
            Time.timeScale = 1f;
            CancelCenterTransition();
            CancelLevelTransition();
            EndOrbitDrag();
        }

        private void InitializeViewFromPlayer()
        {
            Transform player = input != null ? input.transform : transform;
            CubeRoom currentRoom = roomTracker != null ? roomTracker.CurrentRoom : null;
            mapUp = currentRoom != null ? currentRoom.RoomUp.normalized : player.up.normalized;
            if (mapUp.sqrMagnitude < 0.5f) mapUp = Vector3.up;

            Vector3 viewForward = playerLook != null && playerLook.YawRoot != null
                ? playerLook.YawRoot.forward : player.forward;
            Vector3 basisForward = Vector3.ProjectOnPlane(viewForward, mapUp).normalized;
            if (basisForward.sqrMagnitude < 0.5f)
            {
                Vector3 fallback = currentRoom != null ? currentRoom.transform.forward : Vector3.forward;
                basisForward = Vector3.ProjectOnPlane(fallback, mapUp).normalized;
            }
            if (basisForward.sqrMagnitude < 0.5f) basisForward = Vector3.forward;

            FocusPoint = player.position;
            RefreshClusterBounds(FocusPoint);
            const float initialElevation = 55f;
            float radians = initialElevation * Mathf.Deg2Rad;
            orbitOffsetDirection = (-basisForward * Mathf.Cos(radians) + mapUp * Mathf.Sin(radians)).normalized;
            Quaternion initialRotation = Quaternion.LookRotation(-orbitOffsetDirection, mapUp);
            orbitCameraUp = initialRotation * Vector3.up;
            NormalizeOrbitBasis();
            Distance = Mathf.Clamp(initialPlayerDistance, minimumDistance, maximumDistance);
        }

        private void RefreshClusterBounds(Vector3 fallback)
        {
            roomLayout ??= FindAnyObjectByType<CubeRoomClusterGenerator>();
            if (roomLayout == null || roomLayout.Rooms.Count == 0)
            {
                ClusterCenter = fallback;
                PrimaryRoomCenter = fallback;
                return;
            }

            CubeRoom primaryRoom = roomLayout.PrimaryRoom;
            PrimaryRoomCenter = primaryRoom != null
                ? primaryRoom.transform.TransformPoint(new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f))
                : fallback;

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (CubeRoom room in roomLayout.Rooms)
            {
                if (room == null) continue;
                Vector3 center = room.transform.TransformPoint(new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f));
                Bounds roomBounds = new Bounds(center, Vector3.one * CubeRoom.InteriorWidth);
                if (!hasBounds) { bounds = roomBounds; hasBounds = true; }
                else bounds.Encapsulate(roomBounds);
            }
            ClusterCenter = hasBounds ? bounds.center : fallback;
        }

        private void CaptureOpeningView()
        {
            openingFocusPoint = FocusPoint;
            openingMapUp = mapUp;
            openingOrbitOffsetDirection = orbitOffsetDirection;
            openingOrbitCameraUp = orbitCameraUp;
            openingDistance = Distance;
            hasOpeningView = true;
        }

        public void BeginOrbitDrag()
        {
            orbitAxis = OrbitAxis.None;
            orbitGestureTravel = Vector2.zero;
            orbitAxisSwitchTravel = 0f;
        }

        public void ApplyOrbitDragDelta(Vector2 pointerDelta)
        {
            orbitGestureTravel += pointerDelta;
            if (orbitAxis == OrbitAxis.None)
            {
                float largestTravel = Mathf.Max(Mathf.Abs(orbitGestureTravel.x), Mathf.Abs(orbitGestureTravel.y));
                if (largestTravel < orbitAxisLockThreshold) return;
                orbitAxis = Mathf.Abs(orbitGestureTravel.x) >= Mathf.Abs(orbitGestureTravel.y)
                    ? OrbitAxis.Horizontal
                    : OrbitAxis.Vertical;
            }

            float currentMagnitude = orbitAxis == OrbitAxis.Horizontal
                ? Mathf.Abs(pointerDelta.x)
                : Mathf.Abs(pointerDelta.y);
            float alternateMagnitude = orbitAxis == OrbitAxis.Horizontal
                ? Mathf.Abs(pointerDelta.y)
                : Mathf.Abs(pointerDelta.x);
            if (alternateMagnitude > currentMagnitude * orbitAxisSwitchDominance)
            {
                orbitAxisSwitchTravel += alternateMagnitude - currentMagnitude;
                if (orbitAxisSwitchTravel >= orbitAxisSwitchThreshold)
                {
                    orbitAxis = orbitAxis == OrbitAxis.Horizontal ? OrbitAxis.Vertical : OrbitAxis.Horizontal;
                    orbitAxisSwitchTravel = 0f;
                }
            }
            else
            {
                orbitAxisSwitchTravel = Mathf.Max(0f, orbitAxisSwitchTravel - currentMagnitude);
            }

            ApplyOrbitDelta(orbitAxis == OrbitAxis.Horizontal
                ? new Vector2(pointerDelta.x, 0f)
                : new Vector2(0f, pointerDelta.y));
        }

        public void EndOrbitDrag()
        {
            orbitAxis = OrbitAxis.None;
            orbitGestureTravel = Vector2.zero;
            orbitAxisSwitchTravel = 0f;
        }

        private void CancelLevelTransition()
        {
            isLevelTransitioning = false;
            levelTransitionElapsed = 0f;
        }

        private void CancelCenterTransition()
        {
            isCenterTransitioning = false;
            centerTransitionElapsed = 0f;
        }

        private CubeRoom FindRoomAtMapCenter()
        {
            roomLayout ??= FindAnyObjectByType<CubeRoomClusterGenerator>();
            if (mapCamera == null || roomLayout == null) return null;

            Ray worldRay = mapCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            CubeRoom closestRoom = null;
            float closestDistance = float.PositiveInfinity;
            Bounds localBounds = new Bounds(
                new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f),
                new Vector3(CubeRoom.InteriorWidth, CubeRoom.InteriorHeight, CubeRoom.InteriorWidth));
            foreach (CubeRoom room in roomLayout.Rooms)
            {
                if (room == null || !room.gameObject.activeInHierarchy) continue;
                Vector3 localOrigin = room.transform.InverseTransformPoint(worldRay.origin);
                Vector3 localDirection = room.transform.InverseTransformDirection(worldRay.direction).normalized;
                if (!localBounds.IntersectRay(new Ray(localOrigin, localDirection), out float distance)) continue;
                if (distance < 0f || distance >= closestDistance) continue;
                closestDistance = distance;
                closestRoom = room;
            }
            return closestRoom;
        }

        private void EnsureReticles()
        {
            EnsureMapReticleCircle();
            EnsureGameplayReticlePlus();
        }

        private void EnsureMapReticleCircle()
        {
            if (mapOverlayRoot == null) return;
            Transform existing = mapOverlayRoot.transform.Find("Map Reticle");
            GameObject reticle = existing != null
                ? existing.gameObject
                : new GameObject("Map Reticle", typeof(RectTransform));
            if (existing == null) reticle.transform.SetParent(mapOverlayRoot.transform, false);
            RectTransform rect = reticle.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(10f, 10f);
            foreach (Transform child in reticle.transform) child.gameObject.SetActive(false);

            Image image = reticle.GetComponent<Image>() ?? reticle.AddComponent<Image>();
            // Keep the circle sprite serialized by FoundationBuilder. UI/Skin assets are
            // editor resources and cannot be loaded through Resources.GetBuiltinResource at runtime.
            image.preserveAspect = true;
            image.color = new Color(0.72f, 0.95f, 1f, 0.94f);
            image.raycastTarget = false;
            Shadow shadow = reticle.GetComponent<Shadow>() ?? reticle.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        private void EnsureGameplayReticlePlus()
        {
            if (gameplayReticle != null) return;
            Transform canvas = mapOverlayRoot != null ? mapOverlayRoot.transform.parent : null;
            if (canvas == null) return;
            Transform existing = canvas.Find("Gameplay Reticle");
            if (existing != null)
            {
                gameplayReticle = existing.gameObject;
                return;
            }

            gameplayReticle = new GameObject("Gameplay Reticle", typeof(RectTransform));
            gameplayReticle.transform.SetParent(canvas, false);
            RectTransform rect = gameplayReticle.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(42f, 42f);
            Color color = new Color(0.90f, 0.96f, 1f, 0.86f);
            CreateReticleBar("Horizontal", gameplayReticle.transform, new Vector2(28f, 3f), color);
            CreateReticleBar("Vertical", gameplayReticle.transform, new Vector2(3f, 28f), color);
        }

        private static void CreateReticleBar(string label, Transform parent, Vector2 size, Color color)
        {
            GameObject tick = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tick.transform.SetParent(parent, false);
            RectTransform rect = tick.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            Image image = tick.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Shadow shadow = tick.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        private void SetFocusPreservingCamera(Vector3 newFocus)
        {
            Vector3 offset = mapCamera.transform.position - newFocus;
            if (offset.sqrMagnitude < 0.01f) offset = mapUp * minimumDistance;
            Distance = Mathf.Clamp(offset.magnitude, minimumDistance, maximumDistance);
            orbitOffsetDirection = offset.normalized;
            orbitCameraUp = mapCamera.transform.up;
            NormalizeOrbitBasis();
            FocusPoint = newFocus;
            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            if (mapCamera == null) return;
            NormalizeOrbitBasis();
            mapCamera.transform.position = FocusPoint + orbitOffsetDirection * Distance;
            mapCamera.transform.rotation = Quaternion.LookRotation(-orbitOffsetDirection, orbitCameraUp);
            mapCamera.farClipPlane = Mathf.Max(500f, Distance * 4f);
        }

        private void NormalizeOrbitBasis()
        {
            if (orbitOffsetDirection.sqrMagnitude < 0.001f) orbitOffsetDirection = Vector3.back;
            orbitOffsetDirection.Normalize();
            orbitCameraUp = Vector3.ProjectOnPlane(orbitCameraUp, orbitOffsetDirection);
            if (orbitCameraUp.sqrMagnitude < 0.001f)
            {
                Vector3 fallback = Mathf.Abs(Vector3.Dot(orbitOffsetDirection, mapUp)) < 0.95f
                    ? mapUp
                    : Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(orbitOffsetDirection, fallback)) > 0.95f)
                    fallback = Vector3.right;
                orbitCameraUp = Vector3.ProjectOnPlane(fallback, orbitOffsetDirection);
            }
            orbitCameraUp.Normalize();
        }

        private void UpdatePlayerMarker()
        {
            if (playerMarker == null || input == null) return;
            Transform player = input.transform;
            CubeRoom currentRoom = roomTracker != null ? roomTracker.CurrentRoom : null;
            Vector3 up = currentRoom != null ? currentRoom.RoomUp.normalized : player.up.normalized;
            Vector3 forward = playerLook != null && playerLook.YawRoot != null
                ? playerLook.YawRoot.forward : player.forward;
            forward = Vector3.ProjectOnPlane(forward, up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.ProjectOnPlane(player.forward, up).normalized;
            playerMarker.SetPositionAndRotation(player.position + up * 1.2f,
                Quaternion.LookRotation(forward, up));
        }

        private void CacheAndHideGameplayHud()
        {
            if (gameplayHudElements == null) return;
            hudActiveStates = new bool[gameplayHudElements.Length];
            for (int index = 0; index < gameplayHudElements.Length; index++)
            {
                GameObject item = gameplayHudElements[index];
                hudActiveStates[index] = item != null && item.activeSelf;
                if (item != null) item.SetActive(false);
            }
        }

        private void RestoreGameplayHud()
        {
            if (gameplayHudElements == null || hudActiveStates == null) return;
            for (int index = 0; index < gameplayHudElements.Length && index < hudActiveStates.Length; index++)
                if (gameplayHudElements[index] != null) gameplayHudElements[index].SetActive(hudActiveStates[index]);
            hudActiveStates = null;
        }

        private void SetMapPresentation(bool visible)
        {
            if (mapRoot != null) mapRoot.SetActive(visible);
            if (mapOverlayRoot != null) mapOverlayRoot.SetActive(visible);
            if (mapCamera != null) mapCamera.enabled = visible;
        }

        private void ResolveDependencies()
        {
            input ??= FindAnyObjectByType<FirstPersonInput>();
            if (input != null)
            {
                playerLook ??= input.GetComponent<PlayerLook>();
                roomTracker ??= input.GetComponent<PlayerRoomTracker>();
                creationController ??= input.GetComponent<RoomCreationController>();
                deletionController ??= input.GetComponent<RoomDeletionController>();
                traversal ??= input.GetComponent<PlayerRoomTraversal>();
            }
            roomLayout ??= FindAnyObjectByType<CubeRoomClusterGenerator>();
            promptView ??= GetComponent<RoomCreationPromptView>();
        }
    }
}
