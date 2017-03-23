using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResultManager : MonoBehaviour {

    private int score;

    public Text scoreText;

	// Use this for initialization
	void Start () {

        score = GameObject.Find("GameManager").GetComponent<GameManager>().GetScore();
        scoreText.text = score.ToString();
		
	}
	
	public void LoadGame(){
        Camera.main.GetComponent<ScreenFade>().LoadScreenWithFade("Game");
    }

    public void LoadTitle(){
        Camera.main.GetComponent<ScreenFade>().LoadScreenWithFade("Title");
    }
}
