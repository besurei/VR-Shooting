/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */
namespace ZenFulcrum.Track {

using UnityEditor;
using UnityEngine;

class TrackAssetHook : AssetPostprocessor {
	public void OnPostprocessModel(GameObject model) {
		//If a mesh imported, cause all the Track to regen (and therefore reflect any changes in their models).
		foreach (var track in Object.FindObjectsOfType<Track>()) {
			track.ResetMeshGenerator();
		}
	}
}

}
