using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Turns a scene's dialogue choices into a centred, Persona-style pick list. The three options sit
/// in the middle of the screen in the game's bright blue, over a dimmed scene, so the decision owns
/// the moment: nothing behind the veil can be clicked, and the only way forward is to pick.
///
/// The option under the pointer (or the gamepad selection) flips to the pale accent colour with
/// deep navy text and slides over, while the other options recede. The picks arrive with a small
/// pop and keep a slow accent glow until one of them is taken.
///
/// A hidden bootstrap finds the authored ChoiceButton objects as each scene loads, so no scene
/// asset or story script needs changing and every existing click handler keeps working.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class DialogueChoiceOption : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    const float RowWidth = 520f;
    const float RowHeight = 96f;
    const float RowGap = 112f;
    const float LabelPadding = 24f;

    /// <summary>How dark the scene goes while a pick is waiting.</summary>
    const float ScrimAlpha = 0.76f;

    /// <summary>Smaller values snap the highlight to the pointer faster.</summary>
    const float ResponseTime = 0.07f;

    /// <summary>The bright blue the choices were authored with, kept as their resting colour.</summary>
    static readonly Color BrightBlue = new Color(0f, 0.354f, 1f, 1f);
    static readonly Color DeepBlue = new Color(0f, 0.13f, 0.47f, 1f);
    static readonly Color Accent = new Color(0.66f, 0.8f, 1f, 1f);
    static readonly Color Ink = new Color(0.01f, 0.05f, 0.16f, 1f);
    static readonly Color ScrimColor = new Color(0f, 0.012f, 0.045f, 1f);

    static readonly List<DialogueChoiceOption> live = new List<DialogueChoiceOption>();
    static DialogueChoiceOption focused;

    static DialogueChoiceScrim scrim;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        ResetStatics();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetStatics();

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            // The choices start hidden, so they only come into being when a scene offers them.
            Button[] buttons = roots[i].GetComponentsInChildren<Button>(true);

            for (int j = 0; j < buttons.Length; j++)
            {
                Button button = buttons[j];

                if (!button.name.StartsWith("ChoiceButton", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (button.GetComponent<DialogueChoiceOption>() == null)
                {
                    button.gameObject.AddComponent<DialogueChoiceOption>();
                }
            }
        }
    }

    static void ResetStatics()
    {
        live.Clear();
        focused = null;
        scrim = null;
    }

    Button button;
    TMP_Text label;
    Image plate;
    Image halo;
    Image fill;
    Image rim;
    int rowIndex;
    float focus;
    float muted;
    float pop;
    bool hovering;

    void Awake()
    {
        button = GetComponent<Button>();

        ApplyLayout();
        BuildLook();
    }

    void OnEnable()
    {
        if (!live.Contains(this))
        {
            live.Add(this);
        }

        // The picks arrive with a small pop of their own, on top of the fade the scene gives them.
        pop = 0f;

        if (live.Count == 1)
        {
            ShowScrim(this);
        }
    }

    void OnDisable()
    {
        live.Remove(this);

        if (focused == this)
        {
            focused = null;
        }

        hovering = false;
        focus = 0f;
        muted = 0f;

        if (live.Count == 0)
        {
            HideScrim();
        }
    }

    void Update()
    {
        if (label == null && fill == null)
        {
            return;
        }

        bool usable = button == null || button.interactable;
        bool highlighted = hovering && usable;

        float targetFocus = highlighted ? 1f : 0f;
        float targetMute = focused != null && focused != this ? 1f : 0f;

        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / ResponseTime);

        focus = Mathf.Lerp(focus, targetFocus, blend);
        muted = Mathf.Lerp(muted, targetMute, blend);

        pop = Mathf.MoveTowards(pop, 1f, Time.unscaledDeltaTime / 0.22f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, 1f - Mathf.Pow(1f - pop, 3f));

        // A slow breath of blue behind the plate keeps the picks the brightest thing on screen
        // while a decision is open; each row is offset so the stack does not pulse in lockstep.
        if (halo != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((Time.unscaledTime + rowIndex * 0.6f) * 2.2f);

            Color color = BrightBlue;
            color = Color.Lerp(color, Accent, 0.35f);
            color.a = Mathf.Lerp(0.16f + 0.12f * pulse, 0.45f, focus) * Mathf.Lerp(1f, 0.35f, muted);
            halo.color = color;
        }

        // The plate darkens slightly when another option is focused, so the list reads as one
        // choice at a time rather than three equal buttons.
        if (plate != null)
        {
            Color color = plate.color;
            color.a = Mathf.Lerp(0.95f, 1f, focus) * Mathf.Lerp(1f, 0.6f, muted);
            plate.color = color;
        }

        if (fill != null)
        {
            Color color = Accent;
            color.a = focus * 0.94f;
            fill.color = color;
        }

        if (rim != null)
        {
            Color color = Color.Lerp(new Color(0.5f, 0.72f, 1f, 1f), Accent, focus);
            color.a = Mathf.Lerp(0.72f, 0.95f, focus) * Mathf.Lerp(1f, 0.5f, muted);
            rim.color = color;
        }

        if (label != null)
        {
            Color color = Color.Lerp(Color.white, Ink, focus);
            color.a = Mathf.Lerp(1f, 0.35f, muted);
            label.color = color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        focused = this;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;

        if (focused == this)
        {
            focused = null;
        }
    }

    /// <summary>Keyboard and gamepad selection light the option up the same way the pointer does.</summary>
    public void OnSelect(BaseEventData eventData)
    {
        hovering = true;
        focused = this;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        hovering = false;

        if (focused == this)
        {
            focused = null;
        }
    }

    void ApplyLayout()
    {
        rowIndex = ReadIndex();

        RectTransform rect = (RectTransform)transform;

        // Dead centre, stacked upwards and downwards around the middle of the screen. The dimmed
        // scene behind them is what gives the stack its room, so the picks can be this large.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, (1 - rowIndex) * RowGap);
        rect.sizeDelta = new Vector2(RowWidth, RowHeight);
    }

    /// <summary>The authored buttons are numbered; that order is what stacks them around the middle.</summary>
    int ReadIndex()
    {
        string label = this.name;
        int last = label.Length - 1;

        if (last >= 0 && label[last] >= '1' && label[last] <= '9')
        {
            return label[last] - '1';
        }

        return 0;
    }

    void BuildLook()
    {
        // The authored slab becomes the plate itself; the button's own colour tint is switched
        // off so the highlight below owns the colour instead of being overridden each state.
        plate = GetComponent<Image>();

        if (plate != null)
        {
            plate.sprite = PauseMenuArt.Plate;
            plate.type = Image.Type.Sliced;
            plate.color = new Color(1f, 1f, 1f, 0.95f);
            plate.raycastTarget = true;

            if (plate.GetComponent<UIVertexGradient>() == null)
            {
                UIVertexGradient gradient = plate.gameObject.AddComponent<UIVertexGradient>();
                gradient.SetColors(BrightBlue, DeepBlue);
            }
        }

        if (button != null)
        {
            button.transition = Selectable.Transition.None;
        }

        // Drawn over the plate, in this order: the halo, the selected fill, its rim, and finally
        // the label, which is moved to the end of the children so it stays readable above them.
        halo = CreateLayer("Halo", PauseMenuArt.Plate, Image.Type.Sliced,
            new Color(BrightBlue.r, BrightBlue.g, BrightBlue.b, 0.18f), 10f);
        fill = CreateLayer("Selected Fill", PauseMenuArt.Plate, Image.Type.Sliced,
            new Color(Accent.r, Accent.g, Accent.b, 0f), 0f);
        rim = CreateLayer("Rim", PauseMenuArt.PlateFrame, Image.Type.Sliced,
            new Color(0.5f, 0.72f, 1f, 0.72f), 0f);

        label = GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            return;
        }

        label.transform.SetAsLastSibling();
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 1.5f;

        // A long pick shrinks a little rather than running past the plate.
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 28f;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(LabelPadding, 0f);
        labelRect.offsetMax = new Vector2(-LabelPadding, 0f);
    }

    Image CreateLayer(string name, Sprite sprite, Image.Type type, Color color, float expand)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        go.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-expand, -expand);
        rect.offsetMax = new Vector2(expand, expand);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ---------------------------------------------------------------- the dimmed scene

    /// <summary>
    /// A full-screen veil over the scene's own UI. It swallows clicks, so the only way on is to
    /// take one of the picks, and it lifts again as soon as the last option is put away.
    /// </summary>
    static void ShowScrim(DialogueChoiceOption source)
    {
        if (scrim == null)
        {
            scrim = BuildScrim(source);
        }

        if (scrim != null)
        {
            scrim.Show(ScrimAlpha);
        }
    }

    static void HideScrim()
    {
        if (scrim != null)
        {
            scrim.Hide();
        }
    }

    static DialogueChoiceScrim BuildScrim(DialogueChoiceOption source)
    {
        Transform host = RootCanvasTransform(source);

        if (host == null)
        {
            return null;
        }

        GameObject go = new GameObject(
            "Choice Dimmer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(DialogueChoiceScrim));

        go.transform.SetParent(host, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-40f, -40f);
        rect.offsetMax = new Vector2(40f, 40f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(ScrimColor.r, ScrimColor.g, ScrimColor.b, 0f);
        image.raycastTarget = true;

        // The veil sits over the dialogue box and everything else, but under the picks themselves
        // so they stay lit and clickable.
        int index = host.childCount;

        for (int i = 0; i < host.childCount; i++)
        {
            Transform child = host.GetChild(i);

            if (child != go.transform
                && child.name.StartsWith("ChoiceButton", System.StringComparison.Ordinal))
            {
                index = Mathf.Min(index, i);
            }
        }

        go.transform.SetSiblingIndex(index);

        return go.GetComponent<DialogueChoiceScrim>();
    }

    static Transform RootCanvasTransform(DialogueChoiceOption source)
    {
        Canvas[] canvases = source.GetComponentsInParent<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].isRootCanvas)
            {
                return canvases[i].transform;
            }
        }

        return canvases.Length > 0 && canvases[0] != null ? canvases[0].transform : source.transform.root;
    }
}
