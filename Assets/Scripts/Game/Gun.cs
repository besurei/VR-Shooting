using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour {

    private Transform spawnTrans;
    private Vector3 spawnPos;
    private Quaternion spawnRot;
    private Vector3 handPos;
    private Quaternion handRot;
    private GameObject hand;
    private float speed = 3000.0f;

    public int handType;    // 0.右手 1.左手
    public GameObject prefBullet;

    // サウンド
    //public AudioClip audioClip;
    //private AudioSource audioSource;

    // Use this for initialization
    void Start() {

        switch (handType)
        {
            case 0:
                hand = GameObject.Find("RightHandAnchor");
                spawnTrans = transform.FindChild("Spawn_R");
                break;

            case 1:
                hand = GameObject.Find("LeftHandAnchor");
                spawnTrans = transform.FindChild("Spawn_L");
                break;
        }

        // オーディオ初期化
        //audioSource = GetComponent<AudioSource>();
        //audioSource.clip = audioClip;
        //audioSource.loop = false;
	}
	
	// Update is called once per frame
	void Update () {

        // 弾を撃つ
        Shot();

        handPos = hand.transform.position;
        handRot = hand.transform.rotation;
        transform.position = handPos;
        transform.rotation = handRot;
	}

    void Shot(){

        switch (handType)
        {
            case 0:
                if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
                {
                    GetComponent<AudioSource>().Play();
                    spawnPos = spawnTrans.position;
                    spawnRot = spawnTrans.rotation;
                    GameObject bullet = Instantiate(prefBullet, spawnPos, Quaternion.identity) as GameObject;
                    bullet.GetComponent<Rigidbody>().AddForce( spawnRot * Vector3.forward * speed );
                }
                break;

            case 1:
                if (OVRInput.GetDown(OVRInput.RawButton.LIndexTrigger))
                {
                    GetComponent<AudioSource>().Play();
                    spawnPos = spawnTrans.position;
                    spawnRot = spawnTrans.rotation;
                    GameObject bullet = Instantiate(prefBullet, spawnPos, Quaternion.identity) as GameObject;
                    bullet.GetComponent<Rigidbody>().AddForce( spawnRot * Vector3.forward * speed);
                }
                break;
        }
    }
}
