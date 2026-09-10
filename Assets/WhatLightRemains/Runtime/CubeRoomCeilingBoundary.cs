using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Owns the full ceiling and the four edge-aligned passage variants. Generated room assets
    /// may leave passage arrays empty until their ceiling geometry has been rebuilt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CubeRoomCeilingBoundary : MonoBehaviour
    {
        [Serializable]
        public struct EdgeVariant
        {
            [SerializeField] private RoomCeilingEdge edge;
            [SerializeField] private Renderer[] renderers;
            [SerializeField] private Collider[] colliders;
            [SerializeField] private GameObject passageRoot;

            public EdgeVariant(RoomCeilingEdge edge, Renderer[] renderers, Collider[] colliders, GameObject passageRoot = null)
            {
                this.edge = edge;
                this.renderers = renderers ?? Array.Empty<Renderer>();
                this.colliders = colliders ?? Array.Empty<Collider>();
                this.passageRoot = passageRoot;
            }

            public RoomCeilingEdge Edge => edge;
            public IReadOnlyList<Renderer> Renderers => renderers ?? Array.Empty<Renderer>();
            public IReadOnlyList<Collider> Colliders => colliders ?? Array.Empty<Collider>();
            public GameObject PassageRoot => passageRoot;
        }

        [SerializeField] private Renderer[] closedRenderers = Array.Empty<Renderer>();
        [SerializeField] private Collider[] closedColliders = Array.Empty<Collider>();
        [SerializeField] private EdgeVariant[] edgeVariants = Array.Empty<EdgeVariant>();
        [SerializeField] private GameObject[] edgeCrossingRoots = new GameObject[4];
        [SerializeField, HideInInspector] private bool connected;
        [SerializeField, HideInInspector] private RoomPassageKind passageKind;
        [SerializeField, HideInInspector] private RoomCeilingEdge ceilingEdge;
        [SerializeField, HideInInspector] private bool ownsBoundary = true;

        public IReadOnlyList<Renderer> ClosedRenderers => closedRenderers;
        public IReadOnlyList<Collider> ClosedColliders => closedColliders;
        public IReadOnlyList<EdgeVariant> EdgeVariants => edgeVariants;
        public IReadOnlyList<GameObject> EdgeCrossingRoots => edgeCrossingRoots;
        public bool IsConnected => connected;
        public bool HasPassage => passageKind == RoomPassageKind.CeilingToSideDoorway;
        public RoomCeilingEdge CeilingEdge => ceilingEdge;
        public bool OwnsBoundary => ownsBoundary;
        public GameObject ActivePassageRoot { get; private set; }
        public CubeRoom ConnectedRoom
        {
            get
            {
                CubeRoom room = GetComponentInParent<CubeRoom>();
                return room != null ? room.GetConnectedRoom(CubeRoomFace.Ceiling) : null;
            }
        }
        public event Action StateChanged;

        public void Configure(Renderer[] newClosedRenderers, Collider[] newClosedColliders, EdgeVariant[] newEdgeVariants)
        {
            closedRenderers = newClosedRenderers ?? Array.Empty<Renderer>();
            closedColliders = newClosedColliders ?? Array.Empty<Collider>();
            edgeVariants = newEdgeVariants ?? Array.Empty<EdgeVariant>();
            ResetConnectionState();
        }

        public void ConfigureEdgeCrossingRoots(GameObject west, GameObject east, GameObject south, GameObject north)
        {
            edgeCrossingRoots = new[] { west, east, south, north };
            ApplyState();
        }

        public void SetConnectionState(bool newConnected, RoomPassageKind newPassageKind, RoomCeilingEdge newCeilingEdge, bool ownsSharedBoundary)
        {
            connected = newConnected;
            passageKind = newConnected ? newPassageKind : RoomPassageKind.None;
            ceilingEdge = newConnected ? newCeilingEdge : RoomCeilingEdge.None;
            ownsBoundary = !newConnected || ownsSharedBoundary;
            ApplyState();
            NotifyStateChanged();
        }

        public void ResetConnectionState()
        {
            connected = false;
            passageKind = RoomPassageKind.None;
            ceilingEdge = RoomCeilingEdge.None;
            ownsBoundary = true;
            ApplyState();
            NotifyStateChanged();
        }

        public void ApplyState()
        {
            ActivePassageRoot = null;
            bool showClosed = ownsBoundary && (!connected || passageKind == RoomPassageKind.Sealed);
            SetEnabled(closedRenderers, showClosed);
            SetEnabled(closedColliders, showClosed);

            edgeVariants ??= Array.Empty<EdgeVariant>();
            foreach (EdgeVariant variant in edgeVariants)
            {
                bool showVariant = ownsBoundary
                    && connected
                    && passageKind == RoomPassageKind.CeilingToSideDoorway
                    && variant.Edge == ceilingEdge;
                SetEnabled(variant.Renderers, showVariant);
                SetEnabled(variant.Colliders, showVariant);
                if (variant.PassageRoot != null)
                {
                    variant.PassageRoot.SetActive(showVariant);
                    if (showVariant)
                    {
                        ActivePassageRoot = variant.PassageRoot;
                        CeilingLadder ladder = variant.PassageRoot.GetComponentInChildren<CeilingLadder>(true);
                        if (ladder != null)
                        {
                            ladder.SetRooms(GetComponentInParent<CubeRoom>(), ConnectedRoom);
                        }
                    }
                }
            }

            edgeCrossingRoots ??= new GameObject[4];
            for (int index = 0; index < edgeCrossingRoots.Length; index++)
            {
                GameObject crossingRoot = edgeCrossingRoots[index];
                if (crossingRoot == null) continue;
                RoomCeilingEdge edge = (RoomCeilingEdge)(index + 1);
                bool crossesActiveOpening = ownsBoundary
                    && connected
                    && passageKind == RoomPassageKind.CeilingToSideDoorway
                    && ceilingEdge == edge;
                crossingRoot.SetActive(!crossesActiveOpening);
            }
        }

        private void OnValidate()
        {
            closedRenderers ??= Array.Empty<Renderer>();
            closedColliders ??= Array.Empty<Collider>();
            edgeVariants ??= Array.Empty<EdgeVariant>();
            edgeCrossingRoots ??= new GameObject[4];
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

        private static void SetEnabled(IEnumerable<Renderer> renderers, bool enabled)
        {
            if (renderers == null) return;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null) renderer.enabled = enabled;
            }
        }

        private static void SetEnabled(IEnumerable<Collider> colliders, bool enabled)
        {
            if (colliders == null) return;
            foreach (Collider collider in colliders)
            {
                if (collider != null) collider.enabled = enabled;
            }
        }
    }
}
