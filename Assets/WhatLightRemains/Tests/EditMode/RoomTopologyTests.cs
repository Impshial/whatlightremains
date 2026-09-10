using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class RoomTopologyTests
    {
        private const string CubePrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/CubeRoom.prefab";
        private GameObject root;
        private CubeRoomClusterGenerator layout;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
            Assert.That(prefab, Is.Not.Null, "Regenerate foundation assets before running topology tests.");
            root = new GameObject("3D Topology Tests");
            CubeRoom primary = Object.Instantiate(prefab, root.transform).GetComponent<CubeRoom>();
            primary.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            layout = root.AddComponent<CubeRoomClusterGenerator>();
            layout.Configure(primary, prefab.GetComponent<CubeRoom>(), 0);
            layout.InitializeSingleRoom();
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Orientations_ContainExactlyTwentyFourCardinalRightHandedBases()
        {
            Assert.That(RoomOrientation.All.Distinct().Count(), Is.EqualTo(24));
            foreach (RoomOrientation orientation in RoomOrientation.All)
            {
                Assert.That(Vector3.Dot(orientation.Right, orientation.Up), Is.Zero);
                Assert.That(Vector3.Cross(orientation.Right, orientation.Up),
                    Is.EqualTo((Vector3)orientation.Forward).Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(orientation.Rotation * Vector3.up, Is.EqualTo((Vector3)orientation.Up).Using(Vector3ComparerWithEqualsOperator.Instance));
            }
        }

        [Test]
        public void CeilingCandidate_UprightIsSealedAndUsesExactVolumeCenteredRoot()
        {
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.Ceiling,
                RoomOrientation.Identity, out RoomPlacementCandidate candidate), Is.True);
            Assert.That(candidate.GridCell3D, Is.EqualTo(Vector3Int.up));
            Assert.That(candidate.MatingFace, Is.EqualTo(CubeRoomFace.Floor));
            Assert.That(candidate.PassageKind, Is.EqualTo(RoomPassageKind.Sealed));
            Assert.That(candidate.Position, Is.EqualTo(new Vector3(0f, 8f, 0f)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(layout.AnchorsAlign(candidate), Is.True);
        }

        [Test]
        public void CeilingCandidate_SideDownCreatesEdgeAlignedTraversablePassage()
        {
            RoomOrientation tilted = RoomOrientation.Identity.RotateAroundGridAxis(new Vector3Int(0, 0, 1), 1);
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.Ceiling,
                tilted, out RoomPlacementCandidate candidate), Is.True);
            Assert.That(candidate.MatingFace, Is.EqualTo(CubeRoomFace.West));
            Assert.That(candidate.PassageKind, Is.EqualTo(RoomPassageKind.CeilingToSideDoorway));
            Assert.That(candidate.CeilingEdge, Is.EqualTo(RoomCeilingEdge.East));
            Assert.That(candidate.Position, Is.EqualTo(new Vector3(4f, 12f, 0f)).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(candidate.Connections.Count, Is.EqualTo(1));

            Assert.That(layout.TryPlaceRoom(candidate, out CubeRoom placed), Is.True);
            Assert.That(layout.PrimaryRoom.GetConnectedRoom(CubeRoomFace.Ceiling), Is.SameAs(placed));
            Assert.That(placed.GetConnectedRoom(CubeRoomFace.West), Is.SameAs(layout.PrimaryRoom));
            Assert.That(layout.PrimaryRoom.GetConnection(CubeRoomFace.Ceiling).IsTraversable, Is.True);
            Assert.That(placed.GetConnection(CubeRoomFace.West).IsTraversable, Is.True);
        }

        [Test]
        public void CeilingCandidate_CeilingDownRemainsSealed()
        {
            RoomOrientation inverted = RoomOrientation.Identity.RotateAroundGridAxis(new Vector3Int(0, 0, 1), 2);
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.Ceiling,
                inverted, out RoomPlacementCandidate candidate), Is.True);
            Assert.That(candidate.MatingFace, Is.EqualTo(CubeRoomFace.Ceiling));
            Assert.That(candidate.PassageKind, Is.EqualTo(RoomPassageKind.Sealed));
            Assert.That(candidate.CeilingEdge, Is.EqualTo(RoomCeilingEdge.None));
            Assert.That(layout.AnchorsAlign(candidate), Is.True);

            Assert.That(layout.TryPlaceRoom(candidate, out CubeRoom placed), Is.True);
            Assert.That(layout.PrimaryRoom.GetConnection(CubeRoomFace.Ceiling).IsConnected, Is.True);
            Assert.That(layout.PrimaryRoom.GetConnection(CubeRoomFace.Ceiling).IsTraversable, Is.False);
            Assert.That(placed.GetConnection(CubeRoomFace.Ceiling).IsTraversable, Is.False);
        }

        [Test]
        public void QuarterTurnRotation_AroundSourceYAndZUsesDiscreteCardinalSteps()
        {
            RoomOrientation source = RoomOrientation.Identity;
            RoomOrientation aroundY = source.RotateAroundLocalAxis(Vector3Int.up, 1);
            RoomOrientation aroundZ = source.RotateAroundLocalAxis(new Vector3Int(0, 0, 1), 1);

            Assert.That(aroundY.Up, Is.EqualTo(Vector3Int.up));
            Assert.That(aroundY.Forward, Is.EqualTo(Vector3Int.right));
            Assert.That(aroundZ.Forward, Is.EqualTo(new Vector3Int(0, 0, 1)));
            Assert.That(aroundZ.Up, Is.EqualTo(Vector3Int.left));
            Assert.That(source.RotateAroundLocalAxis(Vector3Int.up, 4), Is.EqualTo(source));
            Assert.That(source.RotateAroundLocalAxis(new Vector3Int(0, 0, 1), -1)
                .RotateAroundLocalAxis(new Vector3Int(0, 0, 1), 1), Is.EqualTo(source));
        }

        [Test]
        public void SideCandidate_WithCeilingFacingSourceCreatesTraversablePassage()
        {
            RoomOrientation tilted = RoomOrientation.Identity.RotateAroundGridAxis(new Vector3Int(0, 0, 1), 1);
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.East,
                tilted, out RoomPlacementCandidate candidate), Is.True);
            Assert.That(candidate.MatingFace, Is.EqualTo(CubeRoomFace.Ceiling));
            Assert.That(candidate.PassageKind, Is.EqualTo(RoomPassageKind.CeilingToSideDoorway));
            Assert.That(candidate.CeilingEdge, Is.Not.EqualTo(RoomCeilingEdge.None));
            Assert.That(layout.AnchorsAlign(candidate), Is.True);

            Assert.That(layout.TryPlaceRoom(candidate, out CubeRoom placed), Is.True);
            Assert.That(layout.PrimaryRoom.GetConnection(CubeRoomFace.East).IsTraversable, Is.True);
            Assert.That(placed.GetConnection(CubeRoomFace.Ceiling).IsTraversable, Is.True);
            Assert.That(placed.CeilingBoundary.HasPassage, Is.True);
        }

        [Test]
        public void EverySideTarget_KeepsGhostCandidateValidThroughAllZQuarterTurns()
        {
            foreach (CubeRoomWall wall in System.Enum.GetValues(typeof(CubeRoomWall)))
            {
                for (int turns = 0; turns < 4; turns++)
                {
                    RoomOrientation orientation = RoomOrientation.Identity.RotateAroundGridAxis(
                        new Vector3Int(0, 0, 1), turns);
                    Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoom.ToFace(wall),
                        orientation, out RoomPlacementCandidate candidate), Is.True,
                        $"{wall} lost its placement candidate after {turns * 90} degrees of Z rotation.");
                    Assert.That(candidate.IsValid, Is.True);
                    Assert.That(layout.AnchorsAlign(candidate), Is.True);
                }
            }
        }

        [Test]
        public void SideCandidate_WithFloorFacingSourceRemainsVisibleAndSealed()
        {
            RoomOrientation tilted = RoomOrientation.Identity.RotateAroundGridAxis(new Vector3Int(0, 0, 1), -1);
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.East,
                tilted, out RoomPlacementCandidate candidate), Is.True);
            Assert.That(candidate.MatingFace, Is.EqualTo(CubeRoomFace.Floor));
            Assert.That(candidate.PassageKind, Is.EqualTo(RoomPassageKind.Sealed));
            Assert.That(layout.AnchorsAlign(candidate), Is.True);

            Assert.That(layout.TryPlaceRoom(candidate, out CubeRoom placed), Is.True);
            Assert.That(layout.PrimaryRoom.GetConnection(CubeRoomFace.East).IsConnected, Is.True);
            Assert.That(layout.PrimaryRoom.GetConnection(CubeRoomFace.East).IsTraversable, Is.False);
            Assert.That(layout.PrimaryRoom.GetWallBoundary(CubeRoomWall.East).OwnsBoundary, Is.False,
                "The candidate floor owns this sealed shared face, so the source glass wall must not overlap it.");
            Assert.That(placed.GetConnection(CubeRoomFace.Floor).IsTraversable, Is.False);
        }

        [Test]
        public void SideCandidate_WithOpposedUpDirectionsRemainsVisibleAndSealed()
        {
            RoomOrientation inverted = RoomOrientation.Identity.RotateAroundGridAxis(new Vector3Int(0, 0, 1), 2);
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.East,
                inverted, out RoomPlacementCandidate candidate), Is.True);
            Assert.That(candidate.MatingFace, Is.EqualTo(CubeRoomFace.East));
            Assert.That(candidate.PassageKind, Is.EqualTo(RoomPassageKind.Sealed));

            Assert.That(layout.TryPlaceRoom(candidate, out CubeRoom placed), Is.True);
            Assert.That(layout.PrimaryRoom.GetWallBoundary(CubeRoomWall.East).OwnsBoundary, Is.True);
            Assert.That(placed.GetWallBoundary(CubeRoomWall.East).OwnsBoundary, Is.False);
        }

        [Test]
        public void Targeting_AllowsCeilingButNeverFloor()
        {
            Vector3 origin = new Vector3(0f, 4f, 0f);
            Assert.That(RoomPlacementTargeting.TrySelectPlacementFace(layout.PrimaryRoom,
                new Ray(origin, Vector3.up), out CubeRoomFace ceiling), Is.True);
            Assert.That(ceiling, Is.EqualTo(CubeRoomFace.Ceiling));
            Assert.That(RoomPlacementTargeting.TrySelectPlacementFace(layout.PrimaryRoom,
                new Ray(origin, Vector3.down), out _), Is.False);
        }

        [Test]
        public void Commit_RevalidatesStaleCandidateAtomically()
        {
            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.Ceiling,
                RoomOrientation.Identity, out RoomPlacementCandidate stale), Is.True);
            Assert.That(layout.TryPlaceRoom(stale, out CubeRoom first), Is.True);
            Assert.That(first, Is.Not.Null);
            int count = layout.Rooms.Count;
            Assert.That(layout.TryPlaceRoom(stale, out CubeRoom second), Is.False);
            Assert.That(second, Is.Null);
            Assert.That(layout.Rooms, Has.Count.EqualTo(count));
            Assert.That(layout.TryGetRoom(Vector3Int.up, out CubeRoom registered), Is.True);
            Assert.That(registered, Is.SameAs(first));
        }
    }
}
