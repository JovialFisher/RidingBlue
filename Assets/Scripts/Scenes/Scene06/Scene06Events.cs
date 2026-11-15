using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class Scene06Events : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource bgm;
    [SerializeField] private AudioSource studentChatter;

    [Header("SFX")]
    [SerializeField] private AudioSource phonering;

    [Header("Character Expressions")]
    public GameObject charNeutral;
    public GameObject charSurprised;
    public GameObject charSmirk;
    public GameObject charSmile;
    public GameObject charEmbarrassed;

    public GameObject fadeScreenIn;
    public GameObject textBox;
    [SerializeField] string textToSpeak;
    [SerializeField] int currentTextLength;
    [SerializeField] int textLength;
    [SerializeField] GameObject mainTextObject;
    [SerializeField] GameObject nextButton;
    [SerializeField] int eventPos = 0;
    [SerializeField] GameObject charName;
    [SerializeField] GameObject fadeOut;

    [Header("Choice Buttons")]
    [SerializeField] Button choiceButton1;
    [SerializeField] Button choiceButton2;
    [SerializeField] Button choiceButton3;

    [Header("Choice Lines")]
    private string[] choiceLines;
    private int currentLine = 0;
    private int currentChoice = 0;

    // --- Dialogue segment arrays and indices ---
    private string[] transitionLines =
    {
        "Anyways dude, about what happened to me...",
        "I told you about it on the train, but last night it was even worse. Everything was blue, the streets were completely empty.",
        "I wanted a better look, but I was too scared the fog would get in and...",
        "[he shudders at the thought] I just went back to sleep, I thought I was having a dream. I hope it stays a dream...",
        "[While Koda was going on about his paranormal experience, your phone started ringing. It was that white feline from yesterday again. You decided to answer.]",
    };
    private int transitionLineIndex = 0;

    private string[] phoneCallLines =
    {
        "H- hel-llo?",
        "As--a, can y-ou h--he-ar me?",
        "I--I got a m-mess--age for y-ou!",
        "H-e--lp! H-h-e found m-"
    };
    private int phoneCallLineIndex = 0;

    private string[] phoneDisconnectLines =
    {
        "[The call was suddenly disconnected. You looked at your phone for a long time. It was definitely Midnight, but this time he seemed to be in trouble.]"
    };

    private string[] kodaConcernLines =
    {
        "Wow, you got another call bro? Two days in a row, that's strange.",
        "By the looks on your face, it ain't a prank call.",
        "[He looks at you as you tell him what had happened]",
        "You believe that it was the same cat from yesterday, so you weren't lying...",
        "Can I, come to your place? I wanna see it for myself, you got me real curious!",
    };
    private int kodaConcernLineIndex = 0;

    private string[] finalNarrationLines =
    {
        "[Koda was fully invested in seeing Midnight at night, so he agreed to come to your apartment at night to see the occurrence.]"
    };

    int introIndex = 0;
    bool isTyping = false; // Prevents spamming Next

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        // --- Audio ----
        if (bgm != null)
        {
            bgm.volume = 0.7f;
            bgm.loop = true;
            if (!bgm.isPlaying) bgm.Play();
        }
        if (studentChatter != null)
        {
            studentChatter.volume = 0.1f;
            studentChatter.loop = true;
            if (!studentChatter.isPlaying) studentChatter.Play();
        }
        // --------------

        StartCoroutine(EventStarter());
        choiceButton1.onClick.AddListener(OnChoice1);
        choiceButton2.onClick.AddListener(OnChoice2);
        choiceButton3.onClick.AddListener(OnChoice3);
        nextButton.SetActive(false); // Ensure it's hidden at start
    }

    GameObject[] AllExpressions()
    {
        return new GameObject[]
        {
            charNeutral,
            charSurprised,
            charSmirk,
            charSmile,
            charEmbarrassed
        };
    }

    void HideAllExpressions()
    {
        ExpressionFader.HideAll(AllExpressions());
    }

    void ShowExpression(string expression, float duration = ExpressionFader.DefaultDuration)
    {
        GameObject target = null;

        switch (expression)
        {
            case "neutral":     target = charNeutral; break;
            case "surprised":   target = charSurprised; break;
            case "smirk":       target = charSmirk; break;
            case "smile":       target = charSmile; break;
            case "embarrassed": target = charEmbarrassed; break;
        }

        ExpressionFader.Show(target, AllExpressions(), duration);
    }

    IEnumerator EventStarter()
    {
        if (DialogueCheckpoint.HasPendingForCurrentScene)
        {
            fadeScreenIn.SetActive(false);
            mainTextObject.SetActive(true);
            textBox.SetActive(true);

            if (TryRestoreCheckpoint())
            {
                yield break;
            }
        }

        yield return new WaitForSeconds(2f);
        fadeScreenIn.SetActive(false);
        ShowExpression("neutral");
        yield return new WaitForSeconds(2f);
        mainTextObject.SetActive(true);
        textBox.SetActive(true);
        charName.GetComponent<TMP_Text>().text = "Koda";

        if (TryRestoreCheckpoint())
        {
            yield break;
        }

        introIndex = 0;
        yield return StartCoroutine(PlayIntroLine(introIndex));
        eventPos = 0;
    }

    IEnumerator PlayIntroLine(int index)
    {
        nextButton.SetActive(false);
        isTyping = true;
        switch (index)
        {
            case 0:
                ShowExpression("smirk");
                textToSpeak = "Yo bro, thanks for the homework answers. I owe you one!";
                break;
            case 1:
                ShowExpression("smile");
                textToSpeak = "I thought I was looking at hieroglyphics, but that's just math I guess.";
                break;
            case 2:
                ShowExpression("neutral");
                textToSpeak = "[He said, his smile fading a little]";
                break;
            case 3:
                ShowExpression("embarrassed");
                textToSpeak = "Oh yeah, I've been trying to sit down and study but I just can't focus. My console was calling my name...";
                break;
            case 4:
                ShowExpression("smirk");
                textToSpeak = "Everyone is speaking about how smart you are, but I know the truth. You just have a photographic memory, right? You don't even have to try to remember stuff.";
                break;
            case 5:
                ShowExpression("smirk");
                textToSpeak = "[He looks at you with a teasing smirk, waiting for an answer.]";
                break;
        }
        charName.GetComponent<TMP_Text>().text = "Koda";
        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("intro:" + index, charName.GetComponent<TMP_Text>().text, textToSpeak);
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);
        nextButton.SetActive(true);
        isTyping = false;
    }

    IEnumerator ShowChoiceButtons()
    {
        nextButton.SetActive(false);

        // Let the dialogue box go out first, then bring the options in.
        yield return TextBoxFader.Hide(mainTextObject);
        textBox.SetActive(false);

        yield return UIFade.RiseIn(choiceButton1.gameObject, choiceButton2.gameObject, choiceButton3.gameObject);
    }

    void HideChoiceButtons()
    {
        StartCoroutine(UIFade.FadeOutAndHide(choiceButton1.gameObject, choiceButton2.gameObject, choiceButton3.gameObject));
    }

    public void OnChoice1()
    {
        HideChoiceButtons();
        StartChoiceDialogue(0);
    }

    public void OnChoice2()
    {
        HideChoiceButtons();
        StartChoiceDialogue(1);
    }

    public void OnChoice3()
    {
        HideChoiceButtons();
        StartChoiceDialogue(2);
    }

    bool TryRestoreCheckpoint()
    {
        if (!DialogueCheckpoint.TryTakePending(out string state, out _, out _))
        {
            return false;
        }

        string[] parts = state.Split(':');
        int index;

        if (parts.Length == 2 && parts[0] == "intro" && int.TryParse(parts[1], out index) && index >= 0 && index <= 5)
        {
            eventPos = 0;
            introIndex = index;
            StartCoroutine(PlayIntroLine(index));
            return true;
        }

        if (parts.Length == 3 && parts[0] == "choice"
            && int.TryParse(parts[1], out int choice) && choice >= 0 && choice <= 2
            && int.TryParse(parts[2], out index) && index >= 0)
        {
            StartChoiceDialogue(choice, index);
            return true;
        }

        if (parts.Length == 2 && parts[0] == "transition" && int.TryParse(parts[1], out index)
            && index >= 0 && index < transitionLines.Length)
        {
            eventPos = 2;
            transitionLineIndex = index;
            StartCoroutine(DisplayTransitionLine(index));
            return true;
        }

        if (parts.Length == 2 && parts[0] == "phone" && int.TryParse(parts[1], out index)
            && index >= 0 && index < phoneCallLines.Length)
        {
            eventPos = 3;
            phoneCallLineIndex = index;
            StartCoroutine(DisplayPhoneCallLine(index));
            return true;
        }

        if (parts.Length == 1 && parts[0] == "disconnect")
        {
            eventPos = 4;
            StartCoroutine(DisplayPhoneDisconnectLine());
            return true;
        }

        if (parts.Length == 2 && parts[0] == "concern" && int.TryParse(parts[1], out index)
            && index >= 0 && index < kodaConcernLines.Length)
        {
            eventPos = 5;
            kodaConcernLineIndex = index;
            StartCoroutine(DisplayKodaConcernLine(index));
            return true;
        }

        if (parts.Length == 1 && parts[0] == "final")
        {
            eventPos = 6;
            StartCoroutine(DisplayFinalNarrationLine());
            return true;
        }

        return false;
    }

    void StartChoiceDialogue(int choice, int startLine = 0)
    {
        textBox.SetActive(true);
        mainTextObject.SetActive(true);
        nextButton.SetActive(false);

        switch (choice)
        {
            case 0:
                ShowExpression("smirk");
                choiceLines = new string[]
                {
                    "Yeah, that's what I'm saying. You're like a human hard drive or something dude!",
                    "[with an exaggerated tone] Oh lord Asa, please spread your wisdom to all of us lowlifes",
                    "[He looked around to see if anyone was listening, then smirked at you]",
                    "See, even they know you're a genius...",
                    "They totally aren't looking at me because I'm the one who's talking loudly, right? [laughs nervously]",
                };
                break;
            case 1:
                ShowExpression("smirk");
                choiceLines = new string[]
                {
                    "Come on dude, you're lying to yourself. You'll probably be the one with the highest grade on the test next week.",
                    "Compared to me, you'll at least be at the top of the class.",
                    "[laughs somewhat nervously] I really have to step up my game, huh? I can't let you outshine me like that...",
                };
                break;
            case 2:
                ShowExpression("surprised");
                choiceLines = new string[]
                {
                    "[with an exaggerated tone] WHAT?! No way, the great Asa gets his great knowledge by copying off people?",
                    "Hehe, I know you're lying. You literally gave me the answers, and I doubt you know someone enough to copy off them.",
                    "Excuses, excuses... [chuckles]",
                };
                break;
        }
        if (choiceLines == null || choiceLines.Length == 0)
        {
            return;
        }

        currentChoice = choice;
        eventPos = 1; // Set event state to choice dialogue
        currentLine = Mathf.Clamp(startLine, 0, choiceLines.Length - 1);
        StartCoroutine(DisplayChoiceLine(currentLine));
    }

    IEnumerator DisplayChoiceLine(int lineIndex)
    {
        nextButton.SetActive(false);
        isTyping = true;
        if (lineIndex >= 0 && lineIndex < choiceLines.Length)
        {
            textToSpeak = choiceLines[lineIndex];
            charName.GetComponent<TMP_Text>().text = "Koda";
            textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("choice:" + currentChoice + ":" + lineIndex, charName.GetComponent<TMP_Text>().text, textToSpeak);
            currentTextLength = textToSpeak.Length;
            TextCreator.runTextPrint = true;

            yield return new WaitForSeconds(0.05f);
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => textLength >= currentTextLength);
            yield return new WaitForSeconds(0.5f);
            nextButton.SetActive(true);
        }
        isTyping = false;
    }

    IEnumerator DisplayTransitionLine(int lineIndex)
    {
        nextButton.SetActive(false);
        isTyping = true;

        ShowExpression("neutral");
        if (lineIndex >= 0 && lineIndex < transitionLines.Length)
        {
            textToSpeak = transitionLines[lineIndex];
            // --- Always show "Koda" as per your new requirement ---
            charName.GetComponent<TMP_Text>().text = "Koda";

            textBox.SetActive(true);
            mainTextObject.SetActive(true);
            textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("transition:" + lineIndex, charName.GetComponent<TMP_Text>().text, textToSpeak);
            currentTextLength = textToSpeak.Length;
            TextCreator.runTextPrint = true;

            // Special action: phone rings on the last transition line, and fade BGM
            if (lineIndex == transitionLines.Length - 1)
            {
                StartCoroutine(FadeBGMVolume(bgm, 0.3f, 2f)); // fade bgm to 0.3 over 2s
                StartCoroutine(PlayPhoneRingTwice());
            }

            yield return new WaitForSeconds(0.05f);
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => textLength >= currentTextLength);
            yield return new WaitForSeconds(0.5f);
            nextButton.SetActive(true);
        }
        isTyping = false;
    }

    IEnumerator DisplayPhoneCallLine(int lineIndex)
    {
        nextButton.SetActive(false);
        isTyping = true;
        ShowExpression("embarrassed"); // Optional: show a distressed/embarrassed face for "Unknown"
        if (lineIndex >= 0 && lineIndex < phoneCallLines.Length)
        {
            textToSpeak = phoneCallLines[lineIndex];
            charName.GetComponent<TMP_Text>().text = "Midnight";
            textBox.SetActive(true);
            mainTextObject.SetActive(true);
            textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("phone:" + lineIndex, charName.GetComponent<TMP_Text>().text, textToSpeak);
            currentTextLength = textToSpeak.Length;
            TextCreator.runTextPrint = true;

            yield return new WaitForSeconds(0.05f);
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => textLength >= currentTextLength);
            yield return new WaitForSeconds(0.5f);
            nextButton.SetActive(true);
        }
        isTyping = false;
    }

    IEnumerator DisplayPhoneDisconnectLine()
    {
        nextButton.SetActive(false);
        isTyping = true;
        ShowExpression("neutral");
        textToSpeak = phoneDisconnectLines[0];
        charName.GetComponent<TMP_Text>().text = "";
        textBox.SetActive(true);
        mainTextObject.SetActive(true);
        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("disconnect", charName.GetComponent<TMP_Text>().text, textToSpeak);
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);
        nextButton.SetActive(true);

        isTyping = false;
    }

    IEnumerator DisplayKodaConcernLine(int lineIndex)
    {
        nextButton.SetActive(false);
        isTyping = true;
        ShowExpression("smile");
        if (lineIndex >= 0 && lineIndex < kodaConcernLines.Length)
        {
            textToSpeak = kodaConcernLines[lineIndex];
            charName.GetComponent<TMP_Text>().text = "Koda";
            textBox.SetActive(true);
            mainTextObject.SetActive(true);
            textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("concern:" + lineIndex, charName.GetComponent<TMP_Text>().text, textToSpeak);
            currentTextLength = textToSpeak.Length;
            TextCreator.runTextPrint = true;

            yield return new WaitForSeconds(0.05f);
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => textLength >= currentTextLength);
            yield return new WaitForSeconds(0.5f);
            nextButton.SetActive(true);
        }
        isTyping = false;
    }

    IEnumerator DisplayFinalNarrationLine()
    {
        nextButton.SetActive(false);
        isTyping = true;
        ShowExpression("neutral");
        textToSpeak = finalNarrationLines[0];
        charName.GetComponent<TMP_Text>().text = "";
        textBox.SetActive(true);
        mainTextObject.SetActive(true);
        textBox.GetComponent<TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("final", charName.GetComponent<TMP_Text>().text, textToSpeak);
        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);
        nextButton.SetActive(true);

        isTyping = false;
    }

    // NextButton to be called by Unity UI OnClick
    public void NextButton()
    {
        if (isTyping) return; // Don’t skip the typing animation

        if (eventPos == 0)
        {
            introIndex++;
            if (introIndex <= 5)
            {
                StartCoroutine(PlayIntroLine(introIndex));
            }
            else
            {
                eventPos = 1;
                StartCoroutine(ShowChoiceButtons());
            }
            return;
        }

        if (eventPos == 1)
        {
            if (currentLine < choiceLines.Length - 1)
            {
                currentLine++;
                StartCoroutine(DisplayChoiceLine(currentLine));
            }
            else
            {
                eventPos = 2;
                nextButton.SetActive(false);
                transitionLineIndex = 0;
                StartCoroutine(DisplayTransitionLine(transitionLineIndex));
            }
            return;
        }

        // Handle transition message lines ("Koda monologue + phone rings")
        if (eventPos == 2)
        {
            transitionLineIndex++;
            if (transitionLineIndex < transitionLines.Length)
            {
                StartCoroutine(DisplayTransitionLine(transitionLineIndex));
            }
            else
            {
                eventPos = 3;
                nextButton.SetActive(false);
                phoneCallLineIndex = 0;
                StartCoroutine(DisplayPhoneCallLine(phoneCallLineIndex));
            }
            return;
        }

        // Handle "Unknown" phone call lines
        if (eventPos == 3)
        {
            phoneCallLineIndex++;
            if (phoneCallLineIndex < phoneCallLines.Length)
            {
                StartCoroutine(DisplayPhoneCallLine(phoneCallLineIndex));
            }
            else
            {
                eventPos = 4;
                nextButton.SetActive(false);
                StartCoroutine(DisplayPhoneDisconnectLine());
            }
            return;
        }

        // Handle disconnected narration
        if (eventPos == 4)
        {
            eventPos = 5;
            nextButton.SetActive(false);
            kodaConcernLineIndex = 0;
            StartCoroutine(DisplayKodaConcernLine(kodaConcernLineIndex));
            return;
        }

        // Koda's post-call concern lines
        if (eventPos == 5)
        {
            kodaConcernLineIndex++;
            if (kodaConcernLineIndex < kodaConcernLines.Length)
            {
                StartCoroutine(DisplayKodaConcernLine(kodaConcernLineIndex));
            }
            else
            {
                eventPos = 6;
                nextButton.SetActive(false);
                StartCoroutine(DisplayFinalNarrationLine());
            }
            return;
        }

        // Final narration, then fade out and change scene
        if (eventPos == 6)
        {
            eventPos = 7;
            nextButton.SetActive(false);
            StartCoroutine(EventFour());
            return;
        }
    }

    IEnumerator EventFour()
    {
        nextButton.SetActive(false);
        ShowExpression("smile");
        textBox.SetActive(true);
        yield return TextBoxFader.Hide(mainTextObject);
        fadeOut.SetActive(true);
        yield return SceneLoader.LoadSceneAsync(13, 4f);
    }

    // ========== NEW HELPERS ==========

    IEnumerator FadeBGMVolume(AudioSource audioSource, float targetVolume, float fadeDuration)
    {
        if (audioSource == null) yield break;
        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / fadeDuration);
            yield return null;
        }
        audioSource.volume = targetVolume;
    }

    IEnumerator PlayPhoneRingTwice()
    {
        if (phonering == null || phonering.clip == null) yield break;
        int times = 2;
        for (int i = 0; i < times; i++)
        {
            phonering.Play();
            yield return new WaitForSeconds(phonering.clip.length);
        }
    }
}