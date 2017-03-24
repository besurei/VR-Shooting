using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {

    public Text scoreText;

    private int score;

	void Awake () {

        // リザルトまで残す
        DontDestroyOnLoad(gameObject);

        score = 0;
		
	}
	
	// Update is called once per frame
	void Update () {

        scoreText.text = score.ToString("000");		
	}

    public void AddScore(int add){
        score += add;
    }

    public int GetScore(){
        return score;
    }
}
