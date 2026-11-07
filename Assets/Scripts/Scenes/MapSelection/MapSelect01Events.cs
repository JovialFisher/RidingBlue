using UnityEngine;

/// <summary>
/// The town map that leads into a chapter: clicking the map fades the scene to black through
/// its FadeOut overlay and then opens the scene the map is standing in for.
/// </summary>
[DisallowMultipleComponent]
public class MapSelect01Events : MonoBehaviour
{
    [SerializeField] int nextSceneBuildIndex = 7;
    [SerializeField] GameObject fadeOut;
    [SerializeField, Min(0f)] float activationDelay = 2f;

    bool isLeaving;

    /// <summary>Wired to the map's click target; one click is enough to leave the map.</summary>
    public void NextScene()
    {
        if (isLeaving)
        {
            return;
        }

        // One pick per scene: a second click during the fade must not stack a second load.
        isLeaving = true;

        // The overlay plays the scene's FadeOut animation; the loader holds the scene swap
        // until that has carried the screen to black.
        if (fadeOut != null)
        {
            fadeOut.SetActive(true);
        }

        StartCoroutine(SceneLoader.LoadSceneAsync(nextSceneBuildIndex, activationDelay));
    }
}
