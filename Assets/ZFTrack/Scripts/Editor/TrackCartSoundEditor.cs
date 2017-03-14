/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

// ReSharper disable LocalVariableHidesMember
namespace ZenFulcrum.Track {

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/** Custom editor for TrackCartSounds */
[CustomEditor(typeof(TrackCartSound), true)]
[ExecuteInEditMode]
[CanEditMultipleObjects]
class TrackCartSoundEditor : Editor {
	protected static int currentClip;

	override public void OnInspectorGUI() {
		DrawDefaultInspector();

		EditorGUI.BeginChangeCheck();

		if (serializedObject.isEditingMultipleObjects) {
			GUILayout.Label("Multi-editing sound clips is not supported");
			return;
		}

		if (currentClip > cartSound.clips.Count) currentClip = cartSound.clips.Count;

		var currentText = (currentClip < cartSound.clips.Count) ? (currentClip + 1).ToString() : "+";
		GUILayout.Label("Clips (" + currentText + "/" + cartSound.clips.Count + ")");

		{
			GUILayout.BeginHorizontal(GUIStyle.none);
			GUI.enabled = currentClip > 0;
			if (GUILayout.Button("<<")) {
				--currentClip;
				GUI.FocusControl(null);
			}

			GUI.enabled = currentClip < cartSound.clips.Count;
			if (GUILayout.Button(">>")) {
				++currentClip;
				GUI.FocusControl(null);
			}

			GUI.enabled = true;
			if (GUILayout.Button("Add")) {
				Undo.RecordObject(cartSound, "Add sound clip");
				cartSound.clips.Insert(
					currentClip,
					new CartSoundClipInfo()
				);
			}

			GUI.enabled = currentClip < cartSound.clips.Count;
			if (GUILayout.Button("Delete")) {
				Undo.RecordObject(cartSound, "Remove sound clip");
				cartSound.clips.RemoveAt(currentClip);
			}

			GUILayout.EndHorizontal();
		}


		{
			GUILayout.BeginVertical("box");
			if (currentClip == cartSound.clips.Count) {
				GUILayout.Label("<Click Add to add a new clip.>");
			} else {
				RenderClipGUI();
			}


			GUILayout.EndVertical();
		}


		GUI.enabled = true;

		if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
	}

	protected TrackCartSound cartSound {
		get { return (TrackCartSound)serializedObject.targetObject;	}
	}

	protected SerializedObject cartSer {
		get {
			return new UnityEditor.SerializedObject(cartSound);
		}
	}

	protected Rect workingRange {
		get {
			return new Rect(0, 0, 1, 1);
		}
	}

	protected void RenderClipGUI() {
		EditorGUI.BeginChangeCheck();

		var cartSer = this.cartSer;
		var cartSound = this.cartSound;
		cartSer.Update();

		var clipInfo = cartSound.clips[currentClip];
		if (!clipInfo) {
			//oh dear, how did this happen?
			Debug.LogWarning("Found bad data in unserialized object", cartSound);
			clipInfo = new CartSoundClipInfo();//ScriptableObject.CreateInstance<CartSoundClipInfo>();
			cartSound.clips[currentClip] = clipInfo;
		}

//		Undo.RecordObject(cartSound, "Inspector");

		var clipProperty = "clips.Array.data[" + currentClip + "]";

		EditorGUILayout.PropertyField(cartSer.FindProperty(clipProperty + ".clip"));
		EditorGUILayout.PropertyField(cartSer.FindProperty(clipProperty + ".referenceSpeedPercent"));
		EditorGUILayout.PropertyField(cartSer.FindProperty(clipProperty + ".speedScale"));

		EditorGUILayout.LabelField("Volume vs. Speed");

		var curvesArea = GUILayoutUtility.GetRect(150, Screen.width, 230, 230);
		FillRect(curvesArea, curvesBackground);

		RenderAllCurves(curvesArea);

		//Show reference speed on graph.
		DrawReferenceLine(curvesArea, clipInfo.referenceSpeedPercent, referenceLineColor);


		//FIXYOU: Unity's draw curve swatch et al cache the curve shape too aggressively and won't update when you undo/redo.
		//To workaround it, we always pass a new curve instance to Unity so it can't cache based on reference.

		//FIXYOU: Unity doesn't support editing the curve directly here. WHHYYY? :-(
		var proxyCurve = new AnimationCurve(clipInfo.volumeVsSpeed.keys);
		EditorGUIUtility.DrawCurveSwatch(
			curvesArea,
			proxyCurve, null,//graphProp,
			activeCurve, transparent,
			workingRange
		);

		//...so we'll make them edit it over here instead, I guess.
		//FIXYOU: This is affected by the undo bug mentioned above.
		//Fortunately, this is really small below a big version that will update correctly.
		EditorGUILayout.PropertyField(cartSer.FindProperty(clipProperty + ".volumeVsSpeed"), new GUIContent("Volume Curve"));

		if (Application.isPlaying) {
			//Show current speed when running
			var speed = cartSound.GetComponent<Rigidbody>().velocity.magnitude;
			speed = Mathf.Min(cartSound.maxSpeed, speed);
			DrawReferenceLine(curvesArea, speed / cartSound.maxSpeed, Color.red);
		}

		if (EditorGUI.EndChangeCheck()) cartSer.ApplyModifiedProperties();

		//Buttons
		{
			GUILayout.BeginHorizontal(GUIStyle.none);

			int[] toReset = null;

			if (GUILayout.Button("Reset Curve")) {
				toReset = new[] { currentClip };
			}

			if (GUILayout.Button("Reset All Curves")) {
				toReset = Enumerable.Range(0, cartSound.clips.Count).ToArray();
			}

			if (toReset != null) ResetClips(toReset);

			GUILayout.EndHorizontal();
		}
	}

