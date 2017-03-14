using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movie : MonoBehaviour {

    MovieTexture movieTexture;

    void Start(){
        movieTexture = (MovieTexture)(GetComponent<Renderer>().material.mainTexture);
        movieTexture.loop = true;
        DontDestroyOnLoad(gameObject);
    }

    void PlayMovie(){
        movieTexture.Play();
    }
}
