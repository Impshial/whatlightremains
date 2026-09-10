using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatLightRemains.Runtime
{
    /// <summary>A renderer-only room copy with no colliders, lights, triggers, or room state.</summary>
    public sealed class RoomGhostPreview : IDisposable
    {
        private static readonly Vector3[] Corners = CreateCorners();
        private static readonly Vector2Int[] Edges =
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
            new(4, 5), new(5, 6), new(6, 7), new(7, 4),
            new(0, 4), new(1, 5), new(2, 6), new(3, 7),
        };

        private readonly List<Material> ownedMaterials = new();
        private readonly List<Mesh> ownedMeshes = new();
        private readonly Dictionary<Renderer, Renderer> rendererCopies = new();
        private readonly Dictionary<GameObject, GameObject> objectCopies = new();
        private readonly CubeRoom roomPrefab;

        private RoomGhostPreview(GameObject root, CubeRoom prefab)
        {
            Root = root;
            roomPrefab = prefab;
        }

        public GameObject Root { get; private set; }
        public bool IsVisible => Root != null && Root.activeSelf;

        public static RoomGhostPreview Create(Material material) => Create(material, null);

        public static RoomGhostPreview Create(Material material, CubeRoom prefab)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            GameObject root = new GameObject("Room Creation Ghost");
            RoomGhostPreview preview = new RoomGhostPreview(root, prefab);
            Material bodyMaterial = preview.CreateOpacityVariant(material, "Ghost Body", 0.22f);
            Material floorMaterial = preview.CreateFilledFloorMaterial(material);
            Material accentMaterial = preview.CreateAccentMaterial(material);
            if (prefab != null)
            {
                preview.CopyVisualHierarchy(prefab.transform, root.transform, bodyMaterial, floorMaterial);
            }

            preview.CreateOutline(accentMaterial);
            preview.CreateGravityArrow(accentMaterial);
            root.SetActive(false);
            return preview;
        }

        public void Show(RoomPlacementCandidate candidate)
        {
            if (Root == null || !candidate.IsValid) return;
            Root.transform.SetPositionAndRotation(candidate.Position, candidate.Rotation);
            ApplyPredictedDoorways(candidate);
            Root.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null) Root.SetActive(false);
        }

        public void Dispose()
        {
            if (Root != null)
            {
                DestroyObject(Root);
                Root = null;
            }

            foreach (Material material in ownedMaterials) DestroyObject(material);
            foreach (Mesh mesh in ownedMeshes) DestroyObject(mesh);
            ownedMaterials.Clear();
            ownedMeshes.Clear();
            rendererCopies.Clear();
            objectCopies.Clear();
        }

        private void CopyVisualHierarchy(Transform source, Transform parent, Material material, Material floorMaterial)
        {
            for (int index = 0; index < source.childCount; index++)
            {
                Transform sourceChild = source.GetChild(index);
                GameObject copy = new GameObject(sourceChild.name);
                copy.transform.SetParent(parent, false);
                copy.transform.localPosition = sourceChild.localPosition;
                copy.transform.localRotation = sourceChild.localRotation;
                copy.transform.localScale = sourceChild.localScale;
                objectCopies[sourceChild.gameObject] = copy;
                CopyRenderer(sourceChild, copy, IsFloorVisual(sourceChild) ? floorMaterial : material);
                CopyVisualHierarchy(sourceChild, copy.transform, material, floorMaterial);
                copy.SetActive(sourceChild.gameObject.activeSelf);
            }
        }

        private void CopyRenderer(Transform source, GameObject destination, Material material)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter != null && sourceRenderer != null)
            {
                destination.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer copy = destination.AddComponent<MeshRenderer>();
                ConfigureRendererCopy(sourceRenderer, copy, material);
                rendererCopies[sourceRenderer] = copy;
                return;
            }

            SkinnedMeshRenderer sourceSkinned = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinned == null) return;
            SkinnedMeshRenderer skinnedCopy = destination.AddComponent<SkinnedMeshRenderer>();
            skinnedCopy.sharedMesh = sourceSkinned.sharedMesh;
            ConfigureRendererCopy(sourceSkinned, skinnedCopy, material);
            rendererCopies[sourceSkinned] = skinnedCopy;
        }

        private static void ConfigureRendererCopy(Renderer source, Renderer copy, Material material)
        {
            int count = Mathf.Max(1, source.sharedMaterials.Length);
            Material[] materials = new Material[count];
            for (int index = 0; index < count; index++) materials[index] = material;
            copy.sharedMaterials = materials;
            copy.enabled = source.enabled;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.receiveShadows = false;
            copy.lightProbeUsage = LightProbeUsage.Off;
            copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
            copy.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder;
        }

        private void CreateOutline(Material material)
        {
            GameObject outline = new GameObject("Volume Outline");
            outline.transform.SetParent(Root.transform, false);
            for (int index = 0; index < Edges.Length; index++)
            {
                GameObject edge = new GameObject($"Ghost Edge {index + 1:00}");
                edge.transform.SetParent(outline.transform, false);
                LineRenderer line = edge.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, Corners[Edges[index].x]);
                line.SetPosition(1, Corners[Edges[index].y]);
                line.startWidth = 0.035f;
                line.endWidth = 0.035f;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.alignment = LineAlignment.View;
                line.sharedMaterial = material;
                ConfigureUnlitRenderer(line);
            }
        }

        private void CreateGravityArrow(Material material)
        {
            GameObject arrow = new GameObject("Gravity Direction Arrow");
            arrow.transform.SetParent(Root.transform, false);
            arrow.transform.localPosition = new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f);

            GameObject shaft = new GameObject("Gravity Arrow Shaft");
            shaft.transform.SetParent(arrow.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            shaft.transform.localScale = new Vector3(0.075f, 0.42f, 0.075f);
            shaft.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            MeshRenderer shaftRenderer = shaft.AddComponent<MeshRenderer>();
            shaftRenderer.sharedMaterial = material;
            ConfigureUnlitRenderer(shaftRenderer);

            GameObject head = new GameObject("Gravity Arrow Head");
            head.transform.SetParent(arrow.transform, false);
            head.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            Mesh coneMesh = CreateConeMesh();
            ownedMeshes.Add(coneMesh);
            head.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            MeshRenderer renderer = head.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureUnlitRenderer(renderer);
        }

        private Material CreateFilledFloorMaterial(Material source)
        {
            return CreateOpacityVariant(source, "Filled Floor Ghost", 0.46f);
        }

        private Material CreateAccentMaterial(Material source)
        {
            return CreateOpacityVariant(source, "Ghost Accent", 0.9f);
        }

        private Material CreateOpacityVariant(Material source, string suffix, float alpha)
        {
            Material material = new Material(source)
            {
                name = $"{source.name} ({suffix})",
                hideFlags = HideFlags.DontSave,
            };
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            color.a = Mathf.Clamp01(alpha);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            ownedMaterials.Add(material);
            return material;
        }

        private void ApplyPredictedDoorways(RoomPlacementCandidate candidate)
        {
            if (roomPrefab == null) return;
            foreach (CubeRoomWallBoundary boundary in roomPrefab.GetComponentsInChildren<CubeRoomWallBoundary>(true))
            {
                bool doorway = CandidateUsesSideDoorway(candidate, boundary.Wall);
                SetCopyEnabled(boundary.ClosedGlassRenderer, !doorway);
                SetCopyEnabled(boundary.ClosedBaseTrimRenderer, !doorway);
                SetCopiesEnabled(boundary.DoorwayGlassRenderers, doorway);
                SetCopiesEnabled(boundary.DoorwayFrameRenderers, doorway);
                SetCopiesEnabled(boundary.DoorwayBaseTrimRenderers, doorway);
            }

            CubeRoomCeilingBoundary ceiling = roomPrefab.CeilingBoundary;
            if (ceiling == null) return;
            RoomConnectionPlan? ceilingConnection = null;
            foreach (RoomConnectionPlan connection in candidate.Connections)
            {
                if (connection.CandidateFace == CubeRoomFace.Ceiling)
                {
                    ceilingConnection = connection;
                    break;
                }
            }

            bool showPassage = ceilingConnection.HasValue
                && ceilingConnection.Value.PassageKind == RoomPassageKind.CeilingToSideDoorway;
            SetCopiesEnabled(ceiling.ClosedRenderers, !showPassage);
            foreach (CubeRoomCeilingBoundary.EdgeVariant variant in ceiling.EdgeVariants)
            {
                bool showVariant = showPassage
                    && variant.Edge == ceilingConnection.Value.CeilingEdge;
                SetCopyActive(variant.PassageRoot, showVariant);
                SetCopiesEnabled(variant.Renderers, showVariant);
            }
            for (int index = 0; index < ceiling.EdgeCrossingRoots.Count; index++)
            {
                GameObject crossingRoot = ceiling.EdgeCrossingRoots[index];
                RoomCeilingEdge edge = (RoomCeilingEdge)(index + 1);
                bool crossesOpening = showPassage && edge == ceilingConnection.Value.CeilingEdge;
                SetCopyActive(crossingRoot, !crossesOpening);
            }
        }

        private static bool CandidateUsesSideDoorway(RoomPlacementCandidate candidate, CubeRoomWall wall)
        {
            if (candidate.Connections == null) return false;
            CubeRoomFace face = CubeRoom.ToFace(wall);
            foreach (RoomConnectionPlan connection in candidate.Connections)
            {
                if (connection.CandidateFace == face && connection.IsTraversable) return true;
            }

            return false;
        }

        private void SetCopiesEnabled(IReadOnlyList<Renderer> sources, bool enabled)
        {
            if (sources == null) return;
            for (int index = 0; index < sources.Count; index++) SetCopyEnabled(sources[index], enabled);
        }

        private void SetCopyEnabled(Renderer source, bool enabled)
        {
            if (source != null && rendererCopies.TryGetValue(source, out Renderer copy) && copy != null) copy.enabled = enabled;
        }

        private void SetCopyActive(GameObject source, bool active)
        {
            if (source != null && objectCopies.TryGetValue(source, out GameObject copy) && copy != null)
            {
                copy.SetActive(active);
            }
        }

        private static bool IsFloorVisual(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.name.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static void ConfigureUnlitRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Vector3[] CreateCorners()
        {
            float halfWidth = CubeRoom.InteriorWidth * 0.5f;
            float halfDepth = CubeRoom.InteriorDepth * 0.5f;
            float height = CubeRoom.InteriorHeight;
            return new[]
            {
                new Vector3(-halfWidth, 0f, -halfDepth), new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(halfWidth, 0f, halfDepth), new Vector3(-halfWidth, 0f, halfDepth),
                new Vector3(-halfWidth, height, -halfDepth), new Vector3(halfWidth, height, -halfDepth),
                new Vector3(halfWidth, height, halfDepth), new Vector3(-halfWidth, height, halfDepth),
            };
        }

        private static Mesh CreateConeMesh()
        {
            const int segments = 16;
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = new Vector3(0f, -0.42f, 0f);
            vertices[1] = new Vector3(0f, 0.42f, 0f);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                vertices[index + 2] = new Vector3(Mathf.Cos(angle) * 0.24f, 0.42f, Mathf.Sin(angle) * 0.24f);
                int next = (index + 1) % segments;
                int triangle = index * 6;
                triangles[triangle] = 0;
                triangles[triangle + 1] = index + 2;
                triangles[triangle + 2] = next + 2;
                triangles[triangle + 3] = 1;
                triangles[triangle + 4] = next + 2;
                triangles[triangle + 5] = index + 2;
            }
            Mesh mesh = new Mesh { name = "Ghost Gravity Arrow Cone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
