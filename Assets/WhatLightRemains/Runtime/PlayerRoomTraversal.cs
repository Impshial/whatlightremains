using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Owns elevated Hold-E approaches and the short alignment/landing phase after a genuine
    /// walk-through crossing. Ordinary aligned, floor-level doorways remain normal movement.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerRoomTraversal : MonoBehaviour
    {
        private enum TravelStage { None, Approach, Cross, Align, Land }

        [SerializeField] private FirstPersonInput input;
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private KinematicCapsuleMover mover;
        [SerializeField] private PlayerGravityAlignment gravityAlignment;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private RoomCreationController creationController;
        [SerializeField] private RoomCreationPromptView promptView;
        [SerializeField, Min(0.5f)] private float travelSpeed = 4f;
        [SerializeField, Min(0f)] private float acceleration = 10f;
        [SerializeField, Min(0f)] private float targetingMargin = 0.28f;
        [SerializeField, Min(0f)] private float waypointTolerance = 0.06f;

        private RoomPassage target;
        private RoomPassage activePassage;
        private CubeRoom sourceRoom;
        private CubeRoom destinationRoom;
        private TravelStage stage;
        private float currentSpeed;
        private bool committed;
        private bool inputArmed = true;
        private bool focused = true;

        public bool IsOwningMovement => stage != TravelStage.None;
        public bool IsCommitted => committed;
        public RoomPassage Target => target;

        public void Configure(FirstPersonInput inputSource, PlayerRoomTracker tracker,
            KinematicCapsuleMover capsuleMover, PlayerGravityAlignment alignment, Camera camera,
            PlayerLook look, RoomCreationController creation, RoomCreationPromptView prompt)
        {
            Unsubscribe();
            input = inputSource;
            roomTracker = tracker;
            mover = capsuleMover;
            gravityAlignment = alignment;
            gameplayCamera = camera;
            playerLook = look;
            creationController = creation;
            promptView = prompt;
            Subscribe();
        }

        private void Awake()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            CancelOrFinish(false);
            ClearTarget();
            Unsubscribe();
        }

        private void Update()
        {
            ResolveReferences();
            bool held = input != null && input.TraverseHeld;
            if (!held) inputArmed = true;
            if (!focused) return;

            if (IsOwningMovement)
            {
                promptView?.SetTraversalPrompt(false);
                if (!committed && (!held || input.ReleaseCursorPressedThisFrame))
                {
                    CancelOrFinish(false);
                    return;
                }
                TickTravel(Time.deltaTime);
                return;
            }

            bool canTarget = input != null && playerLook != null && playerLook.IsCursorCaptured
                && (creationController == null || !creationController.IsCreateMode);
            if (!canTarget)
            {
                ClearTarget();
                return;
            }

            AcquireTarget();
            if (target != null && inputArmed && input.TraversePressedThisFrame)
            {
                BeginAssisted(target);
            }
        }

        private void AcquireTarget()
        {
            CubeRoom current = roomTracker != null ? roomTracker.CurrentRoom : null;
            Ray ray = gameplayCamera != null
                ? new Ray(gameplayCamera.transform.position, gameplayCamera.transform.forward)
                : new Ray(transform.position, transform.forward);
            RoomPassage best = null;
            float bestDistance = float.PositiveInfinity;
            CubeRoomClusterGenerator layout = FindAnyObjectByType<CubeRoomClusterGenerator>();
            if (current != null && layout != null)
            {
                foreach (RoomPassage passage in layout.Passages)
                {
                    if (passage == null || passage.GetOtherRoom(current) == null || !passage.IsElevatedFor(current)) continue;
                    RoomAperture aperture = passage.Aperture;
                    Plane plane = new Plane(aperture.Normal, aperture.Center);
                    if (!plane.Raycast(ray, out float distance) || distance < 0f
                        || distance > CubeRoom.InteriorWidth * 2f || distance >= bestDistance) continue;
                    Vector3 point = ray.GetPoint(distance) - aperture.Center;
                    if (Mathf.Abs(Vector3.Dot(point, aperture.HorizontalAxis)) > aperture.Width * 0.5f + targetingMargin
                        || Mathf.Abs(Vector3.Dot(point, aperture.VerticalAxis)) > aperture.Height * 0.5f + targetingMargin) continue;
                    if (Physics.Raycast(ray, out RaycastHit obstruction, Mathf.Max(0f, distance - 0.08f), ~0,
                            QueryTriggerInteraction.Ignore)
                        && obstruction.collider != null
                        && !obstruction.collider.transform.IsChildOf(transform)
                        && obstruction.collider.GetComponentInParent<RoomPassage>() != passage) continue;
                    best = passage;
                    bestDistance = distance;
                }
            }
            SetTarget(best);
        }

        private void BeginAssisted(RoomPassage passage)
        {
            CubeRoom current = roomTracker != null ? roomTracker.CurrentRoom : null;
            if (passage == null || current == null || !passage.IsElevatedFor(current)) return;
            activePassage = passage;
            sourceRoom = current;
            destinationRoom = passage.GetOtherRoom(current);
            if (destinationRoom == null) return;
            committed = false;
            inputArmed = false;
            currentSpeed = 0f;
            stage = TravelStage.Approach;
            roomTracker.BeginRoomAssignmentLock();
            ClearTarget();
        }

        private void TickTravel(float deltaTime)
        {
            if (activePassage == null || sourceRoom == null || destinationRoom == null || mover == null)
            {
                CancelOrFinish(false);
                return;
            }
            Vector3 direction = (GetRoomCenter(destinationRoom) - GetRoomCenter(sourceRoom)).normalized;
            Vector3 sourceRootAtOpening = activePassage.Aperture.Center;
            Vector3 destinationRootAtOpening = activePassage.Aperture.Center;
            switch (stage)
            {
                case TravelStage.Approach:
                    if (MoveToward(sourceRootAtOpening - direction * 0.5f, sourceRoom.RoomUp, deltaTime))
                    {
                        committed = true;
                        stage = TravelStage.Cross;
                    }
                    break;
                case TravelStage.Cross:
                    if (MoveToward(destinationRootAtOpening + direction * 1.35f, sourceRoom.RoomUp, deltaTime))
                    {
                        roomTracker.EndRoomAssignmentLock(destinationRoom);
                        gravityAlignment?.BeginAlignment(destinationRoom);
                        stage = TravelStage.Align;
                    }
                    break;
                case TravelStage.Align:
                    if (gravityAlignment == null || !gravityAlignment.IsAligning) stage = TravelStage.Land;
                    break;
                case TravelStage.Land:
                    if (MoveToward(GetLandingPosition(destinationRoom), destinationRoom.RoomUp, deltaTime))
                        CancelOrFinish(true);
                    break;
            }
        }

        private bool MoveToward(Vector3 targetPosition, Vector3 up, float deltaTime)
        {
            Vector3 delta = targetPosition - transform.position;
            if (delta.magnitude <= waypointTolerance) return true;
            currentSpeed = Mathf.MoveTowards(currentSpeed, travelSpeed, acceleration * deltaTime);
            Vector3 step = Vector3.ClampMagnitude(delta, currentSpeed * deltaTime);
            CapsuleMoveResult result = mover.Move(step, up);
            if (result.Displacement.sqrMagnitude < 0.000001f && step.sqrMagnitude > 0.0001f)
            {
                if (!committed) CancelOrFinish(false);
                return false;
            }
            return (targetPosition - transform.position).magnitude <= waypointTolerance;
        }

        private Vector3 GetLandingPosition(CubeRoom room)
        {
            Vector3 local = room.transform.InverseTransformPoint(activePassage.Aperture.Center);
            local.x = Mathf.Clamp(local.x, -2.8f, 2.8f);
            local.z = Mathf.Clamp(local.z, -2.8f, 2.8f);
            local.y = 0.93f;
            return room.transform.TransformPoint(local);
        }

        private void HandleRoomChanged(CubeRoom previous, CubeRoom current)
        {
            ClearTarget();
            if (IsOwningMovement || previous == null || current == null
                || Vector3.Dot(previous.RoomUp, current.RoomUp) > 0.999f) return;
            CubeRoomClusterGenerator layout = FindAnyObjectByType<CubeRoomClusterGenerator>();
            if (layout == null) return;
            foreach (RoomPassage passage in layout.Passages)
            {
                if (passage == null || passage.GetOtherRoom(previous) != current || !passage.IsFloorLevelFor(previous)) continue;
                activePassage = passage;
                sourceRoom = previous;
                destinationRoom = current;
                committed = true;
                inputArmed = false;
                currentSpeed = 0f;
                // PlayerGravityAlignment also observes CurrentRoomChanged. Restore the source
                // pose until the full capsule clears the frame; rotating while straddling the
                // opening is what previously wedged the player between differently oriented rooms.
                gravityAlignment?.SnapToUp(previous.RoomUp);
                roomTracker.BeginRoomAssignmentLock();
                stage = TravelStage.Cross;
                return;
            }
        }

        private void CancelOrFinish(bool completed)
        {
            if (stage == TravelStage.None) return;
            if (roomTracker != null && roomTracker.IsRoomAssignmentLocked)
                roomTracker.EndRoomAssignmentLock(completed ? destinationRoom : sourceRoom);
            stage = TravelStage.None;
            committed = false;
            currentSpeed = 0f;
            activePassage = null;
            sourceRoom = null;
            destinationRoom = null;
        }

        private void SetTarget(RoomPassage next)
        {
            if (target == next)
            {
                promptView?.SetTraversalPrompt(target != null);
                return;
            }
            if (target != null) target.SetHighlighted(roomTracker != null ? roomTracker.CurrentRoom : null, false);
            target = next;
            if (target != null) target.SetHighlighted(roomTracker.CurrentRoom, true);
            promptView?.SetTraversalPrompt(target != null);
        }

        private void ClearTarget() => SetTarget(null);
        private static Vector3 GetRoomCenter(CubeRoom room) => room.transform.TransformPoint(Vector3.up * 4f);

        private void ResolveReferences()
        {
            input ??= GetComponent<FirstPersonInput>();
            roomTracker ??= GetComponent<PlayerRoomTracker>();
            mover ??= GetComponent<KinematicCapsuleMover>();
            gravityAlignment ??= GetComponent<PlayerGravityAlignment>();
            playerLook ??= GetComponent<PlayerLook>();
            gameplayCamera ??= GetComponentInChildren<Camera>();
            creationController ??= GetComponent<RoomCreationController>();
            promptView ??= FindAnyObjectByType<RoomCreationPromptView>();
        }

        private void Subscribe()
        {
            if (roomTracker == null) return;
            roomTracker.CurrentRoomChanged -= HandleRoomChanged;
            roomTracker.CurrentRoomChanged += HandleRoomChanged;
        }

        private void Unsubscribe()
        {
            if (roomTracker != null) roomTracker.CurrentRoomChanged -= HandleRoomChanged;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus && IsOwningMovement && !committed) CancelOrFinish(false);
            if (!hasFocus) ClearTarget();
        }

        private void OnApplicationPause(bool paused)
        {
            focused = !paused;
            if (paused && IsOwningMovement && !committed) CancelOrFinish(false);
            if (paused) ClearTarget();
        }
    }
}
