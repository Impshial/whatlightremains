using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class DefaultSceneLoader
{
    static DefaultSceneLoader()
    {
        // Only run this if the editor is freshly launching, not when entering Play Mode
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // REPLACE THIS PATH with the relative path to your desired default scene
            string defaultScenePath = "Assets/WhatLightRemains/Generated/Scenes/Foundation.unity";

            // Check if the current open scene matches the default to avoid unnecessary reloading
            if (EditorSceneManager.GetActiveScene().path != defaultScenePath)
            {
                // Optional: Asks you to save changes if you had an unsaved scene open
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(defaultScenePath);
                }
            }
        }
    }
}

