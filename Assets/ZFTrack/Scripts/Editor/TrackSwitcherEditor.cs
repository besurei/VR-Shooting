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

/** Custom editor for TrackSwitcher */
[CustomEditor(typeof(TrackSwitcher), true)]
[ExecuteInEditMode]
[CanEditMultipleObjects]
class TrackSwitcherEditor : Editor {
	public override void OnInspectorGUI() {
		DrawDefaultInspector();

		GUI.enabled = Application.isPlaying;
		var text = Application.isPlaying ? "Switch" : "Switch (available in play mode)";
		if (GUILayout.Button(text)) {
			foreach (TrackSwitcher switcher in serializedObject.targetObjects) {
				switcher.Switch();
			}
		}
	}
}

}
