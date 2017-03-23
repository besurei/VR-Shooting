using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bullet : MonoBehaviour {

    private float life = 3.0f;
    private float time = 0.0f;

	// Use this for initialization
	void Start () {

    }
	
	// Update is called once per frame
	void Update () {

        if(time >= life)
        {
            Destroy(gameObject);
        }

        time += Time.deltaTime;
	}

    void OnCollisionEnter(Collision col) {
        switch (SceneManager.GetActiveScene().name)
        {
            case "Title":
            if (col.gameObject.name == "Start")
            {
                GameObject.Find("TitleManager").GetComponent<TitleManager>().LoadGame();
            }
                break;

            case "Result":
                ResultManager resultManager = GameObject.Find("ResultManager").GetComponent<ResultManager>();
            if (col.gameObject.name == "Retry")
                {
                    resultManager.LoadGame();
                }
            else if( col.gameObject.name == "Title" )
                {
                    resultManager.LoadTitle();
                }
                break;
    }
    }
}
