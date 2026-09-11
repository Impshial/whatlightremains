using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class RoomCreationPlayModeTests
    {
        private const string SceneName = "Foundation";
        private Scene loadedScene;
        private Scene previousActiveScene;

        [UnityTest]
        public IEnumerator CreateMode_ShowsSnappedNonPhysicalGhostAndPlacesExactlyOneRoom()
        {
            yield return LoadFoundation();
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            PlayerRoomTracker tracker = FindInScene<PlayerRoomTracker>().Single();
            RoomCreationController creation = FindInScene<RoomCreationController>().Single();
            RoomCreationPromptView prompt = FindInScene<RoomCreationPromptView>().Single();
            PlayerLook look = creation.GetComponent<PlayerLook>();
            look.CaptureCursor();

            Assert.That(creation.EnterCreateMode(), Is.True);
            Assert.That(prompt.CurrentText, Is.EqualTo(RoomCreationPromptView.CreateText));
            Assert.That(prompt.RotationLabel.gameObject.activeSelf, Is.False);
            Assert.That(prompt.CurrentRotationText, Is.EqualTo(RoomCreationPromptView.RotationIdleText));
            Ray eastRay = CenterRay(layout.PrimaryRoom, CubeRoomWall.East);
            Assert.That(creation.RefreshTarget(eastRay), Is.True);
            Assert.That(prompt.RotationLabel.gameObject.activeSelf, Is.True);
            Assert.That(creation.HasValidPreview, Is.True);
            Assert.That(creation.Candidate.GridCell, Is.EqualTo(Vector2Int.right));
            Assert.That(creation.PreviewObject.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(12));
            Assert.That(creation.PreviewObject.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(creation.PreviewObject.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(creation.PreviewObject.GetComponentsInChildren<CubeRoom>(true), Is.Empty);
            Assert.That(creation.PreviewObject.GetComponentsInChildren<MeshRenderer>(true).Length,
                Is.EqualTo(layout.RoomPrefab.GetComponentsInChildren<MeshRenderer>(true).Length + 2),
                "The ghost must include every prefab mesh plus its shaft-and-head gravity arrow.");
            Assert.That(creation.PreviewObject.GetComponentsInChildren<Transform>(true)
                .Any(child => child.name == "Floor"), Is.True,
                "The preview must include the filled floor rather than only a wire outline.");
            Assert.That(creation.PreviewObject.GetComponentsInChildren<Transform>(true)
                .Any(child => child.name == "Gravity Direction Arrow"), Is.True);

            Assert.That(creation.TryFinalizePlacement(eastRay), Is.True);
            yield return null;
            Assert.That(layout.Rooms, Has.Count.EqualTo(2));
            Assert.That(creation.IsCreateMode, Is.False);
            Assert.That(creation.HasValidPreview, Is.False);
            Assert.That(FindInScene<RoomCreationPromptView>().Single().RotationLabel.gameObject.activeSelf, Is.False);
            Assert.That(creation.PreviewObject, Is.Null);
            Assert.That(prompt.CurrentText, Is.EqualTo(RoomCreationPromptView.NormalText));
            Assert.That(prompt.RotationLabel.gameObject.activeSelf, Is.False);

            CubeRoom created = layout.Rooms[1];
            Assert.That(layout.PrimaryRoom.GetConnectedRoom(CubeRoomWall.East), Is.SameAs(created));
            Assert.That(created.GetConnectedRoom(CubeRoomWall.West), Is.SameAs(layout.PrimaryRoom));
            Assert.That(layout.PrimaryRoom.HasDoorway(CubeRoomWall.East), Is.True);
            Assert.That(created.HasDoorway(CubeRoomWall.West), Is.True);
            Assert.That(tracker.CurrentRoom, Is.SameAs(layout.PrimaryRoom));
            Assert.That(created.GravityStrength, Is.EqualTo(layout.PrimaryRoom.GravityStrength).Within(0.001f));

            float primaryGravity = layout.PrimaryRoom.GravityStrength;
            created.GravityStrength = primaryGravity * 0.5f;
            Assert.That(layout.PrimaryRoom.GravityStrength, Is.EqualTo(primaryGravity).Within(0.001f));
            Assert.That(FindInScene<PlayerRoomTracker>().Length, Is.EqualTo(1));
            Assert.That(FindInScene<Camera>().Length, Is.EqualTo(2));
            Assert.That(FindInScene<HotbarView>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CeilingAim_ShowsAnExactStackedCandidateButFloorAimRemainsInvalid()
        {
            yield return LoadFoundation();
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            RoomCreationController creation = FindInScene<RoomCreationController>().Single();
            creation.GetComponent<PlayerLook>().CaptureCursor();
            Assert.That(creation.EnterCreateMode(), Is.True);

            Vector3 center = layout.PrimaryRoom.transform.TransformPoint(new Vector3(0f, 4f, 0f));
            Assert.That(creation.RefreshTarget(new Ray(center, layout.PrimaryRoom.RoomUp)), Is.True);
            Assert.That(creation.Candidate.SourceFace, Is.EqualTo(CubeRoomFace.Ceiling));
            Assert.That(creation.Candidate.GridCell3D, Is.EqualTo(Vector3Int.up));
            Assert.That(creation.Candidate.PassageKind, Is.EqualTo(RoomPassageKind.Sealed));
            Assert.That(creation.Candidate.Position,
                Is.EqualTo(layout.PrimaryRoom.transform.position + layout.PrimaryRoom.RoomUp * CubeRoom.InteriorHeight)
                    .Using(UnityEngine.TestTools.Utils.Vector3ComparerWithEqualsOperator.Instance));

            Assert.That(creation.RefreshTarget(new Ray(center, -layout.PrimaryRoom.RoomUp)), Is.False);
            Assert.That(creation.HasValidPreview, Is.False);
            Assert.That(creation.IsCreateMode, Is.True);
        }

        [UnityTest]
        public IEnumerator InvalidAimAndConnectedDoorway_HideGhostWithoutLeavingCreateMode()
        {
            yield return LoadFoundation();
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            RoomCreationController creation = FindInScene<RoomCreationController>().Single();
            PlayerLook look = creation.GetComponent<PlayerLook>();
            look.CaptureCursor();
            Assert.That(creation.EnterCreateMode(), Is.True);

            Ray northRay = CenterRay(layout.PrimaryRoom, CubeRoomWall.North);
            Assert.That(creation.RefreshTarget(northRay), Is.True);
            Assert.That(creation.HasValidPreview, Is.True);

            Vector3 center = layout.PrimaryRoom.transform.TransformPoint(new Vector3(0f, 4f, 0f));
            Assert.That(creation.RefreshTarget(new Ray(center, -layout.PrimaryRoom.transform.up)), Is.False);
            Assert.That(creation.IsCreateMode, Is.True);
            Assert.That(creation.HasValidPreview, Is.False);
            Assert.That(creation.TryFinalizePlacement(new Ray(center, -layout.PrimaryRoom.transform.up)), Is.False);
            Assert.That(layout.Rooms, Has.Count.EqualTo(1));

            Assert.That(layout.TryPlaceRoom(layout.PrimaryRoom, CubeRoomWall.North, out CubeRoom created), Is.True);
            Assert.That(creation.RefreshTarget(northRay), Is.False, "A connected current-room boundary must stop targeting at the doorway.");
            Assert.That(creation.HasValidPreview, Is.False);

            trackerEnter(created);
            Ray createdEastRay = CenterRay(created, CubeRoomWall.East);
            Assert.That(creation.RefreshTarget(createdEastRay), Is.True);
            Assert.That(creation.Candidate.SourceRoom, Is.SameAs(created));
            creation.CancelCreateMode();
            Assert.That(creation.PreviewObject, Is.Null);
            Assert.That(FindInScene<RoomCreationPromptView>().Single().CurrentText, Is.EqualTo(RoomCreationPromptView.NormalText));

            void trackerEnter(CubeRoom room)
            {
                FindInScene<PlayerRoomTracker>().Single().EnterRoom(room);
            }
        }

        [UnityTest]
        public IEnumerator DeleteMode_HighlightsAdjacentRoomDeletesItAndStaysActive()
        {
            yield return LoadFoundation();
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            RoomDeletionController deletion = FindInScene<RoomDeletionController>().Single();
            RoomCreationPromptView prompt = FindInScene<RoomCreationPromptView>().Single();
            deletion.GetComponent<PlayerLook>().CaptureCursor();
            Assert.That(layout.TryPlaceRoom(layout.PrimaryRoom, CubeRoomWall.East, out CubeRoom created), Is.True);

            Assert.That(deletion.EnterDeleteMode(), Is.True);
            Assert.That(prompt.InstructionLabel.gameObject.activeSelf, Is.False);
            Assert.That(prompt.DeleteInstructionLabel.text, Is.EqualTo(RoomCreationPromptView.DeleteModeText));
            Assert.That(deletion.RefreshTarget(CenterRay(layout.PrimaryRoom, CubeRoomWall.East)), Is.True);
            Assert.That(deletion.FocusedRoom, Is.SameAs(created));
            Assert.That(prompt.CurrentRotationText, Is.EqualTo(RoomCreationPromptView.DeleteFocusText));
            Assert.That(prompt.RotationLabel.gameObject.activeSelf, Is.True);
            Assert.That(deletion.HighlightObject.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(12));
            Assert.That(deletion.HighlightObject.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(deletion.HighlightObject.GetComponentsInChildren<Light>(true), Is.Empty);

            Assert.That(deletion.TryDeleteFocusedRoom(), Is.True);
            yield return null;
            Assert.That(layout.Rooms, Has.Count.EqualTo(1));
            Assert.That(layout.PrimaryRoom.GetConnectedRoom(CubeRoomWall.East), Is.Null);
            Assert.That(layout.PrimaryRoom.GetWallBoundary(CubeRoomWall.East).ClosedGlassRenderer.enabled, Is.True);
            Assert.That(deletion.IsDeleteMode, Is.True);
            Assert.That(deletion.FocusedRoom, Is.Null);
            Assert.That(prompt.RotationLabel.gameObject.activeSelf, Is.False);

            deletion.CancelDeleteMode();
            Assert.That(prompt.InstructionLabel.gameObject.activeSelf, Is.True);
            Assert.That(prompt.DeleteInstructionLabel.text, Is.EqualTo(RoomCreationPromptView.NormalDeleteText));
        }

        [UnityTest]
        public IEnumerator DeleteMode_NeverTargetsPrimaryButCanTargetVerticalNonPrimaryNeighbor()
        {
            yield return LoadFoundation();
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            PlayerRoomTracker tracker = FindInScene<PlayerRoomTracker>().Single();
            RoomDeletionController deletion = FindInScene<RoomDeletionController>().Single();
            deletion.GetComponent<PlayerLook>().CaptureCursor();

            Assert.That(layout.TryGetPlacementCandidate(layout.PrimaryRoom, CubeRoomFace.Ceiling,
                RoomOrientation.Identity, out RoomPlacementCandidate firstCandidate), Is.True);
            Assert.That(layout.TryPlaceRoom(firstCandidate, out CubeRoom firstUpper), Is.True);
            Assert.That(layout.TryGetPlacementCandidate(firstUpper, CubeRoomFace.Ceiling,
                RoomOrientation.Identity, out RoomPlacementCandidate secondCandidate), Is.True);
            Assert.That(layout.TryPlaceRoom(secondCandidate, out CubeRoom secondUpper), Is.True);
            Assert.That(deletion.EnterDeleteMode(), Is.True);

            tracker.EnterRoom(firstUpper);
            Ray towardPrimary = new Ray(firstUpper.transform.TransformPoint(new Vector3(0f, 4f, 0f)), -firstUpper.RoomUp);
            Assert.That(deletion.RefreshTarget(towardPrimary), Is.False);
            Assert.That(deletion.FocusedRoom, Is.Null);

            tracker.EnterRoom(secondUpper);
            Ray towardFirst = new Ray(secondUpper.transform.TransformPoint(new Vector3(0f, 4f, 0f)), -secondUpper.RoomUp);
            Assert.That(deletion.RefreshTarget(towardFirst), Is.True);
            Assert.That(deletion.FocusedRoom, Is.SameAs(firstUpper));
        }

        [UnityTest]
        public IEnumerator NewRoomsUseCurrentAndFutureGlobalGlassOpacity()
        {
            yield return LoadFoundation();
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            GlassOpacityControl opacity = FindInScene<GlassOpacityControl>().Single();
            opacity.SetOpacity(0.42f);
            Assert.That(layout.TryPlaceRoom(layout.PrimaryRoom, CubeRoomWall.West, out CubeRoom created), Is.True);
            yield return null;

            int overrideId = Shader.PropertyToID("_WLRGlassOpacityOverride");
            Assert.That(Shader.GetGlobalFloat(overrideId), Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(created.GetWallGlassRenderer(CubeRoomWall.West).sharedMaterial,
                Is.SameAs(layout.PrimaryRoom.GetWallGlassRenderer(CubeRoomWall.West).sharedMaterial));
            opacity.SetOpacity(0.77f);
            Assert.That(Shader.GetGlobalFloat(overrideId), Is.EqualTo(0.77f).Within(0.001f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded) SceneManager.SetActiveScene(previousActiveScene);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedScene);
                if (unload != null) yield return unload;
            }

            loadedScene = default;
            previousActiveScene = default;
        }

        private IEnumerator LoadFoundation()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            loadedScene = SceneManager.GetSceneByName(SceneName);
            SceneManager.SetActiveScene(loadedScene);
            yield return null;
        }

        private static Ray CenterRay(CubeRoom room, CubeRoomWall wall)
        {
            return new Ray(room.transform.TransformPoint(new Vector3(0f, 4f, 0f)), room.GetWallNormalWorld(wall));
        }

        private static T[] FindInScene<T>() where T : Component
        {
            return SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
