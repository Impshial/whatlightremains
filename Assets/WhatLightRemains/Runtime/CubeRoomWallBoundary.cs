using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Owns the renderers and colliders for one horizontal face of a cube room.
    /// A connected wall uses three perimeter sections around a floor-level doorway.
    /// Only one of two adjacent rooms owns the shared physical boundary, which avoids
    /// coplanar glass and overlapping colliders while both rooms retain connection state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CubeRoomWallBoundary : MonoBehaviour
    {
        [SerializeField] private CubeRoomWall wall;
        [SerializeField] private Renderer closedGlassRenderer;
        [SerializeField] private Renderer[] doorwayGlassRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] doorwayFrameRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer closedBaseTrimRenderer;
        [SerializeField] private Renderer[] doorwayBaseTrimRenderers = Array.Empty<Renderer>();
        [SerializeField] private Collider closedCollider;
        [SerializeField] private Collider[] doorwayColliders = Array.Empty<Collider>();
        [SerializeField, HideInInspector] private bool connected;
        [SerializeField, HideInInspector] private bool hasDoorway;
        [SerializeField, HideInInspector] private bool ownsBoundary = true;
        [NonSerialized] private bool externalGeometryOwnsBoundary;

        public CubeRoomWall Wall => wall;
        public bool IsConnected => connected;
        public bool HasDoorway => hasDoorway;
        public bool OwnsBoundary => ownsBoundary;
        public bool UsesExternalGeometry => externalGeometryOwnsBoundary;
        public Renderer ClosedGlassRenderer => closedGlassRenderer;
        public IReadOnlyList<Renderer> DoorwayGlassRenderers => doorwayGlassRenderers;
        public IReadOnlyList<Renderer> DoorwayFrameRenderers => doorwayFrameRenderers;
        public Renderer ClosedBaseTrimRenderer => closedBaseTrimRenderer;
        public IReadOnlyList<Renderer> DoorwayBaseTrimRenderers => doorwayBaseTrimRenderers;
        public Collider ClosedCollider => closedCollider;
        public IReadOnlyList<Collider> DoorwayColliders => doorwayColliders;
        public event Action StateChanged;

        public void SetExternalGeometryOwned(bool owned)
        {
            externalGeometryOwnsBoundary = owned;
            ApplyState();
        }

        public void Configure(
            CubeRoomWall newWall,
            Renderer newClosedGlassRenderer,
            Renderer[] newDoorwayGlassRenderers,
            Renderer[] newDoorwayFrameRenderers,
            Collider newClosedCollider,
            Collider[] newDoorwayColliders)
        {
            Configure(
                newWall,
                newClosedGlassRenderer,
                newDoorwayGlassRenderers,
                newDoorwayFrameRenderers,
                null,
                null,
                newClosedCollider,
                newDoorwayColliders);
        }

        public void Configure(
            CubeRoomWall newWall,
            Renderer newClosedGlassRenderer,
            Renderer[] newDoorwayGlassRenderers,
            Renderer[] newDoorwayFrameRenderers,
            Renderer newClosedBaseTrimRenderer,
            Renderer[] newDoorwayBaseTrimRenderers,
            Collider newClosedCollider,
            Collider[] newDoorwayColliders)
        {
            wall = newWall;
            closedGlassRenderer = newClosedGlassRenderer;
            doorwayGlassRenderers = newDoorwayGlassRenderers ?? Array.Empty<Renderer>();
            doorwayFrameRenderers = newDoorwayFrameRenderers ?? Array.Empty<Renderer>();
            closedBaseTrimRenderer = newClosedBaseTrimRenderer;
            doorwayBaseTrimRenderers = newDoorwayBaseTrimRenderers ?? Array.Empty<Renderer>();
            closedCollider = newClosedCollider;
            doorwayColliders = newDoorwayColliders ?? Array.Empty<Collider>();
            ResetConnectionState();
        }

        /// <summary>
        /// Configures whether this face connects to another room and whether this room owns
        /// the single rendered/physical boundary shared by the two adjacent room instances.
        /// </summary>
        public void SetConnectionState(bool connected, bool ownsSharedBoundary)
        {
            SetConnectionState(connected, ownsSharedBoundary, connected);
        }

        /// <summary>
        /// Applies a shared-face state where a neighbor may exist without a traversable
        /// doorway. This lets rotated adjacent rooms share exactly one sealed boundary.
        /// </summary>
        public void SetConnectionState(bool newHasDoorway, bool ownsSharedBoundary, bool hasNeighbor)
        {
            connected = hasNeighbor;
            hasDoorway = newHasDoorway;
            ownsBoundary = !hasNeighbor || ownsSharedBoundary;
            ApplyState();
            NotifyStateChanged();
        }

        public void ResetConnectionState()
        {
            connected = false;
            hasDoorway = false;
            ownsBoundary = true;
            ApplyState();
            NotifyStateChanged();
        }

        public void ApplyState()
        {
            doorwayGlassRenderers ??= Array.Empty<Renderer>();
            doorwayFrameRenderers ??= Array.Empty<Renderer>();
            doorwayBaseTrimRenderers ??= Array.Empty<Renderer>();
            doorwayColliders ??= Array.Empty<Collider>();

            ApplyVisualState();
            ApplyColliderState();
        }

        private void ApplyVisualState()
        {
            bool showClosedBoundary = !externalGeometryOwnsBoundary && ownsBoundary && !hasDoorway;
            bool showDoorwayBoundary = !externalGeometryOwnsBoundary && ownsBoundary && hasDoorway;
            SetEnabled(closedGlassRenderer, showClosedBoundary);
            SetEnabled(doorwayGlassRenderers, showDoorwayBoundary);
            SetEnabled(doorwayFrameRenderers, showDoorwayBoundary);
            SetEnabled(closedBaseTrimRenderer, showClosedBoundary);
            SetEnabled(doorwayBaseTrimRenderers, showDoorwayBoundary);
        }

        private void ApplyColliderState()
        {
            bool showClosedBoundary = !externalGeometryOwnsBoundary && ownsBoundary && !hasDoorway;
            bool showDoorwayBoundary = !externalGeometryOwnsBoundary && ownsBoundary && hasDoorway;
            SetEnabled(closedCollider, showClosedBoundary);
            SetEnabled(doorwayColliders, showDoorwayBoundary);
        }

        /// <summary>
        /// Compatibility hook for callers that temporarily hide a wall's visuals without
        /// changing its logical connection. Re-enabling restores the current state.
        /// </summary>
        public void SetVisualsEnabled(bool enabled)
        {
            if (enabled)
            {
                ApplyVisualState();
                return;
            }

            SetEnabled(closedGlassRenderer, false);
            SetEnabled(doorwayGlassRenderers, false);
            SetEnabled(doorwayFrameRenderers, false);
            SetEnabled(closedBaseTrimRenderer, false);
            SetEnabled(doorwayBaseTrimRenderers, false);
        }

        /// <summary>
        /// Compatibility hook for callers that temporarily disable a wall's collision without
        /// changing its logical connection. Re-enabling restores the current state.
        /// </summary>
        public void SetCollidersEnabled(bool enabled)
        {
            if (enabled)
            {
                ApplyColliderState();
                return;
            }

            SetEnabled(closedCollider, false);
            SetEnabled(doorwayColliders, false);
        }

        private void OnValidate()
        {
            doorwayGlassRenderers ??= Array.Empty<Renderer>();
            doorwayFrameRenderers ??= Array.Empty<Renderer>();
            doorwayBaseTrimRenderers ??= Array.Empty<Renderer>();
            doorwayColliders ??= Array.Empty<Collider>();
            if (!connected)
            {
                ownsBoundary = true;
            }

            ApplyState();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
            CubeRoom room = GetComponentInParent<CubeRoom>();
            if (room != null && room.Lighting != null)
            {
                room.Lighting.ApplySettings();
            }
        }

        private static void SetEnabled(Renderer renderer, bool enabled)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }

        private static void SetEnabled(Collider collider, bool enabled)
        {
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }

        private static void SetEnabled(IEnumerable<Renderer> renderers, bool enabled)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                SetEnabled(renderer, enabled);
            }
        }

        private static void SetEnabled(IEnumerable<Collider> colliders, bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            foreach (Collider collider in colliders)
            {
                SetEnabled(collider, enabled);
            }
        }
    }
}
