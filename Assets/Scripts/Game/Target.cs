using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Target : MonoBehaviour {

    enum TargetType
    {
        RED = 0,
        GREEN,
        BLUE,
    };

    public Material[] mat = new Material[3];    // ０：赤　１：緑  ２：青

    private TargetType type;
    private GameObject hitEffect;

	void Awake(){
        type = (TargetType)Random.Range(0, 3);
        gameObject.GetComponent<Renderer>().material = mat[(int)type];

        // ヒットパーティクルの色の設定
        hitEffect = transform.FindChild("HitEffect").gameObject;
        hitEffect.GetComponent<ParticleSystem>().startColor = gameObject.GetComponent<Renderer>().material.GetColor("_EmissionColor");
        hitEffect.transform.FindChild("01").gameObject.GetComponent<ParticleSystem>().startColor = gameObject.GetComponent<Renderer>().material.GetColor("_EmissionColor");
        hitEffect.transform.FindChild("02").gameObject.GetComponent<ParticleSystem>().startColor = gameObject.GetComponent<Renderer>().material.GetColor("_EmissionColor");
        hitEffect.transform.FindChild("03").gameObject.GetComponent<ParticleSystem>().startColor = gameObject.GetComponent<Renderer>().material.GetColor("_EmissionColor");
        hitEffect.transform.FindChild("04").gameObject.GetComponent<ParticleSystem>().startColor = gameObject.GetComponent<Renderer>().material.GetColor("_EmissionColor");
        hitEffect.transform.FindChild("01_B").gameObject.GetComponent<ParticleSystem>().startColor = gameObject.GetComponent<Renderer>().material.GetColor("_EmissionColor");

        hitEffect.transform.parent = null;
    }
	
	void OnCollisionEnter(Collision col)
    {
        // ボールが当たったらスコア加算
        if(col.gameObject.tag == "Ball")
        {
            GameObject.Find("HitEffect").gameObject.GetComponent<AudioSource>().Play();
            GameManager gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
            switch(type)
            {
                case TargetType.RED:
                    hitEffect.GetComponent<ParticleSystem>().Play();
                    gameManager.AddScore(1);
                    Destroy(gameObject);
                    Destroy(col.gameObject);
                    break;

                case TargetType.GREEN:
                    hitEffect.GetComponent<ParticleSystem>().Play();
                    gameManager.AddScore(2);
                    Destroy(gameObject);
                    Destroy(col.gameObject);
                    break;

                case TargetType.BLUE:
                    hitEffect.GetComponent<ParticleSystem>().Play();
                    gameManager.AddScore(3);
                    Destroy(gameObject);
                    Destroy(col.gameObject);
                    break;
            }
        }
    }
}
