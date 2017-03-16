using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetFactory : MonoBehaviour {

    public GameObject prefTargets;

    private Vector3 vehiclePos;
    private Vector3 vehicleRot;
    private float time;

	// Use this for initialization
	void Start () {
        time = 0;
	}
	
	// Update is called once per frame
	void Update () {

        vehiclePos = GameObject.Find("Vehicle").transform.position;
        vehicleRot = new Vector3 ( GameObject.Find("Vehicle").transform.rotation.x, GameObject.Find("Vehicle").transform.rotation.y, GameObject.Find("Vehicle").transform.rotation.z);

        if (time >= 1)
        {
            Instantiate(prefTargets, transform.position, transform.rotation);
            time = 0.0f;
        }

        time += Time.deltaTime;
	}
}
