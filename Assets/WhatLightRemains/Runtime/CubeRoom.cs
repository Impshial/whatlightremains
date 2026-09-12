using UnityEngine;
using System;

namespace WhatLightRemains.Runtime
{
    public enum CubeRoomWall
    {
        West = 0,
        East = 1,
        South = 2,
        North = 3,
    }

    public enum CubeRoomWallAnchor
    {
        LowerFirst = 0,
        LowerSecond = 1,
        UpperFirst = 2,
        UpperSecond = 3,
    }

    [DisallowMultipleComponent]
    public sealed class CubeRoom : MonoBehaviour
    {
        public const float InteriorWidth = 8f;
        public const float InteriorHeight = 8f;
        public const float InteriorDepth = 8f;
        public const float DoorwayWidth = 2f;
        public const float DoorwayHeight = 2.4f;
        public const float AnchorAlignmentTolerance = 0.001f;
        public const float StandardGravityStrength = 9.7536f; // Exactly 32 ft/s² in metres.

        [SerializeField, Min(0f)] private float gravityStrength = StandardGravityStrength;
        [SerializeField] private bool powerOn = true;
        [SerializeField] private CubeRoomLighting lighting;
        [SerializeField] private DeviceWall deviceWall;
        [SerializeField] private RoomOccupancyVolume occupancyVolume;
        [SerializeField] private Renderer[] wallGlassRenderers = new Renderer[4];
        [SerializeField] private Collider[] wallBoundaryColliders = new Collider[4];
        [SerializeField] private CubeRoomWallBoundary[] wallBoundaries = new CubeRoomWallBoundary[4];
        [SerializeField, HideInInspector] private CubeRoom[] connectedRooms = new CubeRoom[4];
        [SerializeField] private CubeRoomCeilingBoundary ceilingBoundary;
        [SerializeField, HideInInspector] private RoomFaceConnection[] faceConnections = new RoomFaceConnection[6];

        public float GravityStrength
        {
            get => gravityStrength;
            set => gravityStrength = Mathf.Max(0f, value);
        }

        public Vector3 RoomUp => transform.up;
        public Vector3 GravityAcceleration => -RoomUp * gravityStrength;
        public CubeRoomLighting Lighting => lighting;
        public DeviceWall DeviceWall => deviceWall;
        public RoomOccupancyVolume OccupancyVolume => occupancyVolume;
        public Vector3 InteriorSize => new Vector3(InteriorWidth, InteriorHeight, InteriorDepth);
        public CubeRoomCeilingBoundary CeilingBoundary => ceilingBoundary;
        public bool PowerOn => powerOn;
        public event Action<CubeRoom, bool> PowerChanged;
        public event Action<CubeRoom> LightingStateChanged;

        public void SetPower(bool on)
        {
            if (powerOn == on)
            {
                if (lighting != null) lighting.SetPowerState(powerOn);
                LightingStateChanged?.Invoke(this);
                return;
            }
            powerOn = on;
            if (lighting != null) lighting.SetPowerState(powerOn);
            PowerChanged?.Invoke(this, powerOn);
            LightingStateChanged?.Invoke(this);
        }

        public void TogglePower() => SetPower(!powerOn);

        public void Configure(
            float newGravityStrength,
            CubeRoomLighting newLighting,
            RoomOccupancyVolume newOccupancyVolume)
        {
            GravityStrength = newGravityStrength;
            lighting = newLighting;
            occupancyVolume = newOccupancyVolume;

            if (occupancyVolume != null)
            {
                occupancyVolume.Configure(this);
            }
            if (lighting != null) lighting.SetPowerState(powerOn);
            deviceWall ??= GetComponent<DeviceWall>();
        }

        public void ConfigureDeviceWall(DeviceWall wall)
        {
            deviceWall = wall;
            if (deviceWall != null) deviceWall.ConfigureOwner(this);
        }

        public void SetLightingEnabled(bool enabled)
        {
            if (lighting != null)
            {
                lighting.SetLightingEnabled(enabled);
                LightingStateChanged?.Invoke(this);
            }
        }

        public void ConfigureWallGlass(
            Renderer west,
            Renderer east,
            Renderer south,
            Renderer north)
        {
            wallGlassRenderers = new[] { west, east, south, north };
        }

        public void ConfigureWallBoundaryColliders(
            Collider west,
            Collider east,
            Collider south,
            Collider north)
        {
            wallBoundaryColliders = new[] { west, east, south, north };
        }