	public override bool RequiresConstantRepaint() {
		//If we are playing, request to be redrawn frequently so the speed line is always up-to-date.
		return Application.isPlaying;
	}

	private void ResetClips(IEnumerable<int> clips) {
		var cartSer = this.cartSer;
		var cartSound = this.cartSound;

		Undo.RecordObject(cartSound, "Reset sound curve");

		foreach (var index in clips) {
			var clipInfo = cartSound.clips[index];
			var vvsProp = cartSer.FindProperty("clips.Array.data[" + index + "].volumeVsSpeed");

			//This crashes Mono (even without nullables):
			//var prevReferencePos = cartSound.clips
			//	.Select(x => x.referenceSpeed as float?)
			//	.Where(x => x < clipInfo.referenceSpeed)
			//	.Max() ?? 0;
			//So we'll use something less clever, I guess.
			float prevReferencePos = 0, nextReferencePos = cartSound.maxSpeed;
			foreach (var thisClipInfo in cartSound.clips) {
				if (thisClipInfo == clipInfo) continue;
				var p = thisClipInfo.referenceSpeedPercent;
				if (p > clipInfo.referenceSpeedPercent) nextReferencePos = Mathf.Min(p, nextReferencePos);
				else prevReferencePos = Mathf.Max(p, prevReferencePos);
			}

			AnimationCurve newCurve;
			const float curveTop = .5f;
			if (index == cartSound.clips.Count - 1) {
				newCurve = new AnimationCurve(
					new Keyframe(0, 0),
					new Keyframe(prevReferencePos, 0),
					new Keyframe(clipInfo.referenceSpeedPercent, curveTop),
					new Keyframe(1, curveTop)
				);
			} else {
				newCurve = new AnimationCurve(
					new Keyframe(0, 0),
					new Keyframe(prevReferencePos, 0),
					new Keyframe(clipInfo.referenceSpeedPercent, curveTop),
					new Keyframe(nextReferencePos, 0),
					new Keyframe(1, 0)
				);
			}


			//The serialized stuff glitches out if you don't update the value this way.
			vvsProp.animationCurveValue = clipInfo.volumeVsSpeed = newCurve;
		}

		Undo.FlushUndoRecordObjects();
	}

	private void DrawReferenceLine(Rect curvesArea, float speedPercent, Color color) {
		var refPos = speedPercent;
		refPos = Mathf.Max(0, Mathf.Min(refPos, 1));
		refPos *= curvesArea.width;

		FillRect(new Rect(refPos + curvesArea.x, curvesArea.y, 1, curvesArea.height), color);
	}


	/**
	 * EditorGUIUtility.DrawColorSwatch is completely broken, it always leaves about 40 px of "undefined"
	 * garbage at the bottom.
	 * FIXYOU: this function is needed because of that bug
	 *
	 * Without further ado, here's another workaround to deal with another Unity bug:
	 */
	private void FillRect(Rect area, Color color) {
		// http://answers.unity3d.com/questions/37752/how-to-render-a-colored-2d-rectangle.html

		//(Correctly caching these resources in editor mode
		//while there's no perceptible performance impact is simply not worth it.)
		var filler = new Texture2D(1, 1);
		var fillerStyle = new GUIStyle();
		fillerStyle.normal.background = filler;
		fillerStyle.margin = new RectOffset();

		filler.SetPixel(0, 0, color);
		filler.Apply();

		GUI.Box(area, GUIContent.none, fillerStyle);

		Object.DestroyImmediate(filler);
	}

	static readonly Color activeCurve = Color.green;
	static readonly Color inactiveCurve = new Color(.8f, .8f, .8f);
	static readonly Color transparent = new Color(0, 0, 0, 0);
	static readonly Color curvesBackground = new Color(0, .2f, 0, 1);
	static readonly Color referenceLineColor = new Color32(20, 200, 200, 255);
	static readonly Color referenceLineBackgroundColor = new Color32(0, 0, 0, 255);


	private void RenderAllCurves(Rect curvesArea) {
		foreach (var clipInfo in cartSound.clips) {
			if (clipInfo.volumeVsSpeed == null) continue;

			DrawReferenceLine(curvesArea, clipInfo.referenceSpeedPercent, referenceLineBackgroundColor);

			EditorGUIUtility.DrawCurveSwatch(
				curvesArea,
				//FIXYOU: this is needed due to the undo bug mentioned earlier
				new AnimationCurve(clipInfo.volumeVsSpeed.keys), null,
				inactiveCurve / 2f, transparent,
				workingRange
			);
		}
	}
	
}

}
