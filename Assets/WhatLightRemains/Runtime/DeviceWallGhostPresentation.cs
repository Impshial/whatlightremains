using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatLightRemains.Runtime
{
    /// <summary>Renderer-only Device Wall geometry and pooled anchor markers for a room ghost.</summary>
    internal sealed class DeviceWallGhostPresentation : IDisposable
    {
        private const float Clearance = 0.10f;
        private readonly Transform root;
        private readonly Transform hardwareRoot;
        private readonly GameObject[] markerRoots = new GameObject[16];
        private readonly GameObject[] studRoots = new GameObject[16];
        private readonly GameObject[] blockedCrosses = new GameObject[16];
        private readonly Material hardwareMaterial;
        private readonly Material availableMaterial;
        private readonly Material blockedMaterial;
        private readonly CubeRoomWall face;
        private bool lastHasOpening;
        private Rect lastOpening;
        private bool hasLayout;

        public DeviceWallGhostPresentation(Transform roomGhost, CubeRoomWall wall, Material hardware,
            Material available, Material blocked)
        {
            face = wall;
            hardwareMaterial = hardware;
            availableMaterial = available;
            blockedMaterial = blocked;
            root = new GameObject("Ghost Device Wall").transform;
            root.SetParent(roomGhost, false);
            hardwareRoot = new GameObject("Ghost Device Wall Hardware").transform;
            hardwareRoot.SetParent(root, false);
            CreateMarkers();
        }

        public void Refresh(RoomPlacementCandidate candidate, Transform roomTransform)
        {
            bool hasOpening = false;
            Rect opening = default;
            foreach (RoomConnectionPlan connection in candidate.Connections)
            {
                if (connection.CandidateFace != CubeRoom.ToFace(face) || !connection.IsTraversable) continue;
                hasOpening = DeviceWall.TryProjectAperture(connection.Aperture, roomTransform,
                    CubeRoom.ToFace(face), Clearance, out opening);
                break;
            }

            if (!hasLayout || hasOpening != lastHasOpening || (hasOpening && !Approximately(opening, lastOpening)))
            {
                RebuildHardware(hasOpening, opening);
                lastHasOpening = hasOpening;
                lastOpening = opening;
                hasLayout = true;
            }

            const float radius = 0.06f;
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                {
                    int index = row * 4 + column;
                    Vector2 point = new Vector2(-3f + column * 2f, -3f + row * 2f);
                    bool blocked = hasOpening && CircleIntersectsRect(point, radius, opening);
                    SetMarkerMaterial(markerRoots[index], blocked ? blockedMaterial : availableMaterial);
                    blockedCrosses[index].SetActive(blocked);
                    studRoots[index].SetActive(!blocked);
                }
        }

        public void Dispose()
        {
            if (root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
            else UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        private void CreateMarkers()
        {
            DeviceWall.GetWallBasis(face, out Vector3 center, out Vector3 horizontal,
                out Vector3 vertical, out Vector3 inward);
            Quaternion orientation = Quaternion.LookRotation(-inward, vertical);
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                {
                    int index = row * 4 + column;
                    Vector2 point = new Vector2(-3f + column * 2f, -3f + row * 2f);
                    GameObject marker = new GameObject($"Anchor Marker {row},{column}");
                    marker.transform.SetParent(root, false);
                    marker.transform.localPosition = center + horizontal * point.x + vertical * point.y
                        + inward * 0.075f;
                    marker.transform.localRotation = orientation;
                    markerRoots[index] = marker;
                    CreateRing(marker.transform, availableMaterial);
                    GameObject cross = new GameObject("Blocked X");
                    cross.transform.SetParent(marker.transform, false);
                    CreateLine(cross.transform, "Slash", new Vector3(-0.12f, -0.12f),
                        new Vector3(0.12f, 0.12f), blockedMaterial, 0.025f);
                    CreateLine(cross.transform, "Backslash", new Vector3(-0.12f, 0.12f),
                        new Vector3(0.12f, -0.12f), blockedMaterial, 0.025f);
                    blockedCrosses[index] = cross;

                    GameObject stud = new GameObject("Ghost Stud");
                    stud.transform.SetParent(marker.transform, false);
                    stud.transform.localPosition = new Vector3(0f, 0f, -0.035f);
                    MeshFilter filter = stud.AddComponent<MeshFilter>();
                    filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                    MeshRenderer renderer = stud.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = hardwareMaterial;
                    Configure(renderer);
                    stud.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    stud.transform.localScale = new Vector3(0.10f, 0.018f, 0.10f);
                    studRoots[index] = stud;
                }
        }

        private void CreateRing(Transform parent, Material material)
        {
            const int segments = 24;
            GameObject ring = new GameObject("Hollow Ring");
            ring.transform.SetParent(parent, false);
            LineRenderer line = ring.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.sharedMaterial = material;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle) * 0.16f, Mathf.Sin(angle) * 0.16f, 0f));
            }
            Configure(line);
        }

        private static void CreateLine(Transform parent, string name, Vector3 start, Vector3 end,
            Material material, float width)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            LineRenderer line = item.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = material;
            Configure(line);
        }

        private void RebuildHardware(bool hasOpening, Rect opening)
        {
            for (int index = hardwareRoot.childCount - 1; index >= 0; index--)
            {
                GameObject item = hardwareRoot.GetChild(index).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(item);
                else UnityEngine.Object.DestroyImmediate(item);
            }
            DeviceWall.GetWallBasis(face, out Vector3 center, out Vector3 horizontal,
                out Vector3 vertical, out Vector3 inward);
            for (int column = 0; column < 4; column++)
                CreateVerticalSegments($"Ghost Rail {column + 1}", -3f + column * 2f, 0.06f,
                    hasOpening, opening, center, horizontal, vertical, inward);
            CreateVerticalSegments("Ghost Frame Left", -3.96f, 0.08f, hasOpening, opening,
                center, horizontal, vertical, inward);
            CreateVerticalSegments("Ghost Frame Right", 3.96f, 0.08f, hasOpening, opening,
                center, horizontal, vertical, inward);
            CreateHorizontalSegments("Ghost Frame Bottom", -3.96f, 0.08f, hasOpening, opening,
                center, horizontal, vertical, inward);
            CreateHorizontalSegments("Ghost Frame Top", 3.96f, 0.08f, hasOpening, opening,
                center, horizontal, vertical, inward);
        }

        private void CreateVerticalSegments(string name, float x, float width, bool hasOpening, Rect opening,
            Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            if (!hasOpening || x + width * 0.5f < opening.xMin || x - width * 0.5f > opening.xMax)
            { CreateBox(name, x, 0f, width, 8f, center, horizontal, vertical, inward); return; }
            if (opening.yMin > -4f) CreateBox(name + " Lower", x, (-4f + opening.yMin) * 0.5f,
                width, opening.yMin + 4f, center, horizontal, vertical, inward);
            if (opening.yMax < 4f) CreateBox(name + " Upper", x, (opening.yMax + 4f) * 0.5f,
                width, 4f - opening.yMax, center, horizontal, vertical, inward);
        }

        private void CreateHorizontalSegments(string name, float y, float width, bool hasOpening, Rect opening,
            Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            if (!hasOpening || y + width * 0.5f < opening.yMin || y - width * 0.5f > opening.yMax)
            { CreateBox(name, 0f, y, 8f, width, center, horizontal, vertical, inward); return; }
            if (opening.xMin > -4f) CreateBox(name + " Left", (-4f + opening.xMin) * 0.5f, y,
                opening.xMin + 4f, width, center, horizontal, vertical, inward);
            if (opening.xMax < 4f) CreateBox(name + " Right", (opening.xMax + 4f) * 0.5f, y,
                4f - opening.xMax, width, center, horizontal, vertical, inward);
        }

        private void CreateBox(string name, float x, float y, float width, float height,
            Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            if (width <= 0.001f || height <= 0.001f) return;
            GameObject item = new GameObject(name);
            item.transform.SetParent(hardwareRoot, false);
            item.transform.localPosition = center + horizontal * x + vertical * y + inward * 0.035f;
            item.transform.localRotation = Quaternion.LookRotation(-inward, vertical);
            item.transform.localScale = new Vector3(width, height, 0.04f);
            item.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            MeshRenderer renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = hardwareMaterial;
            Configure(renderer);
        }

        private static void SetMarkerMaterial(GameObject marker, Material material)
        {
            if (marker == null) return;
            foreach (LineRenderer line in marker.GetComponentsInChildren<LineRenderer>(true))
                if (line.transform.parent == marker.transform) line.sharedMaterial = material;
        }

        private static void Configure(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static bool CircleIntersectsRect(Vector2 center, float radius, Rect rect)
        {
            float x = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            float y = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            return (center - new Vector2(x, y)).sqrMagnitude <= radius * radius;
        }

        private static bool Approximately(Rect first, Rect second)
        {
            return Mathf.Abs(first.xMin - second.xMin) < 0.001f
                && Mathf.Abs(first.xMax - second.xMax) < 0.001f
                && Mathf.Abs(first.yMin - second.yMin) < 0.001f
                && Mathf.Abs(first.yMax - second.yMax) < 0.001f;
        }
    }
}
