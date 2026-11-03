using System.Collections;
using UnityEngine;

/// <summary>
/// Fades the dialogue box in whenever it is shown — rising it into place from just below
/// its authored spot — and sinks it away again before the scene switches it off.
/// </summary>
[DisallowMultipleComponent]
public class TextBoxFader : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float fadeInDuration = 0.35f;
    [SerializeField, Min(0.01f)] float fadeOutDuration = 0.3f;
    [SerializeField, Min(0f)] float riseDistance = 30f;

    [Tooltip("Size the box starts at, as a fraction of its authored size, before settling to its full size.")]
    [SerializeField, Range(0.9f, 1f)] float entranceScale = 0.985f;

    /// <summary>How much of the rise the box gives back on its way out.</summary>
    const float ExitDrop = 0.6f;

    /// <summary>A single long frame must not skip part of the fade, or it reads as a pop.</summary>
    const float MaxStep = 1f / 30f;

    CanvasGroup canvasGroup;
    RectTransform rectTransform;
    Vector2 restingPosition;
    Vector3 restingScale = Vector3.one;
    bool hasRestingTransform;
    int fadeId;

    void Awake()
    {
        // Give the bare slab its rounded panel and frame before it is ever shown.
        DialoguePanel.Apply(gameObject);

        EnsureCanvasGroup();
        EnsureRestingTransform();
    }

    void OnEnable()
    {
        EnsureCanvasGroup();
        EnsureRestingTransform();

        canvasGroup.alpha = 0f;
        SetOffset(-riseDistance);
        SetScale(entranceScale);

        StartCoroutine(PlayFade(true));
    }

    /// <summary>
    /// Fades the box out and then deactivates it. Safe to call while it is already hidden.
    /// </summary>
    public IEnumerator FadeOutAndHide()
    {
        EnsureCanvasGroup();

        if (!gameObject.activeInHierarchy)
        {
            canvasGroup.alpha = 0f;
            yield break;
        }

        yield return PlayFade(false);

        canvasGroup.alpha = 0f;
        RestoreTransform();

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Fades the given object out and hides it. The box is almost always addressed through one of
    /// its children (the body text), so the fader is looked up the hierarchy rather than only on
    /// the object itself. Falls back to a plain deactivate when no fader owns it.
    /// </summary>
    public static IEnumerator Hide(GameObject textBox)
    {
        if (textBox == null)
        {
            yield break;
        }

        TextBoxFader fader = textBox.GetComponent<TextBoxFader>();

        if (fader == null)
        {
            fader = textBox.GetComponentInParent<TextBoxFader>(true);
        }

        if (fader != null && fader.gameObject.activeInHierarchy)
        {
            yield return fader.FadeOutAndHide();
            yield break;
        }

        if (textBox.activeSelf)
        {
            textBox.SetActive(false);
        }
    }

    IEnumerator PlayFade(bool showing)
    {
        int id = ++fadeId;

        if (canvasGroup == null || rectTransform == null)
        {
            yield break;
        }

        float duration = Mathf.Max(showing ? fadeInDuration : fadeOutDuration, 0.01f);
        float startAlpha = canvasGroup.alpha;
        float startOffset = rectTransform.anchoredPosition.y - restingPosition.y;
        float startScale = CurrentScale();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (id != fadeId)
            {
                // A newer fade (a hide landing mid-entrance, say) has taken the box over.
                yield break;
            }

            float progress = Mathf.Clamp01(elapsed / duration);

            // The alpha eases at both ends so the box materialises instead of snapping on. The
            // slide decelerates into place, and accelerates away again when the box leaves.
            canvasGroup.alpha = Mathf.Lerp(startAlpha, showing ? 1f : 0f, Mathf.SmoothStep(0f, 1f, progress));

            float travel = showing ? EaseOutCubic(progress) : EaseInCubic(progress);
            SetOffset(Mathf.Lerp(startOffset, showing ? 0f : -riseDistance * ExitDrop, travel));
            SetScale(Mathf.Lerp(startScale, 1f, travel));

            yield return null;

            // Measured after the frame, and clamped so a hitch stretches the fade slightly instead
            // of letting it lurch forward.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        }

        if (id != fadeId)
        {
            // A newer fade took over; it owns the transform from here.
            yield break;
        }

        canvasGroup.alpha = showing ? 1f : 0f;
        RestoreTransform();
    }

    static float EaseOutCubic(float progress)
    {
        float inverse = 1f - progress;
        return 1f - inverse * inverse * inverse;
    }

    static float EaseInCubic(float progress)
    {
        return progress * progress * progress;
    }

    void SetOffset(float verticalOffset)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = restingPosition + Vector2.up * verticalOffset;
        }
    }

    void SetScale(float scale)
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = restingScale * scale;
        }
    }

    float CurrentScale()
    {
        if (rectTransform == null || Mathf.Approximately(restingScale.x, 0f))
        {
            return 1f;
        }

        return rectTransform.localScale.x / restingScale.x;
    }

    void RestoreTransform()
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchoredPosition = restingPosition;
        rectTransform.localScale = restingScale;
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

    void EnsureRestingTransform()
    {
        if (hasRestingTransform)
        {
            return;
        }

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        restingPosition = rectTransform.anchoredPosition;
        restingScale = rectTransform.localScale;
        hasRestingTransform = true;
    }
}
