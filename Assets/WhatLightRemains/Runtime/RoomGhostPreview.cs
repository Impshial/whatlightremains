using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatLightRemains.Runtime
{
    /// <summary>Lightweight, non-physical twelve-edge room outline.</summary>
    public sealed class RoomGhostPreview : IDisposable
    {
        private static readonly Vector3[] Corners = CreateCorners();

        private static readonly Vector2Int[] Edges =
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
            new(4, 5), new(5, 6), new(6, 7), new(7, 4),
            new(0, 4), new(1, 5), new(2, 6), new(3, 7),
        };

        private RoomGhostPreview(GameObject root)
        {
            Root = root;
        }

        private static Vector3[] CreateCorners()
        {
            float halfWidth = CubeRoom.InteriorWidth * 0.5f;
            float halfDepth = CubeRoom.InteriorDepth * 0.5f;
            float height = CubeRoom.InteriorHeight;
            return new[]
            {
                new Vector3(-halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, halfDepth),
                new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(-halfWidth, height, -halfDepth),
                new Vector3(halfWidth, height, -halfDepth),
                new Vector3(halfWidth, height, halfDepth),
                new Vector3(-halfWidth, height, halfDepth),
            };
        }

        public GameObject Root { get; private set; }
        public bool IsVisible => Root != null && Root.activeSelf;

        public static RoomGhostPreview Create(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            GameObject root = new GameObject("Room Creation Ghost");
            root.SetActive(false);
            for (int index = 0; index < Edges.Length; index++)
            {
                GameObject edge = new GameObject($"Ghost Edge {index + 1:00}");
                edge.transform.SetParent(root.transform, false);
                LineRenderer line = edge.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, Corners[Edges[index].x]);
                line.SetPosition(1, Corners[Edges[index].y]);
                line.startWidth = 0.055f;
                line.endWidth = 0.055f;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.alignment = LineAlignment.View;
                line.textureMode = LineTextureMode.Stretch;
                line.sharedMaterial = material;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            return new RoomGhostPreview(root);
        }

        public void Show(RoomPlacementCandidate candidate)
        {
            if (Root == null || !candidate.IsValid) return;
            Root.transform.SetPositionAndRotation(candidate.Position, candidate.Rotation);
            Root.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null) Root.SetActive(false);
        }

        public void Dispose()
        {
            if (Root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
            Root = null;
        }
    }
}
