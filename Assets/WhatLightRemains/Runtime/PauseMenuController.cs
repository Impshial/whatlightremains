using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private FirstPersonInput input;
        [SerializeField] private PlayerLook playerLook;
        [SerializeField] private RoomCreationController creationController;
        [SerializeField] private RoomDeletionController deletionController;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private string gameplaySceneName = "Foundation";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private GameObject[] gameplayHudElements;
        private bool[] hudActiveStates;

        public static bool IsPaused { get; private set; }
        public GameObject MenuRoot => menuRoot;

        public void ConfigureGameplayHud(GameObject[] elements) => gameplayHudElements = elements;

        public void Configure(GameObject root, Button resume, Button reset, Button mainMenu, Button quit,
            string gameplayScene, string menuScene)
        {
            menuRoot = root;
            resumeButton = resume;
            resetButton = reset;
            mainMenuButton = mainMenu;
            quitButton = quit;
            gameplaySceneName = gameplayScene;
            mainMenuSceneName = menuScene;
            ResolvePlayer();
            SetVisible(false);
        }

        public void Pause()
        {
            if (IsPaused) return;
            ResolvePlayer();
            IsPaused = true;
            Time.timeScale = 0f;
            playerLook?.ReleaseCursor();
            SetVisible(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            SetVisible(false);
            Time.timeScale = 1f;
            IsPaused = false;
            playerLook?.CaptureCursor();
        }

        public void ResetWorld()
        {
            PrepareSceneChange();
            SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }

        public void ReturnToMainMenu()
        {
            PrepareSceneChange();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        public void QuitToDesktop()
        {
            Time.timeScale = 1f;
            IsPaused = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public bool HandleEscapeRequest()
        {
            ResolvePlayer();
            if (WorldMapController.IsMapOpen || WorldMapController.ClosedByEscapeThisFrame) return false;
            if (IsPaused)
            {
                Resume();
                return true;
            }

            // Create/Delete own the first Escape. Their own Update pass cancels the active mode
            // without releasing the gameplay cursor; only a later Escape may open this menu.
            if ((creationController != null && creationController.IsCreateMode)
                || (deletionController != null && deletionController.IsDeleteMode)) return false;
            Pause();
            return true;
        }

        private void Awake()
        {
            ResolvePlayer();
            Time.timeScale = 1f;
            IsPaused = false;
            SetVisible(false);
        }

        private void OnEnable()
        {
            resumeButton?.onClick.AddListener(Resume);
            resetButton?.onClick.AddListener(ResetWorld);
            mainMenuButton?.onClick.AddListener(ReturnToMainMenu);
            quitButton?.onClick.AddListener(QuitToDesktop);
        }

        private void OnDisable()
        {
            resumeButton?.onClick.RemoveListener(Resume);
            resetButton?.onClick.RemoveListener(ResetWorld);
            mainMenuButton?.onClick.RemoveListener(ReturnToMainMenu);
            quitButton?.onClick.RemoveListener(QuitToDesktop);
            RestoreGameplayHud();
            if (IsPaused)
            {
                Time.timeScale = 1f;
                IsPaused = false;
            }
        }

        private void Update()
        {
            ResolvePlayer();
            if (input == null || !input.EscapePressedThisFrame) return;
            HandleEscapeRequest();
        }

        private void ResolvePlayer()
        {
            if (input != null && playerLook != null) return;
            input ??= FindAnyObjectByType<FirstPersonInput>();
            if (input == null) return;
            playerLook ??= input.GetComponent<PlayerLook>();
            creationController ??= input.GetComponent<RoomCreationController>();
            deletionController ??= input.GetComponent<RoomDeletionController>();
        }

        private void PrepareSceneChange()
        {
            SetVisible(false);
            Time.timeScale = 1f;
            IsPaused = false;
        }

        private void SetVisible(bool visible)
        {
            if (visible) HideGameplayHud();
            else RestoreGameplayHud();
            if (menuRoot != null) menuRoot.SetActive(visible);
        }

        private void HideGameplayHud()
        {
            if (gameplayHudElements == null || hudActiveStates != null) return;
            hudActiveStates = new bool[gameplayHudElements.Length];
            for (int i = 0; i < gameplayHudElements.Length; i++)
            {
                GameObject element = gameplayHudElements[i];
                if (element == null) continue;
                hudActiveStates[i] = element.activeSelf;
                element.SetActive(false);
            }
        }

        private void RestoreGameplayHud()
        {
            if (hudActiveStates == null) return;
            for (int i = 0; i < hudActiveStates.Length; i++)
                if (gameplayHudElements[i] != null) gameplayHudElements[i].SetActive(hudActiveStates[i]);
            hudActiveStates = null;
        }
    }
}
