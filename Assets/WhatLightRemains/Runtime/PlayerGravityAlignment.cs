using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Smoothly rotates the physical player rig so local Y matches the active room's up.
    /// Movement is paused by FirstPersonMotor while an alignment is in progress, but
    /// PlayerLook remains independent and can continue applying local yaw/pitch.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerGravityAlignment : MonoBehaviour
    {
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private KinematicCapsuleMover mover;
        [SerializeField, Min(0f)] private float alignmentDuration = 0.35f;

        private Quaternion startRotation;
        private Quaternion targetRotation;
        private float elapsed;

        public float AlignmentDuration
        {
            get => alignmentDuration;
            set => alignmentDuration = Mathf.Max(0f, value);
        }

        public bool IsAligning { get; private set; }
        public Vector3 TargetUp => targetRotation * Vector3.up;

        public void Configure(PlayerRoomTracker tracker, KinematicCapsuleMover capsuleMover)
        {
            Unsubscribe();
            roomTracker = tracker;
            mover = capsuleMover;
            Subscribe();
        }

        public void BeginAlignment(CubeRoom destinationRoom)
        {
            BeginAlignment(destinationRoom != null ? destinationRoom.RoomUp : transform.up);
        }

        public void BeginAlignment(Vector3 destinationUp)
        {
            Vector3 normalizedUp = destinationUp.sqrMagnitude > 0.000001f
                ? destinationUp.normalized
                : transform.up;
            startRotation = transform.rotation;
            targetRotation = CalculateTargetRotation(startRotation, normalizedUp);
            elapsed = 0f;

            if (alignmentDuration <= 0f || Quaternion.Angle(startRotation, targetRotation) <= 0.01f)
            {
                transform.rotation = targetRotation;
                Physics.SyncTransforms();
                IsAligning = false;
                return;
            }

            IsAligning = true;
        }

        public void SnapToUp(Vector3 destinationUp)
        {
            targetRotation = CalculateTargetRotation(transform.rotation, destinationUp);
            transform.rotation = targetRotation;
            Physics.SyncTransforms();
            elapsed = alignmentDuration;
            IsAligning = false;
        }

        public void Tick(float deltaTime)
        {
            if (!IsAligning || deltaTime <= 0f)
            {
                return;
            }

            float nextElapsed = Mathf.Min(alignmentDuration, elapsed + deltaTime);
            float t = alignmentDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, nextElapsed / alignmentDuration);
            Quaternion nextRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            if (mover != null && mover.isActiveAndEnabled && !mover.CanOccupy(transform.position, nextRotation))
            {
                return;
            }

            transform.rotation = nextRotation;
            Physics.SyncTransforms();
            elapsed = nextElapsed;
            if (elapsed >= alignmentDuration - 0.00001f)
            {
                transform.rotation = targetRotation;
                Physics.SyncTransforms();
                IsAligning = false;
            }
        }

        public static Quaternion CalculateTargetRotation(Quaternion currentRotation, Vector3 destinationUp)
        {
            Vector3 normalizedUp = destinationUp.sqrMagnitude > 0.000001f
                ? destinationUp.normalized
                : currentRotation * Vector3.up;
            Vector3 currentForward = currentRotation * Vector3.forward;
            Vector3 targetForward = Vector3.ProjectOnPlane(currentForward, normalizedUp);
            if (targetForward.sqrMagnitude <= 0.000001f)
            {
                Vector3 currentRight = currentRotation * Vector3.right;
                targetForward = Vector3.Cross(currentRight, normalizedUp);
            }

            if (targetForward.sqrMagnitude <= 0.000001f)
            {
                targetForward = Vector3.ProjectOnPlane(Vector3.forward, normalizedUp);
            }

            if (targetForward.sqrMagnitude <= 0.000001f)
            {
                targetForward = Vector3.ProjectOnPlane(Vector3.right, normalizedUp);
            }

            return Quaternion.LookRotation(targetForward.normalized, normalizedUp);
        }

        private void Awake()
        {
            ResolveReferences();
            targetRotation = transform.rotation;
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            IsAligning = false;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void ResolveReferences()
        {
            roomTracker ??= GetComponent<PlayerRoomTracker>();
            mover ??= GetComponent<KinematicCapsuleMover>();
        }

        private void Subscribe()
        {
            if (roomTracker != null)
            {
                roomTracker.CurrentRoomChanged -= HandleRoomChanged;
                roomTracker.CurrentRoomChanged += HandleRoomChanged;
            }
        }

        private void Unsubscribe()
        {
            if (roomTracker != null)
            {
                roomTracker.CurrentRoomChanged -= HandleRoomChanged;
            }
        }

        private void HandleRoomChanged(CubeRoom previousRoom, CubeRoom currentRoom)
        {
            if (currentRoom != null)
            {
                BeginAlignment(currentRoom.RoomUp);
            }
        }

        private void OnValidate()
        {
            alignmentDuration = Mathf.Max(0f, alignmentDuration);
            ResolveReferences();
        }
    }
}
