using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    public readonly struct RoomPlacementCandidate
    {
        private readonly RoomConnectionPlan[] connections;

        public RoomPlacementCandidate(CubeRoom sourceRoom, CubeRoomFace sourceFace, CubeRoomFace matingFace,
            Vector3Int gridCell, RoomOrientation orientation, Vector3 position, Quaternion rotation,
            RoomPassageKind passageKind, RoomCeilingEdge ceilingEdge, RoomConnectionPlan[] connections)
        {
            SourceRoom = sourceRoom;
            SourceFace = sourceFace;
            MatingFace = matingFace;
            GridCell3D = gridCell;
            Orientation = orientation;
            Position = position;
            Rotation = rotation;
            PassageKind = passageKind;
            CeilingEdge = ceilingEdge;
            this.connections = connections ?? Array.Empty<RoomConnectionPlan>();
        }

        public CubeRoom SourceRoom { get; }
        public CubeRoomFace SourceFace { get; }
        public CubeRoomWall SourceWall => CubeRoom.TryGetWall(SourceFace, out CubeRoomWall wall) ? wall : default;
        public CubeRoomFace MatingFace { get; }
        public Vector3Int GridCell3D { get; }
        public Vector2Int GridCell => new Vector2Int(GridCell3D.x, GridCell3D.z);
        public RoomOrientation Orientation { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public RoomPassageKind PassageKind { get; }
        public RoomCeilingEdge CeilingEdge { get; }
        public IReadOnlyList<RoomConnectionPlan> Connections => connections ?? Array.Empty<RoomConnectionPlan>();
        public bool IsValid => SourceRoom != null && PassageKind != RoomPassageKind.None;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class CubeRoomClusterGenerator : MonoBehaviour
    {
        public const int DefaultAdditionalRoomCount = 0;
        public const float PlacementTolerance = CubeRoom.AnchorAlignmentTolerance;
        private static readonly Vector2Int[] PrimaryAttachmentDirections = { Vector2Int.up, Vector2Int.right, Vector2Int.left };
        private static readonly Vector2Int[] AllAttachmentDirections = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private static readonly Vector3Int[] PositiveGridDirections = { Vector3Int.right, Vector3Int.up, new Vector3Int(0, 0, 1) };
        private static readonly Vector3Int[] AllGridDirections =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        [SerializeField] private CubeRoom primaryRoom;
        [SerializeField] private CubeRoom roomPrefab;
        [SerializeField, Min(0)] private int additionalRoomCount;
        private readonly List<CubeRoom> rooms = new List<CubeRoom>();
        private readonly List<Vector2Int> gridCells = new List<Vector2Int>();
        private readonly List<Vector3Int> gridCells3D = new List<Vector3Int>();
        private readonly Dictionary<Vector3Int, CubeRoom> roomByCell = new Dictionary<Vector3Int, CubeRoom>();
        private readonly Dictionary<CubeRoom, Vector3Int> cellByRoom = new Dictionary<CubeRoom, Vector3Int>();
        private readonly Dictionary<CubeRoom, RoomOrientation> orientationByRoom = new Dictionary<CubeRoom, RoomOrientation>();
        private bool initialized;

        public CubeRoom PrimaryRoom => primaryRoom;
        public CubeRoom RoomPrefab => roomPrefab;
        public int AdditionalRoomCount => additionalRoomCount;
        public IReadOnlyList<CubeRoom> Rooms => rooms;
        public IReadOnlyList<Vector2Int> GridCells => gridCells;
        public IReadOnlyList<Vector3Int> GridCells3D => gridCells3D;
        public int LastSeed { get; private set; }
        public event Action RoomsChanged;

        public void Configure(CubeRoom newPrimaryRoom, CubeRoom newRoomPrefab, int newAdditionalRoomCount = 0)
        {
            if (rooms.Count > 0) ClearGeneratedRooms();
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
            primaryRoom.ResetFaceConnections();
            RegisterRoom(primaryRoom, Vector3Int.zero, RoomOrientation.Identity);
            initialized = true;
            RoomsChanged?.Invoke();
        }

        public bool TryGetCell(CubeRoom room, out Vector2Int cell)
        {
            bool found = TryGetCell3D(room, out Vector3Int cell3D);
            cell = new Vector2Int(cell3D.x, cell3D.z);
            return found;
        }

        public bool TryGetCell3D(CubeRoom room, out Vector3Int cell)
        {
            EnsureInitialized();
            cell = default;
            return room != null && cellByRoom.TryGetValue(room, out cell);
        }

        public bool TryGetRoom(Vector2Int cell, out CubeRoom room) => TryGetRoom(new Vector3Int(cell.x, 0, cell.y), out room);

        public bool TryGetRoom(Vector3Int cell, out CubeRoom room)
        {
            EnsureInitialized();
            return roomByCell.TryGetValue(cell, out room) && room != null;
        }

        public bool TryGetOrientation(CubeRoom room, out RoomOrientation orientation)
        {
            EnsureInitialized();
            return orientationByRoom.TryGetValue(room, out orientation);
        }

        public bool IsRegistered(CubeRoom room)
        {
            EnsureInitialized();
            return room != null && cellByRoom.ContainsKey(room);
        }

        public bool TryGetPlacementCandidate(CubeRoom sourceRoom, CubeRoomWall sourceWall, out RoomPlacementCandidate candidate)
        {
            candidate = default;
            if (!TryGetOrientation(sourceRoom, out RoomOrientation orientation)) return false;
            return TryGetPlacementCandidate(sourceRoom, CubeRoom.ToFace(sourceWall), orientation, out candidate);
        }

        public bool TryGetPlacementCandidate(CubeRoom sourceRoom, CubeRoomFace sourceFace,
            RoomOrientation orientation, out RoomPlacementCandidate candidate)
        {
            candidate = default;
            EnsureInitialized();
            if (sourceRoom == null || sourceFace == CubeRoomFace.Floor || roomPrefab == null
                || !cellByRoom.TryGetValue(sourceRoom, out Vector3Int sourceCell)
                || !orientationByRoom.TryGetValue(sourceRoom, out RoomOrientation sourceOrientation)
                || sourceRoom.GetConnectedRoom(sourceFace) != null) return false;

            Vector3Int sourceDirection = sourceOrientation.TransformDirection(CubeRoom.GetGridDirection(sourceFace));
            Vector3Int targetCell = sourceCell + sourceDirection;
            if (roomByCell.ContainsKey(targetCell)) return false;
            CubeRoomFace matingFace = FaceInGridDirection(orientation, -sourceDirection);
            if (!TryClassifyPassage(sourceFace, sourceOrientation, matingFace, orientation, out RoomPassageKind primaryPassage)) return false;
            if (!TryBuildConnectionPlan(targetCell, orientation, sourceRoom, sourceFace, out RoomConnectionPlan[] plans)) return false;
            RoomConnectionPlan primaryPlan = plans.FirstOrDefault(plan => plan.Neighbor == sourceRoom);
            if (primaryPlan.Neighbor == null || primaryPlan.PassageKind != primaryPassage) return false;

            Vector3 position = GetRoomRootPosition(targetCell, orientation);
            Quaternion rotation = primaryRoom.transform.rotation * orientation.Rotation;
            RoomPlacementCandidate proposed = new RoomPlacementCandidate(sourceRoom, sourceFace, matingFace,
                targetCell, orientation, position, rotation, primaryPlan.PassageKind, primaryPlan.CeilingEdge, plans);
            if (!AnchorsAlign(proposed)) return false;
            candidate = proposed;
            return true;
        }

        public bool TryPlaceRoom(CubeRoom sourceRoom, CubeRoomWall sourceWall, out CubeRoom placedRoom)
        {
            placedRoom = null;
            return TryGetPlacementCandidate(sourceRoom, sourceWall, out RoomPlacementCandidate candidate)
                && TryPlaceRoom(candidate, out placedRoom);
        }

        public bool TryPlaceRoom(RoomPlacementCandidate candidate, out CubeRoom placedRoom)
        {
            placedRoom = null;
            if (!candidate.IsValid
                || !TryGetPlacementCandidate(candidate.SourceRoom, candidate.SourceFace, candidate.Orientation, out RoomPlacementCandidate current)
                || current.GridCell3D != candidate.GridCell3D
                || Vector3.Distance(current.Position, candidate.Position) > PlacementTolerance
                || Quaternion.Angle(current.Rotation, candidate.Rotation) > 0.01f) return false;

            CubeRoom instance = null;
            try
            {
                instance = Instantiate(roomPrefab, current.Position, current.Rotation, transform);
                instance.name = $"Created Cube Room [{current.GridCell3D.x}, {current.GridCell3D.y}, {current.GridCell3D.z}]";
                instance.GravityStrength = candidate.SourceRoom.GravityStrength;
                instance.ResetFaceConnections();
                RegisterRoom(instance, current.GridCell3D, current.Orientation);
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
            if (!candidate.IsValid) return false;
            Matrix4x4 candidateMatrix = Matrix4x4.TRS(candidate.Position, candidate.Rotation, Vector3.one);
            bool[] matched = new bool[4];
            for (int sourceIndex = 0; sourceIndex < 4; sourceIndex++)
            {
                Vector3 sourcePoint = candidate.SourceRoom.GetFaceAnchorWorld(candidate.SourceFace, (CubeRoomWallAnchor)sourceIndex);
                bool found = false;
                for (int targetIndex = 0; targetIndex < 4; targetIndex++)
                {
                    if (matched[targetIndex]) continue;
                    Vector3 targetPoint = candidateMatrix.MultiplyPoint3x4(
                        candidate.SourceRoom.GetFaceAnchorLocal(candidate.MatingFace, (CubeRoomWallAnchor)targetIndex));
                    if (Vector3.Distance(sourcePoint, targetPoint) <= PlacementTolerance)
                    {
                        matched[targetIndex] = true;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            Vector3 sourceNormal = candidate.SourceRoom.GetFaceNormalWorld(candidate.SourceFace).normalized;
            Vector3 candidateNormal = candidate.Rotation * CubeRoom.GetFaceNormalLocal(candidate.MatingFace);
            return Vector3.Dot(sourceNormal, candidateNormal.normalized) < -0.9999f;
        }

        public Vector3 GetCellCenterWorld(Vector3Int cell)
        {
            EnsureInitialized();
            Vector3 localCenter = new Vector3(cell.x * CubeRoom.InteriorWidth,
                CubeRoom.InteriorHeight * 0.5f + cell.y * CubeRoom.InteriorHeight,
                cell.z * CubeRoom.InteriorDepth);
            return primaryRoom.transform.TransformPoint(localCenter);
        }

        public Vector3 GetRoomRootPosition(Vector3Int cell, RoomOrientation orientation)
        {
            Vector3 center = GetCellCenterWorld(cell);
            Quaternion rotation = primaryRoom.transform.rotation * orientation.Rotation;
            return center - rotation * new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f);
        }

        public void Generate() => Generate(CreateRuntimeSeed());

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
                Vector3Int cell = new Vector3Int(layout[index].x, 0, layout[index].y);
                CubeRoom generatedRoom = Instantiate(roomPrefab, GetRoomRootPosition(cell, RoomOrientation.Identity), primaryRoom.transform.rotation, transform);
                generatedRoom.name = $"Generated Cube Room {index:00} [{cell.x}, {cell.z}]";
                generatedRoom.GravityStrength = primaryRoom.GravityStrength;
                generatedRoom.ResetFaceConnections();
                RegisterRoom(generatedRoom, cell, RoomOrientation.Identity);
            }
            ConfigureSharedBoundaries();
            RoomsChanged?.Invoke();
        }

        public void ClearGeneratedRooms()
        {
            for (int index = rooms.Count - 1; index >= 0; index--)
            {
                CubeRoom room = rooms[index];
                if (room == null || room == primaryRoom) continue;
                room.gameObject.SetActive(false);
                DestroyRoomObject(room.gameObject);
            }
            rooms.Clear(); gridCells.Clear(); gridCells3D.Clear(); roomByCell.Clear(); cellByRoom.Clear(); orientationByRoom.Clear();
            if (primaryRoom != null) primaryRoom.ResetFaceConnections();
            initialized = false;
        }

        public static Vector2Int[] CreateLayout(int newAdditionalRoomCount, int seed)
        {
            if (newAdditionalRoomCount < 0) throw new ArgumentOutOfRangeException(nameof(newAdditionalRoomCount));
            List<Vector2Int> layout = new List<Vector2Int>(newAdditionalRoomCount + 1) { Vector2Int.zero };
            if (newAdditionalRoomCount == 0) return layout.ToArray();
            System.Random random = new System.Random(seed);
            layout.Add(PrimaryAttachmentDirections[random.Next(PrimaryAttachmentDirections.Length)]);
            List<Vector2Int> candidates = new List<Vector2Int>();
            while (layout.Count <= newAdditionalRoomCount)
            {
                candidates.Clear();
                foreach (Vector2Int occupiedCell in layout)
                    foreach (Vector2Int direction in AllAttachmentDirections)
                    {
                        Vector2Int possibleCell = occupiedCell + direction;
                        if (!layout.Contains(possibleCell) && !candidates.Contains(possibleCell)) candidates.Add(possibleCell);
                    }
                layout.Add(candidates[random.Next(candidates.Count)]);
            }
            return layout.ToArray();
        }

        private void Start() => EnsureInitialized();
        private void OnValidate() => additionalRoomCount = Mathf.Max(0, additionalRoomCount);
        private void EnsureInitialized() { if (!initialized || rooms.Count == 0) InitializeSingleRoom(); }

        private void RegisterRoom(CubeRoom room, Vector3Int cell, RoomOrientation orientation)
        {
            rooms.Add(room); gridCells.Add(new Vector2Int(cell.x, cell.z)); gridCells3D.Add(cell);
            roomByCell.Add(cell, room); cellByRoom.Add(room, cell); orientationByRoom.Add(room, orientation);
        }

        private void UnregisterRoom(CubeRoom room)
        {
            if (!cellByRoom.TryGetValue(room, out Vector3Int cell)) return;
            cellByRoom.Remove(room); orientationByRoom.Remove(room); roomByCell.Remove(cell);
            int index = rooms.IndexOf(room);
            if (index < 0) return;
            rooms.RemoveAt(index); gridCells.RemoveAt(index); gridCells3D.RemoveAt(index);
        }

        private bool TryBuildConnectionPlan(Vector3Int targetCell, RoomOrientation candidateOrientation,
            CubeRoom requiredSource, CubeRoomFace requiredSourceFace, out RoomConnectionPlan[] plans)
        {
            List<RoomConnectionPlan> result = new List<RoomConnectionPlan>(6);
            foreach (Vector3Int direction in AllGridDirections)
            {
                if (!roomByCell.TryGetValue(targetCell + direction, out CubeRoom neighbor) || neighbor == null) continue;
                RoomOrientation neighborOrientation = orientationByRoom[neighbor];
                CubeRoomFace candidateFace = FaceInGridDirection(candidateOrientation, direction);
                CubeRoomFace neighborFace = FaceInGridDirection(neighborOrientation, -direction);
                if (neighbor.GetConnectedRoom(neighborFace) != null
                    || !TryClassifyPassage(neighborFace, neighborOrientation, candidateFace, candidateOrientation, out RoomPassageKind passage))
                {
                    plans = Array.Empty<RoomConnectionPlan>();
                    return false;
                }
                RoomCeilingEdge edge = GetCeilingEdge(neighborFace, neighborOrientation, candidateFace, candidateOrientation);
                result.Add(new RoomConnectionPlan(neighbor, neighborFace, candidateFace, passage, edge));
            }
            RoomConnectionPlan sourcePlan = result.FirstOrDefault(plan => plan.Neighbor == requiredSource);
            if (sourcePlan.Neighbor == null || sourcePlan.NeighborFace != requiredSourceFace)
            {
                plans = Array.Empty<RoomConnectionPlan>(); return false;
            }
            plans = result.ToArray(); return true;
        }

        private void ConfigureSharedBoundaries()
        {
            foreach (CubeRoom room in rooms) if (room != null) room.ResetFaceConnections();
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                CubeRoom room = rooms[roomIndex]; if (room == null) continue;
                Vector3Int cell = gridCells3D[roomIndex];
                foreach (Vector3Int direction in PositiveGridDirections)
                {
                    if (!roomByCell.TryGetValue(cell + direction, out CubeRoom neighbor) || neighbor == null) continue;
                    RoomOrientation roomOrientation = orientationByRoom[room];
                    RoomOrientation neighborOrientation = orientationByRoom[neighbor];
                    CubeRoomFace roomFace = FaceInGridDirection(roomOrientation, direction);
                    CubeRoomFace neighborFace = FaceInGridDirection(neighborOrientation, -direction);
                    if (!TryClassifyPassage(roomFace, roomOrientation, neighborFace, neighborOrientation, out RoomPassageKind passage)) continue;
                    RoomCeilingEdge edge = GetCeilingEdge(roomFace, roomOrientation, neighborFace, neighborOrientation);
                    bool roomOwns = DetermineBoundaryOwner(roomIndex, rooms.IndexOf(neighbor), roomFace, neighborFace, passage);
                    room.SetFaceConnection(roomFace, neighbor, neighborFace, passage, edge, roomOwns);
                    neighbor.SetFaceConnection(neighborFace, room, roomFace, passage, edge, !roomOwns);
                }
            }
        }

        private static bool DetermineBoundaryOwner(int firstIndex, int secondIndex, CubeRoomFace firstFace, CubeRoomFace secondFace, RoomPassageKind passage)
        {
            if (passage == RoomPassageKind.CeilingToSideDoorway) return firstFace == CubeRoomFace.Ceiling;
            if (passage == RoomPassageKind.Sealed)
            {
                if (firstFace == CubeRoomFace.Floor) return true;
                if (secondFace == CubeRoomFace.Floor) return false;
            }
            return firstIndex < secondIndex;
        }

        private static bool TryClassifyPassage(CubeRoomFace firstFace, RoomOrientation firstOrientation,
            CubeRoomFace secondFace, RoomOrientation secondOrientation, out RoomPassageKind passage)
        {
            if (IsSide(firstFace) && IsSide(secondFace))
            {
                // Floor-level side doorways line up only when both rooms agree on up.
                // Other cardinal orientations still form a valid neighboring cell, but
                // their shared face must remain sealed.
                passage = firstOrientation.Up == secondOrientation.Up
                    ? RoomPassageKind.SideDoorway
                    : RoomPassageKind.Sealed;
                return true;
            }
            if ((firstFace == CubeRoomFace.Ceiling && IsSide(secondFace)) || (secondFace == CubeRoomFace.Ceiling && IsSide(firstFace)))
            {
                passage = RoomPassageKind.CeilingToSideDoorway; return true;
            }

            // Every other cardinal full-face contact is geometrically valid but has no
            // compatible doorway implementation. Keeping it sealed lets all four Z-axis
            // preview orientations remain visible and placeable beside an existing room.
            passage = RoomPassageKind.Sealed;
            return true;
        }

        private static RoomCeilingEdge GetCeilingEdge(CubeRoomFace firstFace, RoomOrientation firstOrientation,
            CubeRoomFace secondFace, RoomOrientation secondOrientation)
        {
            RoomOrientation ceilingOrientation; RoomOrientation sideOrientation;
            if (firstFace == CubeRoomFace.Ceiling && IsSide(secondFace)) { ceilingOrientation = firstOrientation; sideOrientation = secondOrientation; }
            else if (secondFace == CubeRoomFace.Ceiling && IsSide(firstFace)) { ceilingOrientation = secondOrientation; sideOrientation = firstOrientation; }
            else return RoomCeilingEdge.None;
            Vector3Int edgeDirectionLocal = ceilingOrientation.InverseTransformDirection(-sideOrientation.Up);
            if (edgeDirectionLocal == Vector3Int.left) return RoomCeilingEdge.West;
            if (edgeDirectionLocal == Vector3Int.right) return RoomCeilingEdge.East;
            if (edgeDirectionLocal == new Vector3Int(0, 0, -1)) return RoomCeilingEdge.South;
            if (edgeDirectionLocal == new Vector3Int(0, 0, 1)) return RoomCeilingEdge.North;
            return RoomCeilingEdge.None;
        }

        private static CubeRoomFace FaceInGridDirection(RoomOrientation orientation, Vector3Int gridDirection)
        {
            Vector3Int local = orientation.InverseTransformDirection(gridDirection);
            if (local == Vector3Int.left) return CubeRoomFace.West;
            if (local == Vector3Int.right) return CubeRoomFace.East;
            if (local == Vector3Int.down) return CubeRoomFace.Floor;
            if (local == Vector3Int.up) return CubeRoomFace.Ceiling;
            if (local == new Vector3Int(0, 0, -1)) return CubeRoomFace.South;
            if (local == new Vector3Int(0, 0, 1)) return CubeRoomFace.North;
            throw new ArgumentOutOfRangeException(nameof(gridDirection));
        }

        private static bool IsSide(CubeRoomFace face) => (int)face >= 0 && (int)face < 4;
        private static void DestroyRoomObject(GameObject roomObject)
        {
            if (roomObject == null) return;
            if (Application.isPlaying) Destroy(roomObject); else DestroyImmediate(roomObject);
        }
        private static int CreateRuntimeSeed()
        {
            unchecked
            {
                int seed = Guid.NewGuid().GetHashCode(); seed = (seed * 397) ^ Environment.TickCount;
                seed = (seed * 397) ^ (int)DateTime.UtcNow.Ticks; return seed;
            }
        }
    }
}
