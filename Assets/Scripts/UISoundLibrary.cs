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

    [SerializeField, Range(0f, 1f)] float clickVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.5f;

    public AudioClip ClickClip
    {
        get { return clickClip; }
    }

    public AudioClip HoverClip
    {
        get { return hoverClip; }
    }

    public float ClickVolume
    {
        get { return clickVolume; }
    }

    public float HoverVolume
    {
        get { return hoverVolume; }
    }
}
