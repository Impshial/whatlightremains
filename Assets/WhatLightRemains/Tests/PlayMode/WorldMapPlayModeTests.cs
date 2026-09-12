using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class WorldMapPlayModeTests
    {
        [UnityTest]
        public IEnumerator MapUsesLiveWorldAndOwnsCameraCursorAndNavigationUntilClosed()
        {
            yield return SceneManager.LoadSceneAsync("Foundation", LoadSceneMode.Single);
            WorldMapController map = Object.FindObjectsByType<WorldMapController>(FindObjectsInactive.Include).Single();
            FirstPersonInput player = Object.FindAnyObjectByType<FirstPersonInput>();
            PlayerLook look = player.GetComponent<PlayerLook>();
            RoomCreationController creation = player.GetComponent<RoomCreationController>();
            RoomDeletionController deletion = player.GetComponent<RoomDeletionController>();
            PlayerRoomTracker tracker = player.GetComponent<PlayerRoomTracker>();
            RoomCreationPromptView prompt = Object.FindObjectsByType<RoomCreationPromptView>(FindObjectsInactive.Include).Single();
            PauseMenuController pause = Object.FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include).Single();
            CubeRoom room = Object.FindAnyObjectByType<CubeRoom>();
            look.CaptureCursor();

            Assert.That(map.OpenMap(), Is.True);
            Assert.That(map.IsOpen, Is.True);
            Assert.That(WorldMapController.IsMapOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(look.IsCursorCaptured, Is.False);
            Assert.That(creation.EnterCreateMode(), Is.False,
                "The map must exclusively own interaction while it has released the gameplay cursor.");
            Assert.That(deletion.EnterDeleteMode(), Is.False,
                "Delete targeting must not activate behind the live map.");
            prompt.SetCreateMode(false);
            Assert.That(prompt.InstructionLabel.gameObject.activeSelf, Is.False);
            Assert.That(prompt.DeleteInstructionLabel.gameObject.activeSelf, Is.False,
                "Creation and deletion hints must stay hidden for the entire map session.");
            Assert.That(map.MapCamera.enabled, Is.True);
            Assert.That(map.MapCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(map.MapCamera.backgroundColor, Is.EqualTo(Color.black));
            Transform mapReticle = map.MapOverlayRoot.transform.Find("Map Reticle");
            Assert.That(mapReticle, Is.Not.Null);
            Assert.That((mapReticle as RectTransform).anchoredPosition, Is.EqualTo(Vector2.zero),
                "The visible reticle must match the exact center ray used for room leveling.");
            Assert.That(mapReticle.GetComponent<Image>(), Is.Not.Null,
                "The map reticle must be the compact circular targeting marker.");
            Assert.That(map.GameplayReticle, Is.Not.Null);
            Assert.That(map.GameplayReticle.activeSelf, Is.False,
                "The larger gameplay plus must not overlap the map circle.");
            Assert.That((map.MapCamera.cullingMask & (1 << room.gameObject.layer)), Is.Not.Zero,
                "The map camera must render the live room geometry and all of its authored details.");
            Assert.That(map.MapRoot.GetComponentsInChildren<CubeRoom>(true), Is.Empty,
                "The map must not maintain a stale duplicate room model.");
            Assert.That(Vector3.Distance(map.FocusPoint, player.transform.position), Is.LessThan(0.001f));
            Vector3 viewportFocus = map.MapCamera.WorldToViewportPoint(map.FocusPoint);
            Assert.That(viewportFocus.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(viewportFocus.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(map.Distance, Is.EqualTo(20f).Within(0.001f),
                "Opening the map must use a close player-local view instead of fitting the entire cluster.");
            CubeRoom currentRoom = tracker.CurrentRoom != null ? tracker.CurrentRoom : room;
            Vector3 currentRoomCenter = currentRoom.transform.TransformPoint(
                new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f));
            Vector3 viewportRoomCenter = map.MapCamera.WorldToViewportPoint(currentRoomCenter);
            Assert.That(viewportRoomCenter.z, Is.GreaterThan(0f));
            Assert.That(viewportRoomCenter.x, Is.InRange(0f, 1f));
            Assert.That(viewportRoomCenter.y, Is.InRange(0f, 1f));
            Vector3 expectedForward = Vector3.ProjectOnPlane(look.YawRoot.forward, room.RoomUp).normalized;
            Vector3 markerForward = Vector3.ProjectOnPlane(map.PlayerMarker.forward, room.RoomUp).normalized;
            Assert.That(Vector3.Dot(expectedForward, markerForward), Is.GreaterThan(0.999f));

            Vector3 initialCameraPosition = map.MapCamera.transform.position;
            Quaternion initialCameraRotation = map.MapCamera.transform.rotation;
            Vector3 initialFocus = map.FocusPoint;
            float initialDistance = map.Distance;
            map.ApplyOrbitDelta(new Vector2(90f, -20f));
            Assert.That(Vector3.Distance(map.MapCamera.transform.position, initialCameraPosition), Is.GreaterThan(0.1f));
            Assert.That(map.FocusPoint, Is.EqualTo(initialFocus));
            Assert.That(map.Distance, Is.EqualTo(initialDistance).Within(0.001f));

            map.ApplyOrbitDelta(new Vector2(0f, 1200f));
            Vector3 unrestrictedOffset = (map.MapCamera.transform.position - map.FocusPoint).normalized;
            Assert.That(Vector3.Dot(unrestrictedOffset, room.RoomUp), Is.LessThan(0f),
                "Vertical orbit must continue past the former pole limit and underneath the map.");
            Vector3 positionPastPole = map.MapCamera.transform.position;
            map.ApplyOrbitDelta(new Vector2(0f, 200f));
            Assert.That(Vector3.Distance(map.MapCamera.transform.position, positionPastPole), Is.GreaterThan(0.1f),
                "Orbit must remain responsive after crossing a pole.");

            map.ResetViewToOpening();
            Assert.That(Vector3.Distance(map.FocusPoint, player.transform.position), Is.LessThan(0.001f));
            Assert.That(map.Distance, Is.EqualTo(initialDistance).Within(0.001f));
            Assert.That(Vector3.Distance(map.MapCamera.transform.position, initialCameraPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(map.MapCamera.transform.rotation, initialCameraRotation), Is.LessThan(0.001f),
                "R must restore the exact camera view captured when the map opened.");

            map.ApplyPanDelta(new Vector2(30f, -15f));
            Assert.That(Vector3.Distance(map.FocusPoint, initialFocus), Is.GreaterThan(0.01f));
            float beforeZoom = map.Distance;
            map.ApplyZoomSteps(1);
            Assert.That(map.Distance, Is.LessThan(beforeZoom));

            Vector3 lmbFocus = map.FocusPoint;
            Vector3 upBeforeHorizontalDrag = map.MapCamera.transform.up;
            map.BeginOrbitDrag();
            map.ApplyOrbitDragDelta(new Vector2(10f, 0.2f));
            Assert.That(map.FocusPoint, Is.EqualTo(lmbFocus),
                "LMB must orbit the current focal point without reacquiring the player.");
            Assert.That(Vector3.Angle(map.MapCamera.transform.up, upBeforeHorizontalDrag), Is.LessThan(0.01f),
                "Small perpendicular mouse noise must not wobble a horizontal orbit onto another axis.");
            Vector3 upBeforeAxisSwitch = map.MapCamera.transform.up;
            map.ApplyOrbitDragDelta(new Vector2(0.1f, 6f));
            Assert.That(Vector3.Angle(map.MapCamera.transform.up, upBeforeAxisSwitch), Is.GreaterThan(0.01f),
                "A deliberate vertical movement must switch axes without releasing LMB.");
            Vector3 upBeforeSwitchBack = map.MapCamera.transform.up;
            map.ApplyOrbitDragDelta(new Vector2(6f, 0.1f));
            Assert.That(Vector3.Angle(map.MapCamera.transform.up, upBeforeSwitchBack), Is.LessThan(0.01f),
                "A deliberate horizontal movement must switch back during the same drag.");
            map.EndOrbitDrag();

            map.ApplyPanDelta(new Vector2(-25f, 10f));
            Vector3 focusBeforeCenter = map.FocusPoint;
            map.CenterOnPlayer();
            Assert.That(map.IsCenterTransitioning, Is.True);
            Assert.That(Vector3.Distance(map.FocusPoint, focusBeforeCenter), Is.LessThan(0.001f),
                "C must begin from the current focal point instead of snapping.");
            map.AdvanceCenterTransition(0.09f);
            Assert.That(map.IsCenterTransitioning, Is.True);
            Assert.That(Vector3.Distance(map.FocusPoint, focusBeforeCenter), Is.GreaterThan(0.001f));
            Assert.That(Vector3.Distance(map.FocusPoint, player.transform.position), Is.GreaterThan(0.001f));
            map.AdvanceCenterTransition(1f);
            Assert.That(map.IsCenterTransitioning, Is.False);
            Assert.That(Vector3.Distance(map.FocusPoint, player.transform.position), Is.LessThan(0.001f),
                "C must finish centered exactly on the player after its quick eased move.");

            map.ApplyPanDelta(new Vector2(-25f, 10f));
            map.ApplyOrbitDelta(new Vector2(85f, 42f));
            Vector3 focusBeforeLevel = map.FocusPoint;
            Vector3 positionBeforeLevel = map.MapCamera.transform.position;
            float distanceBeforeLevel = map.Distance;
            map.LevelCurrentView();
            Assert.That(map.LevelReferenceRoom, Is.Not.Null,
                "Level Map must select the closest live room under the exact screen center.");
            Vector3 levelRoomUp = map.LevelReferenceRoom.RoomUp.normalized;
            Assert.That(Vector3.Distance(map.FocusPoint, focusBeforeLevel), Is.LessThan(0.001f),
                "RMB must level the existing focal point without returning to the player.");
            Assert.That(Vector3.Distance(map.MapCamera.transform.position, positionBeforeLevel), Is.LessThan(0.001f),
                "Leveling must rotate in place without moving the camera.");
            Assert.That(map.Distance, Is.EqualTo(distanceBeforeLevel).Within(0.001f));
            Assert.That(map.IsLevelTransitioning, Is.True,
                "A tilted view must ease toward the crosshair room's level plane instead of snapping.");
            map.AdvanceLevelTransition(0.08f);
            Assert.That(map.IsLevelTransitioning, Is.True);
            Assert.That(Vector3.Distance(map.MapCamera.transform.position, positionBeforeLevel), Is.LessThan(0.001f));
            map.AdvanceLevelTransition(1f);
            Assert.That(map.IsLevelTransitioning, Is.False);
            Vector3 projectedRoomUp = Vector3.ProjectOnPlane(levelRoomUp, map.MapCamera.transform.forward).normalized;
            float cardinalAlignment = Mathf.Abs(Vector3.Dot(map.MapCamera.transform.up, projectedRoomUp));
            Assert.That(cardinalAlignment, Is.GreaterThan(0.999f),
                "Level Map must fully level the crosshair room rather than using the player's room.");
            map.CloseMap(true);
            Assert.That(map.GameplayReticle.activeSelf, Is.True);
            Assert.That(prompt.InstructionLabel.gameObject.activeSelf, Is.True);
            Assert.That(prompt.DeleteInstructionLabel.gameObject.activeSelf, Is.True);
            Assert.That(map.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(look.IsCursorCaptured, Is.True);
            Assert.That(pause.HandleEscapeRequest(), Is.False,
                "The Escape that closes the map must not also open Pause in the same frame.");
            Assert.That(PauseMenuController.IsPaused, Is.False);
        }

        [UnityTearDown]
        public IEnumerator RestoreGlobalState()
        {
            WorldMapController map = Object.FindAnyObjectByType<WorldMapController>();
            if (map != null && map.IsOpen) map.CloseMap();
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            if (pause != null && PauseMenuController.IsPaused) pause.Resume();
            Time.timeScale = 1f;
            yield return null;
        }
    }
}
