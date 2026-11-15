using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dimmed veil that sits over a scene while a dialogue choice is waiting. It has its own
/// fading so it can clear itself after the picks have already been put away, and it swallows
/// clicks while it is up — the only way past a decision is to take one.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class DialogueChoiceScrim : MonoBehaviour
{
    const float FadeDuration = 0.22f;

    Image image;
    float from;
    float to;
    float changedAt;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    /// <summary>Raises the veil to the given opacity.</summary>
    public void Show(float alpha)
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        gameObject.SetActive(true);

        if (image == null)
        {
            return;
        }

        image.raycastTarget = true;

        from = image.color.a;
        to = Mathf.Clamp01(alpha);
        changedAt = Time.unscaledTime;
    }

    /// <summary>Fades the veil away and switches it off once it is clear.</summary>
    public void Hide()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image == null || !gameObject.activeSelf)
        {
            return;
        }

        from = image.color.a;
        to = 0f;
        changedAt = Time.unscaledTime;
    }

    void Update()
    {
        if (image == null)
        {
            return;
        }

        float progress = Mathf.Clamp01((Time.unscaledTime - changedAt) / FadeDuration);

        Color color = image.color;
        color.a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, progress));
        image.color = color;

        if (to <= 0f && color.a <= 0.001f)
        {
            image.raycastTarget = false;
            gameObject.SetActive(false);
        }
    }
}
