/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

using System;
using UnityEngine.Serialization;

namespace ZenFulcrum.Track {

using UnityEngine;
using System.Collections;

/**
 * Use this script to attach objects to tracks so the slide along it like a cart.
 */
[RequireComponent(typeof(Rigidbody))]
public class TrackCart : MonoBehaviour {
	static bool DebugDrawTracking = false;
	static bool DebugDrawSpeed = false;

	/**
	 * Track we are currently on. Null if not attached to a track.
	 * Changes as we slide around a course.
	 * You may change this before the object is started (such as through the inspector in the editor), but after that
	 * use CurrentTrack to attach the cart to a track.
	 */
	[SerializeField]
	[FormerlySerializedAs("currentTrack")]
	protected Track _currentTrack = null;

	/**
	 * After we run out of track we continue straight for this distance before actually falling off.
	 * 
	 * Without this, or if it's too low, the cart can get "stuck" on the track it's leaving. (Glitchy physics intersection style.)
	 * If this is too high, the cart will mysteriously float in the air for some distance before falling off the track.
	 * 
	 * Set this to roughly one half the length of the cart, measured along the z axis.
	 * 
	 * If the track and cart don't intersect at all, however, you can set this to zero.
	 */
	public float clearingDistance = .5f;

	/**
	 * Avoid pushing the cart past its target speed.
	 * 
	 * Since braking forces can be set high enough to stop AND REVERSE an object in a single tick, enable this
	 * to weaken the force when we think it will be too much.
	 * 
	 * This all-but-required if you have the maximum force for acceleration or braking set to a high value.
	 * 
	 * Pros: High slowing forces don't bounce. More accurate speeds from track accel/decel.
	 * Cons: Since it only takes into account the mass of the cart, a cart with mass attached may not get the full
	 * force it deserves.
	 */
	public bool prorateForces = true;

	/** 
	 * Normally, the cart's +z always faces the track's forward direction. Set this to true to make the -z forward instead.
	 * 
	 * Be careful switching this live: if the cart is well-aligned and you reverse the direction, the physics
	 * system seems to nearly-divide-by-zero and explodes badly. If you reverse the direction, be sure to spin the cart around too.
	 * 
	 * This will automatically be toggled when a cart travels across tracks facing different directions.
	 */
	public bool cartReversed = false;

