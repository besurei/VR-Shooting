/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */
namespace ZenFulcrum.Track {

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;


/** Custom editor for TrackCarts. */
[CustomEditor(typeof(TrackCart), true)]
[ExecuteInEditMode]
[CanEditMultipleObjects]
class TrackCartEditor : Editor {
	static bool showToolbox = true;

	override public void OnInspectorGUI() {
		DrawDefaultInspector();

		showToolbox = EditorGUILayout.Foldout(showToolbox, "Toolbox");
		if (showToolbox) RenderToolbox();
	}

	protected IEnumerable<TrackCart> SelectedCarts {
		get {
			foreach (var obj in serializedObject.targetObjects) {
				var p = obj as TrackCart;
				if (p != null) yield return p;
			}
		}
	}

	protected void RenderToolbox() {
		var allHaveTrack = (bool)SelectedCarts.All(x => x.CurrentTrack);

		if (GUILayout.Button(allHaveTrack ? "Snap to Track" : "Find and Snap to Track")) {
			foreach (var cart in SelectedCarts) {
				if (!cart.CurrentTrack) {
					Undo.RecordObject(cart, "Snap Cart to Track");
					FindNearestTrack(cart);
				}
				if (!cart.CurrentTrack) continue;

				var curve = cart.CurrentTrack.Curve;
				var fraction = curve.GetFraction(cart.CurrentTrack.transform.InverseTransformPoint(cart.transform.position));
				fraction = Mathf.Max(0, Mathf.Min(fraction, 1));
				var pos = cart.CurrentTrack.TrackAbsoluteStart * curve.GetPointAt(fraction);

				Undo.RecordObject(cart.transform, "Snap Cart to Track");
				EditorUtility.SetDirty(cart);

				if (cart.cartReversed) pos.AboutFace();
				cart.transform.position = pos.position;
				cart.transform.rotation = pos.rotation;
			}
		}
	}

	private void FindNearestTrack(TrackCart cart) {
		Track nearestTrack = null;
		var nearestDistance = float.PositiveInfinity;

		foreach (Track track in GameObject.FindObjectsOfType<Track>()) {
			if (!track.enabled) continue;
			var trackNearest = track.NearestPointAbsolute(cart.transform.position).position;
			var distance = Vector3.Distance(cart.transform.position, trackNearest);

			if (distance < nearestDistance) {
				nearestTrack = track;
				nearestDistance = distance;
			}
		}

		if (!nearestTrack) {
			Debug.LogWarning("Could not find a track to put this cart on", cart);
		}

		cart.CurrentTrack = nearestTrack;
	}
}

}
