using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TitleManager : MonoBehaviour {

	public void LoadTutorial(){
        Camera.main.GetComponent<ScreenFade>().LoadScreenWithFade("Tutorial");
    }
}
