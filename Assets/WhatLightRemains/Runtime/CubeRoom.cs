using UnityEngine;

namespace WhatLightRemains.Runtime
{
    public enum CubeRoomWall
    {
        West = 0,
        East = 1,
        South = 2,
        North = 3,
    }

    [DisallowMultipleComponent]
    public sealed class CubeRoom : MonoBehaviour
    {
        public const float InteriorWidth = 8f;
        public const float InteriorHeight = 8f;
        public const float InteriorDepth = 8f;
        public const float DoorwayWidth = 2f;
        public const float DoorwayHeight = 2.4f;

        [SerializeField, Min(0f)] private float gravityStrength = 9.81f;
        [SerializeField] private CubeRoomLighting lighting;
        [SerializeField] private RoomOccupancyVolume occupancyVolume;
        [SerializeField] private Renderer[] wallGlassRenderers = new Renderer[4];
        [SerializeField] private Collider[] wallBoundaryColliders = new Collider[4];
        [SerializeField] private CubeRoomWallBoundary[] wallBoundaries = new CubeRoomWallBoundary[4];

        public float GravityStrength
        {
            get => gravityStrength;
            set => gravityStrength = Mathf.Max(0f, value);
        }

        public Vector3 RoomUp => transform.up;
        public Vector3 GravityAcceleration => -RoomUp * gravityStrength;
        public CubeRoomLighting Lighting => lighting;
        public RoomOccupancyVolume OccupancyVolume => occupancyVolume;
        public Vector3 InteriorSize => new Vector3(InteriorWidth, InteriorHeight, InteriorDepth);

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
        }

        public void SetLightingEnabled(bool enabled)
        {
            if (lighting != null)
            {
                lighting.SetLightingEnabled(enabled);
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

        public void SetWallConnection(CubeRoomWall wall, bool connected, bool ownsSharedBoundary)
        {
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
            }
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
            LinkOccupancyVolume();
        }

        private void OnValidate()
        {
            gravityStrength = Mathf.Max(0f, gravityStrength);
            wallGlassRenderers ??= new Renderer[4];
            wallBoundaryColliders ??= new Collider[4];
            wallBoundaries ??= new CubeRoomWallBoundary[4];

            if (lighting == null)
            {
                lighting = GetComponentInChildren<CubeRoomLighting>(true);
            }

            if (occupancyVolume == null)
            {
                occupancyVolume = GetComponentInChildren<RoomOccupancyVolume>(true);
            }

            DiscoverWallBoundaries();
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
    }
}
