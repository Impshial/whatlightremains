using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoomOccupancyVolume : MonoBehaviour
    {
        [SerializeField] private CubeRoom room;
        [SerializeField] private LayerMask occupantLayers = ~0;

        private readonly Dictionary<PlayerRoomTracker, int> overlapCounts = new();
        private BoxCollider volumeCollider;

        public CubeRoom Room => room;

        public void Configure(CubeRoom owningRoom)
        {
            room = owningRoom;
            EnsureCollider();
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            EnsureCollider();
            if (volumeCollider == null)
            {
                return false;
            }

            Vector3 localPoint = volumeCollider.transform.InverseTransformPoint(worldPoint) - volumeCollider.center;
            Vector3 halfSize = volumeCollider.size * 0.5f;
            const float epsilon = 0.0001f;
            return Mathf.Abs(localPoint.x) <= halfSize.x + epsilon
                && Mathf.Abs(localPoint.y) <= halfSize.y + epsilon
                && Mathf.Abs(localPoint.z) <= halfSize.z + epsilon;
        }

        private void Awake()
        {
            if (room == null)
            {
                room = GetComponentInParent<CubeRoom>();
            }

            EnsureCollider();
        }

        private void Reset()
        {
            room = GetComponentInParent<CubeRoom>();
            EnsureCollider();
        }

        private void OnValidate()
        {
            if (room == null)
            {
                room = GetComponentInParent<CubeRoom>();
            }

            EnsureCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (room == null || !IncludesLayer(other.gameObject.layer))
            {
                return;
            }

            PlayerRoomTracker tracker = other.GetComponentInParent<PlayerRoomTracker>();
            if (tracker == null)
            {
                return;
            }

            overlapCounts.TryGetValue(tracker, out int count);
            overlapCounts[tracker] = count + 1;
            if (count == 0)
            {
                tracker.EnterRoom(room);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerRoomTracker tracker = other.GetComponentInParent<PlayerRoomTracker>();
            if (tracker == null || !overlapCounts.TryGetValue(tracker, out int count))
            {
                return;
            }

            if (count > 1)
            {
                overlapCounts[tracker] = count - 1;
                return;
            }

            overlapCounts.Remove(tracker);
            if (room != null)
            {
                tracker.ExitRoom(room);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (room == null || !IncludesLayer(other.gameObject.layer))
            {
                return;
            }

            PlayerRoomTracker tracker = other.GetComponentInParent<PlayerRoomTracker>();
            if (tracker == null || overlapCounts.ContainsKey(tracker))
            {
                return;
            }

            // Recovers occupancy when a controller begins inside the trigger before
            // Unity has a chance to deliver an enter callback.
            overlapCounts[tracker] = 1;
            tracker.EnterRoom(room);
        }

        private void OnDisable()
        {
            if (room != null)
            {
                foreach (PlayerRoomTracker tracker in overlapCounts.Keys)
                {
                    if (tracker != null)
                    {
                        tracker.ExitRoom(room);
                    }
                }
            }

            overlapCounts.Clear();
        }

        private void EnsureCollider()
        {
            if (volumeCollider == null)
            {
                volumeCollider = GetComponent<BoxCollider>();
            }

            if (volumeCollider != null)
            {
                volumeCollider.isTrigger = true;
            }
        }

        private bool IncludesLayer(int layer)
        {
            return (occupantLayers.value & (1 << layer)) != 0;
        }
    }
}