        public void ConfigureWallBoundaries(
            CubeRoomWallBoundary west,
            CubeRoomWallBoundary east,
            CubeRoomWallBoundary south,
            CubeRoomWallBoundary north)
        {
            wallBoundaries = new[] { west, east, south, north };
            SynchronizeLegacyWallReferences();
        }

        public CubeRoomWallBoundary GetWallBoundary(CubeRoomWall wall)
        {
            int index = (int)wall;
            if (wallBoundaries == null || index < 0 || index >= wallBoundaries.Length)
            {
                return null;
            }

            return wallBoundaries[index];
        }

        public bool HasDoorway(CubeRoomWall wall)
        {
            CubeRoomWallBoundary boundary = GetWallBoundary(wall);
            return boundary != null && boundary.HasDoorway;
        }

        public CubeRoom GetConnectedRoom(CubeRoomWall wall)
        {
            int index = (int)wall;
            if (connectedRooms == null || index < 0 || index >= connectedRooms.Length)
            {
                return null;
            }

            return connectedRooms[index];
        }

        public CubeRoom GetConnectedRoom(CubeRoomFace face)
        {
            EnsureFaceConnectionArray();
            return faceConnections[(int)face].Neighbor;
        }

        public RoomFaceConnection GetConnection(CubeRoomFace face)
        {
            EnsureFaceConnectionArray();
            return faceConnections[(int)face];
        }

        public bool HasDoorway(CubeRoomFace face)
        {
            return GetConnection(face).IsTraversable;
        }

        public void SetFaceConnection(
            CubeRoomFace face,
            CubeRoom neighbor,
            CubeRoomFace neighborFace,
            RoomPassageKind passageKind,
            RoomCeilingEdge ceilingEdge,
            bool ownsSharedBoundary)
        {
            SetFaceConnection(face, neighbor, neighborFace, passageKind, ceilingEdge, ownsSharedBoundary, default);
        }

        public void SetFaceConnection(
            CubeRoomFace face,
            CubeRoom neighbor,
            CubeRoomFace neighborFace,
            RoomPassageKind passageKind,
            RoomCeilingEdge ceilingEdge,
            bool ownsSharedBoundary,
            RoomAperture aperture)
        {
            EnsureFaceConnectionArray();
            faceConnections[(int)face] = new RoomFaceConnection(
                neighbor,
                neighborFace,
                passageKind,
                ceilingEdge,
                ownsSharedBoundary,
                aperture);

            if (TryGetWall(face, out CubeRoomWall wall))
            {
                EnsureConnectionArray();
                connectedRooms[(int)wall] = neighbor;
                CubeRoomWallBoundary boundary = GetWallBoundary(wall);
                if (boundary != null)
                {
                    bool isConnected = neighbor != null && passageKind != RoomPassageKind.None;
                    boundary.SetConnectionState(
                        isConnected && passageKind != RoomPassageKind.Sealed,
                        ownsSharedBoundary,
                        isConnected);
                }
                else
                {
                    SetLegacyWallEnabled(wall, neighbor == null);
                }
            }
            else if (face == CubeRoomFace.Ceiling && ceilingBoundary != null)
            {
                ceilingBoundary.SetConnectionState(neighbor != null, passageKind, ceilingEdge, ownsSharedBoundary);
            }
        }

        public void SetWallConnection(CubeRoomWall wall, CubeRoom neighbor, bool ownsSharedBoundary)
        {
            SetFaceConnection(
                ToFace(wall),
                neighbor,
                ToFace(GetOppositeWall(wall)),
                neighbor != null ? RoomPassageKind.SideDoorway : RoomPassageKind.None,
                RoomCeilingEdge.None,
                ownsSharedBoundary);
        }

        public void SetWallConnection(CubeRoomWall wall, bool connected, bool ownsSharedBoundary)
        {
            EnsureConnectionArray();
            if (!connected)
            {
                connectedRooms[(int)wall] = null;
            }

            CubeRoomWallBoundary boundary = GetWallBoundary(wall);
            if (boundary != null)
            {
                boundary.SetConnectionState(connected, ownsSharedBoundary);
                return;
            }

            // A legacy prefab without segmented doorway geometry cannot form a correctly sized
            // opening, but removing its single pane and collider keeps traversal functional until
            // the generated asset is rebuilt by FoundationBuilder.
            SetLegacyWallEnabled(wall, !connected);
        }

        public void ResetWallConnections()
        {
            ResetFaceConnections();
        }

