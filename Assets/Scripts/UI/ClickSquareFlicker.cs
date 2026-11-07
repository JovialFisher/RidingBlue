using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Keeps the map's click square breathing so the player can see where to click: its alpha
/// sweeps between clear and solid, and holds fully solid while the pointer is over it. A
/// player who waits gets a quicker sweep, so the square reads as the thing to press.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class ClickSquareFlicker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Seconds for one clear → solid sweep; the square then sweeps back to clear.")]
    [SerializeField, Min(0.1f)] float sweepDuration = 1f;

    [Tooltip("Seconds without the pointer over the square before the sweep gets quicker.")]
    [SerializeField, Min(0f)] float impatientDelay = 4f;

    [Tooltip("Seconds for one sweep once the player has waited long enough.")]
    [SerializeField, Min(0.1f)] float impatientSweepDuration = 0.45f;

    [Tooltip("Alpha the square drops to at the bottom of each sweep.")]
    [SerializeField, Range(0f, 1f)] float minAlpha = 0f;

    [Tooltip("Alpha the square reaches at the top of each sweep and while hovered.")]
    [SerializeField, Range(0f, 1f)] float maxAlpha = 1f;

    Graphic graphic;
    float elapsed;
    bool hovering;

    void Awake()
    {
        graphic = GetComponent<Graphic>();
    }

    void OnEnable()
    {
        // Start clear so the square reads as a pulse appearing rather than a solid block.
        elapsed = 0f;
        hovering = false;
        SetAlpha(minAlpha);
    }

    void Update()
    {
        if (graphic == null)
        {
            return;
        }

        // The pointer is already on the target: stay solid.
        if (hovering)
        {
            SetAlpha(maxAlpha);
            return;
        }

        // Unscaled time keeps the sweep going while the scene fades out.
        elapsed += Time.unscaledDeltaTime;

        float sweep = elapsed >= impatientDelay ? impatientSweepDuration : sweepDuration;
        SetAlpha(Mathf.Lerp(minAlpha, maxAlpha, Mathf.PingPong(elapsed / sweep, 1f)));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        SetAlpha(maxAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
    }

    void SetAlpha(float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
