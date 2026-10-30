using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    public static IEnumerator LoadSceneAsync(int buildIndex, float activationDelay = 0f)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"Cannot load scene index {buildIndex}: it is not in Build Settings.");
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
        if (operation == null)
        {
            Debug.LogError($"Could not begin loading scene index {buildIndex}.");
            yield break;
        }

        operation.allowSceneActivation = false;

        if (activationDelay > 0f)
        {
            yield return new WaitForSeconds(activationDelay);
        }

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;
    }
}
