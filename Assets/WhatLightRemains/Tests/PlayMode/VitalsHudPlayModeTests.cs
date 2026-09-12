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
    public sealed class VitalsHudPlayModeTests
    {
        private PlayerVitals player;
        private WorldOxygenReserve oxygen;
        private VitalsHudView hud;

        [UnitySetUp]
        public IEnumerator LoadFoundationDirectly()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Foundation", LoadSceneMode.Single);
            yield return null;
            ReadSession();
            FindInScene<PlayerLook>().Single().CaptureCursor();
        }

        [UnityTest]
        public IEnumerator DirectFoundationStartup_BindsAllGaugesToFullAuthoritativeValues()
        {
            Assert.That(hud.PlayerVitals, Is.SameAs(player));
            Assert.That(hud.OxygenReserve, Is.SameAs(oxygen));
            AssertStartingValues();
            AssertVisible(true);
            Assert.That(player.GetComponent<FirstPersonMotor>(), Is.Not.Null);
            Assert.That(oxygen.transform.IsChildOf(player.transform), Is.False);
            Assert.That(oxygen.GetComponentInParent<CubeRoom>(true), Is.Null);
            Assert.That(oxygen.GetComponentInParent<Canvas>(true), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitMutations_UpdateOnlyTheirGaugeIncludingFractionalAndEmptyStates()
        {
            player.SetHealth(37.5f);
            AssertGauge(hud.HealthGauge, "Health", 37.5f, 100f);
            AssertGauge(hud.HungerGauge, "Hunger", 100f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 100f, 100f);

            player.RemoveHunger(37.75f);
            oxygen.RemoveOxygen(87.5f);
            AssertGauge(hud.HungerGauge, "Hunger", 62.25f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 12.5f, 100f);
            AssertGauge(hud.HealthGauge, "Health", 37.5f, 100f);

            player.RemoveHealth(1000f);
            player.SetHunger(0f);
            oxygen.SetOxygen(0f);
            AssertGauge(hud.HealthGauge, "Health", 0f, 100f);
            AssertGauge(hud.HungerGauge, "Hunger", 0f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 0f, 100f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ElapsedGameplayAndEmptyReserves_HaveNoAutomaticDrainsOrControlPenalties()
        {
            player.SetHealth(51.25f);
            player.SetHunger(61.5f);
            oxygen.SetOxygen(71.75f);
            int changes = 0;
            player.HealthChanged += (_, __) => changes++;
            player.HungerChanged += (_, __) => changes++;
            oxygen.OxygenChanged += (_, __) => changes++;

            yield return new WaitForSeconds(0.15f);
            Assert.That(player.HealthCurrent, Is.EqualTo(51.25f));
            Assert.That(player.HungerCurrent, Is.EqualTo(61.5f));
            Assert.That(oxygen.CurrentOxygen, Is.EqualTo(71.75f));
            Assert.That(changes, Is.Zero, "Elapsed gameplay must not drain or regenerate any value.");

            player.SetHunger(0f);
            oxygen.SetOxygen(0f);
            changes = 0;
            yield return new WaitForSeconds(0.15f);
            Assert.That(player.HealthCurrent, Is.EqualTo(51.25f), "Empty reserves must not damage Health.");
            Assert.That(player.HungerCurrent, Is.Zero);
            Assert.That(oxygen.CurrentOxygen, Is.Zero);
            Assert.That(changes, Is.Zero);

            player.SetHealth(0f);
            FirstPersonMotor motor = player.GetComponent<FirstPersonMotor>();
            Assert.That(motor.isActiveAndEnabled, Is.True);
            Assert.That(player.GetComponent<FirstPersonInput>().isActiveAndEnabled, Is.True);
            Assert.That(player.GetComponent<PlayerRoomTraversal>().isActiveAndEnabled, Is.True);
            Assert.That(motor.WalkSpeed, Is.EqualTo(3f));
            Assert.That(motor.SprintSpeed, Is.EqualTo(6f));
            Assert.That(motor.JumpHeight, Is.EqualTo(1f));

            Vector3 beforeMove = motor.transform.position;
            Vector3 up = player.GetComponent<PlayerRoomTracker>().CurrentRoom.RoomUp;
            motor.Tick(Vector2.up, false, true, 0.05f);
            Assert.That(Vector3.ProjectOnPlane(motor.transform.position - beforeMove, up).magnitude,
                Is.GreaterThan(0.1f), "Zero Health must still allow normal sprint movement.");

            RoomCreationController creation = player.GetComponent<RoomCreationController>();
            RoomDeletionController deletion = player.GetComponent<RoomDeletionController>();
            Assert.That(creation.EnterCreateMode(), Is.True);
            AssertVisible(true);
            creation.CancelCreateMode();
            Assert.That(deletion.EnterDeleteMode(), Is.True);
            AssertVisible(true);
            deletion.CancelDeleteMode();
            yield return new WaitForSeconds(0.1f);
            Assert.That(changes, Is.EqualTo(1), "Only the explicit SetHealth(0) may notify during this sequence.");
            Assert.That(player.HealthCurrent, Is.Zero);
            Assert.That(player.HungerCurrent, Is.Zero);
            Assert.That(oxygen.CurrentOxygen, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MapAndPauseCycles_HideOnlyPresentationAndRestoreCurrentSnapshots()
        {
            WorldMapController map = FindInScene<WorldMapController>().Single();
            PauseMenuController pause = FindInScene<PauseMenuController>().Single();
            player.SetHealth(80f);
            player.SetHunger(70f);
            oxygen.SetOxygen(60f);
            int changes = 0;
            player.HealthChanged += (_, __) => changes++;
            player.HungerChanged += (_, __) => changes++;
            oxygen.OxygenChanged += (_, __) => changes++;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(map.OpenMap(), Is.True);
                yield return null;
                AssertVisible(false);
                AssertOwnersActive();
                player.RemoveHealth(1f);
                oxygen.RemoveOxygen(1f);
                map.CloseMap();
                yield return null;
                AssertVisible(true);
                AssertCurrentGaugeSnapshots();

                pause.Pause();
                yield return null;
                AssertVisible(false);
                AssertOwnersActive();
                player.RemoveHunger(1f);
                pause.Resume();
                yield return null;
                AssertVisible(true);
                AssertCurrentGaugeSnapshots();
            }

            Assert.That(player.HealthCurrent, Is.EqualTo(77f));
            Assert.That(player.HungerCurrent, Is.EqualTo(67f));
            Assert.That(oxygen.CurrentOxygen, Is.EqualTo(57f));
            Assert.That(changes, Is.EqualTo(9), "Visibility changes must never reset or mutate the owners.");
        }

        [UnityTest]
        public IEnumerator DisableAndRebind_DetachesOldOwnersAndRefreshesLatestSnapshotOnEnable()
        {
            PlayerVitals replacementPlayer = new GameObject("Test replacement player values").AddComponent<PlayerVitals>();
            WorldOxygenReserve replacementOxygen = new GameObject("Test replacement world reserve").AddComponent<WorldOxygenReserve>();
            replacementPlayer.SetHealth(28f);
            replacementPlayer.SetHunger(48f);
            replacementOxygen.SetOxygen(68f);

            for (int cycle = 0; cycle < 3; cycle++) hud.Configure(replacementPlayer, replacementOxygen);
            Assert.That(hud.PlayerVitals, Is.SameAs(replacementPlayer));
            Assert.That(hud.OxygenReserve, Is.SameAs(replacementOxygen));
            AssertGauge(hud.HealthGauge, "Health", 28f, 100f);
            AssertGauge(hud.HungerGauge, "Hunger", 48f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 68f, 100f);

            player.SetHealth(1f);
            player.SetHunger(2f);
            oxygen.SetOxygen(3f);
            AssertGauge(hud.HealthGauge, "Health", 28f, 100f);
            AssertGauge(hud.HungerGauge, "Hunger", 48f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 68f, 100f);

            hud.enabled = false;
            replacementPlayer.SetHealth(27f);
            replacementPlayer.SetHunger(47f);
            replacementOxygen.SetOxygen(67f);
            AssertGauge(hud.HealthGauge, "Health", 28f, 100f);
            AssertGauge(hud.HungerGauge, "Hunger", 48f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 68f, 100f);
            hud.enabled = true;
            AssertGauge(hud.HealthGauge, "Health", 27f, 100f);
            AssertGauge(hud.HungerGauge, "Hunger", 47f, 100f);
            AssertGauge(hud.OxygenGauge, "Oxygen", 67f, 100f);

            hud.Configure(player, oxygen);
            Object.Destroy(replacementPlayer.gameObject);
            Object.Destroy(replacementOxygen.gameObject);
            yield return null;
            AssertCurrentGaugeSnapshots();
        }

        [UnityTest]
        public IEnumerator RoomCreationDeletionOwnershipAndLighting_KeepOneUnchangedWorldReserve()
        {
            CubeRoomClusterGenerator layout = FindInScene<CubeRoomClusterGenerator>().Single();
            PlayerRoomTracker tracker = player.GetComponent<PlayerRoomTracker>();
            oxygen.SetOxygen(42.5f);
            float maximum = oxygen.MaxOxygen;
            int oxygenChanges = 0;
            oxygen.OxygenChanged += (_, __) => oxygenChanges++;

            RoomCreationController creation = player.GetComponent<RoomCreationController>();
            Assert.That(creation.EnterCreateMode(), Is.True);
            Ray east = new Ray(layout.PrimaryRoom.transform.TransformPoint(new Vector3(0f, 4f, 0f)),
                layout.PrimaryRoom.GetWallNormalWorld(CubeRoomWall.East));
            Assert.That(creation.RefreshTarget(east), Is.True);
            Assert.That(creation.PreviewObject.GetComponentsInChildren<WorldOxygenReserve>(true), Is.Empty);
            Assert.That(creation.PreviewObject.GetComponentsInChildren<PlayerVitals>(true), Is.Empty);
            creation.CancelCreateMode();

            Assert.That(layout.TryPlaceRoom(layout.PrimaryRoom, CubeRoomWall.East, out CubeRoom bridge), Is.True);
            Assert.That(layout.TryPlaceRoom(bridge, CubeRoomWall.East, out CubeRoom branch), Is.True);
            tracker.EnterRoom(branch);
            Assert.That(tracker.CurrentRoom, Is.SameAs(branch));
            branch.GravityStrength *= 0.5f;
            branch.SetPower(false);
            layout.PrimaryRoom.SetPower(false);
            tracker.EnterRoom(layout.PrimaryRoom);
            Assert.That(layout.TryDeleteRoom(bridge), Is.True);
            Assert.That(layout.IsRegistered(branch), Is.True, "The disconnected branch remains in this same world.");
            yield return null;

            Assert.That(layout.Rooms, Has.Count.EqualTo(2));
            Assert.That(FindInScene<WorldOxygenReserve>(), Has.Length.EqualTo(1));
            Assert.That(FindInScene<PlayerVitals>(), Has.Length.EqualTo(1));
            Assert.That(FindInScene<WorldOxygenReserve>().Single(), Is.SameAs(oxygen));
            Assert.That(oxygen.CurrentOxygen, Is.EqualTo(42.5f));
            Assert.That(oxygen.MaxOxygen, Is.EqualTo(maximum));
            Assert.That(oxygenChanges, Is.Zero);
            AssertGauge(hud.OxygenGauge, "Oxygen", 42.5f, maximum);
            AssertVisible(true);
            Assert.That(hud.OxygenGauge.GetComponentInParent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        }

        [UnityTest]
        public IEnumerator ResetWorld_ReplacesSessionOwnersAndRestoresStartingValues()
        {
            PlayerVitals oldPlayer = player;
            WorldOxygenReserve oldOxygen = oxygen;
            player.SetHealth(15f);
            player.SetHunger(25f);
            oxygen.SetOxygen(35f);
            PauseMenuController pause = FindInScene<PauseMenuController>().Single();
            pause.Pause();
            pause.ResetWorld();
            yield return null;
            yield return null;
            ReadSession();

            Assert.That(oldPlayer == null, Is.True, "Reset must dispose the old player and subscriptions.");
            Assert.That(oldOxygen == null, Is.True, "Reset must dispose the old world reserve.");
            Assert.That(PauseMenuController.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            AssertStartingValues();
            AssertVisible(true);
        }

        [UnityTest]
        public IEnumerator ReturnToMenuThenNewGame_RemovesOldHudAndCreatesFreshFullSession()
        {
            PlayerVitals oldPlayer = player;
            WorldOxygenReserve oldOxygen = oxygen;
            player.SetHealth(10f);
            player.SetHunger(20f);
            oxygen.SetOxygen(30f);
            PauseMenuController pause = FindInScene<PauseMenuController>().Single();
            pause.Pause();
            pause.ReturnToMainMenu();
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(oldPlayer == null, Is.True);
            Assert.That(oldOxygen == null, Is.True);
            Assert.That(FindInScene<PlayerVitals>(), Is.Empty);
            Assert.That(FindInScene<WorldOxygenReserve>(), Is.Empty);
            Assert.That(FindInScene<VitalsHudView>(), Is.Empty);
            Assert.That(FindInScene<VitalGaugeView>(), Is.Empty);
            MainMenuController menu = FindInScene<MainMenuController>().Single();
            menu.Configure(menu.MenuGroup, menu.NewGameButton, "Foundation", 0.01f);
            menu.NewGameButton.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != "Foundation" && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Foundation"));
            yield return null;
            ReadSession();
            AssertStartingValues();
            AssertVisible(true);
        }

#if UNITY_EDITOR
        [UnityTest, Explicit("Opt-in visual capture; run with graphics and inspect the generated PNGs.")]
        public IEnumerator CaptureVitalsAtSupportedResolutionsAndValues()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Vitals visual capture requires a graphics device; do not pass -nographics.");

            string directory = System.IO.Path.GetFullPath("Logs/VitalsHudVisuals");
            System.IO.Directory.CreateDirectory(directory);
            Camera camera = player.GetComponentsInChildren<Camera>(true).Single(candidate => candidate.CompareTag("MainCamera"));
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            player.GetComponent<FirstPersonMotor>().enabled = false;
            player.GetComponent<PlayerLook>().enabled = false;
            player.GetComponent<PlayerGravityAlignment>().enabled = false;
            Vector2Int[] resolutions = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(2560, 1080) };
            foreach (Vector2Int resolution in resolutions)
            {
                for (int state = 0; state < 3; state++)
                {
                    player.SetHealth(state == 0 ? 100f : state == 1 ? 72.5f : 0f);
                    player.SetHunger(state == 0 ? 100f : state == 1 ? 47f : 0f);
                    oxygen.SetOxygen(state == 0 ? 100f : state == 1 ? 21.25f : 0f);
                    yield return null;
                    string stateName = state == 0 ? "full" : state == 1 ? "partial" : "empty";
                    CaptureFrame(camera, canvas, resolution,
                        System.IO.Path.Combine(directory, $"vitals-{resolution.x}x{resolution.y}-{stateName}.png"));
                }
            }

            CubeRoom room = FindInScene<CubeRoomClusterGenerator>().Single().PrimaryRoom;
            room.SetPower(false);
            Vector3 center = room.transform.TransformPoint(new Vector3(0f, CubeRoom.InteriorHeight * 0.5f, 0f));
            room.transform.RotateAround(center, Vector3.forward, 90f);
            // Keep the gameplay camera rolled relative to the room. The HUD must remain upright on screen.
            player.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 20f, 35f));
            player.SetHealth(66f);
            player.SetHunger(33f);
            oxygen.SetOxygen(12.5f);
            yield return null;
            foreach (Vector2Int resolution in resolutions)
            {
                CaptureFrame(camera, canvas, resolution,
                    System.IO.Path.Combine(directory, $"vitals-{resolution.x}x{resolution.y}-dark-rotated.png"));
            }
            Debug.Log("Vitals HUD visual captures written to " + directory);
        }

        private static void CaptureFrame(Camera camera, Canvas canvas, Vector2Int resolution, string path)
        {
            RenderTexture target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlaneDistance = canvas.planeDistance;
            StandaloneRenderResize previousResize = canvas.updateRectTransformForStandalone;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.5f;
                canvas.updateRectTransformForStandalone = StandaloneRenderResize.Enabled;
                // The same authored CanvasScaler uses the target camera's pixel dimensions.
                canvas.GetComponent<CanvasScaler>().SendMessage("Handle", SendMessageOptions.RequireReceiver);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0f, 0f, resolution.x, resolution.y), 0, 0, false);
                pixels.Apply(false, false);
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
                Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(0));
                Assert.That(pixels.width, Is.EqualTo(resolution.x));
                Assert.That(pixels.height, Is.EqualTo(resolution.y));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousPlaneDistance;
                canvas.updateRectTransformForStandalone = previousResize;
                RenderTexture.active = previousActive;
                if (pixels != null) Object.Destroy(pixels);
                target.Release();
                Object.Destroy(target);
                Canvas.ForceUpdateCanvases();
            }
        }