        public void ResetFaceConnections()
        {
            EnsureFaceConnectionArray();
            for (int index = 0; index < 4; index++)
            {
                CubeRoomWallBoundary boundary = GetWallBoundary((CubeRoomWall)index);
                if (boundary != null)
                {
                    boundary.ResetConnectionState();
                }
                else
                {
                    SetLegacyWallEnabled((CubeRoomWall)index, true);
                }

                EnsureConnectionArray();
                connectedRooms[index] = null;
            }

            for (int index = 0; index < faceConnections.Length; index++)
            {
                faceConnections[index] = default;
            }

            if (ceilingBoundary != null)
            {
                ceilingBoundary.ResetConnectionState();
            }
        }

        public void ConfigureCeilingBoundary(CubeRoomCeilingBoundary boundary)
        {
            ceilingBoundary = boundary;
        }

        private void Awake()
        {
            if (lighting != null) lighting.SetPowerState(powerOn);
            deviceWall ??= GetComponent<DeviceWall>();
        }

        public Vector3 GetFaceAnchorLocal(CubeRoomFace face, CubeRoomWallAnchor anchor)
        {
            if (TryGetWall(face, out CubeRoomWall wall))
            {
                return GetWallAnchorLocal(wall, anchor);
            }

            bool second = anchor == CubeRoomWallAnchor.LowerSecond || anchor == CubeRoomWallAnchor.UpperSecond;
            bool upper = anchor == CubeRoomWallAnchor.UpperFirst || anchor == CubeRoomWallAnchor.UpperSecond;
            float x = second ? InteriorWidth * 0.5f : -InteriorWidth * 0.5f;
            float z = upper ? InteriorDepth * 0.5f : -InteriorDepth * 0.5f;
            return new Vector3(x, face == CubeRoomFace.Ceiling ? InteriorHeight : 0f, z);
        }

        public Vector3 GetFaceAnchorWorld(CubeRoomFace face, CubeRoomWallAnchor anchor)
        {
            return transform.TransformPoint(GetFaceAnchorLocal(face, anchor));
        }

        public Vector3 GetFaceNormalWorld(CubeRoomFace face)
        {
            return transform.TransformDirection(GetFaceNormalLocal(face));
        }

        public Vector3 GetWallAnchorLocal(CubeRoomWall wall, CubeRoomWallAnchor anchor)
        {
            bool upper = anchor == CubeRoomWallAnchor.UpperFirst
                || anchor == CubeRoomWallAnchor.UpperSecond;
            bool second = anchor == CubeRoomWallAnchor.LowerSecond
                || anchor == CubeRoomWallAnchor.UpperSecond;
            float height = upper ? InteriorHeight : 0f;
            float along = second ? InteriorWidth * 0.5f : -InteriorWidth * 0.5f;

            return wall switch
            {
                CubeRoomWall.West => new Vector3(-InteriorWidth * 0.5f, height, along),
                CubeRoomWall.East => new Vector3(InteriorWidth * 0.5f, height, along),
                CubeRoomWall.South => new Vector3(along, height, -InteriorDepth * 0.5f),
                CubeRoomWall.North => new Vector3(along, height, InteriorDepth * 0.5f),
                _ => throw new System.ArgumentOutOfRangeException(nameof(wall), wall, null),
            };
        }

        public Vector3 GetWallAnchorWorld(CubeRoomWall wall, CubeRoomWallAnchor anchor)
        {
            return transform.TransformPoint(GetWallAnchorLocal(wall, anchor));
        }

        public Vector3 GetWallNormalWorld(CubeRoomWall wall)
        {
            return transform.TransformDirection(GetWallNormalLocal(wall));
        }

        public static Vector3 GetWallNormalLocal(CubeRoomWall wall)
        {
            return wall switch
            {
                CubeRoomWall.West => Vector3.left,
                CubeRoomWall.East => Vector3.right,
                CubeRoomWall.South => Vector3.back,
                CubeRoomWall.North => Vector3.forward,
                _ => throw new System.ArgumentOutOfRangeException(nameof(wall), wall, null),
            };
        }

        public static Vector3 GetFaceNormalLocal(CubeRoomFace face)
        {
            return face switch
            {
                CubeRoomFace.West => Vector3.left,
                CubeRoomFace.East => Vector3.right,
                CubeRoomFace.South => Vector3.back,
                CubeRoomFace.North => Vector3.forward,
                CubeRoomFace.Floor => Vector3.down,
                CubeRoomFace.Ceiling => Vector3.up,
                _ => throw new System.ArgumentOutOfRangeException(nameof(face), face, null),
            };
        }

