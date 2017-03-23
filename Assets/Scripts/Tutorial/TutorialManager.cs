using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour {

    private AsyncOperation async;
    private GameObject gameStartButton;

    public Text loadingText;

	// Use this for initialization
	void Start () {

        gameStartButton = GameObject.Find("GameStart");
        gameStartButton.SetActive(false);
        StartCoroutine("LoadGame");
    }
	
    // Gameシーンを非同期ロード
	IEnumerator LoadGame()
    {
        async = SceneManager.LoadSceneAsync("Game");
        async.allowSceneActivation = false;

        while (async.progress < 0.9f)
        {
            loadingText.text = (async.progress * 100).ToString("F0") + "%";
            yield return new WaitForEndOfFrame();
        }

        loadingText.text = "100%";
        gameStartButton.SetActive(true);

    }

    public void GameStart()
    {
        async.allowSceneActivation = true;
    }
}
