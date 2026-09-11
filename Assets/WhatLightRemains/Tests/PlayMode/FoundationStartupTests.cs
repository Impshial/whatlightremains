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
        private Scene loadedScene;
        private Scene previousActiveScene;

        [UnityTest]
        public IEnumerator FoundationScene_StartsWithOneSealedRoomAndCreationHudReady()
        {
            Assert.That(Application.CanStreamedLevelBeLoaded(SceneName), Is.True);
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            loadedScene = SceneManager.GetSceneByName(SceneName);
            previousActiveScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
            yield return new WaitForFixedUpdate();

            List<CubeRoom> rooms = FindInScene<CubeRoom>(loadedScene);
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>(loadedScene).Single();
            PlayerRoomTracker tracker = FindInScene<PlayerRoomTracker>(loadedScene).Single();
            RoomCreationController creation = FindInScene<RoomCreationController>(loadedScene).Single();
            RoomDeletionController deletion = FindInScene<RoomDeletionController>(loadedScene).Single();
            RoomCreationPromptView prompt = FindInScene<RoomCreationPromptView>(loadedScene).Single();

            Assert.That(rooms, Has.Count.EqualTo(1));
            Assert.That(layout.AdditionalRoomCount, Is.EqualTo(0));
            Assert.That(layout.Rooms, Has.Count.EqualTo(1));
            Assert.That(layout.GridCells, Is.EqualTo(new[] { Vector2Int.zero }));
            Assert.That(layout.GridCells3D, Is.EqualTo(new[] { Vector3Int.zero }));
            Assert.That(layout.PrimaryRoom, Is.SameAs(rooms[0]));
            Assert.That(tracker.CurrentRoom, Is.SameAs(layout.PrimaryRoom));
            Assert.That(creation.IsCreateMode, Is.False);
            Assert.That(creation.HasValidPreview, Is.False);
            Assert.That(deletion.IsDeleteMode, Is.False);
            Assert.That(prompt.CurrentText, Is.EqualTo(RoomCreationPromptView.NormalText));
            Assert.That(prompt.DeleteInstructionLabel, Is.Not.Null);
            Assert.That(prompt.CurrentDeleteText, Is.EqualTo(RoomCreationPromptView.NormalDeleteText));
            Assert.That(prompt.DeleteInstructionLabel.gameObject.activeSelf, Is.True);
            Assert.That(prompt.RotationLabel, Is.Not.Null);
            Assert.That(prompt.RotationLabel.gameObject.activeSelf, Is.False);
            Assert.That(prompt.TraversalLabel, Is.Not.Null);
            Assert.That(prompt.TraversalLabel.gameObject.activeSelf, Is.False);
            Assert.That(layout.PrimaryRoom.GetConnectedRoom(CubeRoomFace.Ceiling), Is.Null);
            Assert.That(layout.PrimaryRoom.CeilingBoundary, Is.Not.Null);
            Assert.That(layout.PrimaryRoom.CeilingBoundary.ClosedColliders.All(collider => collider.enabled), Is.True);

            foreach (CubeRoomWall wall in System.Enum.GetValues(typeof(CubeRoomWall)))
            {
                Assert.That(layout.PrimaryRoom.HasDoorway(wall), Is.False);
                Assert.That(layout.PrimaryRoom.GetConnectedRoom(wall), Is.Null);
                Assert.That(layout.PrimaryRoom.GetWallGlassRenderer(wall).enabled, Is.True);
                Assert.That(layout.PrimaryRoom.GetWallBoundaryCollider(wall).enabled, Is.True);
            }

            Assert.That(FindInScene<FirstPersonMotor>(loadedScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<KinematicCapsuleMover>(loadedScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<PlayerGravityAlignment>(loadedScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<PlayerRoomTraversal>(loadedScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<CharacterController>(loadedScene), Is.Empty);
            Assert.That(FindInScene<HotbarView>(loadedScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<Camera>(loadedScene), Has.Count.EqualTo(2));
            AssertCameraStack(FindInScene<Camera>(loadedScene));
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
                if (unload != null) yield return unload;
            }

            loadedScene = default;
            previousActiveScene = default;
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToList();
        }

        private static void AssertCameraStack(List<Camera> cameras)
        {
            Camera baseCamera = cameras.Single(camera => camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Base);
            Camera overlayCamera = cameras.Single(camera => camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderType == CameraRenderType.Overlay);
            Assert.That(baseCamera.CompareTag("MainCamera"), Is.True);
            Assert.That(baseCamera.GetComponent<UniversalAdditionalCameraData>().cameraStack, Does.Contain(overlayCamera));
        }
    }
}
