/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

using UnityEngine.Serialization;

namespace ZenFulcrum.Track {

using UnityEngine;

/**
 * Information about one of the clips we'll use or track sounds.
 * 
 * This once was a ScriptabeObject, but that broke a lot of things, like suddenly turning to null when put in a
 * prefab.
 */
[System.Serializable]
public class CartSoundClipInfo {
	public CartSoundClipInfo() {
		GenerateCurve();
	}

	[Tooltip(@"Clip to play")]
	/**
	 * When the cart is traveling this fast, this clip will be played at its natural pitch.
	 * Faster and slower speeds will pitch bend the sound accordingly.
	 * 
	 * This is measured in world units/sec (often meters/s, but depends on your physics world's units)
	 *
	 */
	public AudioClip clip;

	[Tooltip(@"When the cart is moving at this fraction of maxSpeed, the clip will play at its natural pitch.")]
	[Range(0, 1)]
	public float referenceSpeedPercent = .5f;

	/**
	 * How much the pitch is bent based on speed. 
	 * Higher numbers result in more pitch bending as speed changes.
	 * One indicates normal pitch bending.
	 * Lower numbers result in less pitch bending with speed.
	 * Negative numbers don't make sense.
	 * 
	 * Set referenceSpeed to 0 if you want to disable speed-based pitch bending altogether.
	 * 	 
	 */
	[Tooltip(@"How much the pitch of this clip changes with cart speed.")]
	public float speedScale = 1;

	/**
	 * Volume of this clip as a function of speed. The left side represents a stopped cart, the right side represents a
	 * cart traveling at topSpeed. 
	 * (Bounded in a box from [0, 0] to [1, 1]. Values outside this range are automatically clipped.)
	 */
	[Tooltip(@"How loud this clip is in respect to cart speed.")]
	public AnimationCurve volumeVsSpeed;

	/** Reference data that is read and written by TrackCartSound only. */
	[System.NonSerialized]
	internal AudioSource currentSource;

	public void GenerateCurve() {
		volumeVsSpeed = new AnimationCurve(
			new Keyframe(0, 0), new Keyframe(referenceSpeedPercent, .5f), new Keyframe(1, 0)
		);
	}

	public static implicit operator bool(CartSoundClipInfo b) {
		return b != null;
	}
}

}
