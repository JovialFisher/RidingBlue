using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps scene audio from popping in or cutting off: whatever is already playing when a
/// scene opens eases up from silence, and everything still playing is eased down before
/// the next scene is allowed to activate.
///
/// A hidden instance installs itself on play, so no scene or script needs to be wired up
/// and audio sources that no script references (play-on-awake ambience) are covered too.
/// </summary>
public class SceneAudio : MonoBehaviour
{
    /// <summary>How long a scene's audio takes to come up.</summary>
    public const float DefaultFadeInDuration = 2f;

    /// <summary>How long audio takes to come down before a scene change.</summary>
    public const float DefaultFadeOutDuration = 3f;

    // Audio that starts this soon after a scene loads fades in with the scene.
    const float OpeningWindow = 0.35f;

    // Ambience started later (still within this window) is faded in; one-shot stings are not.
    const float RegistrationWindow = 2f;

    static SceneAudio instance;

    readonly List<AudioSource> fadingIn = new List<AudioSource>();
    readonly List<float> fadeInTargets = new List<float>();
    readonly List<float> fadeInElapsed = new List<float>();

    Coroutine fadeInRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("Scene Audio");
        host.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SceneAudio>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Anything left over belongs to the scene that was just unloaded.
        StopAllCoroutines();
        ResetFadeInState();

        fadeInRoutine = StartCoroutine(FadeSceneIn());
    }

    /// <summary>
    /// Eases every audible source in the current scene down to silence and stops it.
    /// Returns how long the caller should wait for that fade (0 when nothing was playing).
    /// </summary>
    /// <param name="availableTime">
    /// Time the caller already gives the transition; the fade is shortened to fit it so a
    /// scene change never takes longer than it used to.
    /// </param>
    public static float FadeOutForSceneChange(float availableTime = 0f)
    {
        if (instance == null)
        {
            return 0f;
        }

        float duration = availableTime > 0f
            ? Mathf.Min(availableTime, DefaultFadeOutDuration)
            : Mathf.Min(1.5f, DefaultFadeOutDuration);

        instance.StopFadeIn();

        bool fadedAnything = false;
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || !source.isPlaying || source.volume <= 0.0001f)
            {
                continue;
            }

            fadedAnything = true;
            instance.StartCoroutine(instance.FadeOutSource(source, duration));
        }

        return fadedAnything ? duration : 0f;
    }

    /// <summary>Eases one source down to silence and stops it. Safe on a stopped source.</summary>
    public static void FadeOut(AudioSource source, float duration = DefaultFadeOutDuration)
    {
        if (source == null || instance == null || !source.isPlaying)
        {
            return;
        }

        instance.StartCoroutine(instance.FadeOutSource(source, duration));
    }

    IEnumerator FadeSceneIn()
    {
        float clock = 0f;
        float endTime = RegistrationWindow + DefaultFadeInDuration + 0.25f;

        while (clock < endTime)
        {
            clock += Time.unscaledDeltaTime;

            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || !source.isPlaying || fadingIn.Contains(source))
                {
                    continue;
                }

                // Already silent: nothing to fade, and a script may be raising it itself.
                if (source.volume <= 0f)
                {
                    continue;
                }

                // A sting that fires later in the scene should keep its punch; only audio
                // that opens the scene, or ambience that loops, gets eased in.
                if (clock > OpeningWindow && !source.loop)
                {
                    continue;
                }

                fadingIn.Add(source);
                fadeInTargets.Add(source.volume);
                fadeInElapsed.Add(0f);

                // Take over from silence so it never starts at its authored volume first.
                source.volume = 0f;
            }

            for (int i = fadingIn.Count - 1; i >= 0; i--)
            {
                AudioSource source = fadingIn[i];
                if (source == null)
                {
                    RemoveAt(i);
                    continue;
                }

                if (!source.isPlaying)
                {
                    // Give the clip back its own volume so a replay later is not stuck quiet.
                    source.volume = fadeInTargets[i];
                    RemoveAt(i);
                    continue;
                }

                float elapsed = fadeInElapsed[i] + Time.unscaledDeltaTime;
                fadeInElapsed[i] = elapsed;
                source.volume = fadeInTargets[i] * Mathf.Clamp01(elapsed / DefaultFadeInDuration);
            }

            yield return null;
        }

        for (int i = 0; i < fadingIn.Count; i++)
        {
            if (fadingIn[i] != null && fadingIn[i].isPlaying)
            {
                fadingIn[i].volume = fadeInTargets[i];
            }
        }

        ResetFadeInState();
        fadeInRoutine = null;
    }

    IEnumerator FadeOutSource(AudioSource source, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        float startVolume = source.volume;

        if (duration <= 0f)
        {
            source.volume = 0f;
            source.Stop();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (source == null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        if (source == null)
        {
            yield break;
        }

        source.volume = 0f;
        source.Stop();
    }

    void StopFadeIn()
    {
        if (fadeInRoutine != null)
        {
            StopCoroutine(fadeInRoutine);
            fadeInRoutine = null;
        }

        ResetFadeInState();
    }

    void ResetFadeInState()
    {
        fadingIn.Clear();
        fadeInTargets.Clear();
        fadeInElapsed.Clear();
    }

    void RemoveAt(int index)
    {
        fadingIn.RemoveAt(index);
        fadeInTargets.RemoveAt(index);
        fadeInElapsed.RemoveAt(index);
    }
}
