using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds two controls to the corner of the dialogue box without touching any scene asset: a LOG
/// button that re-opens the lines this scene has already shown, and a FAST button that runs the
/// rest of the scene at speed for players who have already read it.
///
/// A hidden bootstrap finds the scene's text box as it loads and installs this component on it, so
/// every story scene gains the same pair of buttons. The log reads
/// <see cref="DialogueHistory"/>, and the fast forward presses the scene's own Next button, which
/// keeps each scene in charge of its own dialogue flow.
/// </summary>
[DisallowMultipleComponent]
public class DialogueControls : MonoBehaviour
{
    const float ButtonHeight = 42f;
    const float LogButtonWidth = 134f;
    const float FastButtonWidth = 158f;
    const float ButtonGap = 10f;
    const float TopInset = 10f;
    const float CornerInset = 22f;
    const float LabelSize = 15f;

    const float CardWidth = 1260f;
    const float CardHeight = 680f;
    const float CardMargin = 44f;
    const float EntryIndent = 18f;

    /// <summary>Real seconds between lines while the fast forward runs.</summary>
    const float FastForwardInterval = 0.2f;

    /// <summary>
    /// The scene's own clock is wound up while the fast forward runs, so the fixed waits a scene
    /// places between its lines — the pause before the next plate, the beat after typing — shrink
    /// with everything else instead of holding the skip back.
    /// </summary>
    const float FastForwardTimeScale = 4f;

    /// <summary>Typesetting multiplier while the fast forward runs, on top of the faster clock.</summary>
    const float FastRevealSpeed = 6f;

    /// <summary>How far the scene's audio drops while the log is open.</summary>
    const float LogDuckLevel = 0.55f;

    /// <summary>How long the drop and its return take.</summary>
    const float LogDuckFade = 0.18f;

    const int StreakCount = 16;

