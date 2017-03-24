using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ResultManager : MonoBehaviour {

    private int score;

    public Text scoreText;

	// Use this for initialization
	void Start () {
        GameObject gameManager = GameObject.Find("GameManager");
        score = gameManager.GetComponent<GameManager>().GetScore();
        Destroy(gameManager);
        scoreText.text = score.ToString();
		
	}
	
	public void LoadGame(){
        SceneManager.LoadScene("Game");
    }

    public void LoadTitle(){
        SceneManager.LoadScene("Title");
    }
}
