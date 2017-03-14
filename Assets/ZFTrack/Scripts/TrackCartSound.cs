/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */
namespace ZenFulcrum.Track {

using System;
using UnityEngine;
using System.Collections.Generic;

/**
 * This component plays sounds for carts as they travel along a track.
 * 
 * A list of CartSoundClipInfos along with the cart's current speed is used to determine what sound should be playing, 
 * how loudly, and at what pitch.
 */
[RequireComponent(typeof(TrackCart))]
[RequireComponent(typeof(AudioSource))]
public class TrackCartSound : MonoBehaviour {

	[Tooltip(@"Highest cart speed we are concerned with providing sounds for.
If the cart goes faster than this, all sounds will play as if the cart is only going this fast.
This top speed corresponds to the right edge of the volumeVsSpeed curve.")]
	public float maxSpeed = 60;

	[Tooltip("How much louder the sound gets when doing a barrel roll..")]
	public float rotationAmplification = .01f;
	[Tooltip("How much louder the sound gets when rounding a corner at speed.")]
	public float accelerationAmplification = .01f;

	[HideInInspector]//(editing this field is accomplished through a custom inspector)
	public List<CartSoundClipInfo> clips = new List<CartSoundClipInfo>();



	protected AudioSource primarySource;
	protected TrackCart cart;
	protected Rigidbody cartRB;
	protected float baseVolume, trackiness;
	protected Vector3 lastVelocity;
	protected Quaternion lastRotation;
	protected List<AudioSource> sources = new List<AudioSource>();


	public void Start() {
		//We'll clone AudioSources as we need them, but keep track of the original and the initial volume.
		primarySource = GetComponent<AudioSource>();
		primarySource.loop = true;
		primarySource.playOnAwake = false;
		primarySource.enabled = false;
		primarySource.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;

		baseVolume = primarySource.volume;
		cart = GetComponent<TrackCart>();
		cartRB = GetComponent<Rigidbody>();
	}

	public void FixedUpdate() {
		var speed = cart.GetVelocityOnTrack().magnitude;
		if (speed > maxSpeed) speed = maxSpeed;

		float intensityMod;

		if (!cart.CurrentTrack) {
			//When a cart comes off the track, fade to silent, don't just stop and cause popping.
			speed = lastVelocity.magnitude;
			trackiness *= .8f;
			intensityMod = trackiness;
		} else {
			trackiness = 1;
			intensityMod = GetIntensityModifier();
		}


		//Debug.Log("Speed " + speed + " intensity mod " + intensityMod);

		foreach (var clipInfo in clips) {
			var speedIndex = speed / maxSpeed;

			if (clipInfo.volumeVsSpeed == null) clipInfo.GenerateCurve();

			var volume = clipInfo.volumeVsSpeed.Evaluate(speedIndex);
			volume *= intensityMod;

			if (volume <= 0) {
				if (clipInfo.currentSource) {
					clipInfo.currentSource.enabled = false;
					clipInfo.currentSource = null;
				}
				continue;
			}

			var source = clipInfo.currentSource ?? AllocSource();

			clipInfo.currentSource = source;
			source.clip = clipInfo.clip;

			//determine pitch
			if (clipInfo.referenceSpeedPercent == 0 || clipInfo.speedScale == 0) {
				source.pitch = 1;
				source.enabled = true;
			} else {
				var m = clipInfo.speedScale;
				var x = speed / (clipInfo.referenceSpeedPercent * maxSpeed);
				var b = 1 - m;

				source.pitch = m * x + b;
				source.enabled = source.pitch > 0;
			}

			source.volume = baseVolume * volume;

			if (source.enabled && !source.isPlaying) source.Play();
		}
	}

	/** Guesses at how hard the wheels are pressing against the tracks and returns a volume modifier accordingly. */
	protected float GetIntensityModifier() {
		var modifier = 0f;

		var acceleration = (lastVelocity - cartRB.velocity) / Time.fixedDeltaTime;
		var oneG = Physics.gravity.magnitude;

		//Note that this is the resulting acceleration of the object, which is zero at rest.
		//Add gravity in so we can get a better picture of forces acting on the cart.
		acceleration += Physics.gravity;
		//get accel in local terms
		acceleration = transform.InverseTransformVector(acceleration);
		//zero out the forward/backward component
		acceleration.z = 0;


		//We assume 1G is the "normal" volume. Duck or amplify as we experience more or less.
		modifier += (acceleration.magnitude - oneG) * (accelerationAmplification);


		//Get rotation change.
		var spinAmount = Quaternion.Angle(lastRotation, transform.rotation);
		modifier += spinAmount * rotationAmplification;

		lastVelocity = cartRB.velocity;
		lastRotation = transform.rotation;


		//intensityMod is now a number usually near zero that indicates how much to add or remove.
		//Convert it so we can just multiply our volume against it.
		modifier = 1 + modifier;

		//Clamp it to be (hopefully) reasonable.
		modifier = Mathf.Max(.1f, Mathf.Min(modifier, 10f));

		//Debug.Log("modifier is " + modifier + " accel is " + acceleration.magnitude + " rotation is " + spinAmount);
		return modifier;
	}

	/** Finds or adds a free AudioSource and returns it. */
	protected AudioSource AllocSource() {
		foreach (var source in sources) {
			if (!source.enabled) {
				//this one's free to use
				return source;
			}
		} 

		//Make a new one
		{ 
			var source = gameObject.AddComponent<AudioSource>();

			//Copy over settings from the original.
			source.bypassEffects = primarySource.bypassEffects;
			source.bypassListenerEffects = primarySource.bypassListenerEffects;
			source.bypassReverbZones = primarySource.bypassReverbZones;
			source.dopplerLevel = primarySource.dopplerLevel;
			source.ignoreListenerPause = primarySource.ignoreListenerPause;
			source.ignoreListenerVolume = primarySource.ignoreListenerVolume;
			source.maxDistance = primarySource.maxDistance;
			source.minDistance = primarySource.minDistance;
			source.mute = primarySource.mute;
			source.priority = primarySource.priority;
			source.rolloffMode = primarySource.rolloffMode;
			source.spread = primarySource.spread;
#if UNITY_5
			source.outputAudioMixerGroup = primarySource.outputAudioMixerGroup;
			source.panStereo = primarySource.panStereo;
			source.reverbZoneMix = primarySource.reverbZoneMix;
			source.spatialBlend = primarySource.spatialBlend;
#endif

			source.loop = true;
			source.hideFlags = HideFlags.NotEditable;

			sources.Add(source);
			return source;
		}
	}


}

}