    static readonly Color Accent = new Color(0.66f, 0.8f, 1f, 1f);
    static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.96f);

    static DialogueControls active;

    /// <summary>
    /// True while a log is covering a dialogue box; the pause menu steps aside for it. The frame
    /// the log closes still counts, so the Escape that closed it cannot also open the menu.
    /// </summary>
    public static bool LogOpen
    {
        get { return active != null && (active.logOpen || active.logClosedFrame == Time.frameCount); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        active = null;
        TextCreator.revealSpeed = 1f;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        active = null;
        TextCreator.revealSpeed = 1f;

        // The box is usually inactive until the scene is ready to talk, so the search has to
        // include inactive objects; the fader is what marks the panel the controls belong to.
        TextBoxFader[] boxes = Object.FindObjectsByType<TextBoxFader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (boxes.Length == 0 || boxes[0] == null)
        {
            return;
        }

        if (boxes[0].GetComponent<DialogueControls>() == null)
        {
            boxes[0].gameObject.AddComponent<DialogueControls>();
        }
    }

    sealed class Streak
    {
        public RectTransform Rect;
        public Image Image;
        public float X;
        public float Y;
        public float Width;
        public float Speed;
    }

    TMP_FontAsset font;

    Button logButton;
    Button fastButton;
    Image fastGlow;
    readonly RectTransform[] chevrons = new RectTransform[2];
    readonly Vector2[] chevronHome = new Vector2[2];
    readonly Graphic[] chevronGraphics = new Graphic[2];

    GameObject logRoot;
    CanvasGroup logGroup;
    RectTransform logCard;
    ScrollRect logScroll;
    RectTransform logViewport;
    RectTransform logContent;
    TMP_Text logSubtitle;
    int logBuiltCount = -1;
    bool logOpen;
    int logClosedFrame = -1;
    Coroutine logRoutine;

    GameObject windRoot;
    CanvasGroup windGroup;
    Image wash;
    readonly Streak[] streaks = new Streak[StreakCount];
    float windAlpha;
    bool fastForwarding;
    float restingTimeScale = 1f;
    float pressTimer;
    Button nextButton;

    float listenerVolume = 1f;
    bool ducked;
    Coroutine duckRoutine;

    bool built;

    void Awake()
    {
        active = this;
        EnsureBuilt();
    }

    void OnEnable()
    {
        active = this;
    }

    void OnDisable()
    {
        if (active == this)
        {
            active = null;
        }

        // A box that is being put away must not leave the typesetter wound up, the clock racing or
        // the scene's audio dimmed for the next scene.
        if (fastForwarding)
        {
            ApplyTimeScale(false);
        }

        fastForwarding = false;
        TextCreator.revealSpeed = 1f;
        windAlpha = 0f;

        RestoreListenerVolume();
        logOpen = false;

        if (windRoot != null)
        {
            windRoot.SetActive(false);
        }

        if (logRoot != null && logGroup != null)
        {
            logGroup.alpha = 0f;
            logRoot.SetActive(false);
        }
    }

    void Update()
    {
        if (!built)
        {
            return;
        }

        if (logOpen && EscapePressed())
        {
            SetLogOpen(false);
            return;
        }

        UpdateFastForward();
        UpdateWind();

        // The scene keeps typing behind the log, so the list stays current if a line lands.
        if (logOpen && DialogueHistory.Count != logBuiltCount)
        {
            RefreshLog(false);
        }
    }

    static bool EscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    // ---------------------------------------------------------------- fast forward

    void UpdateFastForward()
    {
        if (!fastForwarding || logOpen)
        {
            return;
        }

        // A stopped clock means the pause menu is up; nothing should move behind it.
        if (Time.timeScale <= 0.01f)
        {
            return;
        }

        // Measured on real time, so the faster scene clock does not turn the pacing into a blur.
        pressTimer -= Time.unscaledDeltaTime;

        if (pressTimer > 0f || !CanAdvance())
        {
            return;
        }

        pressTimer = FastForwardInterval;
        nextButton.onClick.Invoke();
    }

    /// <summary>
    /// A line is only taken when the scene itself is offering the next one. That keeps the fast
    /// forward out of scene openings, fades and endings, which the box cannot tell apart anyway.
    /// </summary>
    bool CanAdvance()
    {
        if (nextButton == null)
        {
            ResolveNextButton();
        }

        return nextButton != null && nextButton.gameObject.activeInHierarchy && nextButton.interactable;
    }

    void ResolveNextButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform root = canvas != null ? canvas.transform : transform.root;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (!string.Equals(all[i].name, "NextButton", System.StringComparison.Ordinal))
            {
                continue;
            }

            Button button = all[i].GetComponent<Button>();
            if (button != null)
            {
                nextButton = button;
                return;
            }
        }
    }

    void SetFastForward(bool running)
    {
        if (fastForwarding == running)
        {
            return;
        }

        fastForwarding = running;
        pressTimer = 0f;
        TextCreator.revealSpeed = running ? FastRevealSpeed : 1f;
        ApplyTimeScale(running);

        if (running)
        {
            return;
        }

        // The button goes dark and the chevrons rest again the moment the fast forward lets go;
        // UpdateWind keeps them in that state even if a frame was missed here.
        UpdateFastGlow(false);

        for (int i = 0; i < chevrons.Length; i++)
        {
            if (chevrons[i] == null)
            {
                continue;
            }

            chevrons[i].anchoredPosition = chevronHome[i];

            Color color = Accent;
            color.a = 0.62f;
            chevronGraphics[i].color = color;
        }
    }

    void ApplyTimeScale(bool fast)
    {
        if (fast)
        {
            restingTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = FastForwardTimeScale;
            return;
        }

        // Only hand the clock back if it is still running; a paused game keeps its own time.
        if (Time.timeScale > 0f)
        {
            Time.timeScale = restingTimeScale;
        }
    }

    /// <summary>
    /// The pill's glow belongs to the effect, not to the click: it only carries light while the
    /// fast forward is actually running, and is cleared outright whenever it is not.
    /// </summary>
    void UpdateFastGlow(bool running)
    {
        if (fastGlow == null)
        {
            return;
        }

        Color glow = Accent;

        if (running)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            glow.a = Mathf.Lerp(0.2f, 0.45f, pulse);
        }
        else
        {
            glow.a = 0f;
        }

        fastGlow.color = glow;
    }

    void UpdateWind()
    {
        bool wanted = fastForwarding && !logOpen;

        windAlpha = Mathf.MoveTowards(windAlpha, wanted ? 1f : 0f, Time.unscaledDeltaTime / 0.16f);

        // Driven every frame rather than only while the streaks are visible, so the plate can
        // never be left glowing behind a fast forward that has already stopped.
        UpdateFastGlow(wanted);

        bool visible = windAlpha > 0.001f;

        if (windRoot.activeSelf != visible)
        {
            windRoot.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        windGroup.alpha = windAlpha;

        RectTransform panel = (RectTransform)transform;
        float halfWidth = panel.rect.width * 0.5f;
        float halfHeight = panel.rect.height * 0.42f;

        if (halfWidth < 200f)
        {
            halfWidth = 960f;
        }

        if (halfHeight < 40f)
        {
            halfHeight = 130f;
        }

        float delta = Time.deltaTime;

        for (int i = 0; i < streaks.Length; i++)
        {
            Streak streak = streaks[i];

            streak.X -= streak.Speed * delta;

            if (streak.X + streak.Width < -halfWidth - 120f)
            {
                PlaceStreak(streak, halfWidth, halfHeight, true);
                continue;
            }

            streak.Rect.anchoredPosition = new Vector2(streak.X, streak.Y);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);

        Color washColor = wash.color;
        washColor.a = Mathf.Lerp(0.03f, 0.085f, pulse);
        wash.color = washColor;

        // The two chevrons run after each other, which is what makes the button read as rolling.
        for (int i = 0; i < chevrons.Length; i++)
        {
            if (chevrons[i] == null)
            {
                continue;
            }

            float phase = Mathf.Repeat(Time.unscaledTime * 2.2f + i * 0.45f, 1f);

            chevrons[i].anchoredPosition = chevronHome[i] + Vector2.right * (phase * 7f);

            Color color = Accent;
            color.a = Mathf.Lerp(0.3f, 1f, Mathf.Sin(phase * Mathf.PI));
            chevronGraphics[i].color = color;
        }
    }

    // ---------------------------------------------------------------- the log

    void SetLogOpen(bool open)
    {
        if (logOpen == open)
        {
            return;
        }

        logOpen = open;

        if (open)
        {
            // Reading takes priority: the scene stops racing and its sound sits back a little.
            SetFastForward(false);

            // Shown before the list is built so the card's own rect is in place for measuring.
            logRoot.SetActive(true);
            RefreshLog(true);
        }
        else
        {
            logClosedFrame = Time.frameCount;
        }

        DuckForLog(open);

        if (logRoutine != null)
        {
            StopCoroutine(logRoutine);
        }

        logRoutine = StartCoroutine(AnimateLog(open));
    }

    /// <summary>
    /// Sits the whole game's audio back while the log covers the screen, so the lines can be read
    /// without the music talking over them, and brings it straight back when the log closes.
    /// </summary>
    void DuckForLog(bool duckedNow)
    {
        if (duckedNow == ducked)
        {
            return;
        }

        if (duckedNow)
        {
            // Whatever the player's own volume setting is, it is what this returns to.
            listenerVolume = AudioListener.volume;
            ducked = true;
        }
        else
        {
            ducked = false;
        }

        float target = duckedNow ? listenerVolume * LogDuckLevel : listenerVolume;

        if (duckRoutine != null)
        {
            StopCoroutine(duckRoutine);
            duckRoutine = null;
        }

        if (!gameObject.activeInHierarchy)
        {
            AudioListener.volume = target;
            return;
        }

        duckRoutine = StartCoroutine(RampListenerVolume(target));
    }

    IEnumerator RampListenerVolume(float target)
    {
        float from = AudioListener.volume;
        float elapsed = 0f;

        while (elapsed < LogDuckFade)
        {
            elapsed += Time.unscaledDeltaTime;
            AudioListener.volume = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / LogDuckFade));
            yield return null;
        }

        AudioListener.volume = target;
        duckRoutine = null;
    }

    void RestoreListenerVolume()
    {
        if (!ducked)
        {
            return;
        }

        ducked = false;

        if (duckRoutine != null)
        {
            StopCoroutine(duckRoutine);
            duckRoutine = null;
        }

        AudioListener.volume = listenerVolume;
    }

    IEnumerator AnimateLog(bool open)
    {
        logRoot.SetActive(true);
        logGroup.blocksRaycasts = open;
        logGroup.interactable = open;

        float from = logGroup.alpha;
        float to = open ? 1f : 0f;
        float fromScale = open ? 0.975f : 1f;
        float toScale = open ? 1f : 0.975f;
        float elapsed = 0f;

        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));

            logGroup.alpha = Mathf.Lerp(from, to, progress);
            logCard.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, progress);
            yield return null;
        }

        logGroup.alpha = to;
        logCard.localScale = Vector3.one;
        logRoutine = null;

        if (!open)
        {
            logRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Rebuilds the list from the scene's history. The card is only scrolled to the newest line
    /// when it was already at the bottom, so a line arriving behind an open log does not yank a
    /// player away from the part they were reading.
    /// </summary>
    void RefreshLog(bool pinToBottom)
    {
        int count = DialogueHistory.Count;
        logBuiltCount = count;

        logSubtitle.text = count == 1
            ? "1 LINE SEEN IN THIS SCENE"
            : count + " LINES SEEN IN THIS SCENE";

        bool wasAtBottom = pinToBottom || logScroll.verticalNormalizedPosition <= 0.01f;

        BuildLogEntries(count);

        Canvas.ForceUpdateCanvases();

        if (wasAtBottom)
        {
            logScroll.verticalNormalizedPosition = 0f;
        }
    }

    void BuildLogEntries(int count)
    {
        for (int i = logContent.childCount - 1; i >= 0; i--)
        {
            GameObject old = logContent.GetChild(i).gameObject;
            old.SetActive(false);
            Destroy(old);
        }

        float textWidth = CardWidth - CardMargin * 2f - EntryIndent - 10f;
        float y = 0f;

        if (count == 0)
        {
            TMP_Text empty = CreateText("Empty", logContent, "Nothing has been said here yet.", 20f,
                TextAlignmentOptions.TopLeft, new Color(1f, 1f, 1f, 0.55f));

            PlaceTop(empty.rectTransform, 0f, 10f, 6f, 28f);
            y = 48f;
        }

        for (int i = 0; i < count; i++)
        {
            DialogueHistory.Entry entry = DialogueHistory.Get(i);
            bool newest = i == count - 1;
            bool hasSpeaker = !string.IsNullOrEmpty(entry.Speaker);
            float speakerHeight = hasSpeaker ? 24f : 0f;

            GameObject entryObject = new GameObject("Line " + (i + 1), typeof(RectTransform));
            entryObject.transform.SetParent(logContent, false);

            RectTransform entryRect = (RectTransform)entryObject.transform;

            TMP_Text body = CreateText("Text", entryRect, entry.Text, 19f,
                TextAlignmentOptions.TopLeft,
                new Color(1f, 1f, 1f, newest ? 0.97f : 0.86f));

            body.lineSpacing = 6f;

            // The wrapped height of the line is what sizes its entry, so it is measured before
            // anything is placed. That keeps the spacing exact without a layout group.
            float bodyHeight = body.GetPreferredValues(entry.Text, textWidth, 0f).y;
            float entryHeight = speakerHeight + bodyHeight + 18f;

            PlaceTop(entryRect, 0f, 0f, y, entryHeight);

            // A short accent bar marks where each line starts, the way a menu's bracket would.
            Image bar = CreateImage("Bar", entryRect, null,
                new Color(Accent.r, Accent.g, Accent.b, newest ? 0.9f : 0.28f),
                Image.Type.Simple, false);
            SetAnchored(bar.rectTransform, new Vector2(0f, 1f), Vector2.zero,
                new Vector2(3f, Mathf.Max(18f, entryHeight - 18f)));

            if (hasSpeaker)
            {
                TMP_Text speaker = CreateText("Speaker", entryRect, entry.Speaker.ToUpperInvariant(), 15f,
                    TextAlignmentOptions.MidlineLeft,
                    new Color(Accent.r, Accent.g, Accent.b, newest ? 1f : 0.72f));

                speaker.characterSpacing = 3f;
                speaker.fontStyle = FontStyles.Bold;
                PlaceTop(speaker.rectTransform, EntryIndent, 10f, 0f, speakerHeight);
            }

            PlaceTop(body.rectTransform, EntryIndent, 10f, speakerHeight, bodyHeight);

            Image separator = CreateImage("Separator", entryRect, null,
                new Color(Accent.r, Accent.g, Accent.b, 0.12f), Image.Type.Simple, false);
            PlaceTop(separator.rectTransform, 0f, 0f, entryHeight - 8f, 1f);

            y += entryHeight;
        }

        logContent.sizeDelta = new Vector2(0f, Mathf.Max(y + 8f, logViewport.rect.height));
        logContent.anchoredPosition = Vector2.zero;
    }

    // ---------------------------------------------------------------- building

    void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        built = true;
        font = ResolveFont();

        // Draw order is sibling order: the wind sits in the panel, the buttons over it, and the
        // log card on its own full-screen layer above the whole scene.
        BuildWind();
        BuildControls();
        BuildLogCard();

        windRoot.SetActive(false);
        logRoot.SetActive(false);
    }

    TMP_FontAsset ResolveFont()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].font != null)
            {
                return texts[i].font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    void BuildControls()
    {
        RectTransform logContent;
        Image logGlow;

        logButton = CreatePill("Log Button", transform,
            new Vector2(-CornerInset, -TopInset),
            new Vector2(LogButtonWidth, ButtonHeight),
            out logContent, out logGlow);

        logButton.onClick.AddListener(() => SetLogOpen(true));

        AddIcon(logContent, BarsSprite, new Vector2(20f, 0f), new Vector2(19f, 14f));

        TMP_Text logLabel = CreateText("Label", logContent, "LOG", LabelSize,
            TextAlignmentOptions.MidlineLeft, LabelColor);
        logLabel.characterSpacing = 2f;
        FillInset(logLabel.rectTransform, 50f, 8f);

        PauseMenuRow logFeedback = logButton.gameObject.AddComponent<PauseMenuRow>();
        logFeedback.Configure(logContent, logLabel, logGlow, 0f, 1.04f, 0.96f);

        RectTransform fastContent;

        fastButton = CreatePill("Fast Forward Button", transform,
            new Vector2(-(CornerInset + LogButtonWidth + ButtonGap), -TopInset),
            new Vector2(FastButtonWidth, ButtonHeight),
            out fastContent, out fastGlow);

        fastButton.onClick.AddListener(() => SetFastForward(!fastForwarding));

        for (int i = 0; i < chevrons.Length; i++)
        {
            Image chevron = CreateImage("Chevron " + (i + 1), fastContent, ChevronSprite,
                new Color(Accent.r, Accent.g, Accent.b, 0.62f), Image.Type.Simple, false);

            RectTransform rect = chevron.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(12f, 15f);
            rect.anchoredPosition = new Vector2(19f + i * 13f, 0f);

            chevrons[i] = rect;
            chevronHome[i] = rect.anchoredPosition;
            chevronGraphics[i] = chevron;
        }

        TMP_Text fastLabel = CreateText("Label", fastContent, "FAST", LabelSize,
            TextAlignmentOptions.MidlineLeft, LabelColor);
        fastLabel.characterSpacing = 2f;
        FillInset(fastLabel.rectTransform, 56f, 8f);

        PauseMenuRow fastFeedback = fastButton.gameObject.AddComponent<PauseMenuRow>();
        fastFeedback.Configure(fastContent, fastLabel, null, 0f, 1.04f, 0.96f);
    }

    void BuildWind()
    {
        windRoot = new GameObject("Fast Forward Wind", typeof(RectTransform), typeof(CanvasGroup));
        windRoot.transform.SetParent(transform, false);

        windGroup = windRoot.GetComponent<CanvasGroup>();
        windGroup.alpha = 0f;

        Stretch((RectTransform)windRoot.transform, 8f);

        wash = CreateImage("Wash", windRoot.transform, PauseMenuArt.Panel,
            new Color(Accent.r, Accent.g, Accent.b, 0.05f), Image.Type.Sliced, false);
        Stretch(wash.rectTransform, 20f);

        for (int i = 0; i < streaks.Length; i++)
        {
            Image image = CreateImage("Streak " + (i + 1), windRoot.transform, StreakSprite,
                new Color(1f, 1f, 1f, 0.12f), Image.Type.Simple, false);

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);

            Streak streak = new Streak
            {
                Rect = rect,
                Image = image
            };

            PlaceStreak(streak, 960f, 130f, false);
            streaks[i] = streak;
        }
    }

    static void PlaceStreak(Streak streak, float halfWidth, float halfHeight, bool offscreen)
    {
        streak.Width = Random.Range(90f, 340f);
        streak.Speed = Random.Range(1100f, 2100f);
        streak.Y = Random.Range(-halfHeight, halfHeight);

        // Respawning sends the streak back past the right edge, so the wind never runs dry.
        streak.X = offscreen
            ? halfWidth + Random.Range(20f, 320f)
            : Random.Range(-halfWidth, halfWidth);

        streak.Rect.sizeDelta = new Vector2(streak.Width, Random.Range(2f, 6f));
        streak.Rect.anchoredPosition = new Vector2(streak.X, streak.Y);

        Color color = new Color(Accent.r, 0.92f, 1f, Random.Range(0.06f, 0.2f));
        streak.Image.color = color;
    }

    void BuildLogCard()
    {
        // The log is a full-screen layer of its own rather than a child of the dialogue box, so it
        // can dim the whole scene and sit dead centre the way a menu page does.
        logRoot = new GameObject("Dialogue Log", typeof(RectTransform), typeof(CanvasGroup));
        logRoot.transform.SetParent(ResolveOverlayHost(), false);
        logRoot.transform.SetAsLastSibling();

        logGroup = logRoot.GetComponent<CanvasGroup>();
        logGroup.alpha = 0f;
        logGroup.blocksRaycasts = false;
        logGroup.interactable = false;

        Stretch((RectTransform)logRoot.transform);

        Image dimmer = CreateImage("Dimmer", logRoot.transform, null,
            new Color(0f, 0.015f, 0.05f, 0.82f), Image.Type.Simple, true);
        Stretch(dimmer.rectTransform, 40f);

        // Clicking the dimmed scene behind the card puts the log away, like closing a page.
        Button dimmerButton = dimmer.gameObject.AddComponent<Button>();
        dimmerButton.targetGraphic = dimmer;
        dimmerButton.transition = Selectable.Transition.None;
        dimmerButton.onClick.AddListener(() => SetLogOpen(false));

        logCard = CreateRect("Card", logRoot.transform);
        SetAnchored(logCard, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

        Image halo = CreateImage("Halo", logCard, PauseMenuArt.Panel,
            PauseMenuArt.HaloColor, Image.Type.Sliced, false);
        Stretch(halo.rectTransform, 54f);

        Image shadow = CreateImage("Shadow", logCard, PauseMenuArt.Panel,
            new Color(0f, 0f, 0f, 0.45f), Image.Type.Sliced, false);
        Stretch(shadow.rectTransform, 16f);

        Image fill = CreateImage("Fill", logCard, PauseMenuArt.Panel,
            new Color(1f, 1f, 1f, 0.97f), Image.Type.Sliced, true);
        Stretch(fill.rectTransform);

        UIVertexGradient gradient = fill.gameObject.AddComponent<UIVertexGradient>();
        gradient.SetColors(PauseMenuArt.GradientTop, PauseMenuArt.GradientBottom);

        Image frame = CreateImage("Frame", logCard, PauseMenuArt.Frame,
            new Color(Accent.r, Accent.g, Accent.b, 0.62f), Image.Type.Sliced, false);
        Stretch(frame.rectTransform);

        // A slightly tilted accent slab sits behind the heading, so the header reads as a banner
        // rather than as another row of text.
        Image banner = CreateImage("Banner", logCard, null,
            new Color(Accent.r, Accent.g, Accent.b, 0.14f), Image.Type.Simple, false);
        SetAnchored(banner.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -26f),
            new Vector2(452f, 58f));
        banner.rectTransform.localEulerAngles = new Vector3(0f, 0f, -2f);

        TMP_Text title = CreateText("Title", logCard, "CONVERSATION LOG", 32f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        title.characterSpacing = 6f;
        title.fontStyle = FontStyles.Bold;
        SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(CardMargin, -32f),
            new Vector2(820f, 46f));

        logSubtitle = CreateText("Subtitle", logCard, string.Empty, 14f,
            TextAlignmentOptions.MidlineLeft, new Color(Accent.r, Accent.g, Accent.b, 0.6f));
        logSubtitle.characterSpacing = 2.5f;
        SetAnchored(logSubtitle.rectTransform, new Vector2(0f, 1f), new Vector2(CardMargin + 4f, -86f),
            new Vector2(820f, 22f));

        Image rule = CreateImage("Rule", logCard, null,
            new Color(Accent.r, Accent.g, Accent.b, 0.3f), Image.Type.Simple, false);
        SetAnchored(rule.rectTransform, new Vector2(0f, 1f), new Vector2(CardMargin, -122f),
            new Vector2(CardWidth - CardMargin * 2f, 1.5f));

        RectTransform scrollArea = CreateRect("Scroll Area", logCard);
        scrollArea.anchorMin = Vector2.zero;
        scrollArea.anchorMax = Vector2.one;
        scrollArea.offsetMin = new Vector2(CardMargin, 66f);
        scrollArea.offsetMax = new Vector2(-CardMargin, -136f);

        Image scrollHit = scrollArea.gameObject.AddComponent<Image>();
        scrollHit.color = new Color(1f, 1f, 1f, 0f);
        scrollHit.raycastTarget = true;

        logScroll = scrollArea.gameObject.AddComponent<ScrollRect>();

        logViewport = CreateRect("Viewport", scrollArea);
        Stretch(logViewport);
        logViewport.gameObject.AddComponent<RectMask2D>();

        logContent = CreateRect("Content", logViewport);
        logContent.anchorMin = new Vector2(0f, 1f);
        logContent.anchorMax = new Vector2(1f, 1f);
        logContent.pivot = new Vector2(0.5f, 1f);
        logContent.anchoredPosition = Vector2.zero;
        logContent.sizeDelta = Vector2.zero;

        logScroll.content = logContent;
        logScroll.viewport = logViewport;
        logScroll.horizontal = false;
        logScroll.vertical = true;
        logScroll.movementType = ScrollRect.MovementType.Clamped;
        logScroll.inertia = true;
        logScroll.scrollSensitivity = 40f;

        TMP_Text footer = CreateText("Footer", logCard, "SCROLL FOR EARLIER LINES  ·  ESC OR CLICK OUTSIDE TO CLOSE",
            12f, TextAlignmentOptions.Center, new Color(Accent.r, Accent.g, Accent.b, 0.42f));
        footer.characterSpacing = 2f;
        SetAnchored(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f),
            new Vector2(CardWidth - CardMargin * 2f, 20f));

        RectTransform closeContent;
        Image closeGlow;

        Button closeButton = CreatePill("Close Log", logCard,
            new Vector2(-28f, -28f), new Vector2(118f, 38f), out closeContent, out closeGlow);

        closeButton.onClick.AddListener(() => SetLogOpen(false));

        TMP_Text closeLabel = CreateText("Label", closeContent, "CLOSE", 13f,
            TextAlignmentOptions.Center, LabelColor);
        closeLabel.characterSpacing = 2f;
        Stretch(closeLabel.rectTransform);

        PauseMenuRow closeFeedback = closeButton.gameObject.AddComponent<PauseMenuRow>();
        closeFeedback.Configure(closeContent, closeLabel, closeGlow, 0f, 1.04f, 0.96f);
    }

    Transform ResolveOverlayHost()
    {
        Canvas[] canvases = GetComponentsInParent<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].isRootCanvas)
            {
                return canvases[i].transform;
            }
        }

        return canvases.Length > 0 && canvases[0] != null ? canvases[0].transform : transform.root;
    }

    Button CreatePill(string name, Transform parent, Vector2 position, Vector2 size,
        out RectTransform content, out Image glow)
    {
        GameObject pill = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        pill.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)pill.transform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image hit = pill.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        content = CreateRect("Content", pill.transform);
        Stretch(content);

        Image shadow = CreateImage("Shadow", content, PauseMenuArt.Plate,
            new Color(0f, 0f, 0f, 0.42f), Image.Type.Sliced, false);
        Stretch(shadow.rectTransform, 2f);
        shadow.rectTransform.offsetMin = new Vector2(-2f, -5f);
        shadow.rectTransform.offsetMax = new Vector2(2f, -1f);

        glow = CreateImage("Glow", content, PauseMenuArt.Plate,
            new Color(Accent.r, Accent.g, Accent.b, 0f), Image.Type.Sliced, false);
        Stretch(glow.rectTransform, 8f);

        Image plate = CreateImage("Plate", content, PauseMenuArt.Plate,
            new Color(1f, 1f, 1f, 0.95f), Image.Type.Sliced, false);
        Stretch(plate.rectTransform);

        UIVertexGradient gradient = plate.gameObject.AddComponent<UIVertexGradient>();
        gradient.SetColors(new Color(0.08f, 0.24f, 0.66f, 1f), new Color(0.008f, 0.03f, 0.1f, 1f));

        Image rim = CreateImage("Rim", content, PauseMenuArt.PlateFrame,
            new Color(Accent.r, Accent.g, Accent.b, 0.6f), Image.Type.Sliced, false);
        Stretch(rim.rectTransform);

        Button button = pill.GetComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        // UISound only wires the buttons that exist when a scene loads, so this one asks for the
        // shared click feedback itself.
        pill.AddComponent<ButtonSfx>();

        return button;
    }

    void AddIcon(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image icon = CreateImage("Icon", parent, sprite,
            new Color(1f, 1f, 1f, 0.92f), Image.Type.Simple, false);

        RectTransform rect = icon.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    TMP_Text CreateText(string name, Transform parent, string content, float size,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite, Color color,
        Image.Type type, bool raycast)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static void Stretch(RectTransform rect, float expand = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-expand, -expand);
        rect.offsetMax = new Vector2(expand, expand);
    }

    /// <summary>Fills the parent horizontally and places the rect a given distance below its top.</summary>
    static void PlaceTop(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void FillInset(RectTransform rect, float left, float right)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 1f);
    }

    static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // ---------------------------------------------------------------- the buttons' shapes

    static Sprite streakSprite;
    static Sprite chevronSprite;
    static Sprite barsSprite;

    static Sprite StreakSprite
    {
        get
        {
            if (streakSprite == null)
            {
                streakSprite = BuildStreakSprite();
            }

            return streakSprite;
        }
    }

    static Sprite ChevronSprite
    {
        get
        {
            if (chevronSprite == null)
            {
                chevronSprite = BuildChevronSprite();
            }

            return chevronSprite;
        }
    }

    static Sprite BarsSprite
    {
        get
        {
            if (barsSprite == null)
            {
                barsSprite = BuildBarsSprite();
            }

            return barsSprite;
        }
    }

    /// <summary>A thin bar that fades out at both ends, so a streak reads as a blur of speed.</summary>
    static Sprite BuildStreakSprite()
    {
        const int width = 128;
        const int height = 12;

        Texture2D texture = CreateTexture("Dialogue Streak", width, height);
        Color32[] pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            float vertical = Mathf.Pow(1f - Mathf.Abs(2f * v - 1f), 1.4f);

            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float taper = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(Mathf.PI * u)), 0.55f);

                pixels[y * width + x] = White(taper * vertical);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return BuildSprite(texture, 0f);
    }

    /// <summary>A solid triangle pointing right, used for the pair of fast forward chevrons.</summary>
    static Sprite BuildChevronSprite()
    {
        const int size = 32;
        const float baseSpan = 0.42f;

        Texture2D texture = CreateTexture("Dialogue Chevron", size, size);
        Color32[] pixels = new Color32[size * size];

        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            float py = y + 0.5f;
            float reach = size * (1f - Mathf.Clamp01(Mathf.Abs(py - half) / (size * baseSpan)));

            for (int x = 0; x < size; x++)
            {
                float margin = reach - (x + 0.5f);

                pixels[y * size + x] = White(Mathf.Clamp01(margin / 1.6f + 0.5f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return BuildSprite(texture, 0f);
    }

    /// <summary>Three stacked bars: the log's mark for a list of things already said.</summary>
    static Sprite BuildBarsSprite()
    {
        const int size = 32;

        Texture2D texture = CreateTexture("Dialogue Log Icon", size, size);
        Color32[] pixels = new Color32[size * size];

        float halfWidth = size * 0.34f;

        for (int y = 0; y < size; y++)
        {
            float py = y + 0.5f;

            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float horizontal = Mathf.Clamp01(halfWidth - Mathf.Abs(px - size * 0.5f) + 0.5f);
                float coverage = 0f;

                for (int bar = 0; bar < 3; bar++)
                {
                    float center = size * (0.24f + bar * 0.26f);
                    float vertical = Mathf.Clamp01(size * 0.07f - Mathf.Abs(py - center) + 0.5f);

                    coverage = Mathf.Max(coverage, vertical * horizontal);
                }

                pixels[y * size + x] = White(coverage);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return BuildSprite(texture, 0f);
    }

    static Texture2D CreateTexture(string name, int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
    }

    static Sprite BuildSprite(Texture2D texture, float border)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));

        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    static Color32 White(float alpha)
    {
        return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
    }
}
