using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatLightRemains.Runtime
{
    /// <summary>A non-physical, renderer-only outline for a room selected for deletion.</summary>
    public sealed class RoomDeletionHighlight : IDisposable
    {
        private const float StrokeWidth = 0.14f;
        private const float ApertureInset = 0.025f;
        private const float GlassTintStrength = 0.15f;
        private const string GlassShaderName = "What Light Remains/Light-Transmitting Glass";

        private static readonly int DeleteTintColorId = Shader.PropertyToID("_WLRDeleteTintColor");
        private static readonly int DeleteTintStrengthId = Shader.PropertyToID("_WLRDeleteTintStrength");
        private static readonly Color DeleteTintColor = new Color(1f, 0.025f, 0.015f, 1f);

        private static readonly Vector3[] Corners = CreateCorners();
        private static readonly Vector2Int[] Edges =
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
            new(4, 5), new(5, 6), new(6, 7), new(7, 4),
            new(0, 4), new(1, 5), new(2, 6), new(3, 7),
        };

        private static readonly CubeRoomFace[] Faces =
        {
            CubeRoomFace.West,
            CubeRoomFace.East,
            CubeRoomFace.South,
            CubeRoomFace.North,
            CubeRoomFace.Floor,
            CubeRoomFace.Ceiling,
        };

        private readonly Material material;
        private readonly List<LineRenderer> apertureEdges = new List<LineRenderer>(24);
        private readonly List<Renderer> tintedGlass = new List<Renderer>(32);
        private readonly HashSet<Renderer> uniqueTintedGlass = new HashSet<Renderer>();
        private readonly MaterialPropertyBlock tintPropertyBlock = new MaterialPropertyBlock();

        private RoomDeletionHighlight(GameObject root, Material outlineMaterial)
        {
            Root = root;
            material = outlineMaterial;
        }

        public GameObject Root { get; private set; }
        public bool IsVisible => Root != null && Root.activeSelf;

        public static RoomDeletionHighlight Create(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            GameObject root = new GameObject("Room Deletion Outline");
            RoomDeletionHighlight highlight = new RoomDeletionHighlight(root, material);
            for (int index = 0; index < Edges.Length; index++)
            {
                LineRenderer line = highlight.CreateEdge($"Delete Bounds Edge {index + 1:00}");
                line.SetPosition(0, Corners[Edges[index].x]);
                line.SetPosition(1, Corners[Edges[index].y]);
            }
            root.SetActive(false);
            return highlight;
        }

        public void Show(CubeRoom room)
        {
            if (Root == null || room == null) return;
            Root.transform.SetPositionAndRotation(room.transform.position, room.transform.rotation);
            Root.transform.localScale = room.transform.lossyScale;
            RefreshApertureEdges(room);
            ApplyGlassTint(room);
            Root.SetActive(true);
        }

        public void Hide()
        {
            ClearGlassTint();
            if (Root != null) Root.SetActive(false);
        }

        public void Dispose()
        {
            ClearGlassTint();
            if (Root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
            Root = null;
        }

        private LineRenderer CreateEdge(string edgeName)
        {
            GameObject edge = new GameObject(edgeName);
            edge.transform.SetParent(Root.transform, false);
            LineRenderer line = edge.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = StrokeWidth;
            line.endWidth = StrokeWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return line;
        }

        private void RefreshApertureEdges(CubeRoom room)
        {
            int usedEdgeCount = 0;
            foreach (CubeRoomFace face in Faces)
            {
                RoomFaceConnection connection = room.GetConnection(face);
                if (!connection.IsTraversable) continue;

                RoomAperture aperture = connection.Aperture;
                Vector3 center = Root.transform.InverseTransformPoint(aperture.Center)
                    - CubeRoom.GetFaceNormalLocal(face) * ApertureInset;
                Vector3 horizontal = Root.transform.InverseTransformVector(
                    aperture.HorizontalAxis * (aperture.Width * 0.5f));
                Vector3 vertical = Root.transform.InverseTransformVector(
                    aperture.VerticalAxis * (aperture.Height * 0.5f));
                Vector3[] corners =
                {
                    center - horizontal - vertical,
                    center + horizontal - vertical,
                    center + horizontal + vertical,
                    center - horizontal + vertical,
                };

                for (int edgeIndex = 0; edgeIndex < 4; edgeIndex++)
                {
                    LineRenderer line = GetApertureEdge(usedEdgeCount++);
                    line.gameObject.name = $"Delete Aperture {face} Edge {edgeIndex + 1:00}";
                    line.SetPosition(0, corners[edgeIndex]);
                    line.SetPosition(1, corners[(edgeIndex + 1) % corners.Length]);
                    line.gameObject.SetActive(true);
                }
            }

            for (int index = usedEdgeCount; index < apertureEdges.Count; index++)
                apertureEdges[index].gameObject.SetActive(false);
        }

        private LineRenderer GetApertureEdge(int index)
        {
            while (apertureEdges.Count <= index)
            {
                LineRenderer line = CreateEdge($"Delete Aperture Edge {apertureEdges.Count + 1:00}");
                apertureEdges.Add(line);
            }

            return apertureEdges[index];
        }

        private void ApplyGlassTint(CubeRoom room)
        {
            ClearGlassTint();
            foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>(true))
                AddTintedGlass(renderer);

            foreach (RoomPassage passage in UnityEngine.Object.FindObjectsByType<RoomPassage>(FindObjectsInactive.Include))
            {
                if (passage == null || (passage.RoomA != room && passage.RoomB != room)) continue;
                foreach (Renderer renderer in passage.GlassRenderers)
                    AddTintedGlass(renderer);
            }
        }

        private void AddTintedGlass(Renderer renderer)
        {
            if (renderer == null || !uniqueTintedGlass.Add(renderer) || !UsesGlassShader(renderer)) return;
            renderer.GetPropertyBlock(tintPropertyBlock);
            tintPropertyBlock.SetColor(DeleteTintColorId, DeleteTintColor);
            tintPropertyBlock.SetFloat(DeleteTintStrengthId, GlassTintStrength);
            renderer.SetPropertyBlock(tintPropertyBlock);
            tintedGlass.Add(renderer);
        }

        private void ClearGlassTint()
        {
            foreach (Renderer renderer in tintedGlass)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(tintPropertyBlock);
                tintPropertyBlock.SetFloat(DeleteTintStrengthId, 0f);
                renderer.SetPropertyBlock(tintPropertyBlock);
            }
            tintedGlass.Clear();
            uniqueTintedGlass.Clear();
        }

        private static bool UsesGlassShader(Renderer renderer)
        {
            foreach (Material sharedMaterial in renderer.sharedMaterials)
            {
                if (sharedMaterial != null && sharedMaterial.shader != null
                    && sharedMaterial.shader.name == GlassShaderName)
                    return true;
            }
            return false;
        }

        private static Vector3[] CreateCorners()
        {
            const float offset = 0.06f;
            float halfWidth = CubeRoom.InteriorWidth * 0.5f + offset;
            float halfDepth = CubeRoom.InteriorDepth * 0.5f + offset;
            float bottom = -offset;
            float top = CubeRoom.InteriorHeight + offset;
            return new[]
            {
                new Vector3(-halfWidth, bottom, -halfDepth), new Vector3(halfWidth, bottom, -halfDepth),
                new Vector3(halfWidth, bottom, halfDepth), new Vector3(-halfWidth, bottom, halfDepth),
                new Vector3(-halfWidth, top, -halfDepth), new Vector3(halfWidth, top, -halfDepth),
                new Vector3(halfWidth, top, halfDepth), new Vector3(-halfWidth, top, halfDepth),
            };
        }
    }
}
