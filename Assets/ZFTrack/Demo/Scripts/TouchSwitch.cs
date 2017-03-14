/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

namespace ZenFulcrum.Track {

using UnityEngine;
using System.Collections;

public class TouchSwitch : MonoBehaviour {
	public TrackSwitcher switcher;

	public void OnMouseDown() {
		switcher.Switch();	
	}
}

}
