using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    public readonly struct RoomPlacementCandidate
    {
        public RoomPlacementCandidate(CubeRoom sourceRoom, CubeRoomWall sourceWall, Vector2Int gridCell, Vector3 position, Quaternion rotation)
        {
            SourceRoom = sourceRoom;
            SourceWall = sourceWall;
            GridCell = gridCell;
            Position = position;
            Rotation = rotation;
        }

        public CubeRoom SourceRoom { get; }
        public CubeRoomWall SourceWall { get; }
        public Vector2Int GridCell { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool IsValid => SourceRoom != null;
    }

    /// <summary>
    /// Authoritative horizontal room registry. Normal gameplay starts with only the authored
    /// primary room; player construction adds exact grid cells through this component.
    /// The legacy deterministic generator remains available only for explicit validation use.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class CubeRoomClusterGenerator : MonoBehaviour
    {
        public const int DefaultAdditionalRoomCount = 0;
        public const float PlacementTolerance = CubeRoom.AnchorAlignmentTolerance;

        private static readonly Vector2Int[] PrimaryAttachmentDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.left,
        };

        private static readonly Vector2Int[] AllAttachmentDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
        };

        [SerializeField] private CubeRoom primaryRoom;
        [SerializeField] private CubeRoom roomPrefab;
        [SerializeField, Min(0)] private int additionalRoomCount;

        private readonly List<CubeRoom> rooms = new();
        private readonly List<Vector2Int> gridCells = new();
        private readonly Dictionary<Vector2Int, CubeRoom> roomByCell = new();
        private readonly Dictionary<CubeRoom, Vector2Int> cellByRoom = new();
        private bool initialized;

        public CubeRoom PrimaryRoom => primaryRoom;
        public CubeRoom RoomPrefab => roomPrefab;
        public int AdditionalRoomCount => additionalRoomCount;
        public IReadOnlyList<CubeRoom> Rooms => rooms;
        public IReadOnlyList<Vector2Int> GridCells => gridCells;
        public int LastSeed { get; private set; }
        public event Action RoomsChanged;

        public void Configure(CubeRoom newPrimaryRoom, CubeRoom newRoomPrefab, int newAdditionalRoomCount = 0)
        {
            if (rooms.Count > 0)
            {
                ClearGeneratedRooms();
            }

            primaryRoom = newPrimaryRoom;
            roomPrefab = newRoomPrefab;
            additionalRoomCount = Mathf.Max(0, newAdditionalRoomCount);
            initialized = false;
        }

        public void InitializeSingleRoom()
        {
            ClearGeneratedRooms();
            if (primaryRoom == null)
            {
                Debug.LogError("Room layout requires an authored primary room.", this);
                return;
            }

            primaryRoom.ResetWallConnections();
            RegisterRoom(primaryRoom, Vector2Int.zero);
            initialized = true;
            RoomsChanged?.Invoke();
        }

        public bool TryGetCell(CubeRoom room, out Vector2Int cell)
        {
            EnsureInitialized();
            cell = default;
            return room != null && cellByRoom.TryGetValue(room, out cell);
        }

        public bool TryGetRoom(Vector2Int cell, out CubeRoom room)
        {
            EnsureInitialized();
            return roomByCell.TryGetValue(cell, out room) && room != null;
        }

        public bool IsRegistered(CubeRoom room)
        {
            EnsureInitialized();
            return room != null && cellByRoom.ContainsKey(room);
        }

        public bool TryGetPlacementCandidate(CubeRoom sourceRoom, CubeRoomWall sourceWall, out RoomPlacementCandidate candidate)
        {
            candidate = default;
            EnsureInitialized();
            if (sourceRoom == null
                || roomPrefab == null
                || !cellByRoom.TryGetValue(sourceRoom, out Vector2Int sourceCell)
                || sourceRoom.HasDoorway(sourceWall)
                || sourceRoom.GetConnectedRoom(sourceWall) != null)
            {
                return false;
            }

            if (Quaternion.Angle(sourceRoom.transform.rotation, primaryRoom.transform.rotation) > 0.01f)
            {
                return false;
            }

            Vector2Int targetCell = sourceCell + CubeRoom.GetGridDirection(sourceWall);
            if (roomByCell.ContainsKey(targetCell) || !AreNeighborFacesAvailable(targetCell))
            {
                return false;
            }

            Vector3 localOffset = new Vector3(targetCell.x * CubeRoom.InteriorWidth, 0f, targetCell.y * CubeRoom.InteriorDepth);
            RoomPlacementCandidate proposed = new RoomPlacementCandidate(
                sourceRoom,
                sourceWall,
                targetCell,
                primaryRoom.transform.TransformPoint(localOffset),
                primaryRoom.transform.rotation);
            if (!AnchorsAlign(proposed))
            {
                return false;
            }

            candidate = proposed;
            return true;
        }

        public bool TryPlaceRoom(CubeRoom sourceRoom, CubeRoomWall sourceWall, out CubeRoom placedRoom)
        {
            placedRoom = null;
            if (!TryGetPlacementCandidate(sourceRoom, sourceWall, out RoomPlacementCandidate candidate))
            {
                return false;
            }

            CubeRoom instance = null;
            try
            {
                instance = Instantiate(roomPrefab, candidate.Position, candidate.Rotation, transform);
                instance.name = $"Created Cube Room [{candidate.GridCell.x}, {candidate.GridCell.y}]";
                instance.GravityStrength = sourceRoom.GravityStrength;
                instance.ResetWallConnections();
                RegisterRoom(instance, candidate.GridCell);
                ConfigureSharedBoundaries();
                placedRoom = instance;
                RoomsChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null)
                {
                    UnregisterRoom(instance);
                    DestroyRoomObject(instance.gameObject);
                }

                ConfigureSharedBoundaries();
                Debug.LogException(exception, this);
                return false;
            }
        }

        public bool AnchorsAlign(RoomPlacementCandidate candidate)
        {
            if (!candidate.IsValid)
            {
                return false;
            }

            CubeRoomWall opposite = CubeRoom.GetOppositeWall(candidate.SourceWall);
            Matrix4x4 candidateMatrix = Matrix4x4.TRS(candidate.Position, candidate.Rotation, Vector3.one);
            for (int index = 0; index < 4; index++)
            {
                CubeRoomWallAnchor anchor = (CubeRoomWallAnchor)index;
                Vector3 sourcePoint = candidate.SourceRoom.GetWallAnchorWorld(candidate.SourceWall, anchor);
                Vector3 candidatePoint = candidateMatrix.MultiplyPoint3x4(candidate.SourceRoom.GetWallAnchorLocal(opposite, anchor));
                if (Vector3.Distance(sourcePoint, candidatePoint) > PlacementTolerance)
                {
                    return false;
                }
            }

            Vector3 sourceNormal = candidate.SourceRoom.GetWallNormalWorld(candidate.SourceWall).normalized;
            Vector3 candidateNormal = candidate.Rotation * CubeRoom.GetWallNormalLocal(opposite);
            return Vector3.Dot(sourceNormal, candidateNormal.normalized) < -0.9999f;
        }

        /// <summary>Explicit deterministic fixture generator; never called by normal startup.</summary>
        public void Generate()
        {
            Generate(CreateRuntimeSeed());
        }

        public void Generate(int seed)
        {
            if (primaryRoom == null || roomPrefab == null)
            {
                Debug.LogError("Cube room generation requires both a primary room and a room prefab.", this);
                return;
            }

            InitializeSingleRoom();
            LastSeed = seed;
            Vector2Int[] layout = CreateLayout(additionalRoomCount, seed);
            for (int index = 1; index < layout.Length; index++)
            {
                Vector2Int cell = layout[index];
                Vector3 localOffset = new Vector3(cell.x * CubeRoom.InteriorWidth, 0f, cell.y * CubeRoom.InteriorDepth);
                CubeRoom generatedRoom = Instantiate(roomPrefab, primaryRoom.transform.TransformPoint(localOffset), primaryRoom.transform.rotation, transform);
                generatedRoom.name = $"Generated Cube Room {index:00} [{cell.x}, {cell.y}]";
                generatedRoom.GravityStrength = primaryRoom.GravityStrength;
                generatedRoom.ResetWallConnections();
                RegisterRoom(generatedRoom, cell);
            }

            ConfigureSharedBoundaries();
            RoomsChanged?.Invoke();
        }

        public void ClearGeneratedRooms()
        {
            for (int index = rooms.Count - 1; index >= 0; index--)
            {
                CubeRoom room = rooms[index];
                if (room == null || room == primaryRoom)
                {
                    continue;
                }

                room.gameObject.SetActive(false);
                DestroyRoomObject(room.gameObject);
            }

            rooms.Clear();
            gridCells.Clear();
            roomByCell.Clear();
            cellByRoom.Clear();
            if (primaryRoom != null)
            {
                primaryRoom.ResetWallConnections();
            }

            initialized = false;
        }

        public static Vector2Int[] CreateLayout(int newAdditionalRoomCount, int seed)
        {
            if (newAdditionalRoomCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newAdditionalRoomCount), "Additional room count cannot be negative.");
            }

            List<Vector2Int> layout = new List<Vector2Int>(newAdditionalRoomCount + 1) { Vector2Int.zero };
            if (newAdditionalRoomCount == 0)
            {
                return layout.ToArray();
            }

            System.Random random = new System.Random(seed);
            layout.Add(PrimaryAttachmentDirections[random.Next(PrimaryAttachmentDirections.Length)]);
            List<Vector2Int> candidates = new List<Vector2Int>();
            while (layout.Count <= newAdditionalRoomCount)
            {
                candidates.Clear();
                foreach (Vector2Int occupiedCell in layout)
                {
                    foreach (Vector2Int direction in AllAttachmentDirections)
                    {
                        Vector2Int possibleCell = occupiedCell + direction;
                        if (!layout.Contains(possibleCell) && !candidates.Contains(possibleCell))
                        {
                            candidates.Add(possibleCell);
                        }
                    }
                }

                layout.Add(candidates[random.Next(candidates.Count)]);
            }

            return layout.ToArray();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void OnValidate()
        {
            additionalRoomCount = Mathf.Max(0, additionalRoomCount);
        }

        private void EnsureInitialized()
        {
            if (!initialized || rooms.Count == 0)
            {
                InitializeSingleRoom();
            }
        }

        private void RegisterRoom(CubeRoom room, Vector2Int cell)
        {
            rooms.Add(room);
            gridCells.Add(cell);
            roomByCell.Add(cell, room);
            cellByRoom.Add(room, cell);
        }

        private void UnregisterRoom(CubeRoom room)
        {
            if (!cellByRoom.TryGetValue(room, out Vector2Int cell))
            {
                return;
            }

            cellByRoom.Remove(room);
            roomByCell.Remove(cell);
            int index = rooms.IndexOf(room);
            if (index >= 0)
            {
                rooms.RemoveAt(index);
                gridCells.RemoveAt(index);
            }
        }

        private bool AreNeighborFacesAvailable(Vector2Int targetCell)
        {
            foreach (Vector2Int direction in AllAttachmentDirections)
            {
                if (!roomByCell.TryGetValue(targetCell + direction, out CubeRoom neighbor) || neighbor == null)
                {
                    continue;
                }

                CubeRoomWall neighborWall = WallFacing(-direction);
                if (neighbor.HasDoorway(neighborWall) || neighbor.GetConnectedRoom(neighborWall) != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void ConfigureSharedBoundaries()
        {
            foreach (CubeRoom room in rooms)
            {
                if (room != null)
                {
                    room.ResetWallConnections();
                }
            }

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                CubeRoom room = rooms[roomIndex];
                if (room == null)
                {
                    continue;
                }

                Vector2Int cell = gridCells[roomIndex];
                ConnectIfPresent(roomIndex, room, cell, Vector2Int.right);
                ConnectIfPresent(roomIndex, room, cell, Vector2Int.up);
            }
        }

        private void ConnectIfPresent(int roomIndex, CubeRoom room, Vector2Int cell, Vector2Int direction)
        {
            if (!roomByCell.TryGetValue(cell + direction, out CubeRoom neighbor) || neighbor == null)
            {
                return;
            }

            int neighborIndex = rooms.IndexOf(neighbor);
            CubeRoomWall roomWall = WallFacing(direction);
            CubeRoomWall neighborWall = CubeRoom.GetOppositeWall(roomWall);
            bool roomOwnsBoundary = roomIndex < neighborIndex;
            room.SetWallConnection(roomWall, neighbor, roomOwnsBoundary);
            neighbor.SetWallConnection(neighborWall, room, !roomOwnsBoundary);
        }

        private static CubeRoomWall WallFacing(Vector2Int direction)
        {
            if (direction == Vector2Int.left) return CubeRoomWall.West;
            if (direction == Vector2Int.right) return CubeRoomWall.East;
            if (direction == Vector2Int.down) return CubeRoomWall.South;
            if (direction == Vector2Int.up) return CubeRoomWall.North;
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Direction must be cardinal.");
        }

        private static void DestroyRoomObject(GameObject roomObject)
        {
            if (roomObject == null) return;
            if (Application.isPlaying) Destroy(roomObject);
            else DestroyImmediate(roomObject);
        }

        private static int CreateRuntimeSeed()
        {
            unchecked
            {
                int seed = Guid.NewGuid().GetHashCode();
                seed = (seed * 397) ^ Environment.TickCount;
                seed = (seed * 397) ^ (int)DateTime.UtcNow.Ticks;
                return seed;
            }
        }
    }
}
