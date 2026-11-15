using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NextButtonFadeIn : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float fadeDuration = 0.2f;
    [SerializeField, Min(0f)] float outlineFadeDuration = 0.15f;

    CanvasGroup canvasGroup;
    Outline outline;
    float outlineAlpha = 1f;
    bool raycastsWhenShown = true;
    Coroutine fadeRoutine;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // The authored value is remembered once. A fade that is cut short leaves raycasts off,
        // so the state at the time of a later fade is not a reliable thing to restore.
        raycastsWhenShown = canvasGroup.blocksRaycasts;

        // The rim is a mesh effect on the same graphic as the fill, so it is faded on its own
        // afterwards; that is what keeps the button from reading as a white shape first.
        outline = GetComponent<Outline>();
        if (outline != null)
        {
            outlineAlpha = outline.effectColor.a;
        }
    }

    void OnEnable()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeIn());
    }

    void OnDisable()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        // A scene that advances a line mid-fade stops this coroutine before it gets to hand the
        // button its raycasts back, which would leave the plate visible but deaf to the pointer
        // for the rest of the scene. Restoring it here keeps the button clickable either way.
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = raycastsWhenShown;
        }

        SetOutlineAlpha(outline, outlineAlpha);
    }

    IEnumerator FadeIn()
    {
        // The fade is alpha only. Touching canvasGroup.interactable here would make the
        // Selectable report itself as disabled for the length of the fade, tinting the
        // button with the pale Disabled colour before it snaps back to its own colour.
        // Swallowing raycasts keeps it unclickable while it appears without changing the tint.
        SetOutlineAlpha(outline, 0f);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;

        // The button is readable as soon as the fill is in, so it becomes clickable then and
        // only the rim is still catching up.
        canvasGroup.blocksRaycasts = raycastsWhenShown;

        if (outline != null && outlineFadeDuration > 0f)
        {
            elapsed = 0f;
            while (elapsed < outlineFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetOutlineAlpha(outline, outlineAlpha * Mathf.Clamp01(elapsed / outlineFadeDuration));
                yield return null;
            }
        }

        SetOutlineAlpha(outline, outlineAlpha);

        fadeRoutine = null;
    }

    static void SetOutlineAlpha(Outline target, float alpha)
    {
        if (target == null)
        {
            return;
        }

        Color color = target.effectColor;
        if (Mathf.Approximately(color.a, alpha))
        {
            return;
        }

        color.a = alpha;
        target.effectColor = color;
    }
}
