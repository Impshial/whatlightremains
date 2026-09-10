using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Describes a non-blocking climb path through a ceiling doorway. Visual ladder
    /// meshes deliberately need no colliders; a trigger collider on this same
    /// GameObject advertises the path to PlayerLadderTraversal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CeilingLadder : MonoBehaviour
    {
        [SerializeField] private CubeRoom lowerRoom;
        [SerializeField] private CubeRoom upperRoom;
        [SerializeField] private Transform lowerMount;
        [SerializeField] private Transform upperMount;
        [SerializeField] private Transform lowerExit;
        [SerializeField] private Transform upperExit;
        [SerializeField] private Vector3 fallbackLowerMount;
        [SerializeField] private Vector3 fallbackUpperMount = Vector3.up * CubeRoom.InteriorHeight;
        [SerializeField] private Vector3 fallbackLowerExit;
        [SerializeField] private Vector3 fallbackUpperExit = Vector3.up * CubeRoom.InteriorHeight;

        public CubeRoom LowerRoom => lowerRoom;
        public CubeRoom UpperRoom => upperRoom;
        public Vector3 LowerMountPosition => lowerMount != null ? lowerMount.position : transform.TransformPoint(fallbackLowerMount);
        public Vector3 UpperMountPosition => upperMount != null ? upperMount.position : transform.TransformPoint(fallbackUpperMount);
        public Vector3 LowerExitPosition => lowerExit != null ? lowerExit.position : transform.TransformPoint(fallbackLowerExit);
        public Vector3 UpperExitPosition => upperExit != null ? upperExit.position : transform.TransformPoint(fallbackUpperExit);
        public Vector3 ClimbDirection => (UpperMountPosition - LowerMountPosition).normalized;
        public float ClimbLength => Vector3.Distance(LowerMountPosition, UpperMountPosition);

        public void SetRooms(CubeRoom newLowerRoom, CubeRoom newUpperRoom)
        {
            lowerRoom = newLowerRoom;
            upperRoom = newUpperRoom;
        }

        public void Configure(
            CubeRoom newLowerRoom,
            CubeRoom newUpperRoom,
            Transform newLowerMount,
            Transform newUpperMount,
            Transform newLowerExit,
            Transform newUpperExit)
        {
            lowerRoom = newLowerRoom;
            upperRoom = newUpperRoom;
            lowerMount = newLowerMount;
            upperMount = newUpperMount;
            lowerExit = newLowerExit;
            upperExit = newUpperExit;
            EnsureTriggerColliders();
        }

        public void ConfigureWorldPath(
            CubeRoom newLowerRoom,
            CubeRoom newUpperRoom,
            Vector3 worldLowerMount,
            Vector3 worldUpperMount,
            Vector3 worldLowerExit,
            Vector3 worldUpperExit)
        {
            lowerRoom = newLowerRoom;
            upperRoom = newUpperRoom;
            lowerMount = null;
            upperMount = null;
            lowerExit = null;
            upperExit = null;
            fallbackLowerMount = transform.InverseTransformPoint(worldLowerMount);
            fallbackUpperMount = transform.InverseTransformPoint(worldUpperMount);
            fallbackLowerExit = transform.InverseTransformPoint(worldLowerExit);
            fallbackUpperExit = transform.InverseTransformPoint(worldUpperExit);
            EnsureTriggerColliders();
        }

        public float ProjectDistance(Vector3 worldPosition)
        {
            return Vector3.Dot(worldPosition - LowerMountPosition, ClimbDirection);
        }

        public Vector3 ClosestPointOnPath(Vector3 worldPosition)
        {
            float distance = Mathf.Clamp(ProjectDistance(worldPosition), 0f, ClimbLength);
            return LowerMountPosition + ClimbDirection * distance;
        }

        private void Awake()
        {
            EnsureTriggerColliders();
        }

        private void Reset()
        {
            EnsureTriggerColliders();
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerLadderTraversal traversal = other.GetComponentInParent<PlayerLadderTraversal>();
            if (traversal != null)
            {
                traversal.OfferLadder(this);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            PlayerLadderTraversal traversal = other.GetComponentInParent<PlayerLadderTraversal>();
            if (traversal != null)
            {
                traversal.OfferLadder(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerLadderTraversal traversal = other.GetComponentInParent<PlayerLadderTraversal>();
            if (traversal != null)
            {
                traversal.WithdrawLadder(this);
            }
        }

        private void EnsureTriggerColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].isTrigger = true;
            }
        }

        private void OnValidate()
        {
            EnsureTriggerColliders();
        }
    }
}
