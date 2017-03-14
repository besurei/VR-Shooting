/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */


using System;
using System.Collections;

namespace ZenFulcrum.Track {

using System.Collections.Generic;
using UnityEngine;

/** 
 */
public class LiftControl : MonoBehaviour {
	public Track feedInTrack, feedOutTrack;
	public Track liftedTrack;
	public TrackCart lifterCart;
	protected TrackCart passengerCart;

	public float liftSpeed = 4;

	protected Func<IEnumerator> stateAction; 

	public void Start() {
		stateAction = TravelToBottom;

		SetTrackSpeed(feedInTrack, 0);
		StartCoroutine(RunLogic());
	}

	public IEnumerator RunLogic() {
		while (true) {
			var en = stateAction();
			while (en.MoveNext()) yield return en;
		}
	}

	protected void SetLifterToMove(float deltaHeight) {

		var speed = liftSpeed;
		var absDelta = Mathf.Abs(deltaHeight);
		if (absDelta < 1) speed *= .5f;
//		if (absDelta < .25f) speed *= .125f;
		if (absDelta < .25f) speed *= .5f;

		SetLiferSpeed(speed * Mathf.Sign(deltaHeight));
	}

	protected void SetLiferSpeed(float speed) {
		foreach (var track in lifterTracks) {
			SetTrackSpeed(track, speed);
		}
	}

	protected void SetTrackSpeed(Track track, float speed, float force = 1000) {
		track.acceleration.direction = speed > 0 ? Track.SpeedAndForce.ActDirection.Forward : Track.SpeedAndForce.ActDirection.Backward;
		track.acceleration.maxForce = 1000;
		track.acceleration.targetSpeed = Mathf.Abs(speed);

		track.brakes.direction = Track.SpeedAndForce.ActDirection.Both;
		track.brakes.maxForce = 1000;
		track.brakes.targetSpeed = Mathf.Abs(speed);
	}

	protected IEnumerable<Track> lifterTracks {
		get {
			var t = lifterCart.CurrentTrack;
			while (t) {
				yield return t;
				t = t.NextTrack;
			}

			t = lifterCart.CurrentTrack.PrevTrack;
			while (t) {
				yield return t;
				t = t.PrevTrack;
			}
		}
	}

	protected IEnumerator TravelToBottom() {
		var en = TravelToX(feedInTrack.TrackAbsoluteEnd.position);
		while (en.MoveNext()) yield return en.Current;

		feedInTrack.NextTrack = liftedTrack;
		liftedTrack.PrevTrack = feedInTrack;

		SetTrackSpeed(feedInTrack, liftSpeed);

		stateAction = WaitForCart;
	}

	protected IEnumerator TravelToTop() {
		var en = TravelToX(feedOutTrack.TrackAbsoluteStart.position);
		while (en.MoveNext()) yield return en.Current;

		feedOutTrack.PrevTrack = liftedTrack;
		liftedTrack.NextTrack = feedOutTrack;

		stateAction = WaitForCartToLeave;
	}

	protected IEnumerator TravelToX(Vector3 position) {
		Debug.Log("Travelling to " + position);
		feedInTrack.NextTrack = null;
		feedOutTrack.PrevTrack = null;
		liftedTrack.NextTrack = null;
		liftedTrack.PrevTrack = null;

		float targetHeight, currentHeight;
		const float tolerance = .1f;
		do {
			targetHeight = position.y;
			currentHeight = liftedTrack.TrackAbsoluteStart.position.y;

			SetLifterToMove(targetHeight - currentHeight);

			yield return null;

		} while (Mathf.Abs(targetHeight - currentHeight) > tolerance);

		Debug.Log("Tracks aligned");
		SetLiferSpeed(0);
	}

	protected IEnumerator WaitForCart() {
		passengerCart = null;
		SetTrackSpeed(liftedTrack, liftSpeed);
		Debug.Log("Waiting for cart");

		Action<TrackCart> onGain = cart => {
			Debug.Log("Cart arrived, waiting for centering");
			passengerCart = cart;
		};

		//wait for a cart
		liftedTrack.onCartEnter += onGain;
		while (!passengerCart) yield return null;
		liftedTrack.onCartEnter -= onGain;

		//wait for it to be closer to the end than the start
		while (
			Vector3.Distance(liftedTrack.TrackAbsoluteStart.position, passengerCart.transform.position)
			<
			Vector3.Distance(liftedTrack.TrackAbsoluteEnd.position, passengerCart.transform.position)
		) yield return null;

		//lock it down
		SetTrackSpeed(liftedTrack, 0);
		SetTrackSpeed(feedInTrack, 0);

		Debug.Log("Cart in place");

		stateAction = TravelToTop;
	}


	protected IEnumerator WaitForCartToLeave() {
		SetTrackSpeed(liftedTrack, liftSpeed);
		Debug.Log("Waiting for cart to leave");

		Action<TrackCart> onLose = cart => {
			if (cart == passengerCart) passengerCart = null;
		};

		//wait for cart to leave
		liftedTrack.onCartLeave += onLose;
		while (passengerCart) yield return null;
		liftedTrack.onCartLeave -= onLose;

		SetTrackSpeed(liftedTrack, 0);

		Debug.Log("Cart is gone");

		stateAction = TravelToBottom;
	}
}

}
