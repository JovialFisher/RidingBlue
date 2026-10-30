using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashToMain : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(SplashSequence());
    }

    // Update is called once per frame
    void Update()
    {

    }

    IEnumerator SplashSequence()
    {
        yield return SceneLoader.LoadSceneAsync(1, 8f);
    }
}
