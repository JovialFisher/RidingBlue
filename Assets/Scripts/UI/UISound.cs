using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays the shared button feedback — a hover tick and a click — from one hidden pair of
/// sources that survives scene changes, so no scene or button needs an AudioSource of its own.
///
/// A hidden instance installs itself on play and drops <see cref="ButtonSfx"/> onto every
/// Button as each scene loads, so buttons in every menu and story scene answer the pointer
/// without being wired by hand. The clips come from the UISoundLibrary asset under Resources;
/// when it is missing, the feedback stays silent instead of throwing.
/// </summary>
public static class UISound
{
    const string LibraryPath = "UISoundLibrary";

    // Rapidly sweeping across a row of buttons should tick, not rattle.
    const float HoverRetriggerDelay = 0.05f;

    static readonly HashSet<AudioSource> uiSources = new HashSet<AudioSource>();

    static UISoundLibrary library;
    static AudioSource clickSource;
    static AudioSource hoverSource;
    static float lastHoverTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (clickSource != null)
        {
            return;
        }

        library = Resources.Load<UISoundLibrary>(LibraryPath);

        if (library == null)
        {
            Debug.LogWarning("UISound: no UISoundLibrary asset found in a Resources folder, so button sounds stay silent.");
        }

        GameObject host = new GameObject("UI Sound");
        host.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(host);

        clickSource = CreateSource(host, "Click");
        hoverSource = CreateSource(host, "Hover");

        SceneManager.sceneLoaded -= AttachToScene;
        SceneManager.sceneLoaded += AttachToScene;
    }

    /// <summary>Plays the click clip once; safe to call when no library is set up.</summary>
    public static void PlayClick()
    {
        if (clickSource == null || library == null || library.ClickClip == null)
        {
            return;
        }

        clickSource.volume = library.ClickVolume;
        clickSource.PlayOneShot(library.ClickClip);
    }

    /// <summary>Plays the hover clip, unless one just played a moment ago.</summary>
    public static void PlayHover()
    {
        if (hoverSource == null || library == null || library.HoverClip == null)
        {
            return;
        }

        if (Time.unscaledTime - lastHoverTime < HoverRetriggerDelay)
        {
            return;
        }

        lastHoverTime = Time.unscaledTime;
        hoverSource.volume = library.HoverVolume;
        hoverSource.PlayOneShot(library.HoverClip);
    }

    /// <summary>
    /// True for the UI feedback sources. SceneAudio uses this to leave them alone, so a
    /// scene's music fade cannot mute or silence the menu sounds.
    /// </summary>
    public static bool IsUISource(AudioSource source)
    {
        return source != null && uiSources.Contains(source);
    }

    static void AttachToScene(Scene scene, LoadSceneMode mode)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            // Inactive buttons are included: choice buttons often start hidden and are
            // shown later, and they need their feedback attached before that happens.
            Button[] buttons = roots[i].GetComponentsInChildren<Button>(true);

            for (int j = 0; j < buttons.Length; j++)
            {
                if (buttons[j].GetComponent<ButtonSfx>() == null)
                {
                    buttons[j].gameObject.AddComponent<ButtonSfx>();
                }
            }
        }
    }

    static AudioSource CreateSource(GameObject host, string name)
    {
        GameObject child = new GameObject(name);
        child.hideFlags = HideFlags.HideInHierarchy;
        child.transform.SetParent(host.transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;

        uiSources.Add(source);
        return source;
    }
}
