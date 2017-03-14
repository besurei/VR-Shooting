/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */
namespace ZenFulcrum.Track {

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;


/** 
 * Built for the sound utility scene, this controls the location of the cart, the speed of the cart,
 */
public class SpeedControl : MonoBehaviour {

	[System.Serializable]
	public class Course {
		public Slider speedSlider;
		[System.NonSerialized]
		public float lastSliderValue;

		public Track track;
	}

	public List<Course> courses;

	private TrackCart cart;

	public void Start() {
		cart = GameObject.FindObjectOfType<TrackCart>();

		if (!cart) throw new MissingComponentException("A cart must be in the scene for this to work.");
	}

	protected void WipeSliders() {
		foreach (var course in courses) {
			course.speedSlider.value = 0;
			course.lastSliderValue = 0;
		}
	}

	protected IEnumerable<Track> GetPieces(Track track) {
		var firstTrack = track;
		while (true) {
			yield return track;

			track = track.NextTrack;
			if (!track || track == firstTrack) yield break;
		}
	}

	public void Update() {
		//Track their cart with the camera
		var behind = -cart.transform.forward;
		behind.y = 0;
		behind.Normalize();
		Camera.main.transform.position = behind * 5 + cart.transform.position + Vector3.up * 2;
		Camera.main.transform.LookAt(cart.transform);

		foreach (var course in courses) {
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (course.lastSliderValue != course.speedSlider.value) {
				//This was moved last, switch to this one.
				var val = course.speedSlider.value;
				WipeSliders();
				course.lastSliderValue = course.speedSlider.value = val;

				var pieces = GetPieces(course.track);

				//jump to the right course if we aren't there
				if (!pieces.Contains(cart.CurrentTrack)) {
					cart.CurrentTrack = course.track;
					course.track.TrackAbsoluteStart.ApplyTo(cart.transform);
					cart.GetComponent<Rigidbody>().velocity = Vector3.zero;
				}

				foreach (var piece in pieces) {
					piece.acceleration.targetSpeed = val;
					piece.brakes.targetSpeed = val;
				}

				//we are done here
				return;
			}
		}
	}
}

}
