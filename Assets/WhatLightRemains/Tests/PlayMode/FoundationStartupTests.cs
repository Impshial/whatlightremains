using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class FoundationStartupTests
    {
        private const string SceneName = "Foundation";
        private static readonly CubeRoomWall[] HorizontalWalls =
        {
            CubeRoomWall.West,
            CubeRoomWall.East,
            CubeRoomWall.South,
            CubeRoomWall.North,
        };

        private Scene loadedScene;
        private Scene previousActiveScene;

        [UnityTest]
        public IEnumerator FoundationScene_StartsWithFourConnectedRoomsAndInitializesPlayerInPrimaryRoom()
        {
            Assert.That(
                Application.CanStreamedLevelBeLoaded(SceneName),
                Is.True,
                "The generated Foundation scene must exist and be enabled in Build Settings. " +
                "Run the What Light Remains foundation builder, then rerun the tests.");

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(loadedScene.IsValid() && loadedScene.isLoaded, Is.True);
            previousActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(loadedScene);

            yield return null;
            yield return new WaitForFixedUpdate();

            List<CubeRoom> rooms = FindInScene<CubeRoom>(loadedScene);
            List<CubeRoomClusterGenerator> generators = FindInScene<CubeRoomClusterGenerator>(loadedScene);
            List<PlayerRoomTracker> trackers = FindInScene<PlayerRoomTracker>(loadedScene);
            List<FirstPersonMotor> motors = FindInScene<FirstPersonMotor>(loadedScene);
            List<Camera> cameras = FindInScene<Camera>(loadedScene);
            List<HotbarView> hotbars = FindInScene<HotbarView>(loadedScene);

            Assert.That(generators, Has.Count.EqualTo(1));
            CubeRoomClusterGenerator generator = generators[0];
            AssertRuntimeCluster(generator, rooms);
            Assert.That(trackers, Has.Count.EqualTo(1));
            Assert.That(motors, Has.Count.EqualTo(1));
            AssertCameraStack(cameras);
            Assert.That(hotbars, Has.Count.EqualTo(1));
            Assert.That(hotbars[0].Slots, Has.Count.EqualTo(8));
            Assert.That(hotbars[0].transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(trackers[0].CurrentRoom, Is.SameAs(generator.PrimaryRoom), "The player must acquire the fixed primary room automatically.");
            Assert.That(motors[0].GetComponent<CharacterController>(), Is.Not.Null);

            Vector3 primaryPosition = generator.PrimaryRoom.transform.position;
            Quaternion primaryRotation = generator.PrimaryRoom.transform.rotation;
            CubeRoom primaryRoom = generator.PrimaryRoom;
            Vector3 localPlayerPosition = generator.PrimaryRoom.transform.InverseTransformPoint(motors[0].transform.position);
            Assert.That(localPlayerPosition.x, Is.InRange(-4f, 4f));
            Assert.That(localPlayerPosition.y, Is.InRange(0f, 8f));
            Assert.That(localPlayerPosition.z, Is.InRange(-4f, 4f));

            generator.Generate(8675309);
            yield return null;

            rooms = FindInScene<CubeRoom>(loadedScene);
            AssertRuntimeCluster(generator, rooms);
            Assert.That(generator.PrimaryRoom, Is.SameAs(primaryRoom), "Regeneration must preserve the authored primary room instance.");
            Assert.That(Vector3.Distance(generator.PrimaryRoom.transform.position, primaryPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(generator.PrimaryRoom.transform.rotation, primaryRotation), Is.LessThan(0.01f));
            Assert.That(generator.LastSeed, Is.EqualTo(8675309));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedScene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            loadedScene = default;
            previousActiveScene = default;
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToList();
        }

        private static void AssertRuntimeCluster(CubeRoomClusterGenerator generator, List<CubeRoom> sceneRooms)
        {
            Assert.That(generator.PrimaryRoom, Is.Not.Null);
            Assert.That(generator.RoomPrefab, Is.Not.Null);
            Assert.That(generator.AdditionalRoomCount, Is.EqualTo(3));
            Assert.That(generator.Rooms, Has.Count.EqualTo(4));
            Assert.That(generator.GridCells, Has.Count.EqualTo(4));
            Assert.That(generator.Rooms[0], Is.SameAs(generator.PrimaryRoom));
            Assert.That(generator.GridCells[0], Is.EqualTo(Vector2Int.zero));
            Assert.That(generator.Rooms.Distinct().Count(), Is.EqualTo(4), "Every grid cell must own a distinct room instance.");
            Assert.That(generator.GridCells.Distinct().Count(), Is.EqualTo(4), "Generated rooms must not overlap in the same grid cell.");
            Assert.That(sceneRooms, Has.Count.EqualTo(4));
            Assert.That(sceneRooms, Is.EquivalentTo(generator.Rooms));

            for (int roomIndex = 0; roomIndex < generator.Rooms.Count; roomIndex++)
            {
                CubeRoom room = generator.Rooms[roomIndex];
                Vector2Int cell = generator.GridCells[roomIndex];
                Vector3 localOffset = generator.PrimaryRoom.transform.InverseTransformPoint(room.transform.position);
                Vector3 expectedOffset = new Vector3(
                    cell.x * CubeRoom.InteriorWidth,
                    0f,
                    cell.y * CubeRoom.InteriorDepth);

                Assert.That(
                    Vector3.Distance(localOffset, expectedOffset),
                    Is.LessThan(0.001f),
                    $"Room {roomIndex} is not centered on its declared eight-metre grid cell {cell}.");
                Assert.That(
                    Quaternion.Angle(room.transform.rotation, generator.PrimaryRoom.transform.rotation),
                    Is.LessThan(0.01f),
                    $"Room {roomIndex} must share the primary room's orientation.");

                if (roomIndex == 0)
                {
                    continue;
                }

                Assert.That(room.Lighting, Is.Not.Null);
                Assert.That(
                    room.Lighting.SupportingLightShadows,
                    Is.EqualTo(LightShadows.None),
                    "Generated rooms must keep their broad strip-emitter spotlights shadow-free.");

                bool attachesToEarlierRoom = generator.GridCells
                    .Take(roomIndex)
                    .Any(existing => ManhattanDistance(existing, cell) == 1);
                Assert.That(attachesToEarlierRoom, Is.True, $"Room {roomIndex} is disconnected from the earlier cluster.");
            }

            Assert.That(
                generator.GridCells.Skip(1).Any(cell => ManhattanDistance(Vector2Int.zero, cell) == 1),
                Is.True,
                "At least one generated room must be directly face-adjacent to the primary.");

            Physics.SyncTransforms();
            for (int roomIndex = 0; roomIndex < generator.GridCells.Count; roomIndex++)
            {
                CubeRoom room = generator.Rooms[roomIndex];
                Vector2Int cell = generator.GridCells[roomIndex];

                foreach (CubeRoomWall wall in HorizontalWalls)
                {
                    Vector2Int direction = DirectionFacing(wall);
                    int neighborIndex = FindCellIndex(generator.GridCells, cell + direction);
                    bool connected = neighborIndex >= 0;

                    Assert.That(
                        room.HasDoorway(wall),
                        Is.EqualTo(connected),
                        $"Room {roomIndex}'s {wall} face must report a doorway exactly when another room occupies {cell + direction}.");
                    CubeRoomWallBoundary boundary = room.GetWallBoundary(wall);
                    Assert.That(
                        boundary,
                        Is.Not.Null,
                        $"Room {roomIndex}'s {wall} face is missing its configurable wall boundary.");

                    Renderer fullPane = room.GetWallGlassRenderer(wall);
                    Collider fullCollider = room.GetWallBoundaryCollider(wall);
                    Assert.That(fullPane, Is.Not.Null);
                    Assert.That(fullCollider, Is.Not.Null);
                    Assert.That(
                        fullPane.enabled,
                        Is.EqualTo(!connected),
                        connected
                            ? $"Room {roomIndex}'s full {wall} glass pane must not cover its doorway."
                            : $"Room {roomIndex}'s unconnected {wall} glass pane must remain visible.");
                    Assert.That(
                        fullCollider.enabled,
                        Is.EqualTo(!connected),
                        connected
                            ? $"Room {roomIndex}'s full {wall} collider must not block its doorway."
                            : $"Room {roomIndex}'s unconnected {wall} collider must remain solid.");

                    if (!connected)
                    {
                        Assert.That(boundary.OwnsBoundary, Is.True);
                        Assert.That(boundary.DoorwayGlassRenderers.Count, Is.EqualTo(3));
                        Assert.That(boundary.DoorwayColliders.Count, Is.EqualTo(3));
                        Assert.That(boundary.DoorwayGlassRenderers.All(renderer => !renderer.enabled), Is.True);
                        Assert.That(boundary.DoorwayColliders.All(collider => !collider.enabled), Is.True);
                        Assert.That(boundary.ClosedBaseTrimRenderer.enabled, Is.True);
                        Assert.That(boundary.DoorwayBaseTrimRenderers.All(renderer => !renderer.enabled), Is.True);
                        AssertBoundaryProbe(
                            room,
                            null,
                            wall,
                            lateralOffset: 0f,
                            height: 1.0f,
                            expectedBlocked: true,
                            message: "An unconnected wall must remain sealed at player height.");
                        continue;
                    }

                    CubeRoom neighbor = generator.Rooms[neighborIndex];
                    CubeRoomWallBoundary neighborBoundary = neighbor.GetWallBoundary(WallFacing(-direction));
                    Assert.That(
                        neighbor.HasDoorway(WallFacing(-direction)),
                        Is.True,
                        $"Doorways must be configured on both sides of the seam between rooms {roomIndex} and {neighborIndex}.");
                    Assert.That(neighborBoundary, Is.Not.Null);
                    Assert.That(
                        (boundary.OwnsBoundary ? 1 : 0) + (neighborBoundary.OwnsBoundary ? 1 : 0),
                        Is.EqualTo(1),
                        "Exactly one room must render and collide with each shared doorway frame.");

                    CubeRoomWallBoundary owningBoundary = boundary.OwnsBoundary ? boundary : neighborBoundary;
                    CubeRoomWallBoundary duplicateBoundary = boundary.OwnsBoundary ? neighborBoundary : boundary;
                    Assert.That(owningBoundary.DoorwayGlassRenderers.Count, Is.EqualTo(3));
                    Assert.That(owningBoundary.DoorwayColliders.Count, Is.EqualTo(3));
                    Assert.That(owningBoundary.DoorwayGlassRenderers.All(renderer => renderer.enabled), Is.True);
                    Assert.That(owningBoundary.DoorwayColliders.All(collider => collider.enabled), Is.True);
                    Assert.That(owningBoundary.ClosedBaseTrimRenderer.enabled, Is.False);
                    Assert.That(owningBoundary.DoorwayBaseTrimRenderers.All(renderer => renderer.enabled), Is.True);
                    Assert.That(duplicateBoundary.DoorwayGlassRenderers.All(renderer => !renderer.enabled), Is.True);
                    Assert.That(duplicateBoundary.DoorwayColliders.All(collider => !collider.enabled), Is.True);
                    Assert.That(duplicateBoundary.ClosedBaseTrimRenderer.enabled, Is.False);
                    Assert.That(duplicateBoundary.DoorwayBaseTrimRenderers.All(renderer => !renderer.enabled), Is.True);

                    AssertBoundaryProbe(
                        room,
                        neighbor,
                        wall,
                        lateralOffset: 0f,
                        height: 1.0f,
                        expectedBlocked: false,
                        message: "The centered, floor-level doorway must be physically open.");
                    AssertBoundaryProbe(
                        room,
                        neighbor,
                        wall,
                        lateralOffset: 1.25f,
                        height: 1.0f,
                        expectedBlocked: true,
                        message: "The wall beside the two-metre-wide doorway must remain solid.");
                    AssertBoundaryProbe(
                        room,
                        neighbor,
                        wall,
                        lateralOffset: 0f,
                        height: 2.65f,
                        expectedBlocked: true,
                        message: "The wall above the 2.4-metre-high doorway must remain solid.");
                }
            }
        }

        private static void AssertBoundaryProbe(
            CubeRoom room,
            CubeRoom neighbor,
            CubeRoomWall wall,
            float lateralOffset,
            float height,
            bool expectedBlocked,
            string message)
        {
            Vector3 localDirection = LocalDirection(wall);
            Vector3 localTangent = new Vector3(localDirection.z, 0f, -localDirection.x);
            Vector3 localOrigin = localDirection * (CubeRoom.InteriorWidth * 0.5f - 0.6f)
                + localTangent * lateralOffset
                + Vector3.up * height;
            Vector3 worldOrigin = room.transform.TransformPoint(localOrigin);
            Vector3 worldDirection = room.transform.TransformDirection(localDirection);

            bool blocked = Physics
                .RaycastAll(worldOrigin, worldDirection, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                .Any(hit => IsRoomBoundary(hit.collider, room, neighbor));

            Assert.That(blocked, Is.EqualTo(expectedBlocked), message);
        }

        private static bool IsRoomBoundary(Collider collider, CubeRoom room, CubeRoom neighbor)
        {
            return collider != null
                && (collider.transform.IsChildOf(room.transform)
                    || (neighbor != null && collider.transform.IsChildOf(neighbor.transform)));
        }

        private static int FindCellIndex(IReadOnlyList<Vector2Int> cells, Vector2Int target)
        {
            for (int index = 0; index < cells.Count; index++)
            {
                if (cells[index] == target)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int ManhattanDistance(Vector2Int first, Vector2Int second)
        {
            Vector2Int delta = first - second;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        }

        private static CubeRoomWall WallFacing(Vector2Int direction)
        {
            if (direction == Vector2Int.left)
            {
                return CubeRoomWall.West;
            }

            if (direction == Vector2Int.right)
            {
                return CubeRoomWall.East;
            }

            if (direction == Vector2Int.down)
            {
                return CubeRoomWall.South;
            }

            Assert.That(direction, Is.EqualTo(Vector2Int.up), $"{direction} is not a cardinal room-facing direction.");
            return CubeRoomWall.North;
        }

        private static Vector2Int DirectionFacing(CubeRoomWall wall)
        {
            return wall switch
            {
                CubeRoomWall.West => Vector2Int.left,
                CubeRoomWall.East => Vector2Int.right,
                CubeRoomWall.South => Vector2Int.down,
                CubeRoomWall.North => Vector2Int.up,
                _ => throw new System.ArgumentOutOfRangeException(nameof(wall), wall, null),
            };
        }

        private static Vector3 LocalDirection(CubeRoomWall wall)
        {
            Vector2Int direction = DirectionFacing(wall);
            return new Vector3(direction.x, 0f, direction.y);
        }

        private static void AssertCameraStack(List<Camera> cameras)
        {
            Assert.That(cameras, Has.Count.EqualTo(2), "Expected one gameplay camera and one viewmodel overlay camera.");

            Camera baseCamera = cameras.SingleOrDefault(camera =>
                camera.TryGetComponent(out UniversalAdditionalCameraData data)
                && data.renderType == CameraRenderType.Base);
            Camera overlayCamera = cameras.SingleOrDefault(camera =>
                camera.TryGetComponent(out UniversalAdditionalCameraData data)
                && data.renderType == CameraRenderType.Overlay);

            Assert.That(baseCamera, Is.Not.Null);
            Assert.That(overlayCamera, Is.Not.Null);
            Assert.That(baseCamera.CompareTag("MainCamera"), Is.True);
            Assert.That(baseCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(baseCamera.backgroundColor.maxColorComponent, Is.LessThanOrEqualTo(0.001f));
            Assert.That(
                baseCamera.GetComponent<UniversalAdditionalCameraData>().cameraStack,
                Does.Contain(overlayCamera));
        }
    }
}
