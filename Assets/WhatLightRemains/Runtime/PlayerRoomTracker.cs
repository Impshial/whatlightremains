using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerRoomTracker : MonoBehaviour
    {
        [SerializeField] private CubeRoom startingRoom;

        private readonly List<CubeRoom> occupiedRooms = new();
        private int assignmentLockDepth;

        public CubeRoom CurrentRoom { get; private set; }
        public Vector3 GravityAcceleration => CurrentRoom != null ? CurrentRoom.GravityAcceleration : Vector3.zero;
        public event Action<CubeRoom, CubeRoom> CurrentRoomChanged;
        public bool IsRoomAssignmentLocked => assignmentLockDepth > 0;

        public void Initialize(CubeRoom room)
        {
            startingRoom = room;
            occupiedRooms.Clear();
            assignmentLockDepth = 0;
            if (room != null)
            {
                occupiedRooms.Add(room);
            }

            if (!IsRoomAssignmentLocked)
            {
                SetCurrentRoom(room);
            }
        }

        public void EnterRoom(CubeRoom room)
        {
            if (room == null)
            {
                return;
            }

            RemoveDestroyedRooms();
            if (!occupiedRooms.Contains(room))
            {
                occupiedRooms.Add(room);
            }

            if (!IsRoomAssignmentLocked)
            {
                SetCurrentRoom(room);
            }
        }

        public void ExitRoom(CubeRoom room)
        {
            if (room == null)
            {
                return;
            }

            occupiedRooms.Remove(room);
            RemoveDestroyedRooms();
            if (!IsRoomAssignmentLocked && CurrentRoom == room)
            {
                CubeRoom replacement = occupiedRooms.Count > 0 ? occupiedRooms[occupiedRooms.Count - 1] : null;
                SetCurrentRoom(replacement);
            }
        }

        /// <summary>
        /// Defers gravity-room changes while an authored traversal (currently a ceiling
        /// ladder) crosses overlapping or empty trigger volumes.
        /// </summary>
        public void BeginRoomAssignmentLock()
        {
            assignmentLockDepth++;
        }

        public void EndRoomAssignmentLock(CubeRoom destinationRoom = null)
        {
            assignmentLockDepth = Mathf.Max(0, assignmentLockDepth - 1);
            if (IsRoomAssignmentLocked)
            {
                return;
            }

            RemoveDestroyedRooms();
            if (destinationRoom != null)
            {
                if (!occupiedRooms.Contains(destinationRoom))
                {
                    occupiedRooms.Add(destinationRoom);
                }

                SetCurrentRoom(destinationRoom);
                return;
            }

            ResolveRoomAtCurrentPosition();
        }

        private void Awake()
        {
            if (startingRoom != null)
            {
                Initialize(startingRoom);
            }
        }

        private void LateUpdate()
        {
            if (IsRoomAssignmentLocked)
            {
                return;
            }

            // CharacterController movement can cross an entire trigger seam between physics
            // updates (tests, low frame rates, teleports, and future room transitions all do
            // this). Recover from the room volumes geometrically if trigger callbacks have not
            // yet transferred ownership.
            if (CurrentRoom != null && CurrentRoom.ContainsWorldPoint(transform.position))
            {
                return;
            }

            ResolveRoomAtCurrentPosition();
        }

        public void ResolveRoomAtCurrentPosition()
        {
            CubeRoom[] rooms = FindObjectsByType<CubeRoom>();
            for (int index = 0; index < rooms.Length; index++)
            {
                CubeRoom candidate = rooms[index];
                if (candidate == null || !candidate.ContainsWorldPoint(transform.position))
                {
                    continue;
                }

                if (!occupiedRooms.Contains(candidate))
                {
                    occupiedRooms.Add(candidate);
                }

                SetCurrentRoom(candidate);
                return;
            }
        }

        private void SetCurrentRoom(CubeRoom room)
        {
            if (CurrentRoom == room)
            {
                return;
            }

            CubeRoom previous = CurrentRoom;
            CurrentRoom = room;
            CurrentRoomChanged?.Invoke(previous, CurrentRoom);
        }

        private void RemoveDestroyedRooms()
        {
            occupiedRooms.RemoveAll(candidate => candidate == null);
        }
    }
}
