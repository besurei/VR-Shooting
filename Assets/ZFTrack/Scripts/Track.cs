/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright © 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

namespace ZenFulcrum.Track {

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

/**
 * A piece/section of track!
 */
[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class Track : MonoBehaviour {

	#region Constants, static, types

	/** Tracks that are close enough and aligned within these tolerances may be considered connected. */
	const float connectTolerance = .001f;
	const float connectAngleTolerance = .1f;


	/** Returns true if the two transforms are close enough to be considered "aligned" and allow cars to pass across them. */
	public static bool IsAligned(SimpleTransform a, SimpleTransform b) {
		if (Vector3.Distance(a.position, b.position) > connectTolerance)
			return false;
		if (Quaternion.Angle(a.rotation, b.rotation) > connectAngleTolerance)
			return false;
		//Debug.Log("Found tolerable: " + Vector3.Distance(a.position, b.position) + " apart, " + Quaternion.Angle(a.rotation, b.rotation) + "deg off base");
		return true;
	}

	public enum EndOfLineBehavior {
		FallOffTrack,//have the cart fall off of the track
		ContinueStraight,//keep going as if the track continued straight
		EndStop,//stop as if we hit a wall
	}

	[System.Serializable]
	public class SpeedAndForce {
		public enum ActDirection {
			Both,
			Forward,
			Backward,
		}

		//[Range(0, Mathf.Infinity)] <- this doesn't work
		public float targetSpeed = 0, maxForce = 0;
		public ActDirection direction = ActDirection.Both;

		/**
		 * Given how fast an object is moving forward (or backward), returns whether or not this (ac|de)celerator
		 * is enabled, and if so, in which direction. (-1 backward, 0 not enabled, 1 forward)
		 * 
		 * {forwardSpeed} is used to calculate which direction we're acting in, but it does not take into account 
		 * if we need to apply additional forces (that is, we'll return 1 if we are moving forward faster than our target speed).
		 */
		public int IsActive(float forwardSpeed) {
			if (maxForce == 0) return 0;

			const float forwardnessTolerance = .001f;//Consider carts nearly stopped to be stopped within this delta

			if (direction == ActDirection.Both) {
				//we're enabled
				//Bias almost stopped carts forward
				if (forwardSpeed + forwardnessTolerance >= 0) return 1;
				else return -1;
			} else if (direction == ActDirection.Forward) {
				if (forwardSpeed + forwardnessTolerance >= 0) return 1;
				else return 0;
			} else if (direction == ActDirection.Backward) {
				//If a cart is almost stopped going forward, send it backward
				if (forwardSpeed - forwardnessTolerance <= 0) return -1;
				return 0;
			} else {
				return 0;
			}
		}
	}
	#endregion


	/** Position and direction of the endTransform of the track, relative to the start. */
	public SimpleTransform endTransform = new SimpleTransform(new Vector3(0, 0, 5));

	/**
	 * Type of interpolation to use to generate curves.
	 * Usually you can leave this alone.
	 */
	public TrackCurve.CurveType curveType = TrackCurve.CurveType.HermiteV2;

	public const float DefaultCurveStrength = 1.4142135f;
	public float curveStartStrength = DefaultCurveStrength, curveEndStrength = DefaultCurveStrength;

	/**
	 * Next and previous tracks in the chain.
	 *
	 * Theses fields are here so the editor can set up an initial state, use PrevTrack and NextTrack in your code
	 * (or anytime after Start is called) to change links at runtime.
	 *
	 */
	[SerializeField]
	protected Track _prevTrack, _nextTrack;

	/** What to do when a cart runs out of track. */
	public EndOfLineBehavior outOfTrack = EndOfLineBehavior.FallOffTrack;

	/**
	 * How much and how fast to speed up or slow down the cart while it is on this piece of track.
	 *
	 * The brakes will apply up to maxForce to a cart to stop it. If the cart is going slower, it does noting.
	 * The acceleration will apply up to maxForce to a cart to speed it up. If the cart is going faster, it does noting.
	 *
	 * To disable brakes or acceleration, set the maxForce for that respective modifier to zero.
	 *
	 * To emulate a chain lift, set acceleration to a moderately high force and a slow speed.
	 *
	 * You can enable both brakes and acceleration - use it to push a cart toward a target speed or to all-but-stop a
	 * cart yet keep it moving. (Typically make acceleration.targetSpeed < brakes.targetSpeed to be useful.)
	 * If the brakes are currently acting on the cart, the acceleration won't, irrespective of the targets given.
	 *
	 * Directions:
	 * "Forward" means the brakes or acceleration will only act if the cart is moving along the track's forward direction (toward the end).
	 * "Backwards" means it will only act if the cart is traveling towards the beginning of the track.
	 * "Both" means it will always act, regardless of direction.
	 * Brakes always slow, acceleration "amplifies" the cart's current motion.
	 *
	 * If a cart is stopped and acceleration is enabled, it will try to push the cart forward.
	 */
	public SpeedAndForce acceleration, brakes;

	/**
	 * These are the input meshes we use to generate the track's appearance.
	 * See the documentation for an explanation of what the meshes should contain.
	 */
	public Mesh railMesh, tieMesh;

	/** How often should track ties be added? */
	public float tieInterval = 2;

	/**
	 * How smooth should generated mesh be?
	 * Higher numbers yield smoother curves, but more polys.
	 * This is scaled by the track length (longer track segments will use more polys for the same value).
	 */
	public float resolution = 1f;

	/** If true, we will render ends caps even if we are linked to tracks. */
	public bool forceEndCaps = false;


	// That's all for properties you can change in the inspector.

	/** These events are fired when a cart enters or leaves a track. */
	public event Action<TrackCart> onCartEnter = cart => {};
	public event Action<TrackCart> onCartLeave = cart => {};


	protected Mesh generatedMesh;
	protected bool isStartup;

	/**
	 * Things we are ignoring collisions with.
	 * This is only part of the list: things we ignore collisions with are actually this list plus
	 * NextTrack.ignoredColliders and PrevTrack.ignoredColliders.
	 */
	protected List<Collider> ignoredColliders = new List<Collider>();

	protected TrackMeshGenerator meshGenerator;

	public void Awake() {
		//Try to find a mesh we can call our own...
		generatedMesh = TryToFindGeneratedMesh();

		if (!generatedMesh) {
			//...or make one to call our own.
			generatedMesh = new Mesh();
			GetComponent<MeshFilter>().sharedMesh = generatedMesh;
		}

		if (acceleration == null) acceleration = new SpeedAndForce();
		if (brakes == null) brakes = new SpeedAndForce();
	}

	public void OnDestroy() {
		//Cause our neighbors to regen and add caps.
		if (NextTrack) NextTrack.Dirty = true;
		if (PrevTrack) PrevTrack.Dirty = true;

#if UNITY_EDITOR
		if (Application.isEditor && !Application.isPlaying) {
			//Try to keep undo/redo/delete from leaking meshes.
			GameObject.DestroyImmediate(generatedMesh);

			//if (Event.current.commandName == "SoftDelete") {
				//So, here's a stupid Unity bug I can't find a workaround for:
				//Delete a track that's linked to other tracks, then undo the deletion.
				//The other tracks now show the undeleted object in their property tabs,
				//but their NextTrack/PrevTrack property for the undeleted track is == null in the code.
				//Saving the scene magically fixes it.
			//}
		}
#endif
	}

	protected Mesh TryToFindGeneratedMesh() {
		if (generatedMesh) return generatedMesh;

		//No mesh should ever be here but one we ourselves generated.
		//If there's a shared mesh, we are waking up from a save, so just grab what was saved with us.
		var mf = GetComponent<MeshFilter>();
		var currentMesh = mf.sharedMesh;

		//But, we could have been pasted/duplicated/Object.Instantiated. If so, we must make our own mesh.
		if (GameObject.FindObjectsOfType<MeshFilter>().Any(x => x != mf && x.sharedMesh == currentMesh)) {
			return null;
		} else {
			return currentMesh;
		}
	}

	public void Start() {
		isStartup = true;
		endTransform.MakeValid();
		UpdatePath();
		isStartup = false;
	}

	public void Update() {
		if (Dirty) {
			UpdatePath();
		}
	}

	/** Point (linear) halfway point between the start and the end of the track. */
	public Vector3 Centeroid {
		get {
			return (transform.position + TrackAbsoluteEnd.position) * .5f;
		}
	}

	/**
	 * Returns the length of this section of track, including any bends and curves of the track.
	 * This is the "ride" length of the track.
	 * To get linear distance use "Distance".
	 */
	public float Length {
		get { return Curve.Length; }
	}

	/**
	 * Returns the distance between the start of the track and the end of the track.
	 * This is a pure linear distance "as the crow flies".
	 * To account for track bends use "Length".
	 */
	public float Distance {
		get { return endTransform.position.magnitude; }
	}

	protected TrackCurve _curve;
	public TrackCurve Curve {
		get {
			if (_curve == null) {
				try {
					_curve = new TrackCurve(curveType, TrackEnd, curveStartStrength, curveEndStrength);
				} catch (ArgumentOutOfRangeException) {
					Debug.LogError("This track's end is coincident with the start.", gameObject);
					Debug.LogWarning("Moving start and end apart.", gameObject);
					this.TrackEnd = new SimpleTransform(new Vector3(0, 0, 1), endTransform.rotation);
					_curve = new TrackCurve(curveType, TrackEnd, curveStartStrength, curveEndStrength);
				}
			}
			return _curve;
		}
		protected set {
			_curve = value;
		}
	}

	/**
	 * Prev/next track.
	 *
	 * Note that, as this system supports linking track back onto itself, it's possible for
	 * not all track to face the same direction. You must (un)link both a to b and b to a.
	 */
	public Track PrevTrack {
		get { return _prevTrack; }
		set {
			var oldPrev = _prevTrack;
			DisableIgnoredColliders();//resume collisions with anything we are unlinking from
			_prevTrack = value;
			RefreshIgnoredColliders();//ignore collisions with adjacent track again
			if (oldPrev) oldPrev.RefreshIgnoredColliders();

			if ((bool)oldPrev != (bool)_prevTrack) Dirty = true;
		}
	}
	public Track NextTrack {
		get { return _nextTrack; }
		set {
			var oldNext = _nextTrack;
			DisableIgnoredColliders();//resume collisions with anything we are unlinking from
			_nextTrack = value;
			RefreshIgnoredColliders();//ignore collisions with adjacent track again
			if (oldNext) oldNext.RefreshIgnoredColliders();

			//If the need for an end cap has changed, regen the mesh.
			if ((bool)oldNext != (bool)_nextTrack) Dirty = true;
		}
	}

	/** Position + direction of the start of the track. The relative start is always the unrotated origin of the object. */
	public SimpleTransform TrackStart {
		get { return new SimpleTransform(); }
		//No setter: The relative start of a track is always forward at the origin of this object and cannot be changed.
	}

	/** Position + direction of the start of the track in world coordinates. */
	public SimpleTransform TrackAbsoluteStart {
		get { return new SimpleTransform(transform.position, transform.rotation); }
		set {
			//Move start, preserve end
			SimpleTransform oldEnd = TrackAbsoluteEnd;

			transform.position = value.position;
			transform.rotation = value.rotation;

			TrackAbsoluteEnd = oldEnd;

			Dirty = true;
		}
	}


	/** Relative-to-self end of this track. */
	public SimpleTransform TrackEnd {
		get { return endTransform.Scaled(transform.lossyScale); }
		set {
			endTransform = value;
			Dirty = true;
		}
	}

	/** The relative-to-world end of this track. */
	public SimpleTransform TrackAbsoluteEnd {
		get { return transform * endTransform.Scaled(transform.lossyScale); }
		set {
			endTransform.position = transform.InverseTransformPoint(value.position);
			endTransform.rotation = Quaternion.Inverse(transform.rotation) * value.rotation;
			Dirty = true;
		}
	}

	/** Sets our next track to "next" and moves our end to line up with its start. (Alias for SnapTogether(next, true, true).) */
	public void ConnectTo(Track next) {
		SnapTogether(next, true, true);
	}

	/** Returns true if the start of this track is nearest the given point, false if the end of this track is nearer. */
	public bool IsStartNearest(Vector3 pos) {
		return Vector3.Distance(transform.position, pos) < Vector3.Distance(TrackAbsoluteEnd.position, pos);
	}

	/** Returns the position+rotation on the track (in local space) that is nearest the given point (in local space). */
	public SimpleTransform NearestPoint(Vector3 pos) {
		var nearestFraction = Curve.GetFraction(pos);
		return Curve.GetPointAt(Mathf.Clamp(nearestFraction, 0, 1));
	}

	/** Returns the position+rotation on the track (in world space) that is nearest the given point (in world space). */
	public SimpleTransform NearestPointAbsolute(Vector3 pos) {
		var localPoint = transform.InverseTransformPoint(pos);
		return transform * NearestPoint(localPoint);
	}


	/**
	 * Moves our start or end (or target's start or end) to the target's/our end/start.
	 * If snapMe is true, moves me, if false moves the other piece.
	 * If snapEnd is true, our end is moving/being moved, if false our start is moving/being moved.
	 *
	 * Also automatically links the tracks.
	 */
	public void SnapTogether(Track target, bool snapMe, bool snapEnd) {
		if (snapEnd) {
			if (snapMe) {
				TrackAbsoluteEnd = target.TrackAbsoluteStart;
			} else {
				target.TrackAbsoluteStart = TrackAbsoluteEnd;
			}
			NextTrack = target;
			target.PrevTrack = this;
		} else {
			if (snapMe) {
				TrackAbsoluteStart = target.TrackAbsoluteEnd;
			} else {
				target.TrackAbsoluteEnd = TrackAbsoluteStart;
			}
			PrevTrack = target;
			target.NextTrack = this;
		}
	}

	/** Is our representation (physics/appearance) out-of-date? */
	[System.NonSerialized]
	protected bool _dirty = true;
	public bool Dirty {
		get { return _dirty; }
		set {
			if (value) _curve = null;
			_dirty = value;
		}
	}



	//syntactic garbage C# requires for its closures
	delegate int I_V3(Vector3 v);
	delegate void V_III(int a, int b, int c);
	delegate GameObject Go_T(SimpleTransform transform, float length);

	#region Generated Mesh and Items


	/**
	 * Updates the representation of the object.
	 * This isn't called every frame (unless the track is actively twisting and warping).
	 */
	protected void UpdatePath() {
		GenerateMesh();
		GenerateCollider();

		Dirty = false;
	}

	protected void GenerateCollider() {
		var meshCollider = GetComponent<MeshCollider>();
		if (!meshCollider) meshCollider = gameObject.AddComponent<MeshCollider>();

		if (!meshCollider.enabled) return;

#if UNITY_EDITOR
		if (GetComponentInParent<Rigidbody>() && !meshCollider.convex) {
			// Unity doesn't have concave moving colliders.
			Debug.LogWarning("To allow approximate collisions on this track, mark the mesh collider as convex", meshCollider);
		}
#endif

		//for now, we just use the visual mesh
		meshCollider.sharedMesh = generatedMesh;

		RefreshIgnoredColliders();
	}

	/**
	 * If you dynamically swap out the meshes used to generate this track call this to make sure
	 * the mesh gets regenerated correctly.
	 */
	public void ResetMeshGenerator() {
		Dirty = true;
		meshGenerator = null;
	}

	protected void GenerateMesh() {
		if (!railMesh && !tieMesh) {
			generatedMesh.Clear();
			return;
		}

		//Profiler.BeginSample("Track.GenerateMesh", this);

#if UNITY_EDITOR
		if (gameObject.isStatic && Application.isPlaying && !isStartup) {
			Debug.LogWarning("Trying to update a mesh on a track marked as static", this);
		}
#endif

		if (meshGenerator == null) meshGenerator = new TrackMeshGenerator();
		meshGenerator.SetMeshes(railMesh, tieMesh);
		meshGenerator.GenerateMesh(generatedMesh, this);
#if UNITY_EDITOR
		if (gameObject.isStatic) {
			Unwrapping.GenerateSecondaryUVSet(generatedMesh);
		}
#endif
		meshGenerator.GenerateMesh(generatedMesh, this);

		//Profiler.EndSample();
	}

	#endregion

	/**
	 * Returns the next track in the chain.
	 * Tracks have direction, but don't have to be linked in any particular order.
	 * It's possible and valid for two adjacent tracks to "point at" each other.
	 * By asking for the previous track piece we can return the piece that will continue in the direction you were traveling,
	 * irrespective of the underlying track direction.
	 */
	public Track GetNext(Track prev) {
		if (prev == PrevTrack) {
			return NextTrack;
		} else if (prev == NextTrack) {
			return PrevTrack;
		} else {
			//not sure what you did here, but we're not connected to it.
			Debug.LogWarning("called Track.getNext with a previous track that's not connected - " + name);
			return null;
		}
	}

	/**
	 * Keeps the given collider form colliding with this track and its adjacent tracks.
	 *
	 * Use this instead of Physics.IgnoreCollision or else a mesh regen will revert your change.
	 *
	 * Collisions are ignored until you unignore them. Ignoring a collider multiple times requires unignoring it
	 * the same number of times to re-enable collisions.
	 */
	public void IgnoreCollisionsWith(Collider other) {
		ignoredColliders.Add(other);
		__ignoreCollisions(other, true);

		if (NextTrack) NextTrack.RefreshIgnoredColliders();
		if (PrevTrack) PrevTrack.RefreshIgnoredColliders();
	}

	/**
	 * Reverts IgnoreCollisionsWith for the given collider.
	 */
	public void ResumeCollisionsWith(Collider other) {

		if (NextTrack) {
			NextTrack.DisableIgnoredColliders();
			NextTrack.RefreshIgnoredColliders();
		}

		if (PrevTrack) {
			PrevTrack.DisableIgnoredColliders();
			PrevTrack.RefreshIgnoredColliders();
		}

		__ignoreCollisions(other, false);
		ignoredColliders.Remove(other);
	}

	/** Updates the physics system with our list of ignored colliders after fiddling with our collider, deactivation, etc. */
	protected void RefreshIgnoredColliders() {
		foreach (var other in ignoredColliders) __ignoreCollisions(other, true);
		if (NextTrack) foreach (var other in NextTrack.ignoredColliders) __ignoreCollisions(other, true);
		if (PrevTrack) foreach (var other in PrevTrack.ignoredColliders) __ignoreCollisions(other, true);
	}

	/** Resumes ignored collisions with everything for a moment (such as before unlinking track). */
	protected void DisableIgnoredColliders() {
		foreach (var other in ignoredColliders) __ignoreCollisions(other, false);
		if (NextTrack) foreach (var other in NextTrack.ignoredColliders) __ignoreCollisions(other, false);
		if (PrevTrack) foreach (var other in PrevTrack.ignoredColliders) __ignoreCollisions(other, false);
	}


	/** Non-persistent, momentary, low-level. Ignores (or un-ignores) collisions for the given collider. */
	protected void __ignoreCollisions(Collider other, bool ignore) {
		//skip if the other thing isn't or isn't active and collidable
		if (!other || !other.enabled) return;
		var oMC = other as MeshCollider;
		if (oMC && !oMC.sharedMesh) return;

		_IgnoreCollisionIfAble(GetComponent<Collider>(), other, ignore);
	}

	protected void _IgnoreCollisionIfAble(Collider a, Collider b, bool ignore) {
		if (!a || !a.enabled || !b || !b.enabled) return;

		var aMC = a as MeshCollider;
		if (aMC && (!aMC.sharedMesh || aMC.sharedMesh.vertexCount == 0)) return;

		var bMC = a as MeshCollider;
		if (bMC && (!bMC.sharedMesh || bMC.sharedMesh.vertexCount == 0)) return;

		//Debug.Log("nocollide " + ignore + " " + a.name + " and  " + b.name);
		Physics.IgnoreCollision(a, b, ignore);
	}

	internal virtual void FireCartEnter(TrackCart cart) {
		onCartEnter(cart);
	}

	internal virtual void FireCartLeave(TrackCart cart) {
		onCartLeave(cart);
	}
}

}
