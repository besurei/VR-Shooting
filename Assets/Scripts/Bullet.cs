using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        time -= Time.deltaTime;
	}
}
