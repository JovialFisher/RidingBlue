using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Remembers exactly which dialogue line is on screen, so a manual save can bring the player
/// back to that line instead of the chapter opening.
///
/// Each story script reports a small state token (its event position and line index) whenever it
/// prints a line. Saving stores that token beside the speaker and text; loading hands the token
/// back to the target scene, which resumes its own state machine from it.
/// </summary>
public static class DialogueCheckpoint
{
    static int currentScene = -1;
    static string currentState = string.Empty;
    static string currentSpeaker = string.Empty;
    static string currentLine = string.Empty;

    static int pendingScene = -1;
    static string pendingState = string.Empty;
    static string pendingSpeaker = string.Empty;
    static string pendingLine = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneGuard()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Reset();
    }

    /// <summary>Build index of the scene that reported the current line, or -1.</summary>
    public static int SceneIndex
    {
        get { return currentScene; }
    }

    /// <summary>True when a line was reported for the scene that is open right now.</summary>
    public static bool IsCurrentScene
    {
        get { return currentScene >= 0 && currentScene == SceneManager.GetActiveScene().buildIndex; }
    }

    public static bool HasLine
    {
        get { return IsCurrentScene && !string.IsNullOrEmpty(currentLine); }
    }

    /// <summary>True when a saved line is waiting for the scene that is open now.</summary>
    public static bool HasPendingForCurrentScene
    {
        get { return pendingScene >= 0 && pendingScene == SceneManager.GetActiveScene().buildIndex; }
    }

    public static string State
    {
        get { return currentState; }
    }

    public static string Speaker
    {
        get { return currentSpeaker; }
    }

    public static string Line
    {
        get { return currentLine; }
    }

    /// <summary>
    /// Called by a scene as it starts printing a line. The state token is intentionally opaque
    /// here; only the scene that wrote it knows how to parse it again.
    /// </summary>
    public static void Report(string state, string speaker, string line)
    {
        currentScene = SceneManager.GetActiveScene().buildIndex;
        currentState = state ?? string.Empty;
        currentSpeaker = speaker ?? string.Empty;
        currentLine = line ?? string.Empty;
    }

    /// <summary>Queues a saved line for the next scene to pick up.</summary>
    public static void PrepareRestore(int sceneIndex, string state, string speaker, string line)
    {
        pendingScene = sceneIndex;
        pendingState = state ?? string.Empty;
        pendingSpeaker = speaker ?? string.Empty;
        pendingLine = line ?? string.Empty;
    }

    /// <summary>
    /// Takes the restore token if it belongs to the active scene. The token is one-shot, so a
    /// later scene load never replays an old line.
    /// </summary>
    public static bool TryTakePending(out string state, out string speaker, out string line)
    {
        state = string.Empty;
        speaker = string.Empty;
        line = string.Empty;

        if (pendingScene < 0 || pendingScene != SceneManager.GetActiveScene().buildIndex)
        {
            return false;
        }

        state = pendingState;
        speaker = pendingSpeaker;
        line = pendingLine;
        ClearPending();

        return !string.IsNullOrEmpty(state);
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A fresh scene has not printed anything yet, so the previous scene's line must not be
        // mistaken for a line that can be saved here.
        currentScene = -1;
        currentState = string.Empty;
        currentSpeaker = string.Empty;
        currentLine = string.Empty;

        // A restore token only belongs to one scene. If the game lands anywhere else, the token
        // is stale and must not surprise a later visit to the saved scene.
        if (pendingScene >= 0 && scene.buildIndex != pendingScene)
        {
            ClearPending();
        }
    }

    static void ClearPending()
    {
        pendingScene = -1;
        pendingState = string.Empty;
        pendingSpeaker = string.Empty;
        pendingLine = string.Empty;
    }

    static void Reset()
    {
        currentScene = -1;
        currentState = string.Empty;
        currentSpeaker = string.Empty;
        currentLine = string.Empty;
        ClearPending();
    }
}
