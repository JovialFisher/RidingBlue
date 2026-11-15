using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the player's progress and manual save slots.
///
/// A manual slot stores the story scene, the exact dialogue line that was on screen, and a small
/// screenshot. Loading hands the line's state token back to the scene so it can rebuild the same
/// moment; older saves without a token still open at the scene's start.
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 3;

    /// <summary>Build index of the title screen, used when returning to the main menu.</summary>
    public const int MainMenuSceneIndex = 1;

    const string LoadStateKey = "LoadState";
    const string ProgressTimeKey = "BlueArika.Progress.Saved";
    const string ProgressStateKey = "BlueArika.Progress.State";
    const string ProgressSpeakerKey = "BlueArika.Progress.Speaker";
    const string ProgressLineKey = "BlueArika.Progress.Line";
    const string SlotSceneKey = "BlueArika.Save.Slot{0}.Scene";
    const string SlotTimeKey = "BlueArika.Save.Slot{0}.Saved";
    const string SlotStateKey = "BlueArika.Save.Slot{0}.State";
    const string SlotSpeakerKey = "BlueArika.Save.Slot{0}.Speaker";
    const string SlotLineKey = "BlueArika.Save.Slot{0}.Line";

    const string ThumbnailFolder = "BlueArika";
    const string ThumbnailFile = "slot{0}.png";

    const int FirstStorySceneIndex = 2;
    const int CreditsSceneIndex = 4;

    const string TimestampFormat = "MMM d, yyyy - h:mm tt";
    const int PreviewLength = 34;

    static readonly Texture2D[] thumbnailCache = new Texture2D[SlotCount];

    /// <summary>One save slot, filled in or empty.</summary>
    public sealed class SlotInfo
    {
        public int Index;
        public bool HasData;
        public int SceneIndex;
        public string SceneLabel;
        public DateTime SavedAtUtc;
        public string TimestampLabel;

        /// <summary>Opaque token the owning scene understands, or empty for a chapter-start save.</summary>
        public string State;

        public string Speaker;
        public string Line;

        public bool HasLine
        {
            get { return HasData && !string.IsNullOrEmpty(Line); }
        }

        /// <summary>A short "Speaker: line..." phrase for the slot row.</summary>
        public string LinePreview
        {
            get
            {
                if (!HasLine)
                {
                    return string.Empty;
                }

                string who = string.IsNullOrEmpty(Speaker) ? "Narration" : Speaker;
                return who + ": " + ShortenLine(Line);
            }
        }
    }

    /// <summary>True for scenes the player can move around in and pause inside.</summary>
    public static bool IsStoryScene(int buildIndex)
    {
        return buildIndex >= FirstStorySceneIndex
            && buildIndex < SceneManager.sceneCountInBuildSettings
            && buildIndex != CreditsSceneIndex;
    }

    /// <summary>True when the title screen has somewhere to continue from.</summary>
    public static bool HasProgress
    {
        get { return IsStoryScene(ProgressSceneIndex); }
    }

    /// <summary>The story scene the title screen would continue from, or -1.</summary>
    public static int ProgressSceneIndex
    {
        get { return PlayerPrefs.GetInt(LoadStateKey, -1); }
    }

    /// <summary>Points the rolling progress at the given story scene.</summary>
    public static void SetProgressScene(int buildIndex)
    {
        if (!IsStoryScene(buildIndex))
        {
            return;
        }

        // Opening a chapter resets the line checkpoint until the scene prints its first line.
        PlayerPrefs.DeleteKey(ProgressStateKey);
        PlayerPrefs.DeleteKey(ProgressSpeakerKey);
        PlayerPrefs.DeleteKey(ProgressLineKey);
        PlayerPrefs.SetInt(LoadStateKey, buildIndex);
        PlayerPrefs.SetString(ProgressTimeKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Copies the line that is on screen into the rolling progress. Called when the pause menu
    /// opens and when the game loses focus, so CONTINUE can resume the same line without writing
    /// PlayerPrefs on every typed line.
    /// </summary>
    public static void CaptureProgressLine()
    {
        int buildIndex = SceneManager.GetActiveScene().buildIndex;

        if (!IsStoryScene(buildIndex)
            || !DialogueCheckpoint.HasLine
            || DialogueCheckpoint.SceneIndex != buildIndex)
        {
            return;
        }

        PlayerPrefs.SetInt(LoadStateKey, buildIndex);
        StoreProgressLine(buildIndex, DialogueCheckpoint.State, DialogueCheckpoint.Speaker, DialogueCheckpoint.Line);
        PlayerPrefs.SetString(ProgressTimeKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    static void StoreProgressLine(int buildIndex, string state, string speaker, string line)
    {
        if (!IsStoryScene(buildIndex) || string.IsNullOrEmpty(state))
        {
            return;
        }

        PlayerPrefs.SetString(ProgressStateKey, state);
        PlayerPrefs.SetString(ProgressSpeakerKey, speaker ?? string.Empty);
        PlayerPrefs.SetString(ProgressLineKey, line ?? string.Empty);
    }

    /// <summary>
    /// Reads the rolling auto-save that follows the player through every story scene, so the
    /// title screen can offer "continue" beside the manual slots.
    /// </summary>
    public static SlotInfo ReadProgress()
    {
        SlotInfo info = new SlotInfo
        {
            Index = -1,
            SceneLabel = "No Progress",
            TimestampLabel = string.Empty,
            State = string.Empty,
            Speaker = string.Empty,
            Line = string.Empty
        };

        int sceneIndex = ProgressSceneIndex;
        if (!IsStoryScene(sceneIndex))
        {
            return info;
        }

        info.HasData = true;
        info.SceneIndex = sceneIndex;
        info.SceneLabel = DescribeScene(sceneIndex);
        info.State = PlayerPrefs.GetString(ProgressStateKey, string.Empty);
        info.Speaker = PlayerPrefs.GetString(ProgressSpeakerKey, string.Empty);
        info.Line = PlayerPrefs.GetString(ProgressLineKey, string.Empty);
        info.SavedAtUtc = ReadTimestamp(ProgressTimeKey);

        if (info.SavedAtUtc != default(DateTime))
        {
            info.TimestampLabel = info.SavedAtUtc.ToLocalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }

        return info;
    }

    /// <summary>Forgets the rolling progress; manual slots are left alone.</summary>
    public static void ClearProgress()
    {
        PlayerPrefs.DeleteKey(LoadStateKey);
        PlayerPrefs.DeleteKey(ProgressTimeKey);
        PlayerPrefs.DeleteKey(ProgressStateKey);
        PlayerPrefs.DeleteKey(ProgressSpeakerKey);
        PlayerPrefs.DeleteKey(ProgressLineKey);
        PlayerPrefs.Save();
    }

    /// <summary>True when at least one slot holds a save.</summary>
    public static bool HasAnySave
    {
        get
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (ReadSlot(i).HasData)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Reads a slot without changing anything.</summary>
    public static SlotInfo ReadSlot(int index)
    {
        SlotInfo info = new SlotInfo
        {
            Index = index,
            SceneLabel = "Empty Slot",
            TimestampLabel = string.Empty,
            State = string.Empty,
            Speaker = string.Empty,
            Line = string.Empty
        };

        if (index < 0 || index >= SlotCount)
        {
            return info;
        }

        int sceneIndex = PlayerPrefs.GetInt(SlotSceneKeyFor(index), -1);
        if (!IsStoryScene(sceneIndex))
        {
            return info;
        }

        info.HasData = true;
        info.SceneIndex = sceneIndex;
        info.SceneLabel = DescribeScene(sceneIndex);
        info.State = PlayerPrefs.GetString(SlotStateKeyFor(index), string.Empty);
        info.Speaker = PlayerPrefs.GetString(SlotSpeakerKeyFor(index), string.Empty);
        info.Line = PlayerPrefs.GetString(SlotLineKeyFor(index), string.Empty);

        info.SavedAtUtc = ReadTimestamp(SlotTimeKeyFor(index));
        if (info.SavedAtUtc != default(DateTime))
        {
            info.TimestampLabel = info.SavedAtUtc.ToLocalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }

        return info;
    }

    /// <summary>Saves the scene and exact line that are currently open into a slot.</summary>
    public static bool SaveCurrentSceneToSlot(int index)
    {
        int buildIndex = SceneManager.GetActiveScene().buildIndex;

        string state = string.Empty;
        string speaker = string.Empty;
        string line = string.Empty;

        if (DialogueCheckpoint.HasLine && DialogueCheckpoint.SceneIndex == buildIndex)
        {
            state = DialogueCheckpoint.State;
            speaker = DialogueCheckpoint.Speaker;
            line = DialogueCheckpoint.Line;
        }

        return SaveSceneToSlot(index, buildIndex, state, speaker, line);
    }

    /// <summary>Saves a story scene at its opening into a slot.</summary>
    public static bool SaveSceneToSlot(int index, int buildIndex)
    {
        return SaveSceneToSlot(index, buildIndex, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>Saves a story scene and an optional dialogue-line checkpoint into a slot.</summary>
    public static bool SaveSceneToSlot(int index, int buildIndex, string state, string speaker, string line)
    {
        if (index < 0 || index >= SlotCount || !IsStoryScene(buildIndex))
        {
            return false;
        }

        PlayerPrefs.SetInt(SlotSceneKeyFor(index), buildIndex);
        PlayerPrefs.SetString(SlotTimeKeyFor(index), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(SlotStateKeyFor(index), state ?? string.Empty);
        PlayerPrefs.SetString(SlotSpeakerKeyFor(index), speaker ?? string.Empty);
        PlayerPrefs.SetString(SlotLineKeyFor(index), line ?? string.Empty);
        SetProgressScene(buildIndex);
        StoreProgressLine(buildIndex, state, speaker, line);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>
    /// Queues the slot's exact line for the scene it names. Call this just before loading the
    /// scene; the scene takes the token when it starts.
    /// </summary>
    public static void PrepareRestore(SlotInfo info)
    {
        if (info == null || !info.HasData || string.IsNullOrEmpty(info.State))
        {
            return;
        }

        DialogueCheckpoint.PrepareRestore(info.SceneIndex, info.State, info.Speaker, info.Line);
    }

    /// <summary>Empties a slot and removes its thumbnail.</summary>
    public static void ClearSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
        {
            return;
        }

        PlayerPrefs.DeleteKey(SlotSceneKeyFor(index));
        PlayerPrefs.DeleteKey(SlotTimeKeyFor(index));
        PlayerPrefs.DeleteKey(SlotStateKeyFor(index));
        PlayerPrefs.DeleteKey(SlotSpeakerKeyFor(index));
        PlayerPrefs.DeleteKey(SlotLineKeyFor(index));
        PlayerPrefs.Save();

        ClearThumbnail(index);
    }

    /// <summary>True when a slot has a screenshot on disk.</summary>
    public static bool HasThumbnail(int index)
    {
        return index >= 0 && index < SlotCount && File.Exists(ThumbnailPathFor(index));
    }

    /// <summary>Loads a slot's screenshot, caching the texture for the life of the run.</summary>
    public static Texture2D GetThumbnail(int index)
    {
        if (index < 0 || index >= SlotCount)
        {
            return null;
        }

        if (thumbnailCache[index] != null)
        {
            return thumbnailCache[index];
        }

        string path = ThumbnailPathFor(index);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(data))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.name = "Save Slot " + (index + 1);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            thumbnailCache[index] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not read save thumbnail " + (index + 1) + ": " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Writes a slot screenshot and takes ownership of the texture. The caller must not destroy
    /// it afterwards, because the cached texture is the one the load list will draw.
    /// </summary>
    public static void SaveThumbnail(int index, Texture2D texture)
    {
        if (index < 0 || index >= SlotCount || texture == null)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            return;
        }

        try
        {
            string folder = Path.Combine(Application.persistentDataPath, ThumbnailFolder);
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(ThumbnailPathFor(index), texture.EncodeToPNG());
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not save slot thumbnail " + (index + 1) + ": " + exception.Message);
        }

        if (thumbnailCache[index] != null && thumbnailCache[index] != texture)
        {
            UnityEngine.Object.Destroy(thumbnailCache[index]);
        }

        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        thumbnailCache[index] = texture;
    }

    /// <summary>A readable name for a scene, built from its asset name.</summary>
    public static string DescribeScene(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return "Unknown Scene";
        }

        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        string sceneName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileNameWithoutExtension(path);

        return string.IsNullOrEmpty(sceneName) ? "Unknown Scene" : PrettifySceneName(sceneName);
    }

    public static void ClearThumbnail(int index)
    {
        if (thumbnailCache[index] != null)
        {
            UnityEngine.Object.Destroy(thumbnailCache[index]);
            thumbnailCache[index] = null;
        }

        try
        {
            string path = ThumbnailPathFor(index);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not delete slot thumbnail " + (index + 1) + ": " + exception.Message);
        }
    }

    static string ThumbnailPathFor(int index)
    {
        string file = string.Format(CultureInfo.InvariantCulture, ThumbnailFile, index);
        return Path.Combine(Path.Combine(Application.persistentDataPath, ThumbnailFolder), file);
    }

    static DateTime ReadTimestamp(string key)
    {
        long ticks;
        string storedTime = PlayerPrefs.GetString(key, string.Empty);

        if (long.TryParse(storedTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
            && ticks > 0
            && ticks <= DateTime.MaxValue.Ticks)
        {
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        return default(DateTime);
    }

    static string SlotSceneKeyFor(int index)
    {
        return string.Format(CultureInfo.InvariantCulture, SlotSceneKey, index);
    }

    static string SlotTimeKeyFor(int index)
    {
        return string.Format(CultureInfo.InvariantCulture, SlotTimeKey, index);
    }

    static string SlotStateKeyFor(int index)
    {
        return string.Format(CultureInfo.InvariantCulture, SlotStateKey, index);
    }

    static string SlotSpeakerKeyFor(int index)
    {
        return string.Format(CultureInfo.InvariantCulture, SlotSpeakerKey, index);
    }

    static string SlotLineKeyFor(int index)
    {
        return string.Format(CultureInfo.InvariantCulture, SlotLineKey, index);
    }

    static string ShortenLine(string line)
    {
        string cleaned = line.Replace("\r", " ").Replace("\n", " ").Trim();

        if (cleaned.Length <= PreviewLength)
        {
            return cleaned;
        }

        return cleaned.Substring(0, PreviewLength - 3) + "...";
    }

    /// <summary>
    /// Turns "KodaCall01-Late" into "Koda Call 01 - Late" so a slot reads like a chapter name
    /// instead of an asset file.
    /// </summary>
    static string PrettifySceneName(string raw)
    {
        StringBuilder builder = new StringBuilder(raw.Length + 8);

        for (int i = 0; i < raw.Length; i++)
        {
            char current = raw[i];

            if (current == '-' || current == '_')
            {
                if (builder.Length > 0 && builder[builder.Length - 1] == ' ')
                {
                    builder.Length -= 1;
                }

                builder.Append(" - ");
                continue;
            }

            if (current != ' ' && i > 0)
            {
                char previous = raw[i - 1];

                bool newWord = char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous));
                bool afterDigit = char.IsLetter(current) && char.IsDigit(previous);
                bool beforeDigit = char.IsDigit(current) && char.IsLetter(previous);

                if (newWord || afterDigit || beforeDigit)
                {
                    if (builder.Length > 0
                        && builder[builder.Length - 1] != ' '
                        && builder[builder.Length - 1] != '-')
                    {
                        builder.Append(' ');
                    }
                }
            }

            builder.Append(current);
        }

        return builder.ToString().Trim();
    }
}
