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

/** Utility functions for track editing. */
class TrackEditorUtil {

	/** 
	 * If the given tracks are all linked together in the same direction, this returns the tracks, in order, else null.
	 */
	public static List<Track> IsContiguousSelection(Object[] objects) {
		List<Track> tracks = new List<Track>(objects.Length);

		foreach (var obj in objects) {
			var track = obj as Track;
			if (!track) return null;
			tracks.Add(track);
		}

		List<Track> seenTracks = new List<Track>(tracks.Count);
		List<Track> unseenTracks = new List<Track>(tracks.Count);

		foreach (var track in tracks) {
			seenTracks.Clear();
			unseenTracks.Clear();
			unseenTracks.AddRange(tracks);

			var current = track;

			while (unseenTracks.Count != 0 && current) {
				var hadIt = unseenTracks.Remove(current);

				if (!hadIt) break;

				seenTracks.Add(current);

				current = current.NextTrack;
			}

			//Don't allow selected loops.
			if (current == track) return null;

			if (unseenTracks.Count == 0) {
				return seenTracks;
			}
		}

		return null;
	}

	/** Adds the given object to the selection and make it the "main" item among the selected items. */
	public static void AddToSelection(GameObject obj) {
		var objects = Selection.objects.ToList();
		if (!objects.Contains(obj)) objects.Add(obj);


		//Force this object to be the "last selected"
		Selection.activeObject = obj;
		//And select everything else too.
		Selection.objects = objects.ToArray();
	}

	public static void SelectConnected(Track track) {
		var selected = new HashSet<Track>();

		selected.Add(track);
		_SelectConnected(track, selected);
		Selection.activeObject = track;
		Selection.objects = selected.Select(x => x.gameObject).ToArray();
	}

	private static void _SelectConnected(Track track, HashSet<Track> selected) {
		if (track.NextTrack && !selected.Contains(track.NextTrack)) {
			selected.Add(track.NextTrack);
			_SelectConnected(track.NextTrack, selected);
		}

		if (track.PrevTrack && !selected.Contains(track.PrevTrack)) {
			selected.Add(track.PrevTrack);
			_SelectConnected(track.PrevTrack, selected);
		}
	}
}

}
