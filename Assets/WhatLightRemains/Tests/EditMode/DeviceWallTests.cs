using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class DeviceWallTests
    {
        private GameObject roomObject;
        private CubeRoom room;
        private DeviceWall wall;

        [SetUp]
        public void SetUp()
        {
            roomObject = new GameObject("Device Wall Test Room");
            room = roomObject.AddComponent<CubeRoom>();
            wall = roomObject.AddComponent<DeviceWall>();
            wall.Configure(room, CubeRoomWall.West, null, null, null, null);
            room.ConfigureDeviceWall(wall);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(roomObject);
        }

        [Test]
        public void ClearWall_HasStableFourByFourGridAtOneMeterInset()
        {
            Assert.That(wall.Face, Is.EqualTo(CubeRoomWall.West));
            Assert.That(wall.Anchors.Count(), Is.EqualTo(16));
            Assert.That(wall.OverlayRoot, Is.Not.Null);
            Assert.That(wall.IsOverlayVisible, Is.False,
                "Placed rooms keep colored anchor indicators hidden during ordinary play.");
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                {
                    DeviceWallAnchor anchor = wall.GetAnchor(row, column);
                    Assert.That(anchor.Index, Is.EqualTo(row * 4 + column));
                    Assert.That(anchor.Row, Is.EqualTo(row));
                    Assert.That(anchor.Column, Is.EqualTo(column));
                    Assert.That(anchor.WallPosition,
                        Is.EqualTo(new Vector2(-3f + column * 2f, -3f + row * 2f)));
                    Assert.That(anchor.IsAvailable, Is.True);
                }
        }

        [Test]
        public void OverlayHook_TogglesWithoutAddingCollisionOrLights()
        {
            wall.SetOverlayVisible(true);
            Assert.That(wall.IsOverlayVisible, Is.True);
            Assert.That(wall.OverlayRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(wall.OverlayRoot.GetComponentsInChildren<Light>(true), Is.Empty);

            wall.SetOverlayVisible(false);
            Assert.That(wall.IsOverlayVisible, Is.False);
        }

        [Test]
        public void InitialOrientation_PutsFixedWestFaceOnPlacementLeftAndPreservesGravity()
        {
            RoomOrientation source = RoomOrientation.Identity.RotateAroundGridAxis(Vector3Int.forward, 1);
            foreach (CubeRoomFace face in new[]
                     { CubeRoomFace.West, CubeRoomFace.East, CubeRoomFace.South, CubeRoomFace.North, CubeRoomFace.Ceiling })
            {
                RoomOrientation result = RoomCreationController.CalculateInitialDeviceWallOrientation(source, face);
                Vector3Int direction = source.TransformDirection(CubeRoom.GetGridDirection(face));
                Vector3Int referenceUp = Mathf.Abs(Dot(direction, source.Up)) == 1
                    ? source.Forward : source.Up;
                Assert.That(result.Up, Is.EqualTo(source.Up));
                Assert.That(result.TransformDirection(Vector3Int.left),
                    Is.EqualTo(Cross(direction, referenceUp)));
            }
        }

        [Test]
        public void Doorway_BlocksOnlyIntersectingStudBasesAndKeepsLogicalIndices()
        {
            GameObject neighborObject = new GameObject("Neighbor");
            try
            {
                CubeRoom neighbor = neighborObject.AddComponent<CubeRoom>();
                RoomAperture aperture = Aperture(new Vector3(-4f, 1.2f, 0f),
                    Vector3.forward, Vector3.up, 2f, 2.4f);
                room.SetFaceConnection(CubeRoomFace.West, neighbor, CubeRoomFace.East,
                    RoomPassageKind.SideDoorway, RoomCeilingEdge.None, true, aperture);
                wall.RefreshFromConnections();

                Assert.That(wall.GetAnchor(0, 1).IsOpeningBlocked, Is.True);
                Assert.That(wall.GetAnchor(0, 2).IsOpeningBlocked, Is.True);
                Assert.That(wall.GetAnchor(0, 0).IsOpeningBlocked, Is.False);
                Assert.That(wall.GetAnchor(1, 1).Index, Is.EqualTo(5));
                Assert.That(wall.Anchors.Count(anchor => anchor.IsOpeningBlocked), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(neighborObject);
            }
        }

        [Test]
        public void Reservations_AreAtomicRotateAndReleaseOnlyOwnedAnchors()
        {
            Assert.That(wall.TryReserve(DeviceFootprint.HorizontalPair, 1, 1,
                DeviceFootprintRotation.Degrees90, out DeviceWallReservation vertical, out _), Is.True);
            Assert.That(vertical.AnchorIndices, Is.EquivalentTo(new[] { 5, 9 }));
            Assert.That(wall.TryReserve(DeviceFootprint.HorizontalPair, 1, 1,
                DeviceFootprintRotation.Degrees0, out _, out DeviceWallPlacementFailure occupied), Is.False);
            Assert.That(occupied, Is.EqualTo(DeviceWallPlacementFailure.Occupied));
            Assert.That(wall.GetAnchor(1, 2).IsOccupied, Is.False,
                "A failed reservation must not partially reserve its other anchor.");

            Assert.That(wall.TryReserve(DeviceFootprint.Single, 2, 2,
                DeviceFootprintRotation.Degrees0, out DeviceWallReservation independent, out _), Is.True);
            Assert.That(wall.Release(vertical), Is.True);
            Assert.That(wall.GetAnchor(1, 1).IsOccupied, Is.False);
            Assert.That(wall.GetAnchor(2, 2).IsOccupied, Is.True);
            Assert.That(wall.Release(independent), Is.True);
        }

        [Test]
        public void PhysicalFootprintCrossingOpening_IsRejectedWhenAnchorCentersAreClear()
        {
            GameObject neighborObject = new GameObject("Neighbor");
            try
            {
                CubeRoom neighbor = neighborObject.AddComponent<CubeRoom>();
                RoomAperture aperture = Aperture(new Vector3(-4f, 3f, 0f),
                    Vector3.forward, Vector3.up, 0.5f, 0.5f);
                room.SetFaceConnection(CubeRoomFace.West, neighbor, CubeRoomFace.East,
                    RoomPassageKind.SideDoorway, RoomCeilingEdge.None, true, aperture);
                wall.RefreshFromConnections();
                Assert.That(wall.GetAnchor(1, 1).IsOpeningBlocked, Is.False);
                Assert.That(wall.GetAnchor(1, 2).IsOpeningBlocked, Is.False);
                Assert.That(wall.TryReserve(DeviceFootprint.HorizontalPair, 1, 1,
                    DeviceFootprintRotation.Degrees0, out _, out DeviceWallPlacementFailure failure), Is.False);
                Assert.That(failure, Is.EqualTo(DeviceWallPlacementFailure.PhysicalBoundsBlocked));
                Assert.That(wall.Anchors.Any(anchor => anchor.IsOccupied), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(neighborObject);
            }
        }

        [Test]
        public void PhysicalBoundsRejectNeighborOverlapEvenWhenAnchorIdsDiffer()
        {
            DeviceFootprint wideSingle = new DeviceFootprint(
                new[] { Vector2Int.zero }, new Vector2(4f, 0.75f));
            Assert.That(wall.TryReserve(wideSingle, 1, 1, DeviceFootprintRotation.Degrees0,
                out DeviceWallReservation first, out _), Is.True);
            Assert.That(first.PhysicalBounds.width, Is.EqualTo(4f).Within(0.001f));

            Assert.That(wall.TryReserve(DeviceFootprint.Single, 1, 2, DeviceFootprintRotation.Degrees0,
                out _, out DeviceWallPlacementFailure failure), Is.False);
            Assert.That(failure, Is.EqualTo(DeviceWallPlacementFailure.PhysicalBoundsBlocked));
            Assert.That(wall.GetAnchor(1, 2).IsOccupied, Is.False,
                "Rejected physical overlap must not partially reserve a distinct anchor.");
        }

        [Test]
        public void LaterOpeningMarksSpanningReservationConflictedWithoutDiscardingIt()
        {
            GameObject neighborObject = new GameObject("Neighbor");
            try
            {
                DeviceFootprint spanning = new DeviceFootprint(
                    new[] { Vector2Int.zero, Vector2Int.right }, new Vector2(2.75f, 0.75f));
                Assert.That(wall.TryReserve(spanning, 1, 1, DeviceFootprintRotation.Degrees0,
                    out DeviceWallReservation reservation, out _), Is.True);

                CubeRoom neighbor = neighborObject.AddComponent<CubeRoom>();
                room.SetFaceConnection(CubeRoomFace.West, neighbor, CubeRoomFace.East,
                    RoomPassageKind.SideDoorway, RoomCeilingEdge.None, true,
                    Aperture(new Vector3(-4f, 3f, 0f), Vector3.forward, Vector3.up, 0.5f, 0.5f));
                wall.RefreshFromConnections();

                Assert.That(reservation.IsConflicted, Is.True);
                Assert.That(reservation.IsReleased, Is.False,
                    "Topology changes report reservation conflicts but do not silently discard future equipment state.");
            }
            finally
            {
                Object.DestroyImmediate(neighborObject);
            }
        }

        [Test]
        public void Reservations_AreIndependentBetweenRooms()
        {
            GameObject secondObject = new GameObject("Second Device Wall Room");
            try
            {
                CubeRoom secondRoom = secondObject.AddComponent<CubeRoom>();
                DeviceWall secondWall = secondObject.AddComponent<DeviceWall>();
                secondWall.Configure(secondRoom, CubeRoomWall.West, null, null, null, null);
                secondRoom.ConfigureDeviceWall(secondWall);
                Assert.That(wall.TryReserve(DeviceFootprint.Single, 0, 0,
                    DeviceFootprintRotation.Degrees0, out _, out _), Is.True);
                Assert.That(secondWall.GetAnchor(0, 0).IsAvailable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
            }
        }

        private RoomAperture Aperture(Vector3 localCenter, Vector3 localHorizontal,
            Vector3 localVertical, float width, float height)
        {
            return new RoomAperture(room.transform.TransformPoint(localCenter),
                room.transform.TransformDirection(localHorizontal),
                room.transform.TransformDirection(localVertical),
                room.transform.TransformDirection(Vector3.left), width, height, room, CubeRoomFace.West);
        }

        private static Vector3Int Cross(Vector3Int first, Vector3Int second)
        {
            return new Vector3Int(first.y * second.z - first.z * second.y,
                first.z * second.x - first.x * second.z,
                first.x * second.y - first.y * second.x);
        }

        private static int Dot(Vector3Int first, Vector3Int second)
        {
            return first.x * second.x + first.y * second.y + first.z * second.z;
        }
    }
}
