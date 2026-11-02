using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gives a button the shared hover and click feedback. UISound adds this to every Button as
/// its scene loads, so scenes and prefabs do not need to be wired by hand; a button that is
/// not interactable stays quiet. The next button keeps the click only, because it sits under
/// the cursor for most of a scene and its hover tick is just noise there.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Button button;
    int exitedFrame = -1;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            return;
        }

        // Subscribed here rather than in Awake so a button that starts inactive is hooked
        // when it is first shown, and re-subscribed only once if it is toggled afterwards.
        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }

        exitedFrame = -1;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Labels and icons are separate graphics inside the button, so sliding onto one
        // reports an exit and an enter in the same frame. That is still the same hover.
        if (Time.frameCount == exitedFrame)
        {
            return;
        }

        if (button != null && !button.interactable)
        {
            return;
        }

        // The next button is under the cursor almost the whole time, so its hover tick reads
        // as noise rather than feedback. It is the only button carrying NextButtonFadeIn, so
        // that is what marks it as click only.
        if (button != null && button.GetComponent<NextButtonFadeIn>() != null)
        {
            return;
        }

        UISound.PlayHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        exitedFrame = Time.frameCount;
    }

    void HandleClick()
    {
        UISound.PlayClick();
    }
}
