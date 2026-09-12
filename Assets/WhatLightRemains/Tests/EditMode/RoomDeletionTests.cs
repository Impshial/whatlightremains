using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class RoomDeletionTests
    {
        private const string CubePrefabPath = "Assets/WhatLightRemains/Generated/Prefabs/CubeRoom.prefab";
        private static readonly int DeleteTintStrengthId = Shader.PropertyToID("_WLRDeleteTintStrength");
        private GameObject layoutRoot;
        private CubeRoomClusterGenerator layout;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
            Assert.That(prefab, Is.Not.Null, "Regenerate the foundation assets before running deletion tests.");
            layoutRoot = new GameObject("Room Deletion Test Layout");
            CubeRoom primary = Object.Instantiate(prefab, layoutRoot.transform).GetComponent<CubeRoom>();
            primary.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            layout = layoutRoot.AddComponent<CubeRoomClusterGenerator>();
            layout.Configure(primary, prefab.GetComponent<CubeRoom>(), 0);
            layout.InitializeSingleRoom();
        }

        [TearDown]
        public void TearDown()
        {
            if (layoutRoot != null) Object.DestroyImmediate(layoutRoot);
        }

        [Test]
        public void DeleteRoom_ProtectsPrimaryAndRejectsUnregisteredRooms()
        {
            GameObject outsiderObject = new GameObject("Unregistered Room");
            CubeRoom outsider = outsiderObject.AddComponent<CubeRoom>();
            try
            {
                Assert.That(layout.CanDeleteRoom(layout.PrimaryRoom), Is.False);
                Assert.That(layout.TryDeleteRoom(layout.PrimaryRoom), Is.False);
                Assert.That(layout.CanDeleteRoom(outsider), Is.False);
                Assert.That(layout.TryDeleteRoom(outsider), Is.False);
                Assert.That(layout.Rooms, Has.Count.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(outsiderObject);
            }
        }

        [Test]
        public void DeleteRoom_RebuildsBoundariesAndLeavesDisconnectedBranchRegistered()
        {
            Assert.That(layout.TryPlaceRoom(layout.PrimaryRoom, CubeRoomWall.East, out CubeRoom middle), Is.True);
            Assert.That(layout.TryPlaceRoom(middle, CubeRoomWall.East, out CubeRoom branch), Is.True);
            Assert.That(layout.Passages, Has.Count.EqualTo(2));
            int changes = 0;
            layout.RoomsChanged += () => changes++;

            Assert.That(layout.TryDeleteRoom(middle), Is.True);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(layout.Rooms, Has.Count.EqualTo(2));
            Assert.That(layout.IsRegistered(branch), Is.True,
                "Deletion must leave downstream rooms floating rather than cascading.");
            Assert.That(layout.TryGetRoom(new Vector3Int(2, 0, 0), out CubeRoom registeredBranch), Is.True);
            Assert.That(registeredBranch, Is.SameAs(branch));
            Assert.That(layout.PrimaryRoom.GetConnectedRoom(CubeRoomWall.East), Is.Null);
            Assert.That(branch.GetConnectedRoom(CubeRoomWall.West), Is.Null);
            Assert.That(layout.Passages, Is.Empty);

            CubeRoomWallBoundary exposed = layout.PrimaryRoom.GetWallBoundary(CubeRoomWall.East);
            Assert.That(exposed.IsConnected, Is.False);
            Assert.That(exposed.ClosedGlassRenderer.enabled, Is.True);
            Assert.That(exposed.ClosedCollider.enabled, Is.True);
        }

        [Test]
        public void DeletionHighlight_HasNoOutlineOrPhysicalComponents()
        {
            RoomDeletionHighlight highlight = RoomDeletionHighlight.Create();
            try
            {
                highlight.Show(layout.PrimaryRoom);
                Assert.That(highlight.IsVisible, Is.True);
                Assert.That(highlight.Root.GetComponentsInChildren<LineRenderer>(true), Is.Empty);
                Assert.That(highlight.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(highlight.Root.GetComponentsInChildren<Light>(true), Is.Empty);
            }
            finally
            {
                highlight.Dispose();
            }
        }

        [Test]
        public void DeletionHighlight_TintsRoomAndSharedPassageGlassWithoutChangingOpacity()
        {
            RoomDeletionHighlight highlight = null;
            try
            {
                Assert.That(layout.TryPlaceRoom(layout.PrimaryRoom, CubeRoomWall.East, out CubeRoom selectedRoom), Is.True);
                Assert.That(layout.Passages, Has.Count.EqualTo(1));
                Renderer roomGlass = FindGlassRenderer(selectedRoom.GetComponentsInChildren<Renderer>(true));
                Renderer passageGlass = FindGlassRenderer(layout.Passages[0].GlassRenderers);
                Assert.That(roomGlass, Is.Not.Null);
                Assert.That(passageGlass, Is.Not.Null);
                float originalOpacity = roomGlass.sharedMaterial.GetFloat("_Translucency");

                highlight = RoomDeletionHighlight.Create();
                highlight.Show(selectedRoom);

                AssertDeleteTint(roomGlass, 0.22f);
                AssertDeleteTint(passageGlass, 0.22f);
                Assert.That(roomGlass.sharedMaterial.GetFloat("_Translucency"), Is.EqualTo(originalOpacity));

                highlight.Hide();
                AssertDeleteTint(roomGlass, 0f);
                AssertDeleteTint(passageGlass, 0f);
                Assert.That(roomGlass.sharedMaterial.GetFloat("_Translucency"), Is.EqualTo(originalOpacity));
            }
            finally
            {
                highlight?.Dispose();
            }
        }

        private static RoomAperture CreateLocalAperture(CubeRoom room, CubeRoomFace face,
            Vector3 localCenter, Vector3 localHorizontal, Vector3 localVertical, float width, float height)
        {
            return new RoomAperture(
                room.transform.TransformPoint(localCenter),
                room.transform.TransformDirection(localHorizontal),
                room.transform.TransformDirection(localVertical),
                room.GetFaceNormalWorld(face),
                width,
                height,
                room,
                face);
        }

        private static Renderer FindGlassRenderer(System.Collections.Generic.IEnumerable<Renderer> renderers)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.shader != null
                        && material.shader.name == "What Light Remains/Light-Transmitting Glass")
                        return renderer;
                }
            }
            return null;
        }

        private static void AssertDeleteTint(Renderer renderer, float expectedStrength)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat(DeleteTintStrengthId), Is.EqualTo(expectedStrength).Within(0.0001f));
        }
    }
}
