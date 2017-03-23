using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TitleManager : MonoBehaviour {

	public void LoadGame(){
        Camera.main.GetComponent<ScreenFade>().LoadScreenWithFade("Tutorial");
    }
}
