using UnityEngine;

namespace WhatLightRemains.Runtime
{
    public enum RoomBoundaryKind
    {
        Floor = 0,
        Ceiling = 1,
        West = 2,
        East = 3,
        South = 4,
        North = 5,
    }

    public readonly struct RoomBoundaryHit
    {
        public RoomBoundaryHit(RoomBoundaryKind boundary, Vector3 worldPoint, float distance)
        {
            Boundary = boundary;
            WorldPoint = worldPoint;
            Distance = distance;
        }

        public RoomBoundaryKind Boundary { get; }
        public Vector3 WorldPoint { get; }
        public float Distance { get; }
    }

    /// <summary>
    /// Analytic, room-filtered view targeting. It returns only the first logical boundary of
    /// CurrentRoom and therefore cannot select a distant wall through glass or a doorway.
    /// </summary>
    public static class RoomPlacementTargeting
    {
        private const float DirectionEpsilon = 0.00001f;
        private const float BoundaryEpsilon = 0.0001f;
        private const float OriginTolerance = 0.05f;

        public static bool TrySelectSideWall(CubeRoom room, Ray worldRay, out CubeRoomWall wall)
        {
            wall = default;
            if (!TryGetFirstBoundary(room, worldRay, out RoomBoundaryHit hit))
            {
                return false;
            }

            switch (hit.Boundary)
            {
                case RoomBoundaryKind.West:
                    wall = CubeRoomWall.West;
                    return true;
                case RoomBoundaryKind.East:
                    wall = CubeRoomWall.East;
                    return true;
                case RoomBoundaryKind.South:
                    wall = CubeRoomWall.South;
                    return true;
                case RoomBoundaryKind.North:
                    wall = CubeRoomWall.North;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetFirstBoundary(CubeRoom room, Ray worldRay, out RoomBoundaryHit hit)
        {
            hit = default;
            if (room == null || worldRay.direction.sqrMagnitude < DirectionEpsilon)
            {
                return false;
            }

            Vector3 origin = room.transform.InverseTransformPoint(worldRay.origin);
            Vector3 direction = room.transform.InverseTransformDirection(worldRay.direction).normalized;
            float halfWidth = CubeRoom.InteriorWidth * 0.5f;
            float halfDepth = CubeRoom.InteriorDepth * 0.5f;
            if (origin.x < -halfWidth - OriginTolerance || origin.x > halfWidth + OriginTolerance
                || origin.y < -OriginTolerance || origin.y > CubeRoom.InteriorHeight + OriginTolerance
                || origin.z < -halfDepth - OriginTolerance || origin.z > halfDepth + OriginTolerance)
            {
                return false;
            }

            float bestDistance = float.PositiveInfinity;
            RoomBoundaryKind bestBoundary = default;
            Vector3 bestPoint = default;

            // Floor and ceiling deliberately win an exact edge/corner tie. This prevents an
            // invalid vertical aim from being substituted with a neighboring side wall.
            ConsiderYPlane(RoomBoundaryKind.Floor, 0f, origin, direction, halfWidth, halfDepth, ref bestDistance, ref bestBoundary, ref bestPoint);
            ConsiderYPlane(RoomBoundaryKind.Ceiling, CubeRoom.InteriorHeight, origin, direction, halfWidth, halfDepth, ref bestDistance, ref bestBoundary, ref bestPoint);
            ConsiderXPlane(RoomBoundaryKind.West, -halfWidth, origin, direction, halfDepth, ref bestDistance, ref bestBoundary, ref bestPoint);
            ConsiderXPlane(RoomBoundaryKind.East, halfWidth, origin, direction, halfDepth, ref bestDistance, ref bestBoundary, ref bestPoint);
            ConsiderZPlane(RoomBoundaryKind.South, -halfDepth, origin, direction, halfWidth, ref bestDistance, ref bestBoundary, ref bestPoint);
            ConsiderZPlane(RoomBoundaryKind.North, halfDepth, origin, direction, halfWidth, ref bestDistance, ref bestBoundary, ref bestPoint);

            if (float.IsPositiveInfinity(bestDistance))
            {
                return false;
            }

            hit = new RoomBoundaryHit(
                bestBoundary,
                room.transform.TransformPoint(bestPoint),
                Vector3.Distance(worldRay.origin, room.transform.TransformPoint(bestPoint)));
            return true;
        }

        private static void ConsiderXPlane(RoomBoundaryKind boundary, float plane, Vector3 origin, Vector3 direction, float halfDepth, ref float bestDistance, ref RoomBoundaryKind bestBoundary, ref Vector3 bestPoint)
        {
            if (Mathf.Abs(direction.x) < DirectionEpsilon) return;
            float distance = (plane - origin.x) / direction.x;
            if (distance <= BoundaryEpsilon || distance >= bestDistance - BoundaryEpsilon) return;
            Vector3 point = origin + direction * distance;
            if (point.y < -BoundaryEpsilon || point.y > CubeRoom.InteriorHeight + BoundaryEpsilon
                || Mathf.Abs(point.z) > halfDepth + BoundaryEpsilon) return;
            bestDistance = distance;
            bestBoundary = boundary;
            bestPoint = point;
        }

        private static void ConsiderYPlane(RoomBoundaryKind boundary, float plane, Vector3 origin, Vector3 direction, float halfWidth, float halfDepth, ref float bestDistance, ref RoomBoundaryKind bestBoundary, ref Vector3 bestPoint)
        {
            if (Mathf.Abs(direction.y) < DirectionEpsilon) return;
            float distance = (plane - origin.y) / direction.y;
            if (distance <= BoundaryEpsilon || distance >= bestDistance - BoundaryEpsilon) return;
            Vector3 point = origin + direction * distance;
            if (Mathf.Abs(point.x) > halfWidth + BoundaryEpsilon || Mathf.Abs(point.z) > halfDepth + BoundaryEpsilon) return;
            bestDistance = distance;
            bestBoundary = boundary;
            bestPoint = point;
        }

        private static void ConsiderZPlane(RoomBoundaryKind boundary, float plane, Vector3 origin, Vector3 direction, float halfWidth, ref float bestDistance, ref RoomBoundaryKind bestBoundary, ref Vector3 bestPoint)
        {
            if (Mathf.Abs(direction.z) < DirectionEpsilon) return;
            float distance = (plane - origin.z) / direction.z;
            if (distance <= BoundaryEpsilon || distance >= bestDistance - BoundaryEpsilon) return;
            Vector3 point = origin + direction * distance;
            if (point.y < -BoundaryEpsilon || point.y > CubeRoom.InteriorHeight + BoundaryEpsilon
                || Mathf.Abs(point.x) > halfWidth + BoundaryEpsilon) return;
            bestDistance = distance;
            bestBoundary = boundary;
            bestPoint = point;
        }
    }
}
