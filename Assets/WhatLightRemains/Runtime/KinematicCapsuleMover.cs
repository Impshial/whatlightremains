using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [System.Flags]
    public enum CapsuleMoveContacts
    {
        None = 0,
        Ground = 1,
        Ceiling = 2,
        Side = 4,
    }

    public readonly struct CapsuleMoveResult
    {
        public CapsuleMoveResult(Vector3 displacement, CapsuleMoveContacts contacts)
        {
            Displacement = displacement;
            Contacts = contacts;
        }

        public Vector3 Displacement { get; }
        public CapsuleMoveContacts Contacts { get; }
        public bool IsGrounded => (Contacts & CapsuleMoveContacts.Ground) != 0;
        public bool HitCeiling => (Contacts & CapsuleMoveContacts.Ceiling) != 0;
    }

    /// <summary>
    /// Small kinematic capsule solver whose capsule axis follows transform.local Y.
    /// Unlike CharacterController this remains usable after the player rig is aligned
    /// to a room whose gravity does not point along world Y.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    public sealed class KinematicCapsuleMover : MonoBehaviour
    {
        [SerializeField] private CapsuleCollider capsule;
        [SerializeField] private Rigidbody body;
        [SerializeField, Min(0.001f)] private float skinWidth = 0.03f;
        [SerializeField, Min(0f)] private float stepOffset = 0.3f;
        [SerializeField, Range(0f, 89f)] private float maximumGroundAngle = 55f;
        [SerializeField, Range(1, 8)] private int maximumSlideIterations = 4;
        [SerializeField] private LayerMask collisionMask = ~0;

        private readonly RaycastHit[] castHits = new RaycastHit[16];

        public CapsuleCollider Capsule => capsule;
        public Rigidbody Body => body;
        public float SkinWidth
        {
            get => skinWidth;
            set => skinWidth = Mathf.Max(0.001f, value);
        }

        public float StepOffset
        {
            get => stepOffset;
            set => stepOffset = Mathf.Max(0f, value);
        }

        public void Configure(CapsuleCollider capsuleCollider, Rigidbody rigidbody)
        {
            capsule = capsuleCollider;
            body = rigidbody;
            ApplyPhysicsSettings();
        }

        public void SetDimensions(float height, float radius, bool centerRoot = true)
        {
            ResolveReferences();
            if (capsule == null)
            {
                return;
            }

            float safeHeight = Mathf.Max(0.1f, height);
            float safeRadius = Mathf.Clamp(radius, 0.05f, safeHeight * 0.5f);
            capsule.direction = 1;
            capsule.height = safeHeight;
            capsule.radius = safeRadius;
            capsule.center = centerRoot ? Vector3.zero : Vector3.up * (safeHeight * 0.5f);
        }

        public CapsuleMoveResult Move(Vector3 requestedDisplacement, Vector3 up)
        {
            ResolveReferences();
            if (capsule == null || !capsule.enabled || requestedDisplacement.sqrMagnitude <= 0.00000001f)
            {
                return new CapsuleMoveResult(Vector3.zero, CapsuleMoveContacts.None);
            }

            Vector3 normalizedUp = SafeUp(up);
            Vector3 startingPosition = transform.position;
            Vector3 remaining = requestedDisplacement;
            CapsuleMoveContacts contacts = CapsuleMoveContacts.None;
            bool mayStep = stepOffset > 0f
                && ProbeGround(normalizedUp, skinWidth + 0.08f, maximumGroundAngle, out _);

            for (int iteration = 0; iteration < maximumSlideIterations; iteration++)
            {
                float distance = remaining.magnitude;
                if (distance <= 0.00001f)
                {
                    break;
                }

                Vector3 direction = remaining / distance;
                GetWorldCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);
                int hitCount = Physics.CapsuleCastNonAlloc(
                    pointA,
                    pointB,
                    radius,
                    direction,
                    castHits,
                    distance + skinWidth,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);

                if (!TryGetNearestValidHit(hitCount, out RaycastHit nearest))
                {
                    Translate(remaining);
                    remaining = Vector3.zero;
                    break;
                }

                float travel = Mathf.Clamp(nearest.distance - skinWidth, 0f, distance);
                if (travel > 0f)
                {
                    Vector3 travelled = direction * travel;
                    Translate(travelled);
                    remaining -= travelled;
                }

                contacts |= ClassifyContact(nearest.normal, normalizedUp);
                if (mayStep
                    && Mathf.Abs(Vector3.Dot(nearest.normal, normalizedUp)) < 0.5f
                    && TryStep(remaining, normalizedUp))
                {
                    remaining = Vector3.Project(remaining, normalizedUp);
                    contacts |= CapsuleMoveContacts.Ground;
                    mayStep = false;
                    continue;
                }

                Vector3 previousRemaining = remaining;
                remaining = Vector3.ProjectOnPlane(remaining, nearest.normal);
                if (remaining.sqrMagnitude >= previousRemaining.sqrMagnitude - 0.0000001f && travel <= 0f)
                {
                    break;
                }
            }

            return new CapsuleMoveResult(transform.position - startingPosition, contacts);
        }

        public bool ProbeGround(Vector3 up, float distance, float maximumGroundAngle, out RaycastHit groundHit)
        {
            ResolveReferences();
            groundHit = default;
            if (capsule == null || !capsule.enabled)
            {
                return false;
            }

            Vector3 normalizedUp = SafeUp(up);
            GetWorldCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);
            int hitCount = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                Mathf.Max(0.001f, radius - skinWidth),
                -normalizedUp,
                castHits,
                Mathf.Max(0f, distance) + skinWidth,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float minimumGroundDot = Mathf.Cos(Mathf.Clamp(maximumGroundAngle, 0f, 89f) * Mathf.Deg2Rad);
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = castHits[i];
                if (!IsValidHit(hit) || Vector3.Dot(hit.normal, normalizedUp) < minimumGroundDot)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    groundHit = hit;
                }
            }

            return groundHit.collider != null;
        }

        public bool CanOccupy(Vector3 position, Quaternion rotation)
        {
            ResolveReferences();
            if (capsule == null)
            {
                return true;
            }

            Vector3 originalPosition = transform.position;
            Quaternion originalRotation = transform.rotation;
            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            GetWorldCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);
            Collider[] overlaps = Physics.OverlapCapsule(
                pointA,
                pointB,
                Mathf.Max(0.001f, radius - skinWidth),
                collisionMask,
                QueryTriggerInteraction.Ignore);
            bool clear = true;
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && overlaps[i] != capsule && !overlaps[i].transform.IsChildOf(transform))
                {
                    clear = false;
                    break;
                }
            }

            transform.SetPositionAndRotation(originalPosition, originalRotation);
            Physics.SyncTransforms();
            return clear;
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyPhysicsSettings();
        }

        private void Reset()
        {
            ResolveReferences();
            ApplyPhysicsSettings();
        }

        private void ResolveReferences()
        {
            capsule ??= GetComponent<CapsuleCollider>();
            body ??= GetComponent<Rigidbody>();
        }

        private void ApplyPhysicsSettings()
        {
            if (capsule != null)
            {
                capsule.direction = 1;
            }

            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                // The body is kinematic, so contacts cannot impart angular velocity. Leaving
                // rotation unconstrained allows PlayerGravityAlignment to orient the capsule.
                body.constraints = RigidbodyConstraints.None;
            }
        }

        private void GetWorldCapsule(out Vector3 pointA, out Vector3 pointB, out float radius)
        {
            Transform capsuleTransform = capsule.transform;
            Vector3 scale = capsuleTransform.lossyScale;
            float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float axialScale = Mathf.Abs(scale.y);
            radius = Mathf.Max(0.001f, capsule.radius * radialScale);
            float halfHeight = Mathf.Max(radius, capsule.height * axialScale * 0.5f);
            float segmentHalfLength = Mathf.Max(0f, halfHeight - radius);
            Vector3 center = capsuleTransform.TransformPoint(capsule.center);
            Vector3 axis = capsuleTransform.up;
            pointA = center + axis * segmentHalfLength;
            pointB = center - axis * segmentHalfLength;
        }

        private bool TryGetNearestValidHit(int hitCount, out RaycastHit nearest)
        {
            nearest = default;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = castHits[i];
                if (!IsValidHit(hit) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                nearest = hit;
            }

            return nearest.collider != null;
        }

        private bool TryStep(Vector3 remaining, Vector3 up)
        {
            Vector3 planar = Vector3.ProjectOnPlane(remaining, up);
            if (planar.sqrMagnitude <= 0.0000001f)
            {
                return false;
            }

            Vector3 originalPosition = transform.position;
            GetWorldCapsule(out Vector3 pointA, out Vector3 pointB, out float radius);
            int upwardHits = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                radius,
                up,
                castHits,
                stepOffset + skinWidth,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            if (TryGetNearestValidHit(upwardHits, out _))
            {
                return false;
            }

            Translate(up * stepOffset);
            GetWorldCapsule(out pointA, out pointB, out radius);
            int forwardHits = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                radius,
                planar.normalized,
                castHits,
                planar.magnitude + skinWidth,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            if (TryGetNearestValidHit(forwardHits, out _))
            {
                transform.position = originalPosition;
                Physics.SyncTransforms();
                return false;
            }

            Translate(planar);
            GetWorldCapsule(out pointA, out pointB, out radius);
            int downwardHits = Physics.CapsuleCastNonAlloc(
                pointA,
                pointB,
                Mathf.Max(0.001f, radius - skinWidth),
                -up,
                castHits,
                stepOffset + skinWidth * 2f,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            float minimumGroundDot = Mathf.Cos(maximumGroundAngle * Mathf.Deg2Rad);
            RaycastHit ground = default;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < downwardHits; i++)
            {
                RaycastHit candidate = castHits[i];
                if (!IsValidHit(candidate)
                    || Vector3.Dot(candidate.normal, up) < minimumGroundDot
                    || candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                ground = candidate;
            }

            if (ground.collider == null)
            {
                transform.position = originalPosition;
                Physics.SyncTransforms();
                return false;
            }

            Translate(-up * Mathf.Max(0f, ground.distance - skinWidth));
            return true;
        }

        private bool IsValidHit(RaycastHit hit)
        {
            return hit.collider != null
                && hit.collider != capsule
                && !hit.collider.transform.IsChildOf(transform);
        }

        private void Translate(Vector3 displacement)
        {
            transform.position += displacement;
            Physics.SyncTransforms();
        }

        private static CapsuleMoveContacts ClassifyContact(Vector3 normal, Vector3 up)
        {
            float alignment = Vector3.Dot(normal, up);
            if (alignment >= 0.5f)
            {
                return CapsuleMoveContacts.Ground;
            }

            if (alignment <= -0.5f)
            {
                return CapsuleMoveContacts.Ceiling;
            }

            return CapsuleMoveContacts.Side;
        }

        private static Vector3 SafeUp(Vector3 up)
        {
            return up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up;
        }

        private void OnValidate()
        {
            skinWidth = Mathf.Max(0.001f, skinWidth);
            stepOffset = Mathf.Max(0f, stepOffset);
            maximumGroundAngle = Mathf.Clamp(maximumGroundAngle, 0f, 89f);
            maximumSlideIterations = Mathf.Clamp(maximumSlideIterations, 1, 8);
            ResolveReferences();
            ApplyPhysicsSettings();
        }
    }
}
