using System.Collections;
using UnityEngine;

/// <summary>
/// Fades the dialogue box in whenever it is shown — rising it into place from just below
/// its authored spot — and fades it back out when the scene's last line is finished.
/// </summary>
[DisallowMultipleComponent]
public class TextBoxFader : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float fadeInDuration = 0.35f;
    [SerializeField, Min(0.01f)] float fadeOutDuration = 0.3f;
    [SerializeField, Min(0f)] float riseDistance = 30f;

    CanvasGroup canvasGroup;
    RectTransform rectTransform;
    Vector2 restingPosition;
    bool hasRestingPosition;
    Coroutine fadeRoutine;

    void Awake()
    {
        // Give the bare slab its rounded panel and frame before it is ever shown.
        DialoguePanel.Apply(gameObject);

        EnsureCanvasGroup();
        EnsureRestingPosition();
    }

    void OnEnable()
    {
        EnsureCanvasGroup();
        EnsureRestingPosition();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        canvasGroup.alpha = 0f;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = restingPosition - Vector2.up * riseDistance;
        }

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

        if (rectTransform != null)
        {
            // Leave it exactly where it belongs so the next entrance starts from the right spot.
            rectTransform.anchoredPosition = restingPosition;
        }

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
        Vector2 startPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);

            if (rectTransform != null)
            {
                // Settle with a small overshoot so the box lands instead of just appearing.
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, restingPosition, EaseOutBack(progress));
            }

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = restingPosition;
        }

        fadeRoutine = null;
    }

    static float EaseOutBack(float progress)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        float shifted = progress - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
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

    void EnsureRestingPosition()
    {
        if (hasRestingPosition)
        {
            return;
        }

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        restingPosition = rectTransform.anchoredPosition;
        hasRestingPosition = true;
    }
}
