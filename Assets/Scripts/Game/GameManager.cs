using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {

    public Text scoreText;

    private int score;

	// Use this for initialization
	void Start () {

        score = 0;
		
	}
	
	// Update is called once per frame
	void Update () {

        scoreText.text = score.ToString("000");
		
	}

    public void AddScore(int add){
        score += add;
    }
}
