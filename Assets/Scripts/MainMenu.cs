using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    const string LoadStateKey = "LoadState";
    const int FirstStorySceneIndex = 2;
    const int CreditsSceneIndex = 4;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterProgressTracking()
    {
        SceneManager.sceneLoaded -= SaveCurrentStoryScene;
        SceneManager.sceneLoaded += SaveCurrentStoryScene;
    }

    static void SaveCurrentStoryScene(Scene scene, LoadSceneMode mode)
    {
        int sceneIndex = scene.buildIndex;
        if (sceneIndex >= FirstStorySceneIndex
            && sceneIndex < SceneManager.sceneCountInBuildSettings
            && sceneIndex != CreditsSceneIndex)
        {
            PlayerPrefs.SetInt(LoadStateKey, sceneIndex);
            PlayerPrefs.Save();
        }
    }

    [SerializeField] Button loadGameButton;
    [SerializeField] GameObject menuPic1;
    [SerializeField] GameObject menuPic2;
    [SerializeField] int picNum = 1;
    [SerializeField] bool isAnimating = false;
    [SerializeField] GameObject fadeOut;
    [SerializeField] int sceneToLoad;
    [SerializeField] int saveTransferValue;
    [SerializeField] GameObject fadeIn;
    bool isTransitioning;

    void Start()
    {
        if (loadGameButton != null)
        {
            loadGameButton.interactable = HasValidSave();
        }

        StartCoroutine(StopFade());
    }

    static bool HasValidSave()
    {
        int savedSceneIndex = PlayerPrefs.GetInt(LoadStateKey, -1);
        return savedSceneIndex >= FirstStorySceneIndex
            && savedSceneIndex < SceneManager.sceneCountInBuildSettings
            && savedSceneIndex != CreditsSceneIndex;
    }

    public void StartGame()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        PlayerPrefs.DeleteKey(LoadStateKey);
        PlayerPrefs.Save();
        ShowFadeOut();
        StartCoroutine(TransferToClassScene());
    }

    public void LoadGame()
    {
        if (isTransitioning || !HasValidSave())
        {
            return;
        }

        isTransitioning = true;
        saveTransferValue = PlayerPrefs.GetInt(LoadStateKey, -1);
        sceneToLoad = saveTransferValue;
        ShowFadeOut();
        StartCoroutine(LoadScene());
    }

    public void GotToCredits()
    {
        if (isTransitioning)
        {
            return;
        }

        isTransitioning = true;
        ShowFadeOut();
        StartCoroutine(TransferToCredits());
    }

    void ShowFadeOut()
    {
        if (fadeOut != null)
        {
            fadeOut.SetActive(true);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        if (isAnimating == false)
        {
            isAnimating = true;
            StartCoroutine(RandomPic());
        }
    }

    IEnumerator RandomPic()
    {
        yield return new WaitForSeconds(1);
        if (menuPic1 == null || menuPic2 == null)
        {
            isAnimating = false;
            yield break;
        }

        if (isTransitioning)
        {
            isAnimating = false;
            yield break;
        }

        picNum = Random.Range(1, 3);
        if (picNum == 1)
        {
            menuPic1.SetActive(true);
            menuPic2.SetActive(false);
        }
        else if (picNum == 2)  // Changed from if (picNum == 2) block
        {
            menuPic1.SetActive(false);
            menuPic2.SetActive(true);
        }
        yield return new WaitForSeconds(7);
        menuPic1.SetActive(false);
        menuPic2.SetActive(false);
        isAnimating = false;
    }

    IEnumerator TransferToClassScene()
    {
        yield return SceneLoader.LoadSceneAsync(5, 3f);
    }

    IEnumerator LoadScene()
    {
        yield return SceneLoader.LoadSceneAsync(sceneToLoad, 3f);
    }

    IEnumerator TransferToCredits()
    {
        yield return SceneLoader.LoadSceneAsync(4, 3f);
    }

    IEnumerator StopFade()
    {
        yield return new WaitForSeconds(2);
        if (fadeIn != null)
        {
            fadeIn.SetActive(false);
        }
    }
}