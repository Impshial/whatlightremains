using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WhatLightRemains.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField] private Button newGameButton;
        [SerializeField] private string gameplaySceneName = "Foundation";
        [SerializeField, Min(0f)] private float fadeDuration = 0.8f;

        private bool isStartingGame;

        public CanvasGroup MenuGroup => menuGroup;
        public Button NewGameButton => newGameButton;
        public string GameplaySceneName => gameplaySceneName;
        public float FadeDuration => fadeDuration;
        public bool IsStartingGame => isStartingGame;

        public void Configure(CanvasGroup group, Button button, string sceneName, float duration)
        {
            menuGroup = group;
            newGameButton = button;
            gameplaySceneName = sceneName;
            fadeDuration = Mathf.Max(0f, duration);
        }

        public void StartNewGame()
        {
            if (isStartingGame)
            {
                return;
            }

            StartCoroutine(FadeAndLoad());
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (menuGroup != null)
            {
                menuGroup.alpha = 1f;
                menuGroup.interactable = true;
                menuGroup.blocksRaycasts = true;
            }
        }

        private void OnEnable()
        {
            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(StartNewGame);
            }
        }

        private void OnDisable()
        {
            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveListener(StartNewGame);
            }
        }

        private IEnumerator FadeAndLoad()
        {
            isStartingGame = true;
            if (newGameButton != null)
            {
                newGameButton.interactable = false;
            }

            if (menuGroup != null)
            {
                menuGroup.interactable = false;
                menuGroup.blocksRaycasts = false;
                float startAlpha = menuGroup.alpha;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    menuGroup.alpha = Mathf.Lerp(startAlpha, 0f, fadeDuration <= 0f ? 1f : elapsed / fadeDuration);
                    yield return null;
                }

                menuGroup.alpha = 0f;
            }

            SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }
    }
}
