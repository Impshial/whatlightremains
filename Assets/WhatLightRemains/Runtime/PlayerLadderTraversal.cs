using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerLadderTraversal : MonoBehaviour
    {
        [SerializeField] private KinematicCapsuleMover mover;
        [SerializeField] private CharacterController legacyController;
        [SerializeField] private PlayerRoomTracker roomTracker;
        [SerializeField] private Transform facingTransform;
        [SerializeField, Min(0f)] private float climbSpeed = 2.5f;
        [SerializeField, Min(0f)] private float detachCooldown = 0.25f;
        [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.25f;
        [SerializeField, Min(0f)] private float endpointTolerance = 0.04f;

        private readonly HashSet<CeilingLadder> availableLadders = new();
        private CeilingLadder activeLadder;
        private float cooldownRemaining;

        public bool IsAttached => activeLadder != null;
        public CeilingLadder ActiveLadder => activeLadder;
        public float ClimbSpeed
        {
            get => climbSpeed;
            set => climbSpeed = Mathf.Max(0f, value);
        }

        public float DetachCooldown
        {
            get => detachCooldown;
            set => detachCooldown = Mathf.Max(0f, value);
        }

        public void Configure(
            KinematicCapsuleMover capsuleMover,
            CharacterController fallbackController,
            PlayerRoomTracker tracker,
            Transform viewFacingTransform = null)
        {
            mover = capsuleMover;
            legacyController = fallbackController;
            roomTracker = tracker;
            facingTransform = viewFacingTransform;
        }

        public void OfferLadder(CeilingLadder ladder)
        {
            if (ladder != null)
            {
                availableLadders.Add(ladder);
            }
        }

        public void WithdrawLadder(CeilingLadder ladder)
        {
            if (ladder != null && ladder != activeLadder)
            {
                availableLadders.Remove(ladder);
            }
        }

        public bool Mount(CeilingLadder ladder)
        {
            if (ladder == null || cooldownRemaining > 0f || ladder.ClimbLength <= 0.001f)
            {
                return false;
            }

            activeLadder = ladder;
            if (roomTracker != null)
            {
                roomTracker.BeginRoomAssignmentLock();
            }

            Vector3 snapped = ladder.ClosestPointOnPath(transform.position);
            SetPosition(snapped);
            return true;
        }

        public void Detach()
        {
            if (activeLadder == null)
            {
                return;
            }

            activeLadder = null;
            cooldownRemaining = detachCooldown;
            if (roomTracker != null)
            {
                roomTracker.EndRoomAssignmentLock();
            }
        }

        /// <summary>
        /// Returns true when ladder traversal owns this motor step. Positive input climbs
        /// toward the upper room, negative input descends, and jump detaches without an impulse.
        /// </summary>
        public bool Tick(float forwardInput, bool jumpPressed, float deltaTime)
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Mathf.Max(0f, deltaTime));

            if (activeLadder == null)
            {
                if (Mathf.Abs(forwardInput) > 0.1f)
                {
                    TryMountAvailable(forwardInput);
                }

                if (activeLadder == null)
                {
                    return false;
                }
            }

            if (jumpPressed)
            {
                Detach();
                return true;
            }

            CeilingLadder ladder = activeLadder;
            float currentDistance = ladder.ProjectDistance(transform.position);
            float nextDistance = Mathf.Clamp(
                currentDistance + Mathf.Clamp(forwardInput, -1f, 1f) * climbSpeed * Mathf.Max(0f, deltaTime),
                0f,
                ladder.ClimbLength);
            Vector3 target = ladder.LowerMountPosition + ladder.ClimbDirection * nextDistance;
            SetPosition(target);

            if (forwardInput > 0f && nextDistance >= ladder.ClimbLength - endpointTolerance)
            {
                CompleteAt(ladder.UpperExitPosition, ladder.UpperRoom);
            }
            else if (forwardInput < 0f && nextDistance <= endpointTolerance)
            {
                CompleteAt(ladder.LowerExitPosition, ladder.LowerRoom);
            }

            return true;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            if (activeLadder != null && roomTracker != null)
            {
                roomTracker.EndRoomAssignmentLock();
            }

            activeLadder = null;
            availableLadders.Clear();
            cooldownRemaining = 0f;
        }

        private void TryMountAvailable(float input)
        {
            if (cooldownRemaining > 0f)
            {
                return;
            }

            CeilingLadder nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (CeilingLadder ladder in availableLadders)
            {
                if (ladder == null || ladder.ClimbLength <= 0.001f || !FacesLadder(ladder))
                {
                    continue;
                }

                float projected = ladder.ProjectDistance(transform.position);
                bool nearLower = projected <= ladder.ClimbLength * 0.5f;
                if ((nearLower && input <= 0f) || (!nearLower && input >= 0f))
                {
                    continue;
                }

                float distance = (ladder.ClosestPointOnPath(transform.position) - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = ladder;
                }
            }

            if (nearest != null)
            {
                Mount(nearest);
            }
        }

        private bool FacesLadder(CeilingLadder ladder)
        {
            Vector3 towardPath = ladder.ClosestPointOnPath(transform.position) - transform.position;
            Vector3 up = roomTracker != null && roomTracker.CurrentRoom != null
                ? roomTracker.CurrentRoom.RoomUp
                : transform.up;
            towardPath = Vector3.ProjectOnPlane(towardPath, up);
            Transform facing = ResolveFacingTransform();
            Vector3 planarForward = Vector3.ProjectOnPlane(facing.forward, up);
            if (towardPath.sqrMagnitude <= 0.000001f || planarForward.sqrMagnitude <= 0.000001f)
            {
                return true;
            }

            return Vector3.Dot(planarForward.normalized, towardPath.normalized) >= minimumFacingDot;
        }

        private void CompleteAt(Vector3 exitPosition, CubeRoom destinationRoom)
        {
            CeilingLadder completedLadder = activeLadder;
            activeLadder = null;
            cooldownRemaining = detachCooldown;
            SetPosition(exitPosition);
            if (roomTracker != null)
            {
                roomTracker.EndRoomAssignmentLock(destinationRoom);
            }

            if (completedLadder != null)
            {
                availableLadders.Add(completedLadder);
            }
        }

        private void SetPosition(Vector3 position)
        {
            if (mover != null && mover.isActiveAndEnabled)
            {
                transform.position = position;
                Physics.SyncTransforms();
                return;
            }

            if (legacyController != null && legacyController.enabled)
            {
                legacyController.Move(position - transform.position);
                return;
            }

            transform.position = position;
            Physics.SyncTransforms();
        }

        private void ResolveReferences()
        {
            mover ??= GetComponent<KinematicCapsuleMover>();
            legacyController ??= GetComponent<CharacterController>();
            roomTracker ??= GetComponent<PlayerRoomTracker>();
        }

        private Transform ResolveFacingTransform()
        {
            if (facingTransform != null)
            {
                return facingTransform;
            }

            PlayerLook look = GetComponent<PlayerLook>();
            if (look != null && look.YawRoot != null)
            {
                return look.YawRoot;
            }

            return transform;
        }

        private void OnValidate()
        {
            climbSpeed = Mathf.Max(0f, climbSpeed);
            detachCooldown = Mathf.Max(0f, detachCooldown);
            endpointTolerance = Mathf.Max(0f, endpointTolerance);
            minimumFacingDot = Mathf.Clamp(minimumFacingDot, -1f, 1f);
            ResolveReferences();
        }
    }
}
