using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Scene03Events : MonoBehaviour
{
    public GameObject fadeScreenIn;
    public GameObject textBox;
    [SerializeField] private string speakerName = "You";

    [SerializeField] string textToSpeak;
    [SerializeField] int currentTextLength;
    [SerializeField] int textLength;
    [SerializeField] GameObject mainTextObject;
    [SerializeField] GameObject nextButton;
    [SerializeField] int eventPos = 0;
    [SerializeField] GameObject charName;
    [SerializeField] GameObject fadeOut;

    int introIndex = 0;

    void Update()
    {
        textLength = TextCreator.charCount;
    }

    void Start()
    {
        StartCoroutine(EventStarter());
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
        yield return new WaitForSeconds(2f);

        mainTextObject.SetActive(true);
        textBox.SetActive(true);
        charName.GetComponent<TMPro.TMP_Text>().text = speakerName;

        if (TryRestoreCheckpoint())
        {
            yield break;
        }

        introIndex = 0;
        yield return StartCoroutine(PlayLine(introIndex));

        eventPos = 0;
    }

    IEnumerator PlayLine(int index)
    {
        nextButton.SetActive(false);

        switch (index)
        {
            case 0:
                textToSpeak = "[You arrived to your apartment after hanging out with Koda]";
                break;
            case 1:
                textToSpeak = "[He quickly scurried back home, as it had gotten pretty late]";
                break;
            case 2:
                textToSpeak = "[Your mind drifted back to that mysterious call…]";
                break;
            case 3:
                textToSpeak = "[Who was that person who called you? How did they find your number?]";
                break;
            case 4:
                textToSpeak = "[Your mind was clouded with unanswered questions]";
                break;
            case 5:
                textToSpeak = "[You decided to shrug it off for the time being.]";
                break;
            case 6:
                textToSpeak = "[What an odd first day of school…]";
                break;
        }

        charName.GetComponent<TMPro.TMP_Text>().text = speakerName;
        textBox.GetComponent<TMPro.TMP_Text>().text = textToSpeak;
        DialogueCheckpoint.Report("line:" + index, speakerName, textToSpeak);

        currentTextLength = textToSpeak.Length;
        TextCreator.runTextPrint = true;

        yield return new WaitForSeconds(0.05f);
        yield return new WaitForSeconds(1f);
        yield return new WaitUntil(() => textLength >= currentTextLength);
        yield return new WaitForSeconds(0.5f);

        nextButton.SetActive(true);
    }

    bool TryRestoreCheckpoint()
    {
        if (!DialogueCheckpoint.TryTakePending(out string state, out _, out _))
        {
            return false;
        }

        string[] parts = state.Split(':');
        int index;

        if (parts.Length == 2 && parts[0] == "line" && int.TryParse(parts[1], out index) && index >= 0 && index <= 6)
        {
            eventPos = 0;
            introIndex = index;
            StartCoroutine(PlayLine(index));
            return true;
        }

        return false;
    }

    IEnumerator EventFour()
    {
        nextButton.SetActive(false);
        textBox.SetActive(true);
        yield return TextBoxFader.Hide(mainTextObject);
        fadeOut.SetActive(true);

        yield return SceneLoader.LoadSceneAsync(11, 4f);
    }

    public void NextButton()
    {
        if (eventPos == 0)
        {
            introIndex++;
            if (introIndex <= 6)
            {
                StartCoroutine(PlayLine(introIndex));
            }
            else
            {
                eventPos = 4;
                StartCoroutine(EventFour());
            }
            return;
        }

        if (eventPos == 4)
        {
            StartCoroutine(EventFour());
        }
    }
}