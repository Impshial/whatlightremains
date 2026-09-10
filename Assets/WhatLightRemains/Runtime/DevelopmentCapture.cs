#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenRequested()
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            string capturePath = ReadArgument(arguments, "-wlrCapture");
            if (string.IsNullOrWhiteSpace(capturePath))
            {
                return;
            }

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

            if (exteriorView)
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
                if (look == null)
                {
                    cursorSmokePassed = false;
                }
                else
                {
                    look.ReleaseCursor();
                    yield return null;
                    bool released = !look.IsCursorCaptured && Cursor.lockState == CursorLockMode.None && Cursor.visible;
                    look.CaptureCursor();
                    yield return null;
                    bool captured = look.IsCursorCaptured && Cursor.lockState == CursorLockMode.Locked && !Cursor.visible;
                    cursorSmokePassed = released && captured;
                    Debug.Log($"Cursor smoke: release={released}, recapture={captured}.");
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

            ScreenCapture.CaptureScreenshot(outputPath);
            float timeout = Time.realtimeSinceStartup + 5f;
            while ((!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                   && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            bool succeeded = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            Debug.Log(succeeded
                ? $"Development screenshot saved to {outputPath} at {Screen.width}x{Screen.height}."
                : $"Development screenshot timed out: {outputPath}.");
            Application.Quit(succeeded && cursorSmokePassed ? 0 : 2);
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

            Transform pitchPivot = motor.transform.Find("Pitch Pivot");
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

            motor.transform.SetPositionAndRotation(new Vector3(0f, 0.35f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Transform pitchPivot = motor.transform.Find("Pitch Pivot");
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.identity;
            }

            if (Camera.main != null)
            {
                Camera.main.fieldOfView = 65f;
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

            Transform pitchPivot = motor.transform.Find("Pitch Pivot");
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
            Transform pitchPivot = motor.transform.Find("Pitch Pivot");
            if (pitchPivot != null)
            {
                pitchPivot.localRotation = Quaternion.identity;
            }

            if (Camera.main != null)
            {
                Camera.main.fieldOfView = fieldOfView;
            }
        }
    }
}
#endif
