using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gives a pause menu row the same pointer feedback as the rest of the game's menus: the content
/// slides right, its label takes the cool accent tint, and the row dims while it cannot be used.
/// Everything eases on unscaled time, because the menu runs while the game is paused.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class PauseMenuRow : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Tooltip("How far, in UI units, the row's content slides right under the pointer.")]
    [SerializeField, Min(0f)] float hoverOffset = 16f;

    [Tooltip("Content scale while hovered; 1 leaves it untouched.")]
    [SerializeField, Min(0f)] float hoverScale = 1.03f;

    [Tooltip("Content scale while pressed, so the click reads as a small squash.")]
    [SerializeField, Min(0f)] float pressedScale = 0.99f;

    [Tooltip("Smaller values snap to the new look faster.")]
    [SerializeField, Min(0.01f)] float responseTime = 0.08f;

    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.35f;

    [SerializeField] Color hoverTint = new Color(0.66f, 0.8f, 1f, 1f);

    Button button;
    RectTransform content;
    Graphic tint;
    Image fill;

    Vector2 restingPosition;
    Vector3 restingScale = Vector3.one;
    Color restingTint = Color.white;
    Color restingFill = Color.white;

    bool hovering;
    bool pressing;

    /// <summary>
    /// Wires the row up after it is built. The content is the rect that slides, the tint is the
    /// label that takes the accent colour, and the fill is an optional plate that brightens.
    /// </summary>
    public void Configure(RectTransform contentRect, Graphic tintGraphic, Image fillImage,
        float slideDistance = 16f, float contentHoverScale = 1.03f, float contentPressedScale = 0.99f)
    {
        button = GetComponent<Button>();
        content = contentRect;
        tint = tintGraphic;
        fill = fillImage;

        hoverOffset = Mathf.Max(0f, slideDistance);
        hoverScale = Mathf.Max(0f, contentHoverScale);
        pressedScale = Mathf.Max(0f, contentPressedScale);

        if (content != null)
        {
            restingPosition = content.anchoredPosition;
            restingScale = content.localScale;
        }

        if (tint != null)
        {
            restingTint = tint.color;
        }

        if (fill != null)
        {
            restingFill = fill.color;
        }

        Apply(restingTint, restingFill, restingPosition, restingScale);
    }

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void Update()
    {
        if (content == null && tint == null && fill == null)
        {
            return;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        bool usable = button == null || button.interactable;
        bool highlighted = usable && hovering;

        Vector2 targetPosition = restingPosition;
        Vector3 targetScale = restingScale;
        Color targetTint = restingTint;
        Color targetFill = restingFill;

        if (!usable)
        {
            targetTint = Fade(restingTint, disabledAlpha);
            targetFill = Fade(restingFill, disabledAlpha * 0.75f);
        }
        else if (highlighted)
        {
            targetPosition = restingPosition + Vector2.right * (pressing ? hoverOffset + 6f : hoverOffset);
            targetScale = restingScale * (pressing ? pressedScale : hoverScale);
            targetTint = hoverTint;
            targetTint.a = restingTint.a;
            targetFill = Color.Lerp(restingFill, hoverTint, 0.25f);
            targetFill.a = Mathf.Min(1f, restingFill.a + 0.12f);
        }

        // Frame-rate independent easing, so the row keeps moving after the pointer stops.
        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / responseTime);

        if (content != null)
        {
            content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, targetPosition, blend);
            content.localScale = Vector3.Lerp(content.localScale, targetScale, blend);
        }

        if (tint != null)
        {
            tint.color = Color.Lerp(tint.color, targetTint, blend);
        }

        if (fill != null)
        {
            fill.color = Color.Lerp(fill.color, targetFill, blend);
        }
    }

    void OnDisable()
    {
        hovering = false;
        pressing = false;

        if (restingScale == Vector3.zero)
        {
            return;
        }

        Apply(restingTint, restingFill, restingPosition, restingScale);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressing = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable)
        {
            return;
        }

        pressing = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }

    void Apply(Color tintColor, Color fillColor, Vector2 position, Vector3 scale)
    {
        if (content != null)
        {
            content.anchoredPosition = position;
            content.localScale = scale;
        }

        if (tint != null)
        {
            tint.color = tintColor;
        }

        if (fill != null)
        {
            fill.color = fillColor;
        }
    }

    static Color Fade(Color color, float alphaScale)
    {
        color.a *= alphaScale;
        return color;
    }
}
