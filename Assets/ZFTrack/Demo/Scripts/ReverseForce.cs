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

/** When the given rigidbody is close enough, this pushes it with {force}. */
public class ReverseForce : MonoBehaviour {
	public Rigidbody thingToReverse;
	public float triggerDistance;
	public Vector3 force;

	void Update() {
		if (Vector3.Distance(thingToReverse.transform.position, transform.position) < triggerDistance) {
			thingToReverse.AddForce(force);
		}
	}
}

}
