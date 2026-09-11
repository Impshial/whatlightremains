using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WhatLightRemains.Runtime;

namespace WhatLightRemains.Tests
{
    public sealed class MainMenuPlayModeTests
    {
        private const string MainMenuScenePath = "Assets/WhatLightRemains/Generated/Scenes/MainMenu.unity";

        [UnityTest]
        public IEnumerator NewGameButton_FadesMenuAndLoadsFoundation()
        {
            yield return SceneManager.LoadSceneAsync(MainMenuScenePath, LoadSceneMode.Single);
            Scene menuScene = SceneManager.GetSceneByPath(MainMenuScenePath);
            MainMenuController controller = menuScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
                .Single();
            controller.Configure(controller.MenuGroup, controller.NewGameButton, "Foundation", 0.05f);

            controller.NewGameButton.onClick.Invoke();
            Assert.That(controller.IsStartingGame, Is.True);
            Assert.That(controller.NewGameButton.interactable, Is.False);

            float deadline = Time.realtimeSinceStartup + 3f;
            while (SceneManager.GetActiveScene().name != "Foundation" && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Foundation"));
            Assert.That(Object.FindObjectsByType<CubeRoom>(FindObjectsInactive.Exclude, FindObjectsSortMode.None),
                Has.Length.EqualTo(1));

            Scene foundationScene = SceneManager.GetActiveScene();
            Scene cleanupScene = SceneManager.CreateScene("Main Menu Test Cleanup");
            SceneManager.SetActiveScene(cleanupScene);
            yield return SceneManager.UnloadSceneAsync(foundationScene);
        }
    }
}
