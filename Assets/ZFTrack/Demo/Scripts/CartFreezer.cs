/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

namespace ZenFulcrum.Track { 

using UnityEngine;
using System.Collections;

/** This script locks the given carts in place until {unlockCar} passes over {unlockPoint}. */
public class CartFreezer : MonoBehaviour {
	public TrackCart[] cartsToFreeze;
	public TrackCart unlockCar;
	public Track unlockPoint;

	void Start() {
		foreach (var cart in cartsToFreeze) {
			cart.GetComponent<Rigidbody>().isKinematic = true;
		}			
	}
	
	void Update() {
		if (unlockCar.CurrentTrack == unlockPoint) {
			foreach (var cart in cartsToFreeze) {
				cart.GetComponent<Rigidbody>().isKinematic = false;
			}
			this.enabled = false;
		}
	}
}

}
