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

/** 
 * When {triggerCart} passes over {triggerTrack} we take the start of {switched} and link it to the *start*
 * of {switchedTo}.
 * 
 * Note that this isn't the usual case of linking tracks in their natural direction: this actually reverses the 
 * flow at the new junction!
 */
public class TriggerTrackSwitch : MonoBehaviour {
	public TrackCart triggerCart;
	public Track triggerTrack;
	public Track switched, switchedTo;

	void Update() {
		if (triggerCart.CurrentTrack == triggerTrack) {
			//disconnect old track
			if (switched.PrevTrack) switched.PrevTrack.NextTrack = null;

			//align the tracks
			switched.TrackAbsoluteStart = switchedTo.TrackAbsoluteStart.AboutFace();
			//link them so carts can pass over the joint
			switched.PrevTrack = switchedTo;
			switchedTo.PrevTrack = switched;//(remember, these tracks face opposite directions, so prev links to prev)
			enabled = false;
		}
	}
}

}
