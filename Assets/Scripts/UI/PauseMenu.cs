using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds a pause menu to every story scene without touching any scene asset: a hidden instance
/// installs itself on play, builds its canvas on demand and answers Escape or the small pause
/// plate in the top-right corner. The menu can save into one of three slots, load a slot back,
/// or return to the title screen, and it draws itself in the same deep-blue language as the
/// dialogue panel and the title menu.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    const float CardWidth = 820f;
    const float CardHeight = 660f;
    const float RowWidth = 600f;
    const float RowHeight = 74f;

    const float PauseButtonDelay = 2.2f;

    enum Page
    {
        Main,
        Slots,
        Confirm,
        Delete
    }

    static PauseMenu instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("Pause Menu");
        host.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(host);
        instance = host.AddComponent<PauseMenu>();
    }

    /// <summary>
    /// Opens the same load list over the title screen, so several manual saves can be chosen.
    /// Returns false when the list could not open, letting the caller load directly instead.
    /// </summary>
    public static bool ShowLoadSlots()
    {
        return instance != null && instance.OpenLoadSlotsFromTitle();
    }

    Canvas canvas;
    GameObject root;
    CanvasGroup rootGroup;
    RectTransform card;
    Image overlay;
    GameObject overlayObject;
    GameObject pauseButtonRoot;
    CanvasGroup pauseButtonGroup;

    GameObject mainSection;
    GameObject slotsSection;
    GameObject confirmSection;
    TMP_Text titleText;
    TMP_Text subtitleText;
    TMP_Text footerText;
    TMP_Text toastText;

    readonly Button[] slotButtons = new Button[SaveSystem.SlotCount];
    readonly TMP_Text[] slotHeadings = new TMP_Text[SaveSystem.SlotCount];
    readonly TMP_Text[] slotDetails = new TMP_Text[SaveSystem.SlotCount];
    readonly TMP_Text[] slotStamps = new TMP_Text[SaveSystem.SlotCount];
    readonly TMP_Text[] slotPreviews = new TMP_Text[SaveSystem.SlotCount];
    readonly RawImage[] slotThumbs = new RawImage[SaveSystem.SlotCount];
    readonly Button[] slotDeleteButtons = new Button[SaveSystem.SlotCount];

    Button resumeButton;
    Button loadButton;
    Button confirmReturnButton;

    Button backButton;
    GameObject continueRow;
    Button continueButton;
    TMP_Text continueDetail;
    TMP_Text continueStamp;

    GameObject deleteSection;
    TMP_Text deleteMessage;
    Button deleteConfirmButton;
    Button deleteCancelButton;
    int pendingDeleteSlot = -1;
    bool isSaving;

    TMP_FontAsset font;
    Coroutine fadeRoutine;
    Coroutine toastRoutine;
    Coroutine revealRoutine;

    Page page = Page.Main;
    bool slotsForSaving;
    bool openedFromTitle;
    bool built;
    bool isOpen;
    bool isLoading;
    float previousTimeScale = 1f;
    float sceneLoadedTime;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        sceneLoadedTime = Time.unscaledTime;
        RefreshPauseButton();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (isOpen)
        {
            RestoreTimeScale();
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        RestoreTimeScale();
    }

    void OnApplicationQuit()
    {
        SaveSystem.CaptureProgressLine();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveSystem.CaptureProgressLine();
        }
    }

    void Update()
    {
        if (isLoading || isSaving)
        {
            return;
        }

        bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool startPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;

        if (!escapePressed && !startPressed)
        {
            return;
        }

        if (isOpen)
        {
            if (escapePressed || page == Page.Main)
            {
                HandleBack();
            }

            return;
        }

        TryOpen();
    }

    void TryOpen()
    {
        if (isOpen || isLoading)
        {
            return;
        }

        if (!SaveSystem.IsStoryScene(SceneManager.GetActiveScene().buildIndex))
        {
            return;
        }

        SaveSystem.CaptureProgressLine();

        EnsureBuilt();
        if (!built)
        {
            return;
        }

        isOpen = true;
        openedFromTitle = false;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        if (pauseButtonRoot != null)
        {
            pauseButtonRoot.SetActive(false);
        }

        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        ShowPage(Page.Main);

        root.SetActive(true);
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(OpenFade());
    }

    bool OpenLoadSlotsFromTitle()
    {
        if (isOpen || isLoading)
        {
            return false;
        }

        EnsureBuilt();
        if (!built)
        {
            return false;
        }

        isOpen = true;
        openedFromTitle = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

        // The title screen keeps its ambience moving; only gameplay needs its clock stopped.
        if (SaveSystem.IsStoryScene(SceneManager.GetActiveScene().buildIndex))
        {
            Time.timeScale = 0f;
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        if (pauseButtonRoot != null)
        {
            pauseButtonRoot.SetActive(false);
        }

        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        slotsForSaving = false;
        ShowPage(Page.Slots);

        root.SetActive(true);
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(OpenFade());
        return true;
    }

    void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        openedFromTitle = false;
        RestoreTimeScale();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(CloseFade());
    }

    void HandleBack()
    {
        if (page == Page.Delete)
        {
            HandleCancelDelete();
            return;
        }

        if (page == Page.Confirm)
        {
            ShowPage(Page.Main);
            return;
        }

        if (page == Page.Main || openedFromTitle)
        {
            Close();
            return;
        }

        ShowPage(Page.Main);
    }

    void RestoreTimeScale()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        previousTimeScale = 1f;
    }

    IEnumerator OpenFade()
    {
        rootGroup.alpha = 0f;

        if (card != null)
        {
            card.localScale = Vector3.one * 0.97f;
        }

        float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            rootGroup.alpha = progress;

            if (card != null)
            {
                card.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, progress);
            }

            yield return null;
        }

        rootGroup.alpha = 1f;

        if (card != null)
        {
            card.localScale = Vector3.one;
        }

        fadeRoutine = null;
    }

    IEnumerator CloseFade()
    {
        rootGroup.blocksRaycasts = false;

        float duration = 0.12f;
        float elapsed = 0f;
        float startAlpha = rootGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rootGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        rootGroup.alpha = 0f;
        root.SetActive(false);
        fadeRoutine = null;

        RefreshPauseButton();
    }

    void ShowPage(Page next)
    {
        page = next;

        if (root == null)
        {
            return;
        }

        mainSection.SetActive(next == Page.Main);
        slotsSection.SetActive(next == Page.Slots);
        confirmSection.SetActive(next == Page.Confirm);
        deleteSection.SetActive(next == Page.Delete);

        if (next == Page.Slots)
        {
            RefreshSlots();
            titleText.text = slotsForSaving ? "SAVE GAME" : "LOAD GAME";
            subtitleText.text = slotsForSaving
                ? "Choose a slot to save this exact line"
                : "Choose a save to resume from";
        }
        else if (next == Page.Confirm)
        {
            titleText.text = "MAIN MENU";
            subtitleText.text = "Return to the title screen?";
        }
        else if (next == Page.Delete)
        {
            titleText.text = "DELETE SAVE";
            subtitleText.text = "This cannot be undone.";
        }
        else
        {
            titleText.text = "PAUSED";
            subtitleText.text = SaveSystem.DescribeScene(SceneManager.GetActiveScene().buildIndex);

            if (loadButton != null)
            {
                loadButton.interactable = SaveSystem.HasAnySave;
            }
        }

        footerText.gameObject.SetActive(next == Page.Main);

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        toastText.gameObject.SetActive(false);

        SelectFirst(next);
    }

    void SelectFirst(Page next)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        Button target = null;

        if (next == Page.Main)
        {
            target = resumeButton;
        }
        else if (next == Page.Slots)
        {
            target = openedFromTitle && continueRow != null && continueRow.activeSelf
                ? continueButton
                : slotButtons[0];
        }
        else if (next == Page.Delete)
        {
            target = deleteCancelButton;
        }
        else
        {
            target = confirmReturnButton;
        }

        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }

    void OpenSlots(bool forSaving)
    {
        slotsForSaving = forSaving;
        ShowPage(Page.Slots);
    }

    void RefreshSlots()
    {
        SaveSystem.SlotInfo progress = SaveSystem.ReadProgress();
        bool showContinue = openedFromTitle && !slotsForSaving && progress.HasData;

        if (continueRow != null)
        {
            continueRow.SetActive(showContinue);
        }

        if (showContinue)
        {
            if (continueDetail != null)
            {
                continueDetail.text = progress.SceneLabel;
            }

            if (continueStamp != null)
            {
                continueStamp.text = progress.TimestampLabel;
            }
        }

        if (backButton != null)
        {
            SetAnchored(backButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, showContinue ? -304f : -246f),
                new Vector2(RowWidth, 40f));
        }

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            SaveSystem.SlotInfo info = SaveSystem.ReadSlot(i);

            if (slotHeadings[i] != null)
            {
                slotHeadings[i].text = "SLOT " + (i + 1);
            }

            if (slotStamps[i] != null)
            {
                slotStamps[i].text = info.HasData ? info.TimestampLabel : string.Empty;
            }

            if (slotDetails[i] != null)
            {
                slotDetails[i].text = info.HasData ? info.SceneLabel : "Empty Slot";
            }

            if (slotPreviews[i] != null)
            {
                string preview = info.HasData ? info.LinePreview : string.Empty;
                slotPreviews[i].text = preview;
                slotPreviews[i].gameObject.SetActive(!string.IsNullOrEmpty(preview));
            }

            if (slotDeleteButtons[i] != null)
            {
                slotDeleteButtons[i].gameObject.SetActive(info.HasData);
            }

            if (slotThumbs[i] != null)
            {
                Texture2D thumbnail = info.HasData ? SaveSystem.GetThumbnail(i) : null;
                slotThumbs[i].texture = thumbnail;
                slotThumbs[i].enabled = thumbnail != null;
            }

            bool usable = slotsForSaving || info.HasData;

            if (slotButtons[i] != null)
            {
                slotButtons[i].interactable = usable;
            }
        }
    }

    void HandleSlotClicked(int slot)
    {
        if (page != Page.Slots)
        {
            return;
        }

        if (slotsForSaving)
        {
            if (!isSaving)
            {
                StartCoroutine(SaveSlotRoutine(slot));
            }

            return;
        }

        SaveSystem.SlotInfo info = SaveSystem.ReadSlot(slot);
        if (!info.HasData)
        {
            return;
        }

        SaveSystem.PrepareRestore(info);
        SaveSystem.SetProgressScene(info.SceneIndex);
        StartCoroutine(TransitionToScene(info.SceneIndex));
    }

    IEnumerator SaveSlotRoutine(int slot)
    {
        isSaving = true;

        if (!SaveSystem.SaveCurrentSceneToSlot(slot))
        {
            isSaving = false;
            yield break;
        }

        float alpha = rootGroup.alpha;
        bool raycasts = rootGroup.blocksRaycasts;
        bool interactable = rootGroup.interactable;

        // Hide the menu for the capture frame so the thumbnail shows the moment being saved,
        // not the pause card sitting on top of it.
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        yield return new WaitForEndOfFrame();

        Texture2D screenshot = null;
        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not capture save thumbnail: " + exception.Message);
        }

        rootGroup.alpha = alpha;
        rootGroup.blocksRaycasts = raycasts;
        rootGroup.interactable = interactable;

        if (screenshot != null)
        {
            SaveSystem.SaveThumbnail(slot, screenshot);
        }
        else
        {
            // Do not leave a previous save's screenshot beside the new save data.
            SaveSystem.ClearThumbnail(slot);
        }

        isSaving = false;
        ShowPage(Page.Main);
        ShowToast("Saved to Slot " + (slot + 1));
    }

    void HandleDeleteSlot(int slot)
    {
        SaveSystem.SlotInfo info = SaveSystem.ReadSlot(slot);
        if (!info.HasData)
        {
            return;
        }

        pendingDeleteSlot = slot;

        if (deleteMessage != null)
        {
            string detail = string.IsNullOrEmpty(info.LinePreview)
                ? info.SceneLabel
                : info.SceneLabel + "\n" + info.LinePreview;

            deleteMessage.text = "Delete Slot " + (slot + 1) + "?\n" + detail + "\nThis cannot be undone.";
        }

        ShowPage(Page.Delete);
    }

    void HandleConfirmDelete()
    {
        if (pendingDeleteSlot < 0)
        {
            return;
        }

        int slot = pendingDeleteSlot;
        pendingDeleteSlot = -1;
        SaveSystem.ClearSlot(slot);
        ShowPage(Page.Slots);
        ShowToast("Slot " + (slot + 1) + " deleted");
    }

    void HandleCancelDelete()
    {
        pendingDeleteSlot = -1;
        ShowPage(Page.Slots);
    }

    void HandleContinueClicked()
    {
        if (page != Page.Slots || slotsForSaving)
        {
            return;
        }

        SaveSystem.SlotInfo progress = SaveSystem.ReadProgress();
        if (!progress.HasData)
        {
            return;
        }

        SaveSystem.PrepareRestore(progress);
        StartCoroutine(TransitionToScene(progress.SceneIndex));
    }

    void HandleReturnToMainMenu()
    {
        StartCoroutine(TransitionToScene(SaveSystem.MainMenuSceneIndex));
    }

    void HandlePauseButtonClicked()
    {
        if (isOpen)
        {
            Close();
            return;
        }

        TryOpen();
    }

    IEnumerator TransitionToScene(int sceneIndex)
    {
        isLoading = true;
        isOpen = false;
        openedFromTitle = false;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        RestoreTimeScale();

        if (pauseButtonRoot != null)
        {
            pauseButtonRoot.SetActive(false);
        }

        if (root != null)
        {
            rootGroup.alpha = 0f;
            root.SetActive(false);
        }

        if (overlayObject != null)
        {
            overlayObject.SetActive(true);
            SetOverlayAlpha(0f);
        }

        yield return FadeOverlay(0f, 1f, 0.4f);

        // The loader already fades the outgoing scene's audio down before activating the next one.
        yield return SceneLoader.LoadSceneAsync(sceneIndex, 3f);

        yield return FadeOverlay(1f, 0f, 0.5f);

        if (overlayObject != null)
        {
            overlayObject.SetActive(false);
        }

        isLoading = false;
        RefreshPauseButton();
    }

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (overlay == null)
        {
            yield break;
        }

        SetOverlayAlpha(from);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetOverlayAlpha(to);
    }

    void SetOverlayAlpha(float alpha)
    {
        if (overlay == null)
        {
            return;
        }

        Color color = overlay.color;
        color.a = alpha;
        overlay.color = color;
    }

    void ShowToast(string message)
    {
        if (toastText == null)
        {
            return;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    IEnumerator ToastRoutine(string message)
    {
        if (footerText != null)
        {
            footerText.gameObject.SetActive(false);
        }

        toastText.gameObject.SetActive(true);
        toastText.text = message;

        Color color = toastText.color;
        color.a = 0f;
        toastText.color = color;

        float elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Clamp01(elapsed / 0.18f);
            toastText.color = color;
            yield return null;
        }

        color.a = 1f;
        toastText.color = color;

        yield return new WaitForSecondsRealtime(1.6f);

        elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = 1f - Mathf.Clamp01(elapsed / 0.35f);
            toastText.color = color;
            yield return null;
        }

        toastText.gameObject.SetActive(false);

        if (footerText != null && page == Page.Main)
        {
            footerText.gameObject.SetActive(true);
        }

        toastRoutine = null;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneLoadedTime = Time.unscaledTime;

        if (isLoading)
        {
            return;
        }

        if (isOpen)
        {
            HideImmediately();
        }

        RefreshPauseButton();
    }

    void RefreshPauseButton()
    {
        if (pauseButtonRoot == null)
        {
            if (!SaveSystem.IsStoryScene(SceneManager.GetActiveScene().buildIndex))
            {
                return;
            }

            EnsurePauseButton();
            if (pauseButtonRoot == null)
            {
                return;
            }
        }

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        pauseButtonRoot.SetActive(false);

        if (isOpen || isLoading)
        {
            return;
        }

        if (!SaveSystem.IsStoryScene(SceneManager.GetActiveScene().buildIndex))
        {
            return;
        }

        revealRoutine = StartCoroutine(RevealPauseButton());
    }

    IEnumerator RevealPauseButton()
    {
        // Give the scene's own opening fade a moment, so the plate does not sit on black.
        // Re-opening the menu later in the same scene shows the plate again straight away.
        float sceneAge = Time.unscaledTime - sceneLoadedTime;
        float wait = Mathf.Max(0f, PauseButtonDelay - sceneAge);

        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }

        if (isOpen || isLoading || !SaveSystem.IsStoryScene(SceneManager.GetActiveScene().buildIndex))
        {
            revealRoutine = null;
            yield break;
        }

        pauseButtonRoot.SetActive(true);
        pauseButtonGroup.alpha = 0f;
        pauseButtonRoot.transform.localScale = Vector3.one * 0.92f;

        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.35f));

            pauseButtonGroup.alpha = progress;
            pauseButtonRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, progress);
            yield return null;
        }

        pauseButtonGroup.alpha = 1f;
        pauseButtonRoot.transform.localScale = Vector3.one;
        revealRoutine = null;
    }

    void HideImmediately()
    {
        isOpen = false;
        openedFromTitle = false;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        RestoreTimeScale();

        if (root != null)
        {
            rootGroup.alpha = 0f;
            root.SetActive(false);
        }
    }

    void EnsurePauseButton()
    {
        EnsureCanvas();

        if (pauseButtonRoot != null)
        {
            return;
        }

        if (font == null)
        {
            font = ResolveFont();
        }

        GameObject buttonObject = new GameObject(
            "Pause Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(CanvasGroup));

        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(118f, 40f);
        rect.anchoredPosition = new Vector2(-36f, -32f);

        // The pill is one hit area; its plate, rim and label live in a content rect so they can
        // lift and settle together under the pointer.
        Image hit = buttonObject.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        RectTransform content = CreateRect("Content", buttonObject.transform);
        Stretch(content);

        Image shadow = CreateImage("Shadow", content, PauseMenuArt.Plate,
            new Color(0f, 0f, 0f, 0.32f), Image.Type.Sliced, false);
        Stretch(shadow.rectTransform, 2f);
        shadow.rectTransform.offsetMin = new Vector2(-2f, -5f);
        shadow.rectTransform.offsetMax = new Vector2(2f, -1f);

        Image glow = CreateImage("Glow", content, PauseMenuArt.Plate,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0f),
            Image.Type.Sliced, false);
        Stretch(glow.rectTransform, 9f);

        Image plate = CreateImage("Plate", content, PauseMenuArt.Plate,
            new Color(1f, 1f, 1f, 0.82f), Image.Type.Sliced, false);
        Stretch(plate.rectTransform);

        UIVertexGradient plateGradient = plate.gameObject.AddComponent<UIVertexGradient>();
        plateGradient.SetColors(new Color(0.06f, 0.19f, 0.55f, 1f), new Color(0.008f, 0.03f, 0.1f, 1f));

        Image rim = CreateImage("Rim", content, PauseMenuArt.PlateFrame,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.45f),
            Image.Type.Sliced, false);
        Stretch(rim.rectTransform);

        Image icon = CreateImage("Icon", content, PauseMenuArt.PauseIcon,
            new Color(1f, 1f, 1f, 0.92f), Image.Type.Simple, false);

        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(21f, 21f);
        iconRect.anchoredPosition = new Vector2(-37f, 0f);

        TMP_Text label = CreateText("Label", content, "PAUSE", 15f, TextAlignmentOptions.MidlineLeft,
            new Color(1f, 1f, 1f, 0.9f));
        label.characterSpacing = 2f;
        SetAnchored(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(16f, 0f), new Vector2(60f, 28f));

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(HandlePauseButtonClicked);

        PauseMenuRow feedback = buttonObject.AddComponent<PauseMenuRow>();
        feedback.Configure(content, label, glow, 0f, 1.045f, 0.965f);

        buttonObject.AddComponent<ButtonSfx>();

        pauseButtonGroup = buttonObject.GetComponent<CanvasGroup>();
        pauseButtonRoot = buttonObject;
        buttonObject.SetActive(false);
    }

    void EnsureCanvas()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "Pause Menu Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(canvasObject);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        EnsurePauseButton();
        BuildUI();
        built = true;
    }

    void BuildUI()
    {
        if (font == null)
        {
            font = ResolveFont();
        }

        root = new GameObject("Pause Menu Root", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        rootGroup = root.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        Stretch((RectTransform)root.transform);

        Image dimmer = CreateImage("Dimmer", root.transform, PauseMenuArt.Dim, PauseMenuArt.DimColor, Image.Type.Simple, true);
        Stretch(dimmer.rectTransform);

        card = CreateRect("Card", root.transform);
        SetAnchored(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

        Image halo = CreateImage("Halo", card, PauseMenuArt.Panel, PauseMenuArt.HaloColor, Image.Type.Sliced, false);
        Stretch(halo.rectTransform, 46f);

        Image shadow = CreateImage("Shadow", card, PauseMenuArt.Panel, PauseMenuArt.ShadowColor, Image.Type.Sliced, false);
        Stretch(shadow.rectTransform, 16f);

        Image fill = CreateImage("Fill", card, PauseMenuArt.Panel, PauseMenuArt.CardTint, Image.Type.Sliced, true);
        Stretch(fill.rectTransform);

        UIVertexGradient gradient = fill.gameObject.AddComponent<UIVertexGradient>();
        gradient.SetColors(PauseMenuArt.GradientTop, PauseMenuArt.GradientBottom);

        Image frame = CreateImage("Frame", card, PauseMenuArt.Frame, PauseMenuArt.FrameColor, Image.Type.Sliced, false);
        Stretch(frame.rectTransform);

        Image rule = CreateImage("Rule", card, null,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.75f),
            Image.Type.Simple, false);
        SetAnchored(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 276f), new Vector2(64f, 2f));

        titleText = CreateText("Title", card, "PAUSED", 62f, TextAlignmentOptions.Center, Color.white);
        SetAnchored(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 222f), new Vector2(760f, 80f));
        titleText.characterSpacing = 8f;

        subtitleText = CreateText("Subtitle", card, string.Empty, 21f, TextAlignmentOptions.Center,
            new Color(1f, 1f, 1f, 0.6f));
        SetAnchored(subtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 168f), new Vector2(720f, 32f));

        BuildMainSection();
        BuildSlotsSection();
        BuildConfirmSection();
        BuildDeleteSection();

        footerText = CreateText("Footer", card, "ESC TO RESUME", 18f, TextAlignmentOptions.Center,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.45f));
        SetAnchored(footerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -282f), new Vector2(600f, 28f));

        toastText = CreateText("Toast", card, string.Empty, 19f, TextAlignmentOptions.Center,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.9f));
        SetAnchored(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -282f), new Vector2(600f, 28f));
        toastText.gameObject.SetActive(false);

        overlay = CreateImage("Transition", canvas.transform, null, Color.black, Image.Type.Simple, true);
        overlayObject = overlay.gameObject;
        Stretch(overlay.rectTransform);
        overlayObject.SetActive(false);

        slotsSection.SetActive(false);
        confirmSection.SetActive(false);
        deleteSection.SetActive(false);
        mainSection.SetActive(true);

        root.SetActive(false);
    }

    void BuildMainSection()
    {
        mainSection = CreateSection("Main Section", card);

        float[] positions = { 104f, 24f, -56f, -136f };
        string[] labels = { "Resume", "Save Game", "Load Game", "Main Menu" };

        Button[] buttons = new Button[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            buttons[i] = CreateMenuRow(mainSection.transform, labels[i], positions[i], RowHeight, 36f);
            CreateSeparator(mainSection.transform, positions[i] - RowHeight * 0.5f, 0.13f);
        }

        resumeButton = buttons[0];
        loadButton = buttons[2];

        resumeButton.onClick.AddListener(Close);
        buttons[1].onClick.AddListener(HandleSaveGameClicked);
        loadButton.onClick.AddListener(HandleLoadGameClicked);
        buttons[3].onClick.AddListener(HandleMainMenuClicked);
    }

    void BuildSlotsSection()
    {
        slotsSection = CreateSection("Slots Section", card);

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            float y = 92f - i * 126f;

            RectTransform content;
            Button button = CreateRow(slotsSection.transform, "Slot " + (i + 1), y, 116f, out content);

            slotButtons[i] = button;

            RectTransform thumbnailRect = CreateRect("Thumbnail", content);
            SetCorner(thumbnailRect, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(104f, 58f));

            Image thumbnailBack = CreateImage("Back", thumbnailRect, PauseMenuArt.Plate,
                new Color(0.01f, 0.04f, 0.12f, 0.8f), Image.Type.Sliced, false);
            Stretch(thumbnailBack.rectTransform);

            GameObject thumbnailObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            thumbnailObject.transform.SetParent(thumbnailRect, false);

            RectTransform thumbnailImageRect = (RectTransform)thumbnailObject.transform;
            thumbnailImageRect.anchorMin = Vector2.zero;
            thumbnailImageRect.anchorMax = Vector2.one;
            thumbnailImageRect.offsetMin = new Vector2(2f, 2f);
            thumbnailImageRect.offsetMax = new Vector2(-2f, -2f);

            RawImage thumbnail = thumbnailObject.GetComponent<RawImage>();
            thumbnail.raycastTarget = false;
            thumbnail.color = Color.white;
            thumbnail.enabled = false;
            slotThumbs[i] = thumbnail;

            Image thumbnailRim = CreateImage("Rim", thumbnailRect, PauseMenuArt.PlateFrame,
                new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.32f),
                Image.Type.Sliced, false);
            Stretch(thumbnailRim.rectTransform);

            TMP_Text heading = CreateText("Heading", content, "SLOT " + (i + 1), 18f,
                TextAlignmentOptions.MidlineLeft,
                new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.75f));
            heading.characterSpacing = 3f;
            SetCorner(heading.rectTransform, new Vector2(0f, 1f), new Vector2(120f, -12f), new Vector2(300f, 22f));
            slotHeadings[i] = heading;

            TMP_Text stamp = CreateText("Stamp", content, string.Empty, 14f,
                TextAlignmentOptions.MidlineRight,
                new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.5f));
            SetCorner(stamp.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -13f), new Vector2(180f, 18f));
            slotStamps[i] = stamp;

            TMP_Text detail = CreateText("Detail", content, "Empty Slot", 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetCorner(detail.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, 10f), new Vector2(390f, 28f));
            slotDetails[i] = detail;

            TMP_Text preview = CreateText("Preview", content, string.Empty, 14f,
                TextAlignmentOptions.MidlineLeft,
                new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.55f));
            SetCorner(preview.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, -26f), new Vector2(340f, 20f));
            preview.gameObject.SetActive(false);
            slotPreviews[i] = preview;

            Button deleteButton = CreateDeleteButton(content, "Delete " + (i + 1), new Vector2(-4f, -48f));
            deleteButton.gameObject.SetActive(false);
            slotDeleteButtons[i] = deleteButton;

            PauseMenuRow feedback = button.GetComponent<PauseMenuRow>();
            feedback.Configure(content, detail, null, 14f, 1f, 0.995f);

            int slotIndex = i;
            button.onClick.AddListener(() => HandleSlotClicked(slotIndex));
            deleteButton.onClick.AddListener(() => HandleDeleteSlot(slotIndex));

            CreateSeparator(slotsSection.transform, y - 63f, 0.1f);
        }

        RectTransform continueContent;
        continueButton = CreateRow(slotsSection.transform, "Continue", -252f, 52f, out continueContent);
        continueRow = continueButton.gameObject;
        continueButton.onClick.AddListener(HandleContinueClicked);

        TMP_Text continueHeading = CreateText("Heading", continueContent, "CONTINUE", 16f,
            TextAlignmentOptions.MidlineLeft,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.75f));
        continueHeading.characterSpacing = 3f;
        SetCorner(continueHeading.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 28f));

        continueDetail = CreateText("Detail", continueContent, "No Progress", 24f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        SetCorner(continueDetail.rectTransform, new Vector2(0f, 0.5f), new Vector2(124f, 0f), new Vector2(260f, 30f));

        continueStamp = CreateText("Stamp", continueContent, string.Empty, 14f,
            TextAlignmentOptions.MidlineRight,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.5f));
        SetCorner(continueStamp.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(160f, 24f));

        PauseMenuRow continueFeedback = continueButton.GetComponent<PauseMenuRow>();
        continueFeedback.Configure(continueContent, continueDetail, null, 12f, 1f, 0.995f);

        backButton = CreateMenuRow(slotsSection.transform, "Back", -246f, 40f, 24f);
        backButton.onClick.AddListener(HandleBack);
    }

    void BuildConfirmSection()
    {
        confirmSection = CreateSection("Confirm Section", card);

        TMP_Text message = CreateText("Message", confirmSection.transform,
            "Return to the main menu?\nYour saved games are kept, but progress since your last save is lost.",
            23f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.72f));
        SetAnchored(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(650f, 92f));

        confirmReturnButton = CreateMenuRow(confirmSection.transform, "Return to Main Menu", -30f, 64f, 32f);
        confirmReturnButton.onClick.AddListener(HandleReturnToMainMenu);

        Button cancel = CreateMenuRow(confirmSection.transform, "Cancel", -108f, 64f, 32f);
        cancel.onClick.AddListener(HandleCancelClicked);
    }

    void BuildDeleteSection()
    {
        deleteSection = CreateSection("Delete Section", card);

        deleteMessage = CreateText("Message", deleteSection.transform, string.Empty, 22f,
            TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.72f));
        SetAnchored(deleteMessage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 74f), new Vector2(660f, 124f));

        deleteConfirmButton = CreateMenuRow(deleteSection.transform, "Delete Slot", -46f, 64f, 30f);
        deleteConfirmButton.onClick.AddListener(HandleConfirmDelete);

        deleteCancelButton = CreateMenuRow(deleteSection.transform, "Cancel", -124f, 64f, 30f);
        deleteCancelButton.onClick.AddListener(HandleCancelDelete);
    }

    void HandleSaveGameClicked()
    {
        OpenSlots(true);
    }

    void HandleLoadGameClicked()
    {
        OpenSlots(false);
    }

    void HandleMainMenuClicked()
    {
        ShowPage(Page.Confirm);
    }

    void HandleCancelClicked()
    {
        ShowPage(Page.Main);
    }

    static TMP_FontAsset ResolveFont()
    {
        // The game's UI all uses the same Bubble Sans asset, which is loaded with the scene but
        // is not under a Resources folder. Reusing the font the scene is already drawing with
        // keeps the pause menu in the same voice instead of falling back to the TMP default.
        TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        TMP_FontAsset fallback = null;

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null || texts[i].font == null)
            {
                continue;
            }

            TMP_FontAsset candidate = texts[i].font;

            if (candidate.name.IndexOf("Bubble", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

        for (int i = 0; i < fonts.Length; i++)
        {
            TMP_FontAsset candidate = fonts[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.name.IndexOf("Bubble", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return candidate;
            }

            if (fallback == null)
            {
                fallback = candidate;
            }
        }

        return fallback != null ? fallback : TMP_Settings.defaultFontAsset;
    }

    Button CreateMenuRow(Transform parent, string label, float y, float height, float fontSize)
    {
        RectTransform content;
        Button button = CreateRow(parent, label, y, height, out content);

        TMP_Text text = CreateText("Label", content, label, fontSize, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform);

        PauseMenuRow feedback = button.GetComponent<PauseMenuRow>();
        feedback.Configure(content, text, null, 16f, 1.03f, 0.99f);

        return button;
    }

    Button CreateRow(Transform parent, string name, float y, float height, out RectTransform content)
    {
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        row.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)row.transform;
        SetAnchored(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(RowWidth, height));

        Image hit = row.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        Button button = row.GetComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        content = CreateRect("Content", row.transform);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(26f, 0f);
        content.offsetMax = new Vector2(-26f, 0f);

        row.AddComponent<PauseMenuRow>();
        row.AddComponent<ButtonSfx>();

        return button;
    }

    Button CreateDeleteButton(Transform parent, string name, Vector2 position)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)buttonObject.transform;
        SetCorner(rect, new Vector2(1f, 0.5f), position, new Vector2(74f, 18f));

        Image hit = buttonObject.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;

        Image underline = CreateImage("Underline", buttonObject.transform, null,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.35f),
            Image.Type.Simple, false);
        SetCorner(underline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(58f, 1f));

        TMP_Text label = CreateText("Label", buttonObject.transform, "DELETE", 11f,
            TextAlignmentOptions.Center,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, 0.7f));
        Stretch(label.rectTransform);

        PauseMenuRow feedback = buttonObject.AddComponent<PauseMenuRow>();
        feedback.Configure(rect, label, underline, 0f, 1.04f, 0.97f);

        buttonObject.AddComponent<ButtonSfx>();
        return button;
    }

    void CreateSeparator(Transform parent, float y, float alpha)
    {
        Image line = CreateImage("Separator", parent, null,
            new Color(PauseMenuArt.Accent.r, PauseMenuArt.Accent.g, PauseMenuArt.Accent.b, alpha),
            Image.Type.Simple, false);

        SetAnchored(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(RowWidth, 1f));
    }

    TMP_Text CreateText(string name, Transform parent, string content, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        textObject.transform.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();

        if (font != null)
        {
            text.font = font;
        }

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.margin = Vector4.zero;

        return text;
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.raycastTarget = raycastTarget;

        return image;
    }

    static GameObject CreateSection(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        Stretch(rect);
        return rect.gameObject;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return (RectTransform)rectObject.transform;
    }

    static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void SetCorner(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, float inflate = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-inflate, -inflate);
        rect.offsetMax = new Vector2(inflate, inflate);
    }
}
