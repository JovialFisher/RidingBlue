using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a button answer the pointer: on hover the label slides right and takes the accent
/// tint, pulls a little further and dips while the press is held, and eases back to its
/// authored look afterwards. A button that cannot be pressed keeps its dimmed colour instead
/// of reacting. Motion runs on unscaled time so it still responds while a scene transition is
/// fading out.
///
/// Framed buttons, like the Next plate, use whole-button mode instead: the button itself
/// grows and dips a little so its fill and rim move with the label, and there is no slide.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Tooltip("Graphic that slides and tints. Left empty, the label under the button is used.")]
    [SerializeField] Graphic label;

    [Tooltip("Scale and nudge the button itself instead of the label inside it, for buttons drawn as a framed plate.")]
    [SerializeField] bool wholeButton;

    [Tooltip("How far, in UI units, the label slides right under the pointer.")]
    [SerializeField, Min(0f)] float hoverOffset = 18f;

    [Tooltip("How far the label slides right while the click is held.")]
    [SerializeField, Min(0f)] float pressedOffset = 28f;

    [Tooltip("Label scale while hovered.")]
    [SerializeField, Min(0f)] float hoverScale = 1.04f;

    [Tooltip("Label scale while pressed, so the click reads as a small squash.")]
    [SerializeField, Min(0f)] float pressedScale = 0.97f;

    [SerializeField] Color hoverColor = new Color(0.66f, 0.8f, 1f, 1f);
    [SerializeField] Color pressedColor = Color.white;

    [Tooltip("Alpha kept by the label while the button is not interactable.")]
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.4f;

    [Tooltip("Smaller values snap to the new look faster.")]
    [SerializeField, Min(0.01f)] float responseTime = 0.09f;

    Button button;
    RectTransform motionRect;
    Graphic tintGraphic;
    Vector2 restingPosition;
    Vector3 restingScale;
    Color restingColor;
    Color disabledColor;
    bool hovering;
    bool pressing;

    bool IsUsable
    {
        get { return button == null || button.interactable; }
    }

    void Awake()
    {
        button = GetComponent<Button>();

        if (label == null)
        {
            label = FindLabel();
        }

        tintGraphic = label;
        motionRect = wholeButton ? (RectTransform)transform : (label != null ? label.rectTransform : null);

        if (motionRect != null)
        {
            restingPosition = motionRect.anchoredPosition;
            restingScale = motionRect.localScale;
        }

        if (tintGraphic != null)
        {
            restingColor = tintGraphic.color;
            disabledColor = restingColor;
            disabledColor.a *= disabledAlpha;
        }
    }

    void OnDisable()
    {
        hovering = false;
        pressing = false;

        if (motionRect != null)
        {
            motionRect.anchoredPosition = restingPosition;
            motionRect.localScale = restingScale;
        }

        if (tintGraphic != null)
        {
            tintGraphic.color = restingColor;
        }
    }

    void Update()
    {
        if (motionRect == null && tintGraphic == null)
        {
            return;
        }

        bool usable = IsUsable;

        Vector2 targetPosition = restingPosition;
        Vector3 targetScale = restingScale;
        Color targetColor = usable ? restingColor : disabledColor;

        if (usable && hovering)
        {
            targetPosition = restingPosition + Vector2.right * (pressing ? pressedOffset : hoverOffset);
            targetScale = restingScale * (pressing ? pressedScale : hoverScale);
            targetColor = pressing ? pressedColor : hoverColor;
        }

        // Frame-rate independent easing: the button keeps moving after the pointer stops,
        // so the response feels smooth rather than stepped.
        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / responseTime);

        if (motionRect != null)
        {
            motionRect.anchoredPosition = Vector2.Lerp(motionRect.anchoredPosition, targetPosition, blend);
            motionRect.localScale = Vector3.Lerp(motionRect.localScale, targetScale, blend);
        }

        if (tintGraphic != null)
        {
            tintGraphic.color = Color.Lerp(tintGraphic.color, targetColor, blend);
        }
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
        if (!IsUsable)
        {
            return;
        }

        pressing = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }

    /// <summary>
    /// Picks the visible label under the button. The button's own graphic is a transparent
    /// hit area, so it is skipped and the first text child is used instead.
    /// </summary>
    Graphic FindLabel()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        Graphic target = button != null ? button.targetGraphic : null;
        Graphic fallback = null;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic == target)
            {
                continue;
            }

            if (graphic is TMPro.TMP_Text || graphic is Text)
            {
                return graphic;
            }

            if (fallback == null)
            {
                fallback = graphic;
            }
        }

        return fallback;
    }
}