	[Tooltip(@"How much speed to preserve when going around corners.

Higher values keep more speed on curves, but also removes an implicit dampening that keeps the cart from speeding up due to accumulated error.")]
	[Range(0, 1)]
	public float curvePreservation = 0;


	// That's all for properties you can change in the inspector.

	[Obsolete("Field currentTrack has been removed. Use CurrentTrack instead.", true)]
	public Track currentTrack { get { return null; } }

	public Track CurrentTrack {
		get { return _currentTrack; }

		/**
		 * Changes the section of track we are currently on.
		 * Before calling this, align the cart with the track "fairly well". If the cart is far from the track
		 * the physics system will try to correct the "error" which tends to be glitchy.
		 */
		set {
			//remove any physics exclusions
			if (_currentTrack) {
				_currentTrack.FireCartLeave(this);
				foreach (var collider in GetComponentsInChildren<Collider>()) {
					_currentTrack.ResumeCollisionsWith(collider);
				}
			}

			if (_currentTrack && !value) {
				//we are about to go off into the blue yonder.
				//Make sure our speed it correct.
				RepairVelocity(LastTangent);
			}

			_currentTrack = value;
			trackRB = _currentTrack ? _currentTrack.GetComponentInParent<Rigidbody>() : null;
			if (guideJoint && guideJoint.connectedBody != trackRB) guideJoint.connectedBody = trackRB;

			//add collision exclusions with new track
			if (_currentTrack) {
				foreach (var collider in GetComponentsInChildren<Collider>()) {
					_currentTrack.IgnoreCollisionsWith(collider);
				}
				_currentTrack.FireCartEnter(this);
			}

			if (brakeJoint) {
				Destroy(brakeJoint);
				brakeJoint = null;
			}			
			
			if (endJoint) {
				Destroy(endJoint);
				endJoint = null;
			}

			if (!_currentTrack) DestroyJoint();
		}
	}

	/**
	 * We use a guide to smooth over issues with Unity's physics API (mostly the fact we can't set the internal target transforms).
	 *
	 * Which...requires a lot of code.
	 */
	GameObject guide;

	/** Connects the guide to the track (rigid joint). */
	ConfigurableJoint trackJoint;
	/** Connects the cart to the guide (free sliding joint) */
	ConfigurableJoint guideJoint;
	/** Breakable joint that sometimes connects the cart to the track when the cart isn't moving. */
	FixedJoint brakeJoint;
	/** When enacting EndOfLineBehavior.EndStop, this joint links the cart and track to prevent the cart from rolling off. */
	ConfigurableJoint endJoint;

	Rigidbody cartRB, trackRB;

	/** Last time we did a physics tick with a track, this was the pos+direction of track section we were connected to. */
	public SimpleTransform LastTangent { get; private set; }

	int brakesCooldown = 0;

	public void Start() {
		//Force the setter code to run on the inspector-assigned data.
		CurrentTrack = _currentTrack;
		cartRB = GetComponent<Rigidbody>();
	}

	const float leadFactor = 1f;

	//c# syntax garbage
	delegate float F_msds(float mass, float speed, float desiredSpeed);

	/**
	 * There is where it all happens.
	 *
	 * Here's how the track constraint works:
	 * (Getting this to work in Unity was blood, sweat, and couple of tears, what's describe below it how we got it to work.)
	 *
	 * Each cart has a "guide". The guide is attached to the track (via fixed* joint). The guide is, each frame, teleported
	 * to the nearest "correct" position on the track and reaffixed there.
	 *
	 * Connected to the guide is a slider joint**, which allows free movement along one axis. The cart slides along this
	 * axis, but the joint, and therefore axis, are moved by the guide.
	 *
	 * That, combined with a dash of magic and some prayer seems to be enough to get it working in Unity.
	 *
	 * (Note: Unity does not seem to correctly support changing most the parameters on a configurable joint while the game is running,
	 * including changing the axis of a slider joint, among other things.)
	 *
	 * (*Actually, it's a configurable joint with specific settings that gives us better results than a regular fixed joint.)
	 * (**A fixed joint with all the axes but one locked.)
	 *
	 * This is not an perfect solution, a native joint type would be better, but it's what we can muster.
	 *
	 * (Unfortunately, this physics method does not take into account torque: for example, a cart on
	 * a "screw" track won't necessarily go forward if you twist it.)
	 */
	public void FixedUpdate() {
		if (brakesCooldown > 0) --brakesCooldown;

		if (!_currentTrack) return;
		if (!guide) SetupGuide();

		SimpleTransform t;
		UpdateTrackSlider(out t);
		LastTangent = t;
		PreserveCurve(LastTangent);
		float forwardness;
		PushThings(LastTangent, out forwardness);
		UpdateEndStop(LastTangent, forwardness);
	}

	protected void UpdateTrackSlider(out SimpleTransform trackTangent) {

		//number of times we've "recursed" (emulated with a goto)
		int iterations = 0;

		Transform cartTransform = transform;
//		Transform cartTransform = guide.transform;

		//Estimate where we'll be in about a frame or so - this allows us to "lead"
		//the constraint's position and minimize error
		Vector3 tickMovement = cartTransform.GetComponent<Rigidbody>().velocity * leadFactor * Time.deltaTime;
		SimpleTransform carSoonPos = new SimpleTransform(cartTransform);
		carSoonPos.position += tickMovement;
		if (DebugDrawTracking) {
			Debug.DrawRay(carSoonPos.position, Vector3.up, Color.yellow);
			Debug.DrawRay(cartTransform.position, Vector3.up, Color.white);
		}

		//DEBUG("now: " << m_rbB.getWorldTransform().position << " mv: " << tickMovement << " predicted: " << (carSoonPos.position + tickMovement));

		//goto label used to emulate a stateful tail-end recursion
		//todo: cars shouldn't be jumping far enough to need tail-end recursion, switch to a closure
		calculate:
		Track track = _currentTrack;

		//Position of cart
		float pos = track.Curve.GetFraction(track.transform.InverseTransformPoint(carSoonPos.position));


		if (pos < 0) {
			//before the start
			//DEBUG("Before the start");
			if (track.PrevTrack && iterations == 0) {
				//Move to the next track and run as that track
				RollToNextTrack(false);
				++iterations;
				goto calculate;
			} else if (track.PrevTrack) {
				//we've hit the iteration limit, just go straight until next frame
				pos = 0;
			} else {
				//We're past the start with no previous.

				if (_currentTrack.outOfTrack == Track.EndOfLineBehavior.FallOffTrack) {
					//wait until it's clear of the track (so it doesn't intersect with it), then drop it off
					//(note that we use the cart's actual instead of projected position here)
					if (Vector3.Distance(track.transform.position, cartTransform.position) >= clearingDistance) {
						RemoveFromTrack();
					}
				} else if (_currentTrack.outOfTrack == Track.EndOfLineBehavior.ContinueStraight) {
					//do nothing special here, just keep going
				}

				// In all cases, set our guide to line up with the start of the track.
				pos = 0;
			}
		} else if (pos > 1) {
			//past the end
			//DEBUG("After the end");
			if (track.NextTrack && iterations == 0) {
				//Move to the next track and run as that track
				RollToNextTrack(true);
				++iterations;
				goto calculate;
			} else if (track.NextTrack) {
				//we've hit the iteration limit, just go straight until next frame
				pos = 1;
			} else {
				//We're past the end with no next.

				if (_currentTrack.outOfTrack == Track.EndOfLineBehavior.FallOffTrack) {
					//wait until it's clear of the track (so it doesn't intersect with it), then drop it off
					if (Vector3.Distance(track.TrackAbsoluteEnd.position, cartTransform.position) >= clearingDistance) {
						RemoveFromTrack();
					}
				} else if (_currentTrack.outOfTrack == Track.EndOfLineBehavior.ContinueStraight) {
					//do nothing special here, just keep going
				}

				// In all cases, set our guide to line up with the end of the track.
				pos = 1;
			}
		} else {
			//calculate position normally
		}

		if (DebugDrawTracking) {
			var col = new Color(0, 0, 0, .3f);
			Debug.DrawLine(cartTransform.position, track.TrackAbsoluteEnd.position, col);
			Debug.DrawLine(cartTransform.position, track.transform.position, col);
		}
		//Debug.Log("d1: " + d1  + ", d2: " + d2 + " maxDist: " + maxDist);


		//Get position and direction at this point on the track
		trackTangent = track.transform * track.Curve.GetPointAt(pos);

		if (cartReversed) {
			trackTangent.AboutFace();
		}

		if (DebugDrawTracking && _currentTrack) {
			//(these two should be more-or-less on top of each other, if there's a big divergence we have an issue)
			Debug.DrawRay(guide.transform.position, guide.transform.rotation * Vector3.forward, Color.red);
			Debug.DrawRay(guide.transform.position, guide.transform.rotation * Vector3.up, Color.blue);

			Debug.DrawRay(trackTangent.position, trackTangent.rotation * Vector3.forward, Color.red);
			Debug.DrawRay(trackTangent.position, trackTangent.rotation * Vector3.up, Color.blue);
		}


		//We tell the joint what rotation we want by feeding it two (unit) vectors.
		if (trackJoint) {
			//to trick Unity into recalculating the targeted internal transformations, we'll switch the rigidbody on the joint
			Rigidbody differentRb = guideJoint.connectedBody ? null : GetComponent<Rigidbody>();
			Rigidbody originalRb = guideJoint.connectedBody;

			//switch rb so we can change things without fear
			guideJoint.connectedBody = differentRb;

			//move the guide to where we want it to be now
			guide.transform.position = trackTangent.position;
			guide.transform.rotation = trackTangent.rotation;

			//put the rb back as it should, causing Unity to recalculate the desired position for the joint
			guideJoint.connectedBody = originalRb;
		}

	}

	/**
	 * Does some bits to work around the fact that we aren't a first-class physics constraint.
	 */
	protected void PreserveCurve(SimpleTransform trackTangent) {
		//Instead of relying wholly on ERP to turn the cart on a curve, let's transfer the velocity to the new angle.
		//Because PhysX lies to us about speed when the track is moving, we can't use this when the track has a rigidbody.
		if (trackRB || cartRB.IsSleeping()) return;

		var cartVel = cartRB.velocity;
		var newDir = trackTangent.rotation * Vector3.forward;
		var movingForward = Mathf.Sign(Vector3.Dot(cartVel, newDir));
		var newVel = movingForward * cartVel.magnitude * newDir;
		cartRB.velocity = Vector3.Lerp(cartVel, newVel, curvePreservation);
	}

	/**
	 * The constraints do strange things, like give us the wrong speed while on a moving piece of track.
	 * 
	 * When we get off the track, call this and we'll fix it.
	 */
	protected void RepairVelocity(SimpleTransform trackTangent) {
		if (!trackRB) return;

		var vel = cartRB.velocity;

		//Find the component of our motion that doesn't align with the track and eliminate it.
		//Often, this works out to be "kick the cart so it's traveling the same speed as the track."
		var relVel = cartRB.velocity - GetTrackVelocityAt(trackTangent);
		var error = relVel - Vector3.Project(relVel, trackTangent.rotation * Vector3.forward);

		vel -= error;

		//Can't set velocity right now, things will still interact with it, do it in just a moment.
		StartCoroutine(DeferredSetVelocity(vel));
	}

	protected IEnumerator DeferredSetVelocity(Vector3 vel) {
		yield return new WaitForFixedUpdate();
		cartRB.velocity = vel;
	}

	protected void PushThings(SimpleTransform trackTangent, out float forwardness) {
		//forwardness measures which direction we are moving relative to the tracks's forward direction
		//at the current point
		forwardness = 0;

		if (!_currentTrack) return;

		//apply acceleration/deceleration as per the track's spec

		var velocity = GetVelocityOnTrack(trackTangent);

		forwardness = Vector3.Dot(velocity, trackTangent.rotation * Vector3.forward);
		//Track actions are based on track direction, not ours.
		if (cartReversed) forwardness *= -1;

		float speed = velocity.magnitude;

		F_msds getOptimumForce = (currentSpeed, optimumSpeed, mass) => {
			//f = m*a
			//s[t] = a*t + s[0] -> (s[t] - s[0]) / t = a
			//f = m * (s[t] - s[0]) / t
			float deltaV = optimumSpeed - currentSpeed;
			return mass * deltaV / Time.deltaTime;
		};

		if (_currentTrack.brakes == null) { Debug.LogWarning("No brakes object " + _currentTrack.name); }

		var brakesDirection = _currentTrack.brakes.IsActive(forwardness);
		var accelDirection = _currentTrack.acceleration.IsActive(forwardness);
		var brakesHoldingBack = false;

		if (brakesDirection != 0 && speed > _currentTrack.brakes.targetSpeed) {
			//slow down
			float force = -_currentTrack.brakes.maxForce;
			if (prorateForces) {
				float bestForce = getOptimumForce(speed, _currentTrack.brakes.targetSpeed, cartRB.mass);
				//Debug.Log("Best braking force: " + bestForce + " max breaking force " + force);
				if (bestForce > force) {//(force and bestForce should be negative, hence greater than)
					force = bestForce;
					//it's hard to push something to hold still, so when we get to where we aren't pushing as hard as possible 
					//we'll consider it all-but-stopped and clamp on the joint
					brakesHoldingBack = true;
				}
			}

			//Only apply force of we can enact brakes. When an object hits a stopped cart, {brakeJoint} takes the hit and 
			//absorbs energy. If we also apply our forces in that same tick we'll hold too firmly.
			if (brakesCooldown == 0) {
				AddForceFromTrack(velocity.normalized * force);
			}
		} else if (accelDirection != 0 && speed < _currentTrack.acceleration.targetSpeed) {
			//push faster in this direction
			float force = _currentTrack.acceleration.maxForce;
			if (prorateForces) {
				float bestForce = getOptimumForce(speed, _currentTrack.acceleration.targetSpeed, cartRB.mass);
				//Debug.Log("Best accel force: " + bestForce + " max accel force " + force);
				if (bestForce < force) force = bestForce;
			}

			var pushDirection = trackTangent.rotation * Vector3.forward * accelDirection;
			if (cartReversed) pushDirection = -pushDirection;
			//Debug.Log("push " + name + " " + pushDirection * force);
			AddForceFromTrack(pushDirection * force);
		}

		//If the brakes are trying to stop completely, lock in position when we stop
		const float stopThreshold = .006f;
		if (
			brakesCooldown == 0 &&
			brakesDirection != 0 && 
			_currentTrack.brakes.targetSpeed == 0
		) {
			if ((brakesHoldingBack || Mathf.Abs(forwardness) < stopThreshold) && !brakeJoint) {
				brakeJoint = gameObject.AddComponent<FixedJoint>();
				brakeJoint.connectedBody = _currentTrack.GetComponentInParent<Rigidbody>();
				brakeJoint.breakForce = _currentTrack.brakes.maxForce;
			}
		} else if (brakeJoint) {
			Destroy(brakeJoint);
			brakeJoint = null;
		}

	}

	/** Applies a force to the cart as if the force had come from the current track piece. */
	public void AddForceFromTrack(Vector3 force) {
		cartRB.AddForce(force);
		if (DebugDrawSpeed) {
			Debug.DrawLine(cartRB.transform.position, cartRB.transform.position + force, new Color(0, 1, 0, .5f));
		}
		if (trackRB) trackRB.AddForceAtPosition(-force, cartRB.transform.position);
	}

	protected void UpdateEndStop(SimpleTransform trackTangent, float forwardness) {
		if (!_currentTrack || _currentTrack.outOfTrack != Track.EndOfLineBehavior.EndStop) {
			if (endJoint) { 
				Destroy(endJoint);
				endJoint = null;
			}
			return;
		}



		if (
			//moving forward
			forwardness > 0 && 
			//no next track
			!_currentTrack.NextTrack && 
			//within 2 * clearingDistance of the end
			Vector3.Distance(_currentTrack.TrackAbsoluteEnd.position, transform.position) < clearingDistance * 2
		) {
			BlockMovement(trackTangent, _currentTrack.TrackAbsoluteEnd.position);

		} else if (
			//moving backward
			forwardness < 0 && 
			//no prev track
			!_currentTrack.PrevTrack && 
			//within 2 * clearingDistance of the start
			Vector3.Distance(_currentTrack.TrackAbsoluteStart.position, transform.position) < clearingDistance * 2
		) {
			BlockMovement(trackTangent.Clone().AboutFace(), _currentTrack.TrackAbsoluteStart.position);

		} else if (endJoint) { 
			Destroy(endJoint);
			endJoint = null;
		}
		
	}

	/** 
	 * Returns the world velocity of the track at the given point.
	 */
	protected Vector3 GetTrackVelocityAt(SimpleTransform trackTangent) {
		if (!trackRB) return Vector3.zero;
		return trackRB.GetPointVelocity(trackTangent.position);
	}

	/**
	 * Returns our velocity on the track, relative to the track, but in the world coordinate system.
	 * 
	 * This will account for errors in the physics system where a cert's velocity doesn't fully include 
	 * the track's velocity.
	 */
	public Vector3 GetVelocityOnTrack(SimpleTransform trackTangent = null) {
		if (!_currentTrack) return Vector3.zero;
		if (trackTangent == null) trackTangent = LastTangent;
		if (trackTangent == null) return Vector3.zero;


		var worldVel = cartRB.velocity;

		var vel = worldVel - GetTrackVelocityAt(trackTangent);

		//Ideally, vel would have the same (or opposite) direction as trackTangent.rotation * Vector3.forward
		//In practice, it can be way off when the track is moving because the physics system lies about cartRB.velocity
		//Cope:
		vel = Vector3.Project(vel, trackTangent.rotation * Vector3.forward);

		if (DebugDrawSpeed) {
			Debug.DrawLine(transform.position, transform.position + GetTrackVelocityAt(trackTangent), Color.blue);
			if (trackRB) Debug.DrawLine(trackRB.transform.position, trackRB.transform.position + trackRB.velocity, Color.yellow);
			Debug.DrawLine(transform.position, transform.position + worldVel, Color.magenta);
			Debug.DrawLine(transform.position, transform.position + vel, Color.red);
		}

		return vel;
	}

	protected void BlockMovement(SimpleTransform trackTangent, Vector3 trackEnd) {
		if (!endJoint) {
			endJoint = gameObject.AddComponent<ConfigurableJoint>();
			setAllAxies(endJoint, ConfigurableJointMotion.Free);
			endJoint.zMotion = ConfigurableJointMotion.Limited;
			endJoint.connectedBody = trackRB;
			endJoint.axis = Vector3.right;
			endJoint.secondaryAxis = Vector3.up;

			endJoint.autoConfigureConnectedAnchor = false;
			endJoint.anchor = Vector3.zero;//center of cart

			//Set the limit so we will "hit the end" when we our center is clearingDistance away from the track end.
			//(and we'll place the joint's "center" 2*clearingDistance away from the end)
			var l = endJoint.linearLimit;
			l.limit = clearingDistance;
			endJoint.linearLimit = l;
		}

		// The anchor should be 2*clearingDistance away from the end
		var anchorDistance = clearingDistance * 2;
		var anchorPos = trackEnd + trackTangent.backward * anchorDistance;
		// connectedAnchor is in local coordinates for the track/world
		endJoint.connectedAnchor = trackRB ? trackRB.transform.InverseTransformPoint(anchorPos) : anchorPos;
	}

	public void OnJointBreak(float force) {
		brakesCooldown = 2;
	}


	protected void SetupGuide() {
		if (guide) Object.Destroy(guide);

		//create the guide and set it on top of us
		guide = new GameObject("TrackCartGuide");
		guide.transform.parent = transform.parent;//same parent as the cart
		//guide.hideFlags = HideFlags.HideInHierarchy;

		//needs a rigidbody
		Rigidbody rb = guide.AddComponent<Rigidbody>();
		rb.drag = 0;
		rb.angularDrag = 0;
		rb.useGravity = cartRB.useGravity;
		//note: don't make the mass of the guide small. This typically will cause instability due to rounding, esp. if the cart is heavy

		//Create a fixed joint to tie the guide to the track.
		//align with track
		guide.transform.position = _currentTrack.transform.position;
		guide.transform.rotation = _currentTrack.transform.rotation;
		//we use a configurable joint instead of a fixed joint because a fixed joint seems to break down as we depart from the local origin
		guideJoint = guide.AddComponent<ConfigurableJoint>();
		setAllAxies(guideJoint, ConfigurableJointMotion.Locked);
		//set constraint's rigidbody to track's rigidbody if it has one and/or can move
		guideJoint.connectedBody = trackRB;
		guideJoint.swapBodies = true;//config joined + swapped bodies greatly increases stability


		//create the main "track" constraint as a simple free slider.
		//align with cart
		guide.transform.position = transform.position;
		guide.transform.rotation = transform.rotation;
		trackJoint = guide.AddComponent<ConfigurableJoint>();
		setAllAxies(trackJoint, ConfigurableJointMotion.Locked);
		trackJoint.zMotion = ConfigurableJointMotion.Free;
		trackJoint.anchor = Vector3.zero;
		//trackJoint.configuredInWorldSpace = true;
		//and bind it to our guide
		trackJoint.connectedBody = GetComponent<Rigidbody>();
	}

	protected void DestroyJoint() {
		Object.Destroy(guide);
		guide = null;
		guideJoint = null;
		trackJoint = null;
	}

	public void OnDisable() {
		DestroyJoint();
		Destroy(endJoint);
		endJoint = null;
		Destroy(brakeJoint);
		brakeJoint = null;
	}

	protected static void setAllAxies(ConfigurableJoint joint, ConfigurableJointMotion motion) {
		joint.xMotion = motion;
		joint.yMotion = motion;
		joint.zMotion = motion;
		joint.angularXMotion = motion;
		joint.angularYMotion = motion;
		joint.angularZMotion = motion;
	}

	public void RemoveFromTrack() {
		CurrentTrack = null;
	}

	/**
	 * Moves to the next track after this one (off the start of off the end).
	 * If the next piece of track is facing a different way, toggles the cart's direction 
	 * so it doesn't spin around.
	 */
	protected void RollToNextTrack(bool offEnd) {
		var nextTrack = offEnd ? _currentTrack.NextTrack : _currentTrack.PrevTrack;

		if (nextTrack && _currentTrack) {
			var leavingPoint = offEnd ? _currentTrack.TrackAbsoluteEnd.position : _currentTrack.TrackAbsoluteStart.position;
			var startNearest = nextTrack.IsStartNearest(leavingPoint);

			if ((offEnd && !startNearest) || (!offEnd && startNearest)) {
				cartReversed = !cartReversed;
			}
		}

		CurrentTrack = nextTrack;
	}

	[Obsolete("Use the CurrentTrack property instead.")]
	public void SwitchCurrentSectionTo(Track track) {
		CurrentTrack = track;
	}
}

}
