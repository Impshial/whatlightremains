using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Grows a connected horizontal cluster from one authored primary room. The primary room
    /// remains fixed, while every generated room occupies a unique face-adjacent grid cell.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class CubeRoomClusterGenerator : MonoBehaviour
    {
        public const int DefaultAdditionalRoomCount = 3;

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
        [SerializeField, Min(0)] private int additionalRoomCount = DefaultAdditionalRoomCount;

        private readonly List<CubeRoom> rooms = new();
        private readonly List<Vector2Int> gridCells = new();
        private bool hasGenerated;

        public CubeRoom PrimaryRoom => primaryRoom;
        public CubeRoom RoomPrefab => roomPrefab;
        public int AdditionalRoomCount => additionalRoomCount;
        public IReadOnlyList<CubeRoom> Rooms => rooms;
        public IReadOnlyList<Vector2Int> GridCells => gridCells;
        public int LastSeed { get; private set; }

        public void Configure(CubeRoom newPrimaryRoom, CubeRoom newRoomPrefab, int newAdditionalRoomCount)
        {
            if (rooms.Count > 0)
            {
                ClearGeneratedRooms();
            }

            primaryRoom = newPrimaryRoom;
            roomPrefab = newRoomPrefab;
            additionalRoomCount = Mathf.Max(0, newAdditionalRoomCount);
            hasGenerated = false;
        }

        public void Generate()
        {
            Generate(CreateRuntimeSeed());
        }

        public void Generate(int seed)
        {
            if (primaryRoom == null || roomPrefab == null)
            {
                Debug.LogError(
                    "Cube room cluster generation requires both a primary room and a room prefab.",
                    this);
                return;
            }

            ClearGeneratedRooms();
            LastSeed = seed;
            hasGenerated = true;

            Vector2Int[] layout = CreateLayout(additionalRoomCount, seed);
            primaryRoom.ResetWallConnections();
            rooms.Add(primaryRoom);
            gridCells.Add(Vector2Int.zero);

            for (int index = 1; index < layout.Length; index++)
            {
                Vector2Int cell = layout[index];
                Vector3 localOffset = new Vector3(
                    cell.x * CubeRoom.InteriorWidth,
                    0f,
                    cell.y * CubeRoom.InteriorDepth);
                Vector3 worldPosition = primaryRoom.transform.TransformPoint(localOffset);

                CubeRoom generatedRoom = Instantiate(
                    roomPrefab,
                    worldPosition,
                    primaryRoom.transform.rotation,
                    transform);
                generatedRoom.name = $"Connected Cube Room {index:00} [{cell.x}, {cell.y}]";
                generatedRoom.ResetWallConnections();
                if (generatedRoom.Lighting != null)
                {
                    generatedRoom.Lighting.SupportingLightShadows = LightShadows.None;
                }
                rooms.Add(generatedRoom);
                gridCells.Add(cell);
            }

            ConfigureSharedBoundaries();

            string cells = string.Join(", ", gridCells.Select(cell => $"({cell.x},{cell.y})"));
            Debug.Log(
                $"Generated {rooms.Count}-room cube cluster with seed {LastSeed}: {cells}",
                this);
        }

        public void ClearGeneratedRooms()
        {
            for (int index = rooms.Count - 1; index >= 1; index--)
            {
                CubeRoom room = rooms[index];
                if (room == null)
                {
                    continue;
                }

                room.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(room.gameObject);
                }
                else
                {
                    DestroyImmediate(room.gameObject);
                }
            }

            rooms.Clear();
            gridCells.Clear();

            if (primaryRoom != null)
            {
                primaryRoom.ResetWallConnections();
            }
        }

        /// <summary>
        /// Returns a deterministic connected grid layout. Element zero is always the primary
        /// room at the origin. The first generated room uses one of the primary room's three
        /// presentation-facing walls; later rooms grow from any open cluster face.
        /// </summary>
        public static Vector2Int[] CreateLayout(int newAdditionalRoomCount, int seed)
        {
            if (newAdditionalRoomCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(newAdditionalRoomCount),
                    "Additional room count cannot be negative.");
            }

            List<Vector2Int> layout = new List<Vector2Int>(newAdditionalRoomCount + 1)
            {
                Vector2Int.zero,
            };

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
                        Vector2Int candidate = occupiedCell + direction;
                        if (!layout.Contains(candidate) && !candidates.Contains(candidate))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }

                layout.Add(candidates[random.Next(candidates.Count)]);
            }

            return layout.ToArray();
        }

        private void Start()
        {
            if (!hasGenerated)
            {
                Generate();
            }
        }

        private void OnValidate()
        {
            additionalRoomCount = Mathf.Max(0, additionalRoomCount);
        }

        private void ConfigureSharedBoundaries()
        {
            for (int firstIndex = 0; firstIndex < gridCells.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < gridCells.Count; secondIndex++)
                {
                    Vector2Int direction = gridCells[secondIndex] - gridCells[firstIndex];
                    if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
                    {
                        continue;
                    }

                    // Both rooms logically own a doorway on the connected face. The earlier
                    // room supplies the single segmented glass/frame/collider boundary while
                    // the later room suppresses its coplanar duplicate. The clear center is a
                    // real floor-level opening, so the player can traverse the seam.
                    CubeRoomWall earlierWall = WallFacing(direction);
                    CubeRoomWall laterWall = WallFacing(-direction);
                    rooms[firstIndex].SetWallConnection(earlierWall, true, true);
                    rooms[secondIndex].SetWallConnection(laterWall, true, false);
                }
            }
        }

        private static CubeRoomWall WallFacing(Vector2Int direction)
        {
            if (direction == Vector2Int.left)
            {
                return CubeRoomWall.West;
            }

            if (direction == Vector2Int.right)
            {
                return CubeRoomWall.East;
            }

            if (direction == Vector2Int.down)
            {
                return CubeRoomWall.South;
            }

            return CubeRoomWall.North;
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
