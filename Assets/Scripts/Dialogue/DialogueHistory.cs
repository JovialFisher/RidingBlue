using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps every line that has already been shown in the scene that is open right now, so the
/// dialogue box's log can bring them back without a single scene storing them itself.
///
/// The story scripts already report each line to <see cref="DialogueCheckpoint"/> when it is
/// printed, and that report is what fills this list. The list is cleared whenever a new scene
/// loads, because the log is meant to answer "what did they just say?" for the scene in front of
/// the player, not for the whole game.
/// </summary>
public static class DialogueHistory
{
    public struct Entry
    {
        public string State;
        public string Speaker;
        public string Text;
    }

    static readonly List<Entry> entries = new List<Entry>();

    /// <summary>How many lines this scene has shown so far.</summary>
    public static int Count
    {
        get { return entries.Count; }
    }

    /// <summary>The entries are in the order they appeared on screen.</summary>
    public static Entry Get(int index)
    {
        return entries[index];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneGuard()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        entries.Clear();
    }

    /// <summary>
    /// Remembers a printed line. A line that is printed again with the same state token — which is
    /// what a restored checkpoint does — replaces the previous entry instead of repeating it.
    /// </summary>
    public static void Record(string state, string speaker, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Entry entry = new Entry
        {
            State = state ?? string.Empty,
            Speaker = speaker ?? string.Empty,
            Text = text
        };

        int last = entries.Count - 1;

        if (last >= 0 && !string.IsNullOrEmpty(entry.State) && entries[last].State == entry.State)
        {
            entries[last] = entry;
            return;
        }

        entries.Add(entry);
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        entries.Clear();
    }
}
