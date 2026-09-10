using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class RoomPlacementTests
    {
        private const string CubePrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/CubeRoom.prefab";
        private GameObject layoutRoot;
        private CubeRoomClusterGenerator layout;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
            Assert.That(prefab, Is.Not.Null, "Regenerate the foundation assets before running room-placement tests.");
            layoutRoot = new GameObject("Room Placement Test Layout");
            CubeRoom primary = UnityEngine.Object.Instantiate(prefab, layoutRoot.transform).GetComponent<CubeRoom>();
            primary.name = "Test Primary Room";
            primary.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            layout = layoutRoot.AddComponent<CubeRoomClusterGenerator>();
            layout.Configure(primary, prefab.GetComponent<CubeRoom>(), 0);
            layout.InitializeSingleRoom();
        }

        [TearDown]
        public void TearDown()
        {
            if (layoutRoot != null) UnityEngine.Object.DestroyImmediate(layoutRoot);
        }

        [Test]
        public void EverySideWall_ProducesAnExactAnchorAlignedCandidate()
        {
            foreach (CubeRoomWall wall in Enum.GetValues(typeof(CubeRoomWall)))
            {
                Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, wall, out RoomPlacementCandidate candidate), Is.True);
                Assert.That(candidate.GridCell, Is.EqualTo(CubeRoom.GetGridDirection(wall)));
                Assert.That(layout.AnchorsAlign(candidate), Is.True);
                Assert.That(Quaternion.Angle(candidate.Rotation, layout.PrimaryRoom.transform.rotation), Is.LessThan(0.001f));

                CubeRoomWall opposite = CubeRoom.GetOppositeWall(wall);
                Matrix4x4 target = Matrix4x4.TRS(candidate.Position, candidate.Rotation, Vector3.one);
                foreach (CubeRoomWallAnchor anchor in Enum.GetValues(typeof(CubeRoomWallAnchor)))
                {
                    Vector3 sourcePoint = layout.PrimaryRoom.GetWallAnchorWorld(wall, anchor);
                    Vector3 targetPoint = target.MultiplyPoint3x4(layout.PrimaryRoom.GetWallAnchorLocal(opposite, anchor));
                    Assert.That(Vector3.Distance(sourcePoint, targetPoint), Is.LessThanOrEqualTo(CubeRoom.AnchorAlignmentTolerance));
                }
            }
        }

        [Test]
        public void AnalyticTargeting_SelectsFirstCurrentRoomBoundaryAndRejectsFloorOrCeiling()
        {
            Vector3 origin = layout.PrimaryRoom.transform.TransformPoint(new Vector3(0f, 4f, 0f));
            foreach (CubeRoomWall wall in Enum.GetValues(typeof(CubeRoomWall)))
            {
                Ray ray = new Ray(origin, layout.PrimaryRoom.GetWallNormalWorld(wall));
                Assert.That(RoomPlacementTargeting.TrySelectSideWall(layout.PrimaryRoom, ray, out CubeRoomWall selected), Is.True);
                Assert.That(selected, Is.EqualTo(wall));
            }

            Assert.That(RoomPlacementTargeting.TrySelectSideWall(layout.PrimaryRoom, new Ray(origin, Vector3.down), out _), Is.False);
            Assert.That(RoomPlacementTargeting.TryGetFirstBoundary(layout.PrimaryRoom, new Ray(origin, Vector3.down), out RoomBoundaryHit floorHit), Is.True);
            Assert.That(floorHit.Boundary, Is.EqualTo(RoomBoundaryKind.Floor));
            Assert.That(RoomPlacementTargeting.TrySelectSideWall(layout.PrimaryRoom, new Ray(origin, Vector3.up), out _), Is.False);
        }

        [Test]
        public void Placement_RejectsOccupiedCellAndDoesNotCreateCornerOnlyConnection()
        {
            CubeRoom north = Place(Vector2Int.zero, CubeRoomWall.North);
            CubeRoom east = Place(Vector2Int.zero, CubeRoomWall.East);
            Assert.That(layout.Rooms, Has.Count.EqualTo(3));
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomWall.North, out _), Is.False);
            Assert.That(north.GetConnectedRoom(CubeRoomWall.East), Is.Null, "Diagonal corner contact must not create a doorway.");
            Assert.That(east.GetConnectedRoom(CubeRoomWall.North), Is.Null, "Diagonal corner contact must not create a doorway.");
        }

        [Test]
        public void GapPlacement_ConnectsEveryFullFaceNeighbor()
        {
            CubeRoom north = Place(Vector2Int.zero, CubeRoomWall.North);
            CubeRoom northEast = Place(new Vector2Int(0, 1), CubeRoomWall.East);
            CubeRoom gap = Place(Vector2Int.zero, CubeRoomWall.East);

            Assert.That(gap.GetConnectedRoom(CubeRoomWall.West), Is.SameAs(layout.PrimaryRoom));
            Assert.That(gap.GetConnectedRoom(CubeRoomWall.North), Is.SameAs(northEast));
            Assert.That(layout.PrimaryRoom.GetConnectedRoom(CubeRoomWall.East), Is.SameAs(gap));
            Assert.That(northEast.GetConnectedRoom(CubeRoomWall.South), Is.SameAs(gap));
            Assert.That(north.GetConnectedRoom(CubeRoomWall.East), Is.SameAs(northEast));
            Assert.That(ConnectedWallCount(gap), Is.EqualTo(2));
        }

        [Test]
        public void SurroundedGapPlacement_OpensAllFourReciprocalDoorways()
        {
            Place(Vector2Int.zero, CubeRoomWall.North);          // (0,1)
            Place(new Vector2Int(0, 1), CubeRoomWall.East);     // (1,1)
            Place(new Vector2Int(1, 1), CubeRoomWall.East);     // (2,1)
            Place(new Vector2Int(2, 1), CubeRoomWall.South);    // (2,0)
            Place(new Vector2Int(2, 0), CubeRoomWall.South);    // (2,-1)
            Place(new Vector2Int(2, -1), CubeRoomWall.West);    // (1,-1)
            CubeRoom center = Place(Vector2Int.zero, CubeRoomWall.East); // (1,0)

            Assert.That(ConnectedWallCount(center), Is.EqualTo(4));
            foreach (CubeRoomWall wall in Enum.GetValues(typeof(CubeRoomWall)))
            {
                CubeRoom neighbor = center.GetConnectedRoom(wall);
                Assert.That(neighbor, Is.Not.Null, $"Center room is missing its {wall} neighbor.");
                Assert.That(neighbor.GetConnectedRoom(CubeRoom.GetOppositeWall(wall)), Is.SameAs(center));
                Assert.That(center.HasDoorway(wall), Is.True);
            }
        }

        [Test]
        public void LongPlacementChain_RetainsExactGridAlignmentWithoutDrift()
        {
            CubeRoom current = layout.PrimaryRoom;
            for (int index = 1; index <= 12; index++)
            {
                Assert.That(layout.TryPlaceRoom(current, CubeRoomWall.East, out current), Is.True);
                Assert.That(layout.TryGetCell(current, out Vector2Int cell), Is.True);
                Assert.That(cell, Is.EqualTo(new Vector2Int(index, 0)));
                Vector3 expected = layout.PrimaryRoom.transform.TransformPoint(new Vector3(index * CubeRoom.InteriorWidth, 0f, 0f));
                Assert.That(Vector3.Distance(current.transform.position, expected), Is.LessThan(0.0001f));
            }
        }

        private CubeRoom Place(Vector2Int sourceCell, CubeRoomWall wall)
        {
            Assert.That(layout.TryGetRoom(sourceCell, out CubeRoom source), Is.True, $"Missing source room at {sourceCell}.");
            Assert.That(layout.TryPlaceRoom(source, wall, out CubeRoom placed), Is.True, $"Failed to place from {sourceCell} through {wall}.");
            return placed;
        }

        private static int ConnectedWallCount(CubeRoom room)
        {
            return Enum.GetValues(typeof(CubeRoomWall)).Cast<CubeRoomWall>().Count(wall => room.GetConnectedRoom(wall) != null);
        }
    }
}
