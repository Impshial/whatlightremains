using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private KinematicCapsuleMover capsuleMover;
        [SerializeField] private FirstPersonInput input;
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private PlayerGravityAlignment gravityAlignment;
        [SerializeField] private PlayerLadderTraversal ladderTraversal;
        [SerializeField] private Transform movementReference;

        [Header("Controller")]
        [SerializeField, Min(0.1f)] private float controllerHeight = 1.8f;
        [SerializeField, Min(0.05f)] private float controllerRadius = 0.3f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [SerializeField, Min(0f)] private float sprintSpeed = 6f;
        [SerializeField, Min(0f)] private float jumpHeight = 1f;
        [SerializeField, Min(0f)] private float groundedSnapSpeed = 2f;
        [SerializeField, Min(0f)] private float terminalFallSpeed = 50f;

        [Header("Grounding")]
        [SerializeField, Min(0f)] private float groundProbeDistance = 0.08f;
        [SerializeField, Range(0f, 89f)] private float maximumGroundAngle = 55f;
        [SerializeField] private LayerMask groundMask = ~0;

        private readonly RaycastHit[] groundHits = new RaycastHit[8];
        private float verticalSpeed;

        public float WalkSpeed
        {
            get => walkSpeed;
            set => walkSpeed = Mathf.Max(0f, value);
        }

        public float SprintSpeed
        {
            get => sprintSpeed;
            set => sprintSpeed = Mathf.Max(0f, value);
        }

        public float JumpHeight
        {
            get => jumpHeight;
            set => jumpHeight = Mathf.Max(0f, value);
        }

        public float ControllerHeight
        {
            get => controllerHeight;
            set
            {
                controllerHeight = Mathf.Max(0.1f, value);
                controllerRadius = Mathf.Min(controllerRadius, controllerHeight * 0.5f);
                ApplyControllerDimensions();
            }
        }

        public float ControllerRadius
        {
            get => controllerRadius;
            set
            {
                controllerRadius = Mathf.Clamp(value, 0.05f, controllerHeight * 0.5f);
                ApplyControllerDimensions();
            }
        }

        public bool IsGrounded { get; private set; }
        public float VerticalSpeed => verticalSpeed;
        public Transform MovementReference => movementReference != null ? movementReference : transform;

        public void Configure(
            CharacterController controller,
            FirstPersonInput inputSource,
            PlayerRoomTracker tracker)
        {
            if (isActiveAndEnabled && roomTracker != null)
            {
                roomTracker.CurrentRoomChanged -= HandleRoomChanged;
            }

            characterController = controller;
            input = inputSource;
            roomTracker = tracker;
            ApplyControllerDimensions();

            if (isActiveAndEnabled && roomTracker != null)
            {
                roomTracker.CurrentRoomChanged += HandleRoomChanged;
            }
        }

        public void Configure(
            KinematicCapsuleMover mover,
            FirstPersonInput inputSource,
            PlayerRoomTracker tracker,
            PlayerGravityAlignment alignment = null,
            PlayerLadderTraversal ladder = null,
            Transform viewMovementReference = null)
        {
            if (isActiveAndEnabled && roomTracker != null)
            {
                roomTracker.CurrentRoomChanged -= HandleRoomChanged;
            }

            capsuleMover = mover;
            input = inputSource;
            roomTracker = tracker;
            gravityAlignment = alignment;
            ladderTraversal = ladder;
            movementReference = viewMovementReference;
            ApplyControllerDimensions();

            if (ladderTraversal != null)
            {
                ladderTraversal.Configure(capsuleMover, characterController, roomTracker, MovementReference);
            }

            if (gravityAlignment != null)
            {
                gravityAlignment.Configure(roomTracker, capsuleMover);
            }

            if (isActiveAndEnabled && roomTracker != null)
            {
                roomTracker.CurrentRoomChanged += HandleRoomChanged;
            }
        }

        public void ResetMotion()
        {
            verticalSpeed = 0f;
            IsGrounded = false;
        }

        public static Vector3 CalculatePlanarMove(
            Vector2 movementInput,
            Vector3 forward,
            Vector3 right,
            float speed)
        {
            Vector2 normalizedInput = Vector2.ClampMagnitude(movementInput, 1f);
            Vector3 normalizedForward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.zero;
            Vector3 orthogonalRight = Vector3.ProjectOnPlane(right, normalizedForward);
            orthogonalRight = orthogonalRight.sqrMagnitude > 0.000001f ? orthogonalRight.normalized : Vector3.zero;
            Vector3 direction = normalizedForward * normalizedInput.y + orthogonalRight * normalizedInput.x;
            return Vector3.ClampMagnitude(direction, 1f) * Mathf.Max(0f, speed);
        }

        public static Vector3 ComputePlanarVelocity(
            Vector2 movementInput,
            Vector3 viewForward,
            Vector3 up,
            float speed)
        {
            Vector3 normalizedUp = up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;
            Vector3 planarForward = Vector3.ProjectOnPlane(viewForward, normalizedUp);
            if (planarForward.sqrMagnitude <= 0.000001f)
            {
                Vector3 fallbackAxis = Mathf.Abs(Vector3.Dot(Vector3.forward, normalizedUp)) < 0.9f
                    ? Vector3.forward
                    : Vector3.right;
                planarForward = Vector3.ProjectOnPlane(fallbackAxis, normalizedUp);
            }

            planarForward.Normalize();
            Vector3 planarRight = Vector3.Cross(normalizedUp, planarForward).normalized;
            return CalculatePlanarMove(movementInput, planarForward, planarRight, speed);
        }

        public static float CalculateJumpSpeed(float desiredJumpHeight, float gravityMagnitude)
        {
            return Mathf.Sqrt(2f * Mathf.Max(0f, gravityMagnitude) * Mathf.Max(0f, desiredJumpHeight));
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyControllerDimensions();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (roomTracker != null)
            {
                roomTracker.CurrentRoomChanged += HandleRoomChanged;
            }
        }

        private void OnDisable()
        {
            if (roomTracker != null)
            {
                roomTracker.CurrentRoomChanged -= HandleRoomChanged;
            }
        }

        private void Update()
        {
            if (!HasEnabledMover() || input == null)
            {
                return;
            }

            Tick(input.Move, input.JumpPressedThisFrame, input.SprintHeld, Time.deltaTime);
        }

        /// <summary>
        /// Advances the motor from already-resolved gameplay input. Keeping this step separate
        /// from the Input System lets tests and future AI/replay drivers feed the same motor.
        /// </summary>
        public void Tick(Vector2 movementInput, bool jumpPressed, float deltaTime)
        {
            Tick(movementInput, jumpPressed, false, deltaTime);
        }

        /// <summary>
        /// Advances the motor using an explicit sprint state. Rotation modifiers never
        /// suppress sprint; input arbitration remains the responsibility of the caller.
        /// </summary>
        public void Tick(Vector2 movementInput, bool jumpPressed, bool sprintHeld, float deltaTime)
        {
            if (!HasEnabledMover() || deltaTime <= 0f)
            {
                return;
            }

            if (gravityAlignment != null && gravityAlignment.IsAligning)
            {
                verticalSpeed = 0f;
                IsGrounded = false;
                return;
            }

            if (ladderTraversal != null && ladderTraversal.Tick(movementInput.y, jumpPressed, deltaTime))
            {
                verticalSpeed = 0f;
                IsGrounded = false;
                return;
            }

            CubeRoom room = roomTracker != null ? roomTracker.CurrentRoom : null;
            Vector3 roomUp = room != null ? room.RoomUp : transform.up;
            float gravityMagnitude = room != null ? room.GravityAcceleration.magnitude : 0f;

            bool touchingGround = ProbeGround(roomUp);
            IsGrounded = touchingGround && verticalSpeed <= 0f;
            if (IsGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -groundedSnapSpeed;
            }

            if (IsGrounded && jumpPressed && gravityMagnitude > 0f)
            {
                verticalSpeed = CalculateJumpSpeed(jumpHeight, gravityMagnitude);
                IsGrounded = false;
            }

            verticalSpeed = Mathf.Max(verticalSpeed - gravityMagnitude * deltaTime, -terminalFallSpeed);
            float movementSpeed = sprintHeld ? sprintSpeed : walkSpeed;
            Vector3 planarVelocity = ComputePlanarVelocity(movementInput, MovementReference.forward, roomUp, movementSpeed);
            Vector3 displacement = (planarVelocity + roomUp * verticalSpeed) * deltaTime;

            bool hitCeiling;
            bool hitGround;
            if (capsuleMover != null && capsuleMover.isActiveAndEnabled)
            {
                CapsuleMoveResult result = capsuleMover.Move(displacement, roomUp);
                hitCeiling = result.HitCeiling;
                hitGround = result.IsGrounded;
            }
            else
            {
                CollisionFlags collisionFlags = characterController.Move(displacement);
                hitCeiling = (collisionFlags & CollisionFlags.Above) != 0;
                hitGround = (collisionFlags & CollisionFlags.Below) != 0;
            }

            if (hitCeiling && verticalSpeed > 0f)
            {
                verticalSpeed = 0f;
            }

            if (hitGround)
            {
                IsGrounded = true;
                if (verticalSpeed < 0f)
                {
                    verticalSpeed = -groundedSnapSpeed;
                }
            }
        }

        private bool ProbeGround(Vector3 up)
        {
            if (capsuleMover != null && capsuleMover.isActiveAndEnabled)
            {
                return capsuleMover.ProbeGround(up, groundProbeDistance, maximumGroundAngle, out _);
            }

            if (characterController == null || !characterController.enabled)
            {
                return false;
            }

            if (characterController.isGrounded)
            {
                return true;
            }

            Transform controllerTransform = characterController.transform;
            Vector3 lossyScale = controllerTransform.lossyScale;
            float radialScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
            float verticalScale = Mathf.Abs(lossyScale.y);
            float radius = Mathf.Max(0.001f, characterController.radius * radialScale * 0.9f);
            float halfHeight = Mathf.Max(radius, characterController.height * verticalScale * 0.5f);
            float castDistance = halfHeight - radius + characterController.skinWidth + groundProbeDistance;
            Vector3 center = controllerTransform.TransformPoint(characterController.center);
            Vector3 down = -up.normalized;
            int hitCount = Physics.SphereCastNonAlloc(
                center,
                radius,
                down,
                groundHits,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            float minimumGroundDot = Mathf.Cos(maximumGroundAngle * Mathf.Deg2Rad);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.collider == characterController)
                {
                    continue;
                }

                if (Vector3.Dot(hit.normal, up.normalized) >= minimumGroundDot)
                {
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            characterController ??= GetComponent<CharacterController>();
            capsuleMover ??= GetComponent<KinematicCapsuleMover>();
            input ??= GetComponent<FirstPersonInput>();
            roomTracker ??= GetComponent<PlayerRoomTracker>();
            gravityAlignment ??= GetComponent<PlayerGravityAlignment>();
            ladderTraversal ??= GetComponent<PlayerLadderTraversal>();
        }

        private void ApplyControllerDimensions()
        {
            controllerHeight = Mathf.Max(0.1f, controllerHeight);
            controllerRadius = Mathf.Clamp(controllerRadius, 0.05f, controllerHeight * 0.5f);
            if (capsuleMover != null)
            {
                capsuleMover.SetDimensions(controllerHeight, controllerRadius, true);
            }

            if (characterController != null)
            {
                characterController.height = controllerHeight;
                characterController.radius = controllerRadius;
                characterController.center = Vector3.up * (controllerHeight * 0.5f);
            }
        }

        private bool HasEnabledMover()
        {
            return (capsuleMover != null && capsuleMover.isActiveAndEnabled)
                || (characterController != null && characterController.enabled);
        }

        private void HandleRoomChanged(CubeRoom previousRoom, CubeRoom currentRoom)
        {
            verticalSpeed = 0f;
            IsGrounded = false;
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            sprintSpeed = Mathf.Max(0f, sprintSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            controllerHeight = Mathf.Max(0.1f, controllerHeight);
            controllerRadius = Mathf.Clamp(controllerRadius, 0.05f, controllerHeight * 0.5f);
            groundedSnapSpeed = Mathf.Max(0f, groundedSnapSpeed);
            terminalFallSpeed = Mathf.Max(0f, terminalFallSpeed);
            groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
            maximumGroundAngle = Mathf.Clamp(maximumGroundAngle, 0f, 89f);
            ResolveReferences();
            ApplyControllerDimensions();
        }
    }
}
