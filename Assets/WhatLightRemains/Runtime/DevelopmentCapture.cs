#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WhatLightRemains.Runtime
{
    /// <summary>
    /// Opt-in visual verification hook for development builds. It has no scene object
    /// and does nothing unless the process receives -wlrCapture &lt;absolute path&gt;.
    /// </summary>
    internal sealed class DevelopmentCapture : MonoBehaviour
    {
        private string outputPath;
        private int width;
        private int height;
        private bool disableLighting;
        private bool exteriorView;
        private bool validationView;
        private bool cursorSmoke;
        private bool creationPreview;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenRequested()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            string capturePath = ReadArgument(arguments, "-wlrCapture");
            if (string.IsNullOrWhiteSpace(capturePath))
            {
                return;
            }

            // Automated verification launches the development player without stealing
            // focus from the user. Keep rendering long enough to produce the capture.
            Application.runInBackground = true;

            if (int.TryParse(ReadArgument(arguments, "-wlrClusterSeed"), out int clusterSeed))
            {
                CubeRoomClusterGenerator cluster = FindAnyObjectByType<CubeRoomClusterGenerator>();
                if (cluster != null)
                {
                    // This runs before Start. CubeRoomClusterGenerator remembers the explicit
                    // generation, so its automatic startup pass will not replace this layout.
                    cluster.Generate(clusterSeed);
                }
            }

            int captureWidth = ReadPositiveInt(arguments, "-wlrWidth", 1920);
            int captureHeight = ReadPositiveInt(arguments, "-wlrHeight", 1080);
            GameObject host = new GameObject("Development Capture");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            DevelopmentCapture capture = host.AddComponent<DevelopmentCapture>();
            capture.outputPath = Path.GetFullPath(capturePath);
            capture.width = captureWidth;
            capture.height = captureHeight;
            capture.disableLighting = HasArgument(arguments, "-wlrLightingOff");
            capture.exteriorView = HasArgument(arguments, "-wlrExterior");
            capture.validationView = HasArgument(arguments, "-wlrValidationView");
            capture.cursorSmoke = HasArgument(arguments, "-wlrCursorSmoke");
            capture.creationPreview = HasArgument(arguments, "-wlrCreationPreview");
        }

        private IEnumerator Start()
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            if (disableLighting)
            {
                foreach (CubeRoom room in FindObjectsByType<CubeRoom>())
                {
                    room.SetLightingEnabled(false);
                }
            }

            if (creationPreview)
            {
                // Allow the scene's normal Start methods to capture input and establish
                // the authored starting-room assignment before entering Create mode.
                yield return null;
                ConfigureCreationPreview();
            }
            else if (exteriorView)
            {
                ConfigureExteriorView();
            }
            else if (validationView)
            {
                ConfigureValidationView();
            }

            bool cursorSmokePassed = true;
            if (cursorSmoke)
            {
                yield return null;
                PlayerLook look = FindAnyObjectByType<PlayerLook>();
                PauseMenuController pause = FindAnyObjectByType<PauseMenuController>();
                if (look == null || pause == null)
                {
                    cursorSmokePassed = false;
                }
                else
                {
                    pause.Pause();
                    yield return null;
                    bool releasedForMenu = PauseMenuController.IsPaused && pause.MenuRoot.activeSelf
                        && !look.IsCursorCaptured && Cursor.lockState == CursorLockMode.None && Cursor.visible;
                    pause.Resume();
                    yield return null;
                    bool gameplayCaptured = !PauseMenuController.IsPaused && !pause.MenuRoot.activeSelf
                        && look.IsCursorCaptured && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;
                    cursorSmokePassed = releasedForMenu && gameplayCaptured;
                    Debug.Log($"Cursor smoke: pauseMenu={releasedForMenu}, gameplay={gameplayCaptured}.");
                }
            }

            for (int frame = 0; frame < 12; frame++)
            {
                yield return new WaitForEndOfFrame();
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            bool succeeded = CaptureOffscreenFrame();
            Debug.Log(succeeded
                ? $"Development screenshot saved to {outputPath} at {Screen.width}x{Screen.height}."
                : $"Development screenshot timed out: {outputPath}.");
            Application.Quit(succeeded && cursorSmokePassed ? 0 : 2);
        }

        private bool CaptureOffscreenFrame()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("Development capture requires a tagged Main Camera.");
                return false;
            }

            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            List<(Canvas canvas, RenderMode mode, Camera worldCamera, float planeDistance)> canvases = new();
            Texture2D pixels = null;
            try
            {
                // Camera.Render can run while the verification player is hidden. Temporarily
                // route overlay canvases through the gameplay camera so the HUD is included.
                foreach (Canvas canvas in FindObjectsByType<Canvas>())
                {
                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        continue;
                    }

                    canvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 0.5f;
                }

                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                foreach ((Canvas canvas, RenderMode mode, Camera worldCamera, float planeDistance) in canvases)
                {
                    if (canvas == null) continue;
                    canvas.renderMode = mode;
                    canvas.worldCamera = worldCamera;
                    canvas.planeDistance = planeDistance;
                }

                if (pixels != null) Destroy(pixels);
                target.Release();
                Destroy(target);
            }
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }

        private static int ReadPositiveInt(string[] arguments, string name, int fallback)
        {
            string value = ReadArgument(arguments, name);
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private static bool HasArgument(string[] arguments, string name)
        {
            foreach (string argument in arguments)
            {
                if (string.Equals(argument, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureExteriorView()
        {
            FirstPersonMotor motor = PrepareStaticPlayerView();
            if (motor == null)
            {
                return;
            }

            CubeRoomClusterGenerator cluster = FindAnyObjectByType<CubeRoomClusterGenerator>();
            if (cluster == null || cluster.PrimaryRoom == null || cluster.GridCells.Count == 0)
            {
                motor.transform.SetPositionAndRotation(new Vector3(0f, 2.35f, -13f), Quaternion.identity);
                ResetPitchAndFov(motor, 60f);
                return;
            }

            int minX = cluster.GridCells[0].x;
            int maxX = minX;
            int minZ = cluster.GridCells[0].y;
            int maxZ = minZ;
            foreach (Vector2Int cell in cluster.GridCells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minZ = Mathf.Min(minZ, cell.y);
                maxZ = Mathf.Max(maxZ, cell.y);
            }

            float centerX = (minX + maxX) * CubeRoom.InteriorWidth * 0.5f;
            float centerZ = (minZ + maxZ) * CubeRoom.InteriorDepth * 0.5f;
            float spanX = (maxX - minX + 1) * CubeRoom.InteriorWidth;
            float spanZ = (maxZ - minZ + 1) * CubeRoom.InteriorDepth;
            float nearestOuterZ = minZ * CubeRoom.InteriorDepth - CubeRoom.InteriorDepth * 0.5f;
            float standOff = Mathf.Max(13f, Mathf.Max(spanX, spanZ) * 0.9f);

            Transform roomBasis = cluster.PrimaryRoom.transform;
            Vector3 rootPosition = roomBasis.TransformPoint(
                new Vector3(centerX, 2.35f, nearestOuterZ - standOff));
            Vector3 target = roomBasis.TransformPoint(new Vector3(centerX, 4f, centerZ));
            Vector3 flatForward = Vector3.ProjectOnPlane(target - rootPosition, roomBasis.up).normalized;
            motor.transform.SetPositionAndRotation(
                rootPosition,
                Quaternion.LookRotation(flatForward, roomBasis.up));

            Transform yawPivot = GetYawPivot(motor);
            if (yawPivot != null) yawPivot.localRotation = Quaternion.identity;
            Transform pitchPivot = GetPitchPivot(motor);
            if (pitchPivot != null)
            {
                pitchPivot.rotation = Quaternion.LookRotation(target - pitchPivot.position, roomBasis.up);
            }

            if (Camera.main != null)
            {
                Camera.main.fieldOfView = 60f;
            }
        }

        private static void ConfigureValidationView()
        {
            FirstPersonMotor motor = PrepareStaticPlayerView();
            if (motor == null)
            {
                return;
            }

            motor.transform.SetPositionAndRotation(new Vector3(0f, 0.93f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Transform yawPivot = GetYawPivot(motor);
            if (yawPivot != null) yawPivot.localRotation = Quaternion.identity;
            Transform pitchPivot = GetPitchPivot(motor);
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.identity;
            }

            if (Camera.main != null)
            {
                Camera.main.fieldOfView = 65f;
            }
        }

        private static void ConfigureCreationPreview()
        {
            FirstPersonMotor motor = FindAnyObjectByType<FirstPersonMotor>();
            CubeRoomClusterGenerator layout = FindAnyObjectByType<CubeRoomClusterGenerator>();
            RoomCreationController creation = FindAnyObjectByType<RoomCreationController>();
            Camera camera = Camera.main;
            if (motor == null || layout == null || layout.PrimaryRoom == null || creation == null || camera == null)
            {
                Debug.LogError("Creation preview capture could not resolve the player, room layout, and creation controller.");
                return;
            }

            CubeRoom room = layout.PrimaryRoom;
            motor.enabled = false;
            CharacterController controller = motor.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Vector3 playerPosition = room.transform.TransformPoint(new Vector3(0f, 0.93f, 0f));
            Vector3 north = room.GetWallNormalWorld(CubeRoomWall.North);
            motor.transform.SetPositionAndRotation(playerPosition, Quaternion.LookRotation(north, room.RoomUp));
            Transform yawPivot = GetYawPivot(motor);
            if (yawPivot != null) yawPivot.localRotation = Quaternion.identity;
            Transform pitchPivot = GetPitchPivot(motor);
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.identity;
            }

            PlayerLook look = motor.GetComponent<PlayerLook>();
            look?.CaptureCursor();
            if (!creation.EnterCreateMode()
                || !creation.RefreshTarget(new Ray(camera.transform.position, camera.transform.forward)))
            {
                Debug.LogError("Creation preview capture failed to establish a valid snapped room ghost.");
            }
        }

        private static FirstPersonMotor PrepareStaticPlayerView()
        {
            FirstPersonMotor motor = FindAnyObjectByType<FirstPersonMotor>();
            if (motor == null)
            {
                return null;
            }

            motor.enabled = false;
            PlayerLook look = motor.GetComponent<PlayerLook>();
            if (look != null)
            {
                look.enabled = false;
            }

            CharacterController controller = motor.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Transform pitchPivot = GetPitchPivot(motor);
            Transform viewmodel = pitchPivot != null ? pitchPivot.Find("Viewmodel") : null;
            if (viewmodel != null)
            {
                viewmodel.gameObject.SetActive(false);
            }

            HotbarView hotbar = FindAnyObjectByType<HotbarView>();
            if (hotbar != null)
            {
                hotbar.gameObject.SetActive(false);
            }

            return motor;
        }

        private static void ResetPitchAndFov(FirstPersonMotor motor, float fieldOfView)
        {
            Transform yawPivot = GetYawPivot(motor);
            if (yawPivot != null) yawPivot.localRotation = Quaternion.identity;
            Transform pitchPivot = GetPitchPivot(motor);
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.identity;
            }

            if (Camera.main != null)
            {
                Camera.main.fieldOfView = fieldOfView;
            }
        }

        private static Transform GetYawPivot(FirstPersonMotor motor)
        {
            return motor != null ? motor.transform.Find("Yaw Pivot") : null;
        }

        private static Transform GetPitchPivot(FirstPersonMotor motor)
        {
            Transform yawPivot = GetYawPivot(motor);
            return yawPivot != null
                ? yawPivot.Find("Pitch Pivot")
                : motor != null ? motor.transform.Find("Pitch Pivot") : null;
        }
    }
}
#endif
