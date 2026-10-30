using System.Collections;
using UnityEngine;

/// <summary>
/// Fades the dialogue box in whenever it is shown and fades it back out
/// when the scene's last line is finished.
/// </summary>
[DisallowMultipleComponent]
public class TextBoxFader : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float fadeInDuration = 0.35f;
    [SerializeField, Min(0.01f)] float fadeOutDuration = 0.3f;

    CanvasGroup canvasGroup;
    Coroutine fadeRoutine;

    void Awake()
    {
        EnsureCanvasGroup();
    }

    void OnEnable()
    {
        EnsureCanvasGroup();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        canvasGroup.alpha = 0f;
        fadeRoutine = StartCoroutine(FadeTo(1f, fadeInDuration));
    }

    /// <summary>
    /// Fades the box out and then deactivates it. Safe to call while it is already hidden.
    /// </summary>
    public IEnumerator FadeOutAndHide()
    {
        EnsureCanvasGroup();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (!gameObject.activeInHierarchy)
        {
            canvasGroup.alpha = 0f;
            yield break;
        }

        yield return FadeTo(0f, fadeOutDuration);

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Fades the given box out and hides it. Falls back to a plain deactivate when
    /// the box has no fader attached or is already hidden.
    /// </summary>
    public static IEnumerator Hide(GameObject textBox)
    {
        if (textBox == null)
        {
            yield break;
        }

        TextBoxFader fader = textBox.GetComponent<TextBoxFader>();
        if (fader != null && textBox.activeInHierarchy)
        {
            yield return fader.FadeOutAndHide();
            yield break;
        }

        if (textBox.activeSelf)
        {
            textBox.SetActive(false);
        }
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;
    }

    void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
}