        public static Vector3Int GetGridDirection(CubeRoomFace face)
        {
            Vector3 normal = GetFaceNormalLocal(face);
            return Vector3Int.RoundToInt(normal);
        }

        public static CubeRoomFace GetOppositeFace(CubeRoomFace face)
        {
            return face switch
            {
                CubeRoomFace.West => CubeRoomFace.East,
                CubeRoomFace.East => CubeRoomFace.West,
                CubeRoomFace.South => CubeRoomFace.North,
                CubeRoomFace.North => CubeRoomFace.South,
                CubeRoomFace.Floor => CubeRoomFace.Ceiling,
                CubeRoomFace.Ceiling => CubeRoomFace.Floor,
                _ => throw new System.ArgumentOutOfRangeException(nameof(face), face, null),
            };
        }

        public static CubeRoomFace ToFace(CubeRoomWall wall) => (CubeRoomFace)(int)wall;

        public static bool TryGetWall(CubeRoomFace face, out CubeRoomWall wall)
        {
            if ((int)face >= 0 && (int)face < 4)
            {
                wall = (CubeRoomWall)(int)face;
                return true;
            }

            wall = default;
            return false;
        }

        public static Vector2Int GetGridDirection(CubeRoomWall wall)
        {
            return wall switch
            {
                CubeRoomWall.West => Vector2Int.left,
                CubeRoomWall.East => Vector2Int.right,
                CubeRoomWall.South => Vector2Int.down,
                CubeRoomWall.North => Vector2Int.up,
                _ => throw new System.ArgumentOutOfRangeException(nameof(wall), wall, null),
            };
        }

        public static CubeRoomWall GetOppositeWall(CubeRoomWall wall)
        {
            return wall switch
            {
                CubeRoomWall.West => CubeRoomWall.East,
                CubeRoomWall.East => CubeRoomWall.West,
                CubeRoomWall.South => CubeRoomWall.North,
                CubeRoomWall.North => CubeRoomWall.South,
                _ => throw new System.ArgumentOutOfRangeException(nameof(wall), wall, null),
            };
        }

        public Renderer GetWallGlassRenderer(CubeRoomWall wall)
        {
            CubeRoomWallBoundary boundary = GetWallBoundary(wall);
            if (boundary != null && boundary.ClosedGlassRenderer != null)
            {
                return boundary.ClosedGlassRenderer;
            }

            int index = (int)wall;
            if (wallGlassRenderers == null || index < 0 || index >= wallGlassRenderers.Length)
            {
                return null;
            }

            return wallGlassRenderers[index];
        }

        public void SetWallGlassVisible(CubeRoomWall wall, bool visible)
        {
            CubeRoomWallBoundary boundary = GetWallBoundary(wall);
            if (boundary != null)
            {
                boundary.SetVisualsEnabled(visible);
                return;
            }

            Renderer wallRenderer = GetWallGlassRenderer(wall);
            if (wallRenderer != null)
            {
                wallRenderer.enabled = visible;
            }
        }

        public void SetAllWallGlassVisible(bool visible)
        {
            for (int index = 0; index < 4; index++)
            {
                SetWallGlassVisible((CubeRoomWall)index, visible);
            }
        }

        public Collider GetWallBoundaryCollider(CubeRoomWall wall)
        {
            CubeRoomWallBoundary boundary = GetWallBoundary(wall);
            if (boundary != null && boundary.ClosedCollider != null)
            {
                return boundary.ClosedCollider;
            }

            int index = (int)wall;
            if (wallBoundaryColliders == null || index < 0 || index >= wallBoundaryColliders.Length)
            {
                return null;
            }

            return wallBoundaryColliders[index];
        }

        public void SetWallBoundaryColliderEnabled(CubeRoomWall wall, bool enabled)
        {
            CubeRoomWallBoundary boundary = GetWallBoundary(wall);
            if (boundary != null)
            {
                boundary.SetCollidersEnabled(enabled);
                return;
            }

            Collider wallCollider = GetWallBoundaryCollider(wall);
            if (wallCollider != null)
            {
                wallCollider.enabled = enabled;
            }
        }

        public void SetAllWallBoundaryCollidersEnabled(bool enabled)
        {
            for (int index = 0; index < 4; index++)
            {
                SetWallBoundaryColliderEnabled((CubeRoomWall)index, enabled);
            }
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (occupancyVolume != null)
            {
                return occupancyVolume.ContainsWorldPoint(worldPoint);
            }

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(localPoint.x) <= InteriorWidth * 0.5f
                && localPoint.y >= 0f
                && localPoint.y <= InteriorHeight
                && Mathf.Abs(localPoint.z) <= InteriorDepth * 0.5f;
        }

