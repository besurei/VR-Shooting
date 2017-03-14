/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */
namespace ZenFulcrum.Track {

using System.Collections.Generic;

public static class ListExtensions {
	/** Ensures that there is at least "neededCount" free space in the list's capacity. */
	public static void EnsureSpace<T>(this List<T> list, int neededCount) {
		var free = list.Capacity - list.Count;
		if (free < neededCount) {
			list.Capacity = list.Capacity + neededCount;
		}
	}
}

}
