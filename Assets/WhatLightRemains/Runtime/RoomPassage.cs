using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Runtime representation of one physical opening shared by two rooms. It owns the
    /// segmented glass/collision around the clear aperture and the independently-powered
    /// frame faces on each side.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomPassage : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const float BoundaryThickness = 0.1f;
        private const float FrameWidth = 0.09f;
        private const float FacingDepth = 0.018f;
        public const float FloorLevelTolerance = 0.08f;

        [SerializeField] private CubeRoom roomA;
        [SerializeField] private CubeRoom roomB;
        [SerializeField] private CubeRoomFace faceA;
        [SerializeField] private CubeRoomFace faceB;
        [SerializeField] private RoomAperture aperture;
        [SerializeField] private Renderer[] facingA = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] facingB = Array.Empty<Renderer>();
        [SerializeField] private Light[] lightsA = Array.Empty<Light>();
        [SerializeField] private Light[] lightsB = Array.Empty<Light>();

        private readonly List<GameObject> generated = new List<GameObject>();
        private readonly List<Renderer> glassRenderers = new List<Renderer>(4);
        private MaterialPropertyBlock propertyBlock;
        private CubeRoom highlightedFrom;
        private bool ownsBoundaryOverrides;

        public CubeRoom RoomA => roomA;
        public CubeRoom RoomB => roomB;
        public CubeRoomFace FaceA => faceA;
        public CubeRoomFace FaceB => faceB;
        public RoomAperture Aperture => aperture;
        public IReadOnlyList<Renderer> GlassRenderers => glassRenderers;

        public CubeRoom GetOtherRoom(CubeRoom room) => room == roomA ? roomB : room == roomB ? roomA : null;
        public CubeRoomFace GetFace(CubeRoom room) => room == roomA ? faceA : room == roomB ? faceB : default;

        public bool IsFloorLevelFor(CubeRoom room)
        {
            if (room == null || (room != roomA && room != roomB)) return false;
            Vector3 lowest = aperture.LowestPointFor(room);
            return room.transform.InverseTransformPoint(lowest).y <= FloorLevelTolerance;
        }

        public bool IsElevatedFor(CubeRoom room) => !IsFloorLevelFor(room);

        public void Configure(CubeRoom first, CubeRoomFace firstFace, CubeRoom second,
            CubeRoomFace secondFace, RoomAperture sharedAperture)
        {
            Unsubscribe();
            roomA = first;
            faceA = firstFace;
            roomB = second;
            faceB = secondFace;
            aperture = sharedAperture;
            name = $"Passage [{first?.name}:{firstFace} <-> {second?.name}:{secondFace}]";
            BuildGeometry();
            Subscribe();
            ApplyPower();
        }

        public void SetHighlighted(CubeRoom sourceRoom, bool highlighted)
        {
            highlightedFrom = highlighted ? sourceRoom : null;
            ApplyPower();
        }

        private void BuildGeometry()
        {
            ClearGenerated();
            SetBoundaryOverride(roomA, faceA, true);
            SetBoundaryOverride(roomB, faceB, true);
            ownsBoundaryOverrides = true;

            Material glass = FindGlassMaterial(roomA, faceA) ?? FindGlassMaterial(roomB, faceB);
            Material frame = FindFrameMaterial(roomA, faceA) ?? FindFrameMaterial(roomB, faceB) ?? glass;
            Material trim = FindTrimMaterial(roomA, faceA) ?? FindTrimMaterial(roomB, faceB) ?? frame;
            Vector3 u = aperture.HorizontalAxis.normalized;
            Vector3 v = aperture.VerticalAxis.normalized;
            Vector3 n = aperture.Normal.normalized;
            Vector3 towardA = (roomA.transform.TransformPoint(Vector3.up * 4f) - aperture.Center).normalized;
            Vector3 planeCenter = GetFaceCenterWorld(roomA, faceA);
            float cu = Vector3.Dot(aperture.Center - planeCenter, u);
            float cv = Vector3.Dot(aperture.Center - planeCenter, v);
            float leftEdge = cu - aperture.Width * 0.5f;
            float rightEdge = cu + aperture.Width * 0.5f;
            float bottomEdge = cv - aperture.Height * 0.5f;
            float topEdge = cv + aperture.Height * 0.5f;
            const float half = CubeRoom.InteriorWidth * 0.5f;

            CreatePanel("Glass Left", planeCenter, u, v, n, -half, leftEdge, -half, half, glass);
            CreatePanel("Glass Right", planeCenter, u, v, n, rightEdge, half, -half, half, glass);
            CreatePanel("Glass Below", planeCenter, u, v, n, leftEdge, rightEdge, -half, bottomEdge, glass);
            CreatePanel("Glass Above", planeCenter, u, v, n, leftEdge, rightEdge, topEdge, half, glass);

            // Preserve the dark room-separation rail at the floor edge.  It is split around
            // the clear aperture and is built with the passage, so it appears on the same
            // frame as the newly registered room instead of one topology rebuild later.
            const float railHeight = 0.12f;
            CreateRail("Base Rail Left", planeCenter, u, v, n, -half, leftEdge,
                bottomEdge + railHeight * 0.5f, trim);
            CreateRail("Base Rail Right", planeCenter, u, v, n, rightEdge, half,
                bottomEdge + railHeight * 0.5f, trim);

            List<Renderer> firstFacing = new List<Renderer>();
            List<Renderer> secondFacing = new List<Renderer>();
            bool includeBottom = !IsFloorLevelFor(roomA) && !IsFloorLevelFor(roomB);
            CreateFramePair("Frame Left", aperture.Center - u * (aperture.Width * 0.5f + FrameWidth * 0.5f),
                FrameWidth, aperture.Height + FrameWidth * 2f, u, v, n, towardA, trim, frame, firstFacing, secondFacing);
            CreateFramePair("Frame Right", aperture.Center + u * (aperture.Width * 0.5f + FrameWidth * 0.5f),
                FrameWidth, aperture.Height + FrameWidth * 2f, u, v, n, towardA, trim, frame, firstFacing, secondFacing);
            CreateFramePair("Frame Top", aperture.Center + v * (aperture.Height * 0.5f + FrameWidth * 0.5f),
                aperture.Width, FrameWidth, u, v, n, towardA, trim, frame, firstFacing, secondFacing);
            if (includeBottom)
            {
                CreateFramePair("Frame Bottom", aperture.Center - v * (aperture.Height * 0.5f + FrameWidth * 0.5f),
                    aperture.Width, FrameWidth, u, v, n, towardA, trim, frame, firstFacing, secondFacing);
            }

            facingA = firstFacing.ToArray();
            facingB = secondFacing.ToArray();
            lightsA = CreateFacingLights("A", roomA, u, v,
                (roomA.transform.TransformPoint(Vector3.up * 4f) - aperture.Center).normalized);
            lightsB = CreateFacingLights("B", roomB, u, v,
                (roomB.transform.TransformPoint(Vector3.up * 4f) - aperture.Center).normalized);
        }

        private void CreatePanel(string objectName, Vector3 planeCenter, Vector3 u, Vector3 v, Vector3 n,
            float minU, float maxU, float minV, float maxV, Material material)
        {
            if (maxU - minU <= 0.001f || maxV - minV <= 0.001f) return;
            Vector3 center = planeCenter + u * ((minU + maxU) * 0.5f) + v * ((minV + maxV) * 0.5f);
            glassRenderers.Add(CreateCube(objectName, center, u, v, n,
                new Vector3(maxU - minU, maxV - minV, BoundaryThickness), material, true));
        }

        private void CreateRail(string objectName, Vector3 planeCenter, Vector3 u, Vector3 v, Vector3 n,
            float minU, float maxU, float centerV, Material material)
        {
            if (maxU - minU <= 0.001f) return;
            Vector3 center = planeCenter + u * ((minU + maxU) * 0.5f) + v * centerV;
            CreateCube(objectName, center, u, v, n,
                new Vector3(maxU - minU, 0.12f, 0.14f), material, true);
        }

        private void CreateFramePair(string objectName, Vector3 center, float width, float height,
            Vector3 u, Vector3 v, Vector3 n, Vector3 towardA, Material structureMaterial, Material facingMaterial,
            List<Renderer> firstFacing, List<Renderer> secondFacing)
        {
            CreateCube(objectName + " Structure", center, u, v, n,
                new Vector3(width, height, BoundaryThickness), structureMaterial, true);
            firstFacing.Add(CreateCube(objectName + " A", center + towardA * (BoundaryThickness * 0.5f + FacingDepth),
                u, v, n, new Vector3(width, height, FacingDepth), facingMaterial, false));
            secondFacing.Add(CreateCube(objectName + " B", center - towardA * (BoundaryThickness * 0.5f + FacingDepth),
                u, v, n, new Vector3(width, height, FacingDepth), facingMaterial, false));
        }

        private Light[] CreateFacingLights(string suffix, CubeRoom room, Vector3 u, Vector3 v, Vector3 inward)
        {
            if (room == null || room.Lighting == null) return Array.Empty<Light>();
            Vector3[] positions =
            {
                aperture.Center - u * (aperture.Width * 0.5f + 0.04f),
                aperture.Center + u * (aperture.Width * 0.5f + 0.04f),
                aperture.Center + v * (aperture.Height * 0.5f + 0.04f),
            };
            Light[] result = new Light[positions.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                GameObject lightObject = new GameObject($"Doorway Facing Light {suffix} {index + 1}");
                generated.Add(lightObject);
                lightObject.transform.SetParent(transform, true);
                lightObject.transform.position = positions[index] + inward * 0.04f;
                lightObject.transform.rotation = Quaternion.LookRotation(inward, v);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = room.Lighting.StripColor;
                light.intensity = room.Lighting.SupportingLightIntensity * room.Lighting.DoorwayFramePowerRatio;
                light.range = room.Lighting.SupportingLightRange;
                light.spotAngle = room.Lighting.SupportingLightSpotAngle;
                light.innerSpotAngle = room.Lighting.SupportingLightInnerSpotAngle;
                light.shadows = LightShadows.None;
                light.bounceIntensity = 0f;
                result[index] = light;
            }
            return result;
        }

        private Renderer CreateCube(string objectName, Vector3 center, Vector3 u, Vector3 v, Vector3 n,
            Vector3 scale, Material material, bool collider)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            generated.Add(item);
            item.name = objectName;
            item.transform.SetParent(transform, true);
            item.transform.position = center;
            item.transform.rotation = Quaternion.LookRotation(n, v);
            item.transform.localScale = scale;
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            Collider itemCollider = item.GetComponent<Collider>();
            if (itemCollider != null) itemCollider.enabled = collider;
            return renderer;
        }

        private void ApplyPower()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            ApplyFacing(facingA, roomA, highlightedFrom == roomA);
            ApplyFacing(facingB, roomB, highlightedFrom == roomB);
            SetLights(lightsA, roomA != null && roomA.Lighting != null && roomA.Lighting.IsEffectivelyLit);
            SetLights(lightsB, roomB != null && roomB.Lighting != null && roomB.Lighting.IsEffectivelyLit);
        }

        private void ApplyFacing(Renderer[] renderers, CubeRoom room, bool highlighted)
        {
            bool powered = room != null && room.Lighting != null && room.Lighting.IsEffectivelyLit;
            float intensity = powered && room.Lighting != null
                ? room.Lighting.DoorwayFrameEmissionIntensity
                : 0f;
            Color color = highlighted ? new Color(0.15f, 1f, 1f, 1f)
                : room != null && room.Lighting != null ? room.Lighting.StripColor : Color.white;
            Color emission = color * (highlighted ? Mathf.Max(2f, intensity) : intensity);
            foreach (Renderer renderer in renderers ?? Array.Empty<Renderer>())
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, powered || highlighted ? color : new Color(0.06f, 0.07f, 0.08f, 1f));
                propertyBlock.SetColor(ColorId, powered || highlighted ? color : new Color(0.06f, 0.07f, 0.08f, 1f));
                propertyBlock.SetColor(EmissionColorId, emission);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void SetLights(Light[] lights, bool enabled)
        {
            foreach (Light light in lights ?? Array.Empty<Light>()) if (light != null) light.enabled = enabled;
        }

        private static Vector3 GetFaceCenterWorld(CubeRoom room, CubeRoomFace face)
        {
            Vector3 local = face switch
            {
                CubeRoomFace.West => new Vector3(-4f, 4f, 0f),
                CubeRoomFace.East => new Vector3(4f, 4f, 0f),
                CubeRoomFace.South => new Vector3(0f, 4f, -4f),
                CubeRoomFace.North => new Vector3(0f, 4f, 4f),
                CubeRoomFace.Floor => Vector3.zero,
                _ => new Vector3(0f, 8f, 0f),
            };
            return room.transform.TransformPoint(local);
        }

        private static Material FindGlassMaterial(CubeRoom room, CubeRoomFace face)
        {
            if (room == null) return null;
            if (CubeRoom.TryGetWall(face, out CubeRoomWall wall))
                return room.GetWallBoundary(wall)?.ClosedGlassRenderer?.sharedMaterial;
            if (face == CubeRoomFace.Ceiling && room.CeilingBoundary != null)
            {
                foreach (Renderer renderer in room.CeilingBoundary.ClosedRenderers)
                    if (renderer != null) return renderer.sharedMaterial;
            }
            return null;
        }

        private static Material FindFrameMaterial(CubeRoom room, CubeRoomFace face)
        {
            if (room == null) return null;
            if (CubeRoom.TryGetWall(face, out CubeRoomWall wall))
            {
                CubeRoomWallBoundary boundary = room.GetWallBoundary(wall);
                if (boundary != null)
                    foreach (Renderer renderer in boundary.DoorwayFrameRenderers)
                        if (renderer != null) return renderer.sharedMaterial;
            }
            if (face == CubeRoomFace.Ceiling && room.CeilingBoundary != null)
            {
                foreach (CubeRoomCeilingBoundary.EdgeVariant variant in room.CeilingBoundary.EdgeVariants)
                    foreach (Renderer renderer in variant.Renderers)
                        if (renderer != null && renderer.name.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0)
                            return renderer.sharedMaterial;
            }
            return null;
        }

        private static Material FindTrimMaterial(CubeRoom room, CubeRoomFace face)
        {
            if (room == null) return null;
            if (CubeRoom.TryGetWall(face, out CubeRoomWall wall))
                return room.GetWallBoundary(wall)?.ClosedBaseTrimRenderer?.sharedMaterial;
            return null;
        }

        private static void SetBoundaryOverride(CubeRoom room, CubeRoomFace face, bool owned)
        {
            if (room == null) return;
            if (CubeRoom.TryGetWall(face, out CubeRoomWall wall)) room.GetWallBoundary(wall)?.SetExternalGeometryOwned(owned);
            else if (face == CubeRoomFace.Ceiling) room.CeilingBoundary?.SetExternalGeometryOwned(owned);
        }

        private void Subscribe()
        {
            if (roomA != null) roomA.LightingStateChanged += HandleLightingChanged;
            if (roomB != null) roomB.LightingStateChanged += HandleLightingChanged;
        }

        private void Unsubscribe()
        {
            if (roomA != null) roomA.LightingStateChanged -= HandleLightingChanged;
            if (roomB != null) roomB.LightingStateChanged -= HandleLightingChanged;
        }

        private void HandleLightingChanged(CubeRoom room) => ApplyPower();

        private void OnDestroy()
        {
            Unsubscribe();
            ReleaseGeometryOwnership();
        }

        public void ReleaseGeometryOwnership()
        {
            if (!ownsBoundaryOverrides) return;
            ownsBoundaryOverrides = false;
            SetBoundaryOverride(roomA, faceA, false);
            SetBoundaryOverride(roomB, faceB, false);
        }

        private void ClearGenerated()
        {
            foreach (GameObject item in generated)
            {
                if (item == null) continue;
                if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
            }
            generated.Clear();
            glassRenderers.Clear();
        }
    }
}
