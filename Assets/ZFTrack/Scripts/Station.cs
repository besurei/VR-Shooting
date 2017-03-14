using System;
using UnityEngine;
using System.Collections;

namespace ZenFulcrum.Track {

/**
 * A track station that automatically stops and starts carts that roll onto it.
 * This will change/override the brakes/acceleration of the attached track piece to 
 * get the desired result.
 * 
 * This will only function correctly with one cart at a time.
 */
[RequireComponent(typeof(Track))]
public class Station : MonoBehaviour {

	[Tooltip("How long should we wait with a cart before sending it off, in seconds? Set to negative to never send it off automatically.")]
	public float waitTime = 5;

	public TrackCart[] cartsToStop;

	public Track.SpeedAndForce startingForce = new Track.SpeedAndForce() {
		direction = Track.SpeedAndForce.ActDirection.Forward,
		maxForce = 5,
		targetSpeed = 10,
	};

	public Track.SpeedAndForce stoppingForce = new Track.SpeedAndForce() {
		direction = Track.SpeedAndForce.ActDirection.Both,
		maxForce = 10,
		targetSpeed = 0,
	};

	public float speedTolerance = .01f;

	/** Fired when a cart enters our track and we start to slow it. */
	public event Action<TrackCart> onCartArriving = cart => {};
	/** Fired when a cart has entered our track and slowed to the desired speed. */
	public event Action<TrackCart> onCartArrived = cart => {};
	/** Fired when we start to push a cart out. */
	public event Action<TrackCart> onCartLeaving = cart => {};
	/** Fired when a cart has completely left out track section. */
	public event Action<TrackCart> onCartLeft = cart => {};

	private Track track;
	private TrackCart currentCart;
	private float waitStartTime;
	private bool holdCart = false;

	public void Start() {
		track = GetComponent<Track>();
		StartCoroutine(WaitForCart());
	}

	private readonly WaitForFixedUpdate fixedWait = new WaitForFixedUpdate();

	/** Watches the current cart. If it leaves, fires events, nulls the cart, and returns false. */ 
	private bool VerifyCart() {
		if (!currentCart || currentCart.CurrentTrack != track) {
			var oldCart = currentCart;
			currentCart = null;
			onCartLeft(oldCart);
			return false;
		}
		return true;
	}

	private IEnumerator WaitForCart() {
		track.acceleration = new Track.SpeedAndForce();
		track.brakes = stoppingForce;

		while (!currentCart) {
			foreach (var cart in cartsToStop) {
				if (cart.CurrentTrack == track) {
					currentCart = cart;
					holdCart = true;
					onCartArriving(currentCart);
					StartCoroutine(StopTheCart());
					yield break;
				}
			}

			yield return fixedWait;
		}
	}

	private IEnumerator StopTheCart() {
		var rb = currentCart.GetComponent<Rigidbody>();

		//brakes were already set in WaitForCart, so just wait
		while (VerifyCart() && rb.velocity.magnitude - speedTolerance > stoppingForce.targetSpeed && holdCart) {
			yield return fixedWait;
		}

		if (!holdCart) yield break;

		StartCoroutine(currentCart ? WaitForLoading() : WaitForCart());
	}

	private IEnumerator WaitForLoading() {
		onCartArrived(currentCart);

		if (waitTime < 0) waitStartTime = float.PositiveInfinity;
		else waitStartTime = Time.fixedTime;

		var _waitTime = Mathf.Max(waitTime, 0);

		//wait for it to slow down
		while (VerifyCart() && Time.fixedTime - waitStartTime < _waitTime && holdCart) {
			//note that we always evaluate the time incrementally instead of using WaitForSeconds because 
			//Send() could be called while we are waiting.
			yield return fixedWait;
		}

		if (!holdCart) yield break;

		if (currentCart) Send();
		else StartCoroutine(WaitForCart());
	}

	/** Sends the current train, if any, off. */
	public void Send() {
		holdCart = false;
		onCartLeaving(currentCart);
		StartCoroutine(StartTheCart());
	}
	
	private IEnumerator StartTheCart() {
		track.acceleration = startingForce;
		track.brakes = new Track.SpeedAndForce();

		while (VerifyCart()) yield return fixedWait;

		StartCoroutine(WaitForCart());
	}
}

}
