using UnityEngine;

namespace ZenFulcrum.Track {

/** 
 * Spins an object with the track its on. 
 * Do not give the wheels rigidbodies, make them direct children of the cart with no other parents.
 * 
 * Doesn't support spinning wheels when we fall off the track.
 */
public class WheelSpin : MonoBehaviour {
	public float wheelRadius = .25f;

	protected TrackCart cart;
	protected Rigidbody rb;

	protected void Start() {
		cart = gameObject.GetComponentInParent<TrackCart>();
		rb = cart.GetComponent<Rigidbody>();
		if (!cart) {
			Debug.LogWarning("No parent TrackCart, cannot spin wheels", this);
			return;
		}

	}

	protected void Update() {
		if (!cart.CurrentTrack) return;

		var rot = transform.rotation;

		var distance = Vector3.Dot(cart.GetVelocityOnTrack(), cart.transform.forward) * Time.deltaTime;
		var angle = distance / (2 * Mathf.PI * wheelRadius) * 360;

		rot = Quaternion.AngleAxis(angle, cart.transform.right) * rot;

		transform.rotation = rot;

	}

}

}
