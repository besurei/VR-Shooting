using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndGame : MonoBehaviour {

	void OnTriggerEnter( Collider col )
    {
        if (col.gameObject.CompareTag("Player"))
        {
            Camera.main.GetComponent<ScreenFade>().LoadScreenWithFade("Result");
        }
    }
}
