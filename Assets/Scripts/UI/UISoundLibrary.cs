using UnityEngine;

/// <summary>
/// The clips and levels behind the shared button feedback. It lives in a Resources folder so
/// UISound can find it from any scene without wiring, and keeping the clips here means they
/// can be swapped or re-levelled in one place instead of scene by scene.
/// </summary>
[CreateAssetMenu(menuName = "Blue Arika/UI Sound Library", fileName = "UISoundLibrary")]
public class UISoundLibrary : ScriptableObject
{
    [Tooltip("Played when a button is clicked.")]
    [SerializeField] AudioClip clickClip;

    [Tooltip("Played when the pointer moves onto a button.")]
    [SerializeField] AudioClip hoverClip;

    [Tooltip("Played when the pause menu appears.")]
    [SerializeField] AudioClip popInClip;

    [Tooltip("Played when a prompt asks the player to confirm something.")]
    [SerializeField] AudioClip confirmClip;

    [SerializeField, Range(0f, 1f)] float clickVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] float popInVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] float confirmVolume = 0.9f;

    public AudioClip ClickClip
    {
        get { return clickClip; }
    }

    public AudioClip HoverClip
    {
        get { return hoverClip; }
    }

    public AudioClip PopInClip
    {
        get { return popInClip; }
    }

    public AudioClip ConfirmClip
    {
        get { return confirmClip; }
    }

    public float ClickVolume
    {
        get { return clickVolume; }
    }

    public float HoverVolume
    {
        get { return hoverVolume; }
    }

    public float PopInVolume
    {
        get { return popInVolume; }
    }

    public float ConfirmVolume
    {
        get { return confirmVolume; }
    }
}
