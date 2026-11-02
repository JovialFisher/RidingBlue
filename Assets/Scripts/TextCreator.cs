using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextCreator : MonoBehaviour
{
    public static TMPro.TMP_Text viewText;

    public static bool runTextPrint;
    public static int charCount;
    [SerializeField, Min(1f)] float charactersPerSecond = 34f;
    [SerializeField] string transferText;
    [SerializeField] int internalCount;

    TMPro.TMP_Text textComponent;
    Coroutine revealCoroutine;

    void Awake()
    {
        textComponent = GetComponent<TMPro.TMP_Text>();
        if (textComponent == null)
        {
            Debug.LogError("TextCreator requires a TMP_Text component on the same GameObject.", this);
        }
    }

    // Update is called once per frame
    void Update()
    {
        internalCount = charCount;
        if (!runTextPrint)
        {
            return;
        }

        runTextPrint = false;
        if (textComponent == null)
        {
            charCount = int.MaxValue;
            return;
        }

        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
        }

        viewText = textComponent;
        transferText = textComponent.text;
        textComponent.ForceMeshUpdate();
        int visibleCharacterCount = textComponent.textInfo.characterCount;
        textComponent.maxVisibleCharacters = 0;
        charCount = 0;
        if (visibleCharacterCount == 0)
        {
            textComponent.maxVisibleCharacters = int.MaxValue;
            charCount = transferText.Length;
            revealCoroutine = null;
            return;
        }

        revealCoroutine = StartCoroutine(RollText(visibleCharacterCount, transferText.Length));
    }

    IEnumerator RollText(int visibleCharacterCount, int sourceLength)
    {
        float characterProgress = 0f;
        int revealedCharacters = 0;

        while (revealedCharacters < visibleCharacterCount)
        {
            characterProgress += Time.deltaTime * charactersPerSecond;
            int charactersToReveal = Mathf.Min(Mathf.FloorToInt(characterProgress), visibleCharacterCount);
            if (charactersToReveal > revealedCharacters)
            {
                revealedCharacters = charactersToReveal;
                textComponent.maxVisibleCharacters = revealedCharacters;
                charCount = revealedCharacters;
            }

            yield return null;
        }

        textComponent.maxVisibleCharacters = visibleCharacterCount;
        charCount = sourceLength;
        revealCoroutine = null;
    }
}
