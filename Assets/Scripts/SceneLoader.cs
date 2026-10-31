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

        // Ease the outgoing scene's music and ambience down before the next one activates.
        float audioFadeDuration = SceneAudio.FadeOutForSceneChange(activationDelay);

        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
        if (operation == null)
        {
            Debug.LogError($"Could not begin loading scene index {buildIndex}.");
            yield break;
        }

        operation.allowSceneActivation = false;

        float wait = Mathf.Max(activationDelay, audioFadeDuration);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;
    }
}
