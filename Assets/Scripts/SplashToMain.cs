using System.Collections;
using UnityEngine;

public class SplashToMain : MonoBehaviour
{
    [SerializeField] private GameObject fadeOut;
    [SerializeField] private float fadeOutDuration = 2f;

    void Start()
    {
        if (fadeOut == null)
        {
            Debug.LogError("FadeOut GameObject is not assigned.", this);
        }
        else
        {
            fadeOut.SetActive(false);
        }

        StartCoroutine(SplashSequence());
    }

    IEnumerator SplashSequence()
    {
        yield return new WaitForSeconds(3.5f);

        if (fadeOut != null)
        {
            fadeOut.SetActive(true);
        }

        yield return SceneLoader.LoadSceneAsync(1, fadeOutDuration);
    }
}