#endif

        [UnityTearDown]
        public IEnumerator CleanUpSession()
        {
            foreach (WorldMapController map in FindInScene<WorldMapController>())
                if (map.IsOpen) map.CloseMap();
            foreach (PauseMenuController pause in FindInScene<PauseMenuController>())
                if (PauseMenuController.IsPaused) pause.Resume();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Scene session = SceneManager.GetActiveScene();
            if (session.IsValid() && session.isLoaded && (session.name == "Foundation" || session.name == "MainMenu"))
            {
                Scene cleanup = SceneManager.CreateScene("Vitals Test Cleanup");
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(session);
            }
            player = null;
            oxygen = null;
            hud = null;
        }

        private void ReadSession()
        {
            Assert.That(FindInScene<PlayerVitals>(), Has.Length.EqualTo(1));
            Assert.That(FindInScene<WorldOxygenReserve>(), Has.Length.EqualTo(1));
            Assert.That(FindInScene<VitalsHudView>(), Has.Length.EqualTo(1));
            player = FindInScene<PlayerVitals>().Single();
            oxygen = FindInScene<WorldOxygenReserve>().Single();
            hud = FindInScene<VitalsHudView>().Single();
        }

        private void AssertStartingValues()
        {
            Assert.That(player.HealthCurrent, Is.EqualTo(100f));
            Assert.That(player.HealthMax, Is.EqualTo(100f));
            Assert.That(player.HealthNormalized, Is.EqualTo(1f));
            Assert.That(player.HungerCurrent, Is.EqualTo(100f));
            Assert.That(player.HungerMax, Is.EqualTo(100f));
            Assert.That(player.HungerNormalized, Is.EqualTo(1f));
            Assert.That(oxygen.CurrentOxygen, Is.EqualTo(100f));
            Assert.That(oxygen.MaxOxygen, Is.EqualTo(100f));
            Assert.That(oxygen.OxygenNormalized, Is.EqualTo(1f));
            AssertCurrentGaugeSnapshots();
        }

        private void AssertOwnersActive()
        {
            Assert.That(player.gameObject.activeInHierarchy, Is.True);
            Assert.That(oxygen.gameObject.activeInHierarchy, Is.True);
        }

        private void AssertCurrentGaugeSnapshots()
        {
            AssertGauge(hud.HealthGauge, "Health", player.HealthCurrent, player.HealthMax);
            AssertGauge(hud.HungerGauge, "Hunger", player.HungerCurrent, player.HungerMax);
            AssertGauge(hud.OxygenGauge, "Oxygen", oxygen.CurrentOxygen, oxygen.MaxOxygen);
        }

        private void AssertVisible(bool expected)
        {
            foreach (VitalGaugeView gauge in new[] { hud.HealthGauge, hud.HungerGauge, hud.OxygenGauge })
            {
                Image fill = gauge.FillImage;
                bool visible = fill.isActiveAndEnabled
                    && fill.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha > 0.001f);
                Assert.That(visible, Is.EqualTo(expected), gauge.name + " presentation visibility");
            }
        }

        private static void AssertGauge(VitalGaugeView gauge, string label, float current, float maximum)
        {
            Assert.That(gauge, Is.Not.Null);
            Assert.That(gauge.name, Is.EqualTo(label + " Gauge"));
            Assert.That(gauge.FillImage.fillAmount, Is.EqualTo(current / maximum).Within(0.0001f));
        }

        private static T[] FindInScene<T>() where T : Component
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray()
                : new T[0];
        }
    }
}
