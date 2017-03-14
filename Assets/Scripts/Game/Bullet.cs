using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour {

    private float life = 3.0f;
    private float time = 0.0f;
    private TitleManager titleManager;

	// Use this for initialization
	void Start () {
        titleManager = GameObject.Find("TitleManager").GetComponent<TitleManager>();

    }
	
	// Update is called once per frame
	void Update () {

        if(time >= life)
        {
            Destroy(gameObject);
        }

        time += Time.deltaTime;
	}

    void OnCollisionEnter(Collision col){
        if(col.gameObject.name == "Start")
        {
            titleManager.GameStart();
        }
    }
}
