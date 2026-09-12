using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WhatLightRemains.Runtime
{
    public enum DeviceFootprintRotation
    {
        Degrees0 = 0,
        Degrees90 = 1,
        Degrees180 = 2,
        Degrees270 = 3,
    }

    public enum DeviceWallPlacementFailure
    {
        None,
        InvalidWall,
        InvalidFootprint,
        OutOfBounds,
        OpeningBlocked,
        Occupied,
        PhysicalBoundsBlocked,
    }

    [Serializable]
    public sealed class DeviceFootprint
    {
        [SerializeField] private Vector2Int[] anchorOffsets = { Vector2Int.zero };
        [SerializeField] private Vector2 physicalSize = new Vector2(0.75f, 0.75f);

        public DeviceFootprint() { }

        public DeviceFootprint(IEnumerable<Vector2Int> offsets, Vector2 size)
        {
            anchorOffsets = offsets != null ? new List<Vector2Int>(offsets).ToArray() : Array.Empty<Vector2Int>();
            physicalSize = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        }

        public IReadOnlyList<Vector2Int> AnchorOffsets => anchorOffsets ?? Array.Empty<Vector2Int>();
        public Vector2 PhysicalSize => physicalSize;
        public static DeviceFootprint Single => new DeviceFootprint(
            new[] { Vector2Int.zero }, new Vector2(0.75f, 0.75f));
        public static DeviceFootprint HorizontalPair => new DeviceFootprint(
            new[] { Vector2Int.zero, Vector2Int.right }, new Vector2(2.75f, 0.75f));

        public Vector2Int GetRotatedOffset(int index, DeviceFootprintRotation rotation)
        {
            Vector2Int value = anchorOffsets[index];
            return Rotate(value, rotation);
        }

        public Rect GetPhysicalBounds(Vector2 origin, DeviceFootprintRotation rotation)
        {
            Vector2 size = physicalSize;
            if (rotation == DeviceFootprintRotation.Degrees90 || rotation == DeviceFootprintRotation.Degrees270)
                size = new Vector2(size.y, size.x);
            Vector2Int minimum = GetRotatedOffset(0, rotation);
            Vector2Int maximum = minimum;
            for (int index = 1; index < AnchorOffsets.Count; index++)
            {
                Vector2Int offset = GetRotatedOffset(index, rotation);
                minimum = Vector2Int.Min(minimum, offset);
                maximum = Vector2Int.Max(maximum, offset);
            }
            Vector2 centerOffset = ((Vector2)minimum + (Vector2)maximum) * (DeviceWall.AnchorSpacing * 0.5f);
            return new Rect(origin + centerOffset - size * 0.5f, size);
        }

        public static Vector2Int Rotate(Vector2Int value, DeviceFootprintRotation rotation)
        {
            return rotation switch
            {
                DeviceFootprintRotation.Degrees90 => new Vector2Int(-value.y, value.x),
                DeviceFootprintRotation.Degrees180 => new Vector2Int(-value.x, -value.y),
                DeviceFootprintRotation.Degrees270 => new Vector2Int(value.y, -value.x),
                _ => value,
            };
        }
    }

    public sealed class DeviceWallReservation
    {
        internal DeviceWallReservation(DeviceWall owner, int[] indices, Rect physicalBounds)
        {
            Owner = owner;
            AnchorIndices = indices;
            PhysicalBounds = physicalBounds;
        }

        internal DeviceWall Owner { get; }
        public IReadOnlyList<int> AnchorIndices { get; }
        public Rect PhysicalBounds { get; }
        public bool IsReleased { get; internal set; }
        public bool IsConflicted { get; internal set; }
    }

    public sealed class DeviceWallAnchor
    {
        internal DeviceWallAnchor(int index, int row, int column, Vector2 position)
        {
            Index = index;
            Row = row;
            Column = column;
            WallPosition = position;
        }

        public int Index { get; }
        public int Row { get; }
        public int Column { get; }
        public Vector2 WallPosition { get; }
        public bool IsOpeningBlocked { get; internal set; }
        public bool IsOccupied => Reservation != null && !Reservation.IsReleased;
        public bool IsAvailable => !IsOpeningBlocked && !IsOccupied;
        internal DeviceWallReservation Reservation { get; set; }
    }

    /// <summary>
    /// The room's fixed West face is its Device Wall. Wall coordinates are centered on the
    /// 8 m face: horizontal +X points along room-local +Z, vertical +Y points along room-local
    /// +Y, and positive normal points inward along room-local +X.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeviceWall : MonoBehaviour
    {
        public const int GridSize = 4;
        public const float AnchorSpacing = 2f;
        public const float AnchorInset = 1f;
        public const CubeRoomWall DefaultFace = CubeRoomWall.West;

        [SerializeField] private CubeRoom owner;
        [SerializeField] private CubeRoomWall face = DefaultFace;
        [SerializeField] private Material smokedGlassMaterial;
        [SerializeField] private Material metalMaterial;
        [SerializeField] private Material availableMarkerMaterial;
        [SerializeField] private Material blockedMarkerMaterial;
        [SerializeField, Range(0.08f, 0.12f)] private float studDiameter = 0.10f;
        [SerializeField, Range(0.02f, 0.03f)] private float studProjection = 0.025f;
        [SerializeField, Min(0.02f)] private float railWidth = 0.06f;
        [SerializeField, Min(0.02f)] private float frameWidth = 0.08f;
        [SerializeField, Min(0f)] private float apertureFrameClearance = 0.10f;
        [SerializeField, Min(0f)] private float edgeTolerance = 0.01f;
        [SerializeField, HideInInspector] private Transform hardwareRoot;
        [SerializeField, HideInInspector] private Transform overlayRoot;

        private readonly DeviceWallAnchor[] anchors = new DeviceWallAnchor[GridSize * GridSize];
        private readonly List<DeviceWallReservation> reservations = new List<DeviceWallReservation>();
        private bool anchorsInitialized;
        private bool hasOpening;
        private Rect openingBounds;

        public CubeRoom Owner => owner;
        public CubeRoomWall Face => face;
        public Material SmokedGlassMaterial => smokedGlassMaterial;
        public Material MetalMaterial => metalMaterial;
        public Material AvailableMarkerMaterial => availableMarkerMaterial;
        public Material BlockedMarkerMaterial => blockedMarkerMaterial;
        public Transform HardwareRoot => hardwareRoot;
        public Transform OverlayRoot => overlayRoot;
        public bool IsOverlayVisible => overlayRoot != null && overlayRoot.gameObject.activeSelf;
        public IReadOnlyList<DeviceWallAnchor> Anchors
        {
            get { EnsureAnchors(); return anchors; }
        }
        public bool HasOpening => hasOpening;
        public Rect OpeningBounds => openingBounds;

        public void Configure(CubeRoom room, CubeRoomWall wall, Material glass, Material metal,
            Material availableMarker, Material blockedMarker)
        {
            owner = room;
            face = wall;
            smokedGlassMaterial = glass;
            metalMaterial = metal;
            availableMarkerMaterial = availableMarker;
            blockedMarkerMaterial = blockedMarker;
            ApplySmokedGlassMaterial();
            RefreshFromConnections();
            SetOverlayVisible(false);
        }

        public void ConfigureOwner(CubeRoom room)
        {
            owner = room;
            ApplySmokedGlassMaterial();
        }

        public DeviceWallAnchor GetAnchor(int row, int column)
        {
            EnsureAnchors();
            return row < 0 || row >= GridSize || column < 0 || column >= GridSize
                ? null : anchors[row * GridSize + column];
        }

        public Vector3 GetAnchorLocalPosition(int index)
        {
            EnsureAnchors();
            if (index < 0 || index >= anchors.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Vector2 point = anchors[index].WallPosition;
            return WallCenterLocal + HorizontalLocal * point.x + VerticalLocal * point.y
                + InwardNormalLocal * studProjection;
        }

        public Vector3 GetAnchorWorldPosition(int index) => transform.TransformPoint(GetAnchorLocalPosition(index));

        public bool CanReserve(DeviceFootprint footprint, int originRow, int originColumn,
            DeviceFootprintRotation rotation, out DeviceWallPlacementFailure failure)
        {
            return ValidateFootprint(footprint, originRow, originColumn, rotation, out _, out _, out failure);
        }

        public bool TryReserve(DeviceFootprint footprint, int originRow, int originColumn,
            DeviceFootprintRotation rotation, out DeviceWallReservation reservation,
            out DeviceWallPlacementFailure failure)
        {
            reservation = null;
            if (!ValidateFootprint(footprint, originRow, originColumn, rotation,
                    out int[] indices, out Rect physicalBounds, out failure))
                return false;

            reservation = new DeviceWallReservation(this, indices, physicalBounds);
            for (int index = 0; index < indices.Length; index++) anchors[indices[index]].Reservation = reservation;
            reservations.Add(reservation);
            RefreshOverlayIfVisible();
            return true;
        }

        public bool Release(DeviceWallReservation reservation)
        {
            if (reservation == null || reservation.Owner != this || reservation.IsReleased) return false;
            foreach (int index in reservation.AnchorIndices)
                if (index >= 0 && index < anchors.Length && anchors[index].Reservation == reservation)
                    anchors[index].Reservation = null;
            reservation.IsReleased = true;
            reservations.Remove(reservation);
            RefreshOverlayIfVisible();
            return true;
        }

        /// <summary>
        /// Reveals the logical anchor state for a future equipment-placement mode. The overlay is
        /// renderer-only and remains hidden during ordinary play.
        /// </summary>
        public void SetOverlayVisible(bool visible)
        {
            EnsureOverlayRoot();
            if (visible) RebuildOverlay();
            overlayRoot.gameObject.SetActive(visible);
        }

        public void RefreshFromConnections()
        {
            EnsureAnchors();
            ApplySmokedGlassMaterial();
            hasOpening = TryGetOpeningBounds(owner, CubeRoom.ToFace(face), transform, apertureFrameClearance,
                out openingBounds);
            float radius = studDiameter * 0.5f + edgeTolerance;
            foreach (DeviceWallAnchor anchor in anchors)
            {
                anchor.IsOpeningBlocked = hasOpening && CircleIntersectsRect(anchor.WallPosition, radius, openingBounds);
            }
            foreach (DeviceWallReservation reservation in reservations)
            {
                reservation.IsConflicted = hasOpening && reservation.PhysicalBounds.Overlaps(openingBounds, true);
                foreach (int index in reservation.AnchorIndices)
                    if (index >= 0 && index < anchors.Length && anchors[index].IsOpeningBlocked)
                    {
                        reservation.IsConflicted = true;
                        break;
                    }
            }
            RebuildHardware();
            RefreshOverlayIfVisible();
        }

        public static bool TryGetOpeningBounds(CubeRoom room, CubeRoomFace wallFace, Transform roomTransform,
            float clearance, out Rect bounds)
        {
            bounds = default;
            if (room == null || roomTransform == null) return false;
            RoomFaceConnection connection = room.GetConnection(wallFace);
            if (!connection.IsTraversable || !connection.Aperture.IsValid) return false;
            return TryProjectAperture(connection.Aperture, roomTransform, wallFace, clearance, out bounds);
        }

        public static bool TryProjectAperture(RoomAperture aperture, Transform roomTransform,
            CubeRoomFace wallFace, float clearance, out Rect bounds)
        {
            bounds = default;
            if (!aperture.IsValid || roomTransform == null || !CubeRoom.TryGetWall(wallFace, out CubeRoomWall wall))
                return false;
            GetWallBasis(wall, out Vector3 center, out Vector3 horizontal, out Vector3 vertical, out _);
            Vector3 localCenter = roomTransform.InverseTransformPoint(aperture.Center) - center;
            Vector3 localHorizontal = roomTransform.InverseTransformDirection(aperture.HorizontalAxis);
            Vector3 localVertical = roomTransform.InverseTransformDirection(aperture.VerticalAxis);
            float centerX = Vector3.Dot(localCenter, horizontal);
            float centerY = Vector3.Dot(localCenter, vertical);
            float extentX = Mathf.Abs(Vector3.Dot(localHorizontal, horizontal)) * aperture.Width * 0.5f
                + Mathf.Abs(Vector3.Dot(localVertical, horizontal)) * aperture.Height * 0.5f + clearance;
            float extentY = Mathf.Abs(Vector3.Dot(localHorizontal, vertical)) * aperture.Width * 0.5f
                + Mathf.Abs(Vector3.Dot(localVertical, vertical)) * aperture.Height * 0.5f + clearance;
            bounds = Rect.MinMaxRect(centerX - extentX, centerY - extentY, centerX + extentX, centerY + extentY);
            return true;
        }

        public static void GetWallBasis(CubeRoomWall wall, out Vector3 center, out Vector3 horizontal,
            out Vector3 vertical, out Vector3 inward)
        {
            vertical = Vector3.up;
            switch (wall)
            {
                case CubeRoomWall.West:
                    center = new Vector3(-4f, 4f, 0f); horizontal = Vector3.forward; inward = Vector3.right; break;
                case CubeRoomWall.East:
                    center = new Vector3(4f, 4f, 0f); horizontal = Vector3.back; inward = Vector3.left; break;
                case CubeRoomWall.South:
                    center = new Vector3(0f, 4f, -4f); horizontal = Vector3.left; inward = Vector3.forward; break;
                default:
                    center = new Vector3(0f, 4f, 4f); horizontal = Vector3.right; inward = Vector3.back; break;
            }
        }

        private Vector3 WallCenterLocal { get { GetWallBasis(face, out Vector3 v, out _, out _, out _); return v; } }
        private Vector3 HorizontalLocal { get { GetWallBasis(face, out _, out Vector3 v, out _, out _); return v; } }
        private Vector3 VerticalLocal { get { GetWallBasis(face, out _, out _, out Vector3 v, out _); return v; } }
        private Vector3 InwardNormalLocal { get { GetWallBasis(face, out _, out _, out _, out Vector3 v); return v; } }

        private void Awake()
        {
            owner ??= GetComponent<CubeRoom>();
            EnsureAnchors();
            ApplySmokedGlassMaterial();
        }

        private void EnsureAnchors()
        {
            if (anchorsInitialized) return;
            for (int row = 0; row < GridSize; row++)
                for (int column = 0; column < GridSize; column++)
                {
                    int index = row * GridSize + column;
                    anchors[index] = new DeviceWallAnchor(index, row, column,
                        new Vector2(-3f + column * AnchorSpacing, -3f + row * AnchorSpacing));
                }
            anchorsInitialized = true;
        }

        private bool ValidateFootprint(DeviceFootprint footprint, int originRow, int originColumn,
            DeviceFootprintRotation rotation, out int[] indices, out Rect physicalBounds,
            out DeviceWallPlacementFailure failure)
        {
            EnsureAnchors();
            indices = null;
            physicalBounds = default;
            if (owner == null) { failure = DeviceWallPlacementFailure.InvalidWall; return false; }
            if (footprint == null || footprint.AnchorOffsets.Count == 0)
            { failure = DeviceWallPlacementFailure.InvalidFootprint; return false; }
            List<int> selected = new List<int>(footprint.AnchorOffsets.Count);
            for (int index = 0; index < footprint.AnchorOffsets.Count; index++)
            {
                Vector2Int offset = footprint.GetRotatedOffset(index, rotation);
                int row = originRow + offset.y;
                int column = originColumn + offset.x;
                DeviceWallAnchor anchor = GetAnchor(row, column);
                if (anchor == null) { failure = DeviceWallPlacementFailure.OutOfBounds; return false; }
                if (anchor.IsOpeningBlocked) { failure = DeviceWallPlacementFailure.OpeningBlocked; return false; }
                if (anchor.IsOccupied) { failure = DeviceWallPlacementFailure.Occupied; return false; }
                if (selected.Contains(anchor.Index))
                { failure = DeviceWallPlacementFailure.InvalidFootprint; return false; }
                selected.Add(anchor.Index);
            }
            DeviceWallAnchor origin = GetAnchor(originRow, originColumn);
            if (origin == null) { failure = DeviceWallPlacementFailure.OutOfBounds; return false; }
            Rect physical = footprint.GetPhysicalBounds(origin.WallPosition, rotation);
            const float half = 4f;
            if (physical.xMin < -half + edgeTolerance || physical.xMax > half - edgeTolerance
                || physical.yMin < -half + edgeTolerance || physical.yMax > half - edgeTolerance)
            { failure = DeviceWallPlacementFailure.OutOfBounds; return false; }
            if (hasOpening && physical.Overlaps(openingBounds, true))
            { failure = DeviceWallPlacementFailure.PhysicalBoundsBlocked; return false; }
            foreach (DeviceWallReservation existing in reservations)
                if (!existing.IsReleased && physical.Overlaps(existing.PhysicalBounds, true))
                { failure = DeviceWallPlacementFailure.PhysicalBoundsBlocked; return false; }
            indices = selected.ToArray();
            physicalBounds = physical;
            failure = DeviceWallPlacementFailure.None;
            return true;
        }

        private void ApplySmokedGlassMaterial()
        {
            if (owner == null || smokedGlassMaterial == null) return;
            CubeRoomWallBoundary boundary = owner.GetWallBoundary(face);
            if (boundary == null) return;
            ApplyMaterial(boundary.ClosedGlassRenderer, smokedGlassMaterial);
            foreach (Renderer renderer in boundary.DoorwayGlassRenderers) ApplyMaterial(renderer, smokedGlassMaterial);
        }

        private static void ApplyMaterial(Renderer renderer, Material material)
        {
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private void RebuildHardware()
        {
            EnsureHardwareRoot();
            ClearChildren(hardwareRoot);
            if (metalMaterial == null) return;
            GetWallBasis(face, out Vector3 center, out Vector3 horizontal, out Vector3 vertical, out Vector3 inward);
            float depth = Mathf.Max(0.02f, studProjection);
            for (int column = 0; column < GridSize; column++)
                CreateVerticalSegments($"Rail {column + 1}", -3f + column * AnchorSpacing,
                    -4f, 4f, railWidth, depth * 0.60f, center, horizontal, vertical, inward);
            CreateVerticalSegments("Frame Left", -4f + frameWidth * 0.5f, -4f, 4f,
                frameWidth, depth, center, horizontal, vertical, inward);
            CreateVerticalSegments("Frame Right", 4f - frameWidth * 0.5f, -4f, 4f,
                frameWidth, depth, center, horizontal, vertical, inward);
            CreateHorizontalSegments("Frame Bottom", -4f, -4f, 4f, frameWidth, depth,
                center, horizontal, vertical, inward);
            CreateHorizontalSegments("Frame Top", 4f, -4f, 4f, frameWidth, depth,
                center, horizontal, vertical, inward);
            foreach (DeviceWallAnchor anchor in anchors)
                if (!anchor.IsOpeningBlocked) CreateStud(anchor, center, horizontal, vertical, inward);
        }

        private void CreateVerticalSegments(string label, float x, float minY, float maxY, float width, float depth,
            Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            if (!hasOpening || x + width * 0.5f < openingBounds.xMin || x - width * 0.5f > openingBounds.xMax)
            { CreateBox(label, x, (minY + maxY) * 0.5f, width, maxY - minY, depth, center, horizontal, vertical, inward); return; }
            if (openingBounds.yMin > minY)
                CreateBox(label + " Lower", x, (minY + Mathf.Min(maxY, openingBounds.yMin)) * 0.5f,
                    width, Mathf.Min(maxY, openingBounds.yMin) - minY, depth, center, horizontal, vertical, inward);
            if (openingBounds.yMax < maxY)
                CreateBox(label + " Upper", x, (Mathf.Max(minY, openingBounds.yMax) + maxY) * 0.5f,
                    width, maxY - Mathf.Max(minY, openingBounds.yMax), depth, center, horizontal, vertical, inward);
        }

        private void CreateHorizontalSegments(string label, float y, float minX, float maxX, float width, float depth,
            Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            if (!hasOpening || y + width * 0.5f < openingBounds.yMin || y - width * 0.5f > openingBounds.yMax)
            { CreateBox(label, (minX + maxX) * 0.5f, y, maxX - minX, width, depth, center, horizontal, vertical, inward); return; }
            if (openingBounds.xMin > minX)
                CreateBox(label + " Left", (minX + Mathf.Min(maxX, openingBounds.xMin)) * 0.5f, y,
                    Mathf.Min(maxX, openingBounds.xMin) - minX, width, depth, center, horizontal, vertical, inward);
            if (openingBounds.xMax < maxX)
                CreateBox(label + " Right", (Mathf.Max(minX, openingBounds.xMax) + maxX) * 0.5f, y,
                    maxX - Mathf.Max(minX, openingBounds.xMax), width, depth, center, horizontal, vertical, inward);
        }

        private void CreateBox(string label, float x, float y, float width, float height, float depth,
            Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            if (width <= 0.001f || height <= 0.001f) return;
            GameObject item = new GameObject(label);
            item.name = label;
            item.transform.SetParent(hardwareRoot, false);
            item.transform.localPosition = center + horizontal * x + vertical * y + inward * (depth * 0.5f + 0.015f);
            item.transform.localRotation = Quaternion.LookRotation(-inward, vertical);
            item.transform.localScale = new Vector3(width, height, depth);
            item.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            ConfigureHardwareRenderer(item.AddComponent<MeshRenderer>());
        }

        private void CreateStud(DeviceWallAnchor anchor, Vector3 center, Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            Vector3 basePosition = center + horizontal * anchor.WallPosition.x + vertical * anchor.WallPosition.y;
            GameObject baseObject = new GameObject($"Anchor {anchor.Row},{anchor.Column} Base");
            baseObject.name = $"Anchor {anchor.Row},{anchor.Column} Base";
            baseObject.transform.SetParent(hardwareRoot, false);
            baseObject.transform.localPosition = basePosition + inward * (studProjection * 0.40f + 0.015f);
            baseObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, inward);
            baseObject.transform.localScale = new Vector3(studDiameter, studProjection * 0.40f, studDiameter);
            baseObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            ConfigureHardwareRenderer(baseObject.AddComponent<MeshRenderer>());

            GameObject collar = new GameObject($"Anchor {anchor.Row},{anchor.Column} Locking Collar");
            collar.name = $"Anchor {anchor.Row},{anchor.Column} Locking Collar";
            collar.transform.SetParent(hardwareRoot, false);
            collar.transform.localPosition = basePosition + inward * (studProjection * 0.82f + 0.015f);
            collar.transform.localRotation = Quaternion.FromToRotation(Vector3.up, inward);
            collar.transform.localScale = new Vector3(studDiameter * 0.62f, studProjection * 0.24f, studDiameter * 0.62f);
            collar.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            ConfigureHardwareRenderer(collar.AddComponent<MeshRenderer>());

            GameObject groove = new GameObject($"Anchor {anchor.Row},{anchor.Column} Groove");
            groove.name = $"Anchor {anchor.Row},{anchor.Column} Groove";
            groove.transform.SetParent(hardwareRoot, false);
            groove.transform.localPosition = basePosition + inward * (studProjection + 0.016f);
            groove.transform.localRotation = Quaternion.LookRotation(-inward, vertical);
            groove.transform.localScale = new Vector3(studDiameter * 0.52f, 0.012f, 0.008f);
            groove.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            ConfigureHardwareRenderer(groove.AddComponent<MeshRenderer>());
        }

        private void ConfigureHardwareRenderer(Renderer renderer)
        {
            renderer.sharedMaterial = metalMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void RefreshOverlayIfVisible()
        {
            if (IsOverlayVisible) RebuildOverlay();
        }

        private void RebuildOverlay()
        {
            EnsureAnchors();
            EnsureOverlayRoot();
            ClearChildren(overlayRoot);
            GetWallBasis(face, out Vector3 center, out Vector3 horizontal, out Vector3 vertical, out Vector3 inward);
            foreach (DeviceWallAnchor anchor in anchors)
            {
                bool available = anchor.IsAvailable;
                Material markerMaterial = available ? availableMarkerMaterial : blockedMarkerMaterial;
                if (markerMaterial == null) continue;
                CreateMarkerRing(anchor, markerMaterial, center, horizontal, vertical, inward);
                if (!available) CreateBlockedMarkerCross(anchor, markerMaterial, center, horizontal, vertical, inward);
            }
        }

        private void CreateMarkerRing(DeviceWallAnchor anchor, Material material, Vector3 center,
            Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            const int segmentCount = 24;
            const float radius = 0.18f;
            Vector3 markerCenter = center + horizontal * anchor.WallPosition.x + vertical * anchor.WallPosition.y
                + inward * (studProjection + 0.03f);
            GameObject marker = new GameObject($"Anchor {anchor.Row},{anchor.Column} Indicator");
            marker.transform.SetParent(overlayRoot, false);
            LineRenderer line = marker.AddComponent<LineRenderer>();
            ConfigureOverlayLine(line, material, segmentCount, true);
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = Mathf.PI * 2f * index / segmentCount;
                line.SetPosition(index, markerCenter + horizontal * (Mathf.Cos(angle) * radius)
                    + vertical * (Mathf.Sin(angle) * radius));
            }
        }

        private void CreateBlockedMarkerCross(DeviceWallAnchor anchor, Material material, Vector3 center,
            Vector3 horizontal, Vector3 vertical, Vector3 inward)
        {
            const float extent = 0.12f;
            Vector3 markerCenter = center + horizontal * anchor.WallPosition.x + vertical * anchor.WallPosition.y
                + inward * (studProjection + 0.031f);
            CreateOverlaySegment($"Anchor {anchor.Row},{anchor.Column} X A", markerCenter - horizontal * extent - vertical * extent,
                markerCenter + horizontal * extent + vertical * extent, material);
            CreateOverlaySegment($"Anchor {anchor.Row},{anchor.Column} X B", markerCenter - horizontal * extent + vertical * extent,
                markerCenter + horizontal * extent - vertical * extent, material);
        }

        private void CreateOverlaySegment(string label, Vector3 start, Vector3 end, Material material)
        {
            GameObject marker = new GameObject(label);
            marker.transform.SetParent(overlayRoot, false);
            LineRenderer line = marker.AddComponent<LineRenderer>();
            ConfigureOverlayLine(line, material, 2, false);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static void ConfigureOverlayLine(LineRenderer line, Material material, int positionCount, bool loop)
        {
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.positionCount = positionCount;
            line.loop = loop;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private void EnsureHardwareRoot()
        {
            if (hardwareRoot != null) return;
            Transform found = transform.Find("Device Wall Hardware");
            if (found != null) hardwareRoot = found;
            else
            {
                GameObject root = new GameObject("Device Wall Hardware");
                root.transform.SetParent(transform, false);
                hardwareRoot = root.transform;
            }
        }

        private void EnsureOverlayRoot()
        {
            if (overlayRoot != null) return;
            Transform found = transform.Find("Device Wall Anchor Overlay");
            if (found != null) overlayRoot = found;
            else
            {
                GameObject root = new GameObject("Device Wall Anchor Overlay");
                root.transform.SetParent(transform, false);
                overlayRoot = root.transform;
                overlayRoot.gameObject.SetActive(false);
            }
        }

        private static bool CircleIntersectsRect(Vector2 center, float radius, Rect rect)
        {
            float closestX = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            float closestY = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            return (center - new Vector2(closestX, closestY)).sqrMagnitude <= radius * radius;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int index = root.childCount - 1; index >= 0; index--)
                DestroyUnityObject(root.GetChild(index).gameObject);
        }

        private static void DestroyUnityObject(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