        private void Reset()
        {
            lighting = GetComponentInChildren<CubeRoomLighting>(true);
            occupancyVolume = GetComponentInChildren<RoomOccupancyVolume>(true);
            DiscoverWallBoundaries();
            ceilingBoundary = GetComponentInChildren<CubeRoomCeilingBoundary>(true);
            LinkOccupancyVolume();
        }

        private void OnValidate()
        {
            gravityStrength = Mathf.Max(0f, gravityStrength);
            wallGlassRenderers ??= new Renderer[4];
            wallBoundaryColliders ??= new Collider[4];
            wallBoundaries ??= new CubeRoomWallBoundary[4];
            EnsureConnectionArray();
            EnsureFaceConnectionArray();

            if (lighting == null)
            {
                lighting = GetComponentInChildren<CubeRoomLighting>(true);
            }
            if (lighting != null)
            {
                lighting.SetPowerState(powerOn);
            }
            LightingStateChanged?.Invoke(this);

            if (occupancyVolume == null)
            {
                occupancyVolume = GetComponentInChildren<RoomOccupancyVolume>(true);
            }

            DiscoverWallBoundaries();
            if (ceilingBoundary == null)
            {
                ceilingBoundary = GetComponentInChildren<CubeRoomCeilingBoundary>(true);
            }
            LinkOccupancyVolume();
        }

        private void LinkOccupancyVolume()
        {
            if (occupancyVolume != null && occupancyVolume.Room != this)
            {
                occupancyVolume.Configure(this);
            }
        }

        private void DiscoverWallBoundaries()
        {
            wallBoundaries ??= new CubeRoomWallBoundary[4];
            foreach (CubeRoomWallBoundary boundary in GetComponentsInChildren<CubeRoomWallBoundary>(true))
            {
                if (boundary == null)
                {
                    continue;
                }

                int index = (int)boundary.Wall;
                if (index >= 0 && index < wallBoundaries.Length)
                {
                    wallBoundaries[index] = boundary;
                }
            }

            SynchronizeLegacyWallReferences();
        }

        private void SynchronizeLegacyWallReferences()
        {
            wallGlassRenderers ??= new Renderer[4];
            wallBoundaryColliders ??= new Collider[4];

            for (int index = 0; index < 4; index++)
            {
                CubeRoomWallBoundary boundary = GetWallBoundary((CubeRoomWall)index);
                if (boundary == null)
                {
                    continue;
                }

                if (boundary.ClosedGlassRenderer != null)
                {
                    wallGlassRenderers[index] = boundary.ClosedGlassRenderer;
                }

                if (boundary.ClosedCollider != null)
                {
                    wallBoundaryColliders[index] = boundary.ClosedCollider;
                }
            }
        }

        private void SetLegacyWallEnabled(CubeRoomWall wall, bool enabled)
        {
            Renderer wallRenderer = GetWallGlassRenderer(wall);
            if (wallRenderer != null)
            {
                wallRenderer.enabled = enabled;
            }

            Collider wallCollider = GetWallBoundaryCollider(wall);
            if (wallCollider != null)
            {
                wallCollider.enabled = enabled;
            }
        }

        private void EnsureConnectionArray()
        {
            if (connectedRooms == null || connectedRooms.Length != 4)
            {
                connectedRooms = new CubeRoom[4];
            }
        }

        private void EnsureFaceConnectionArray()
        {
            if (faceConnections == null || faceConnections.Length != 6)
            {
                faceConnections = new RoomFaceConnection[6];
            }
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            for (int wallIndex = 0; wallIndex < 4; wallIndex++)
            {
                CubeRoomWall wall = (CubeRoomWall)wallIndex;
                Gizmos.color = HasDoorway(wall)
                    ? new Color(0.20f, 0.70f, 1f, 0.9f)
                    : new Color(0.20f, 1f, 0.42f, 0.9f);
                for (int anchorIndex = 0; anchorIndex < 4; anchorIndex++)
                {
                    Gizmos.DrawSphere(
                        GetWallAnchorLocal(wall, (CubeRoomWallAnchor)anchorIndex),
                        0.09f);
                }

                Vector3 center = GetWallNormalLocal(wall) * (InteriorWidth * 0.5f)
                    + Vector3.up * (InteriorHeight * 0.5f);
                Gizmos.DrawLine(center, center + GetWallNormalLocal(wall) * 0.65f);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
