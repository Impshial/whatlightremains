using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    public enum CubeRoomFace
    {
        West = 0,
        East = 1,
        South = 2,
        North = 3,
        Floor = 4,
        Ceiling = 5,
    }

    public enum RoomPassageKind
    {
        None = 0,
        Sealed = 1,
        SideDoorway = 2,
        CeilingToSideDoorway = 3,
        CeilingOpening = 4,
    }

    /// <summary>
    /// The single, world-space clear opening shared by two contacting room faces.  Keeping
    /// this data on the reciprocal connection records prevents either player's current room
    /// or a later renderer rebuild from re-authoring the opening.
    /// </summary>
    [Serializable]
    public struct RoomAperture
    {
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 horizontalAxis;
        [SerializeField] private Vector3 verticalAxis;
        [SerializeField] private Vector3 normal;
        [SerializeField] private float width;
        [SerializeField] private float height;
        [SerializeField] private CubeRoom authorityRoom;
        [SerializeField] private CubeRoomFace authorityFace;

        public RoomAperture(Vector3 center, Vector3 horizontalAxis, Vector3 verticalAxis,
            Vector3 normal, float width, float height, CubeRoom authorityRoom, CubeRoomFace authorityFace)
        {
            this.center = center;
            this.horizontalAxis = horizontalAxis.normalized;
            this.verticalAxis = verticalAxis.normalized;
            this.normal = normal.normalized;
            this.width = Mathf.Max(0f, width);
            this.height = Mathf.Max(0f, height);
            this.authorityRoom = authorityRoom;
            this.authorityFace = authorityFace;
        }

        public Vector3 Center => center;
        public Vector3 HorizontalAxis => horizontalAxis;
        public Vector3 VerticalAxis => verticalAxis;
        public Vector3 Normal => normal;
        public float Width => width;
        public float Height => height;
        public CubeRoom AuthorityRoom => authorityRoom;
        public CubeRoomFace AuthorityFace => authorityFace;
        public bool IsValid => width > 0.001f && height > 0.001f
            && horizontalAxis.sqrMagnitude > 0.9f && verticalAxis.sqrMagnitude > 0.9f;

        public Vector3 LowestPointFor(CubeRoom room)
        {
            if (room == null) return center;
            Vector3 up = room.RoomUp.normalized;
            float horizontalContribution = Mathf.Abs(Vector3.Dot(horizontalAxis, up)) * width * 0.5f;
            float verticalContribution = Mathf.Abs(Vector3.Dot(verticalAxis, up)) * height * 0.5f;
            return center - up * (horizontalContribution + verticalContribution);
        }
    }

    public enum RoomCeilingEdge
    {
        None = 0,
        West = 1,
        East = 2,
        South = 3,
        North = 4,
    }

    [Serializable]
    public readonly struct RoomOrientation : IEquatable<RoomOrientation>
    {
        [SerializeField] private readonly Vector3Int right;
        [SerializeField] private readonly Vector3Int up;
        [SerializeField] private readonly Vector3Int forward;

        public RoomOrientation(Vector3Int right, Vector3Int up, Vector3Int forward)
        {
            if (!IsCardinal(right) || !IsCardinal(up) || !IsCardinal(forward)
                || Dot(right, up) != 0
                || Cross(right, up) != forward)
            {
                throw new ArgumentException("Room orientation axes must form a right-handed cardinal basis.");
            }

            this.right = right;
            this.up = up;
            this.forward = forward;
        }

        public static RoomOrientation Identity => new RoomOrientation(Vector3Int.right, Vector3Int.up, new Vector3Int(0, 0, 1));
        public Vector3Int Right => right == Vector3Int.zero ? Vector3Int.right : right;
        public Vector3Int Up => up == Vector3Int.zero ? Vector3Int.up : up;
        public Vector3Int Forward => forward == Vector3Int.zero ? new Vector3Int(0, 0, 1) : forward;
        public Quaternion Rotation => Quaternion.LookRotation((Vector3)Forward, (Vector3)Up);

        public Vector3Int TransformDirection(Vector3Int localDirection)
        {
            return Right * localDirection.x + Up * localDirection.y + Forward * localDirection.z;
        }

        public Vector3Int InverseTransformDirection(Vector3Int gridDirection)
        {
            return new Vector3Int(
                Dot(gridDirection, Right),
                Dot(gridDirection, Up),
                Dot(gridDirection, Forward));
        }

        public RoomOrientation RotateAroundGridAxis(Vector3Int gridAxis, int quarterTurns)
        {
            int turns = ((quarterTurns % 4) + 4) % 4;
            Vector3Int nextRight = Right;
            Vector3Int nextUp = Up;
            Vector3Int nextForward = Forward;
            for (int index = 0; index < turns; index++)
            {
                nextRight = RotatePositiveQuarter(nextRight, gridAxis);
                nextUp = RotatePositiveQuarter(nextUp, gridAxis);
                nextForward = RotatePositiveQuarter(nextForward, gridAxis);
            }

            return new RoomOrientation(nextRight, nextUp, nextForward);
        }

        public RoomOrientation RotateAroundLocalAxis(Vector3Int localAxis, int quarterTurns)
        {
            return RotateAroundGridAxis(TransformDirection(localAxis), quarterTurns);
        }

        public static RoomOrientation FromRotation(Quaternion rotation, Quaternion gridRotation)
        {
            Quaternion local = Quaternion.Inverse(gridRotation) * rotation;
            Vector3Int snappedRight = SnapCardinal(local * Vector3.right);
            Vector3Int snappedUp = SnapCardinal(local * Vector3.up);
            return new RoomOrientation(snappedRight, snappedUp, Cross(snappedRight, snappedUp));
        }

        public static IReadOnlyList<RoomOrientation> All
        {
            get
            {
                List<RoomOrientation> orientations = new List<RoomOrientation>(24);
                Vector3Int[] directions =
                {
                    Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
                    new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
                };
                foreach (Vector3Int candidateUp in directions)
                {
                    foreach (Vector3Int candidateRight in directions)
                    {
                        if (Dot(candidateUp, candidateRight) != 0) continue;
                        orientations.Add(new RoomOrientation(candidateRight, candidateUp, Cross(candidateRight, candidateUp)));
                    }
                }

                return orientations;
            }
        }

        public bool Equals(RoomOrientation other)
        {
            return Right == other.Right && Up == other.Up && Forward == other.Forward;
        }

        public override bool Equals(object obj) => obj is RoomOrientation other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Right.GetHashCode();
                hash = (hash * 397) ^ Up.GetHashCode();
                return (hash * 397) ^ Forward.GetHashCode();
            }
        }
        public static bool operator ==(RoomOrientation left, RoomOrientation right) => left.Equals(right);
        public static bool operator !=(RoomOrientation left, RoomOrientation right) => !left.Equals(right);

        private static Vector3Int RotatePositiveQuarter(Vector3Int direction, Vector3Int axis)
        {
            if (!IsCardinal(axis)) throw new ArgumentException("Rotation axis must be cardinal.", nameof(axis));
            return Cross(axis, direction) + axis * Dot(axis, direction);
        }

        private static Vector3Int SnapCardinal(Vector3 direction)
        {
            float x = Mathf.Abs(direction.x);
            float y = Mathf.Abs(direction.y);
            float z = Mathf.Abs(direction.z);
            if (x >= y && x >= z) return direction.x >= 0f ? Vector3Int.right : Vector3Int.left;
            if (y >= z) return direction.y >= 0f ? Vector3Int.up : Vector3Int.down;
            return direction.z >= 0f ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 0, -1);
        }

        private static bool IsCardinal(Vector3Int value)
        {
            return Mathf.Abs(value.x) + Mathf.Abs(value.y) + Mathf.Abs(value.z) == 1;
        }

        private static int Dot(Vector3Int first, Vector3Int second)
        {
            return first.x * second.x + first.y * second.y + first.z * second.z;
        }

        private static Vector3Int Cross(Vector3Int first, Vector3Int second)
        {
            return new Vector3Int(
                first.y * second.z - first.z * second.y,
                first.z * second.x - first.x * second.z,
                first.x * second.y - first.y * second.x);
        }
    }

    [Serializable]
    public struct RoomFaceConnection
    {
        [SerializeField] private CubeRoom neighbor;
        [SerializeField] private CubeRoomFace neighborFace;
        [SerializeField] private RoomPassageKind passageKind;
        [SerializeField] private RoomCeilingEdge ceilingEdge;
        [SerializeField] private bool ownsSharedBoundary;
        [SerializeField] private RoomAperture aperture;

        public RoomFaceConnection(CubeRoom neighbor, CubeRoomFace neighborFace, RoomPassageKind passageKind,
            RoomCeilingEdge ceilingEdge, bool ownsSharedBoundary, RoomAperture aperture = default)
        {
            this.neighbor = neighbor;
            this.neighborFace = neighborFace;
            this.passageKind = passageKind;
            this.ceilingEdge = ceilingEdge;
            this.ownsSharedBoundary = ownsSharedBoundary;
            this.aperture = aperture;
        }

        public CubeRoom Neighbor => neighbor;
        public CubeRoomFace NeighborFace => neighborFace;
        public RoomPassageKind PassageKind => passageKind;
        public RoomCeilingEdge CeilingEdge => ceilingEdge;
        public bool OwnsSharedBoundary => ownsSharedBoundary;
        public RoomAperture Aperture => aperture;
        public bool IsConnected => neighbor != null;
        public bool IsTraversable => aperture.IsValid && passageKind != RoomPassageKind.None && passageKind != RoomPassageKind.Sealed;
    }

    public readonly struct RoomConnectionPlan
    {
        public RoomConnectionPlan(CubeRoom neighbor, CubeRoomFace neighborFace, CubeRoomFace candidateFace,
            RoomPassageKind passageKind, RoomCeilingEdge ceilingEdge, RoomAperture aperture = default)
        {
            Neighbor = neighbor;
            NeighborFace = neighborFace;
            CandidateFace = candidateFace;
            PassageKind = passageKind;
            CeilingEdge = ceilingEdge;
            Aperture = aperture;
        }

        public CubeRoom Neighbor { get; }
        public CubeRoomFace NeighborFace { get; }
        public CubeRoomFace CandidateFace { get; }
        public RoomPassageKind PassageKind { get; }
        public RoomCeilingEdge CeilingEdge { get; }
        public RoomAperture Aperture { get; }
        public bool IsTraversable => Aperture.IsValid && PassageKind != RoomPassageKind.None && PassageKind != RoomPassageKind.Sealed;
    }
}
