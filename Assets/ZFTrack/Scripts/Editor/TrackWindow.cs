using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZenFulcrum.Track {
public class TrackWindow : EditorWindow {
	public const string WindowName = "Track Bender";
	public const int ButtonHeight = 40;

	public class ToggleGroup {
		public int perRow;
		public int selected;
		public string[] names;
		public ToggleElement[] items;
	}

	public class ToggleElement {
		public Action action;
		public Vector3 shim = new Vector3(Single.NaN, Single.NaN, Single.NaN);
		public Vector3 bend = new Vector3(Single.NaN, Single.NaN, Single.NaN);
	}

	/**
	 * Direction the track is offset, relative to the end of the track.
	 */
	private Vector3 shim = new Vector3(0, 0, 5);

	/**
	 * Direction of the track, in Euler angles, in the world coordinate system.
	 */
	private Vector3 bend;


	private readonly ToggleGroup aimGrid = new ToggleGroup();
	private readonly ToggleGroup lengthButtons = new ToggleGroup();

	private List<Action> deferredActions = new List<Action>();

	private int workDirection = 1;
	private int alignViewIdx;
	private float snapAngle = 45;
	private float snapLength = 1;

	private bool ignoreNextChange = false;

	[MenuItem("Window/" + WindowName)]
	public static void OpenIt() {
		EditorWindow.GetWindow<TrackWindow>().Show();
	}

	public void OnEnable() {
#if UNITY_5_0 || UNITY_5_1 || UNITY_5_2
		title = WindowName;
#else
		titleContent = new GUIContent(WindowName);
#endif

		Undo.undoRedoPerformed += OnUndoRedo;

		aimGrid.perRow = 3;
		aimGrid.selected = -1;
		aimGrid.names = new[] {
			"↶", "↑", "↷",
			"←", "Build", "→",
			"Straight", "↓", "Flat",
		};
		aimGrid.items = new[] {
			new ToggleElement() {bend = new Vector3(float.NaN, float.NaN, 1)},
			new ToggleElement() {bend = new Vector3(-1, float.NaN, float.NaN)},
			new ToggleElement() {bend = new Vector3(float.NaN, float.NaN, -1)},

			new ToggleElement() {bend = new Vector3(float.NaN, -1, float.NaN)},
			new ToggleElement() {action = Build},
			new ToggleElement() {bend = new Vector3(float.NaN, 1, float.NaN)},

			new ToggleElement() {action = Straighten},
			new ToggleElement() {bend = new Vector3(1, float.NaN, float.NaN)},
			new ToggleElement() {action = Flatten},
		};

		lengthButtons.perRow = 4;
		lengthButtons.selected = workDirection > 0 ? 3 : 0;
		lengthButtons.names = new[] {
			"Unbuild", "Shorter", "Longer", "Reverse",
		};
		lengthButtons.items = new[] {
			new ToggleElement() {action = () => Defer(Unbuild)},
			new ToggleElement() {shim = new Vector3(float.NaN, float.NaN, -1)},
			new ToggleElement() {shim = new Vector3(float.NaN, float.NaN, 1)},
			new ToggleElement() {action = () => SetBuildDirection(-workDirection)},
		};

		SetBuildDirection(workDirection);
	}

	public void OnDisable() {
		Undo.undoRedoPerformed -= OnUndoRedo;
	}

	public void OnSelectionChange() {
		Regen();
	}

	public void OnHierarchyChange() {
		Regen();
	}

	private void OnUndoRedo() {
		Regen();
	}

	private SimpleTransform lastEnd;
	public void OnInspectorUpdate() {
		var track = Track;

		if (track && lastEnd != track.TrackEnd) {
			lastEnd = track.TrackEnd;
			Regen();
		}
	}

	private void Regen() {
		AlignView();

		if (ignoreNextChange) {
			ignoreNextChange = false;
			return;
		}

		var track = Track;

		if (track) {
			if (workDirection > 0) bend = track.TrackAbsoluteEnd.rotation.eulerAngles;
			else bend = track.TrackAbsoluteStart.rotation.eulerAngles;

			bend.x = (bend.x + 180) % 360 - 180;
			bend.y = (bend.y + 180) % 360 - 180;
			bend.z = (bend.z + 180) % 360 - 180;

			shim = new Vector3(0, 0, track.Length);
		}

		Repaint();
	}

	public static Track Track {
		get { return Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Track>() : null; }
	}

	private bool multiple;

	private Vector2 scrollPos;

	public void OnGUI() {
		var track = Track;

		if (!track) {
			if (GUILayout.Button("Create Track", GUILayout.Height(40))) CreateNewTrackUnderSelection();
			return;
		}

		multiple = Selection.gameObjects.Length > 1;

		scrollPos = GUILayout.BeginScrollView(scrollPos);

		GUI.enabled = !multiple;
		RenderDirectionControls();

		GUI.enabled = true;
		RenderNavButtons();

		GUI.enabled = !multiple;
		RenderSliders();

		GUI.enabled = true;

		{
			EditorGUI.BeginChangeCheck();
			var sceneViews = GetSceneViews();
			var viewNames = new string[sceneViews.Count + 1];
			var i = 1;
			viewNames[0] = "None";
			foreach (SceneView view in sceneViews) {
				viewNames[i] = string.IsNullOrEmpty(view.name) ? "Scene View " + i : view.name;
				i++;
			}
			alignViewIdx = EditorGUILayout.Popup("Align View", alignViewIdx, viewNames);
			if (EditorGUI.EndChangeCheck()) AlignView();
		}

		snapAngle = EditorGUILayout.FloatField("Snap Angle", snapAngle);
		snapLength = EditorGUILayout.FloatField("Snap Length", snapLength);

		GUILayout.EndScrollView();
	}

	private void RenderSliders() {
		GUILayout.BeginHorizontal(GUIStyle.none, GUILayout.ExpandWidth(true));
		{
			GUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true));
			{
				GUILayout.Label("Length");
				EditorGUI.BeginChangeCheck();
				shim.z = GUILayout.HorizontalSlider(shim.z, .5f, 50);
				if (EditorGUI.EndChangeCheck()) Bend();
			}
			GUILayout.EndVertical();

			GUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true));
			{
				GUILayout.Label("Roll");
				EditorGUI.BeginChangeCheck();
				bend.z = -GUILayout.HorizontalSlider(-bend.z * workDirection, -180, 180) * workDirection;
				if (EditorGUI.EndChangeCheck()) Bend();
			}
			GUILayout.EndVertical();
		}
		GUILayout.EndHorizontal();

		GUILayout.BeginHorizontal(GUIStyle.none, GUILayout.ExpandWidth(true));
		{
			GUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true));
			{
				GUILayout.Label("Left/Right");
				EditorGUI.BeginChangeCheck();
				bend.y = GUILayout.HorizontalSlider(bend.y, -180, 180);
				if (EditorGUI.EndChangeCheck()) Bend();
			}
			GUILayout.EndVertical();

			GUILayout.BeginVertical(GUIStyle.none, GUILayout.ExpandWidth(true));
			{
				GUILayout.Label("Up/Down");
				EditorGUI.BeginChangeCheck();
				bend.x = -GUILayout.HorizontalSlider(-bend.x * workDirection, -180, 180) * workDirection;
				if (EditorGUI.EndChangeCheck()) Bend();
			}
			GUILayout.EndVertical();
		}
		GUILayout.EndHorizontal();
	}

	private void RenderDirectionControls() {
		var largeText = new GUIStyle(GUI.skin.button) {fontSize = 20};

		EditorGUI.BeginChangeCheck();
		var button = GUILayout.SelectionGrid(-1, aimGrid.names, aimGrid.perRow, largeText, GUILayout.Height(ButtonHeight * 4));
		if (EditorGUI.EndChangeCheck()) Clicked(aimGrid.items[button]);

		EditorGUI.BeginChangeCheck();
		button = GUILayout.SelectionGrid(lengthButtons.selected, lengthButtons.names, lengthButtons.perRow, GUILayout.Height(ButtonHeight));
		if (EditorGUI.EndChangeCheck()) Clicked(lengthButtons.items[button]);
	}

	private void RenderNavButtons() {
		var track = Track;
		
		GUILayout.BeginHorizontal(GUIStyle.none);
		{
			var shift = Event.current.shift;

			GUI.enabled = workDirection > 0 ? track.PrevTrack : track.NextTrack;
			if (GUILayout.Button("<<", GUILayout.Height(ButtonHeight))) Defer(() => Nav(-1, shift));
			GUI.enabled = workDirection > 0 ? track.NextTrack : track.PrevTrack;
			if (GUILayout.Button(">>", GUILayout.Height(ButtonHeight))) Defer(() => Nav(1, shift));
			GUI.enabled = true;
		}
		GUILayout.EndHorizontal();
	}

	/**
	 * SceneView.sceneViews is in an undefined order.
	 * This sorts them in a predictable way.
	 */
	private static List<SceneView> GetSceneViews() {
		var ret = new List<SceneView>(SceneView.sceneViews.Count);
		ret.AddRange(SceneView.sceneViews.Cast<SceneView>());

		ret.Sort((a, b) => {
			//sort by screen position
			var ra = a.position;
			var rb = b.position;

			if (Math.Abs(rb.y - ra.y) > .001f) return (int)(ra.y - rb.y);
			return (int)(ra.x - rb.x);
		});

		return ret;
	}

	protected void Defer(Action action) {
		deferredActions.Add(action);
	}

	protected void Update() {
		if (deferredActions.Count != 0) {
			var actions = deferredActions.ToList();
			deferredActions.Clear();
			foreach (var action in actions) action();
		}
	}

	/** If a view has been selected to be aligned, this will align it with the current selection. */
	public void AlignView() {
		if (GUIUtility.hotControl != 0) {
			//don't move views while dragging, wait for them to finish
			if (!deferredActions.Contains(AlignView)) Defer(AlignView);
			return;
		}

		var track = Track;
		if (!track) return;
		if (PrefabUtility.GetPrefabType(track) == PrefabType.Prefab) return;

		if (alignViewIdx > 0 && alignViewIdx <= SceneView.sceneViews.Count) {
			//Here we use the "undocumented, unsupported" SceneView API.
			var view = GetSceneViews()[alignViewIdx - 1];

			var transform = track.transform;
			var oldPos = new SimpleTransform(transform);
			var absStart = track.TrackAbsoluteStart;
			var absEnd = track.TrackAbsoluteEnd;
			var hoverAwayVector = (Vector3.up + Vector3.back * 3);

			//move transform so we can align the view (and then revert it)
			if (workDirection > 0) {
				transform.LookAt(absEnd.position, absStart.up);
				transform.position += transform.rotation * hoverAwayVector;
			} else {
				absEnd.AboutFaced().ApplyTo(transform);//move to end looking at start

				transform.LookAt(absStart.position, absEnd.up);
				transform.position += transform.rotation * hoverAwayVector;
			}

			view.AlignViewToObject(transform);
			oldPos.ApplyTo(transform);
		}
	}

	private void Straighten() {
		var track = Track;
		Undo.RecordObjects(TrackEditor.GetObjectsInvolvedWithTrack(track), "Straighten");
		var length = track.Length;

		track.curveEndStrength = track.curveStartStrength = Track.DefaultCurveStrength;

		if (workDirection > 0) {
			var newEnd = new SimpleTransform(new Vector3(0, 0, length));
			track.TrackEnd = newEnd;
			if (track.NextTrack) track.NextTrack.TrackAbsoluteStart = track.TrackAbsoluteEnd;
		} else {
			var absEnd = track.TrackAbsoluteEnd;
			track.TrackAbsoluteStart = new SimpleTransform(
				absEnd.position + absEnd.rotation * new Vector3(0, 0, -length),
				absEnd.rotation
			);

			if (track.PrevTrack) track.PrevTrack.TrackAbsoluteEnd = track.TrackAbsoluteStart;
		}

		track.Update();
	}

	private void Flatten() {
		var track = Track;

		Undo.RecordObjects(TrackEditor.GetObjectsInvolvedWithTrack(track), "Flatten");
		var endPos = workDirection > 0 ? track.TrackAbsoluteEnd : track.TrackAbsoluteStart;

		//flatten the end and quantize the direction the nearest 45°
		var forward = endPos.rotation * Vector3.forward;
		forward.y = 0;
		if (forward.sqrMagnitude < .001f) endPos.rotation = Quaternion.identity;
		else {
			var angle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
			angle = Mathf.Round(angle / snapAngle) * snapAngle;
			endPos.rotation = Quaternion.AngleAxis(angle, Vector3.up);
		}


		if (workDirection > 0) {
			track.TrackAbsoluteEnd = endPos;
			if (track.NextTrack) track.NextTrack.TrackAbsoluteStart = track.TrackAbsoluteEnd;
		} else {
			track.TrackAbsoluteStart = endPos;
			if (track.PrevTrack) track.PrevTrack.TrackAbsoluteEnd = track.TrackAbsoluteStart;
		}
		track.Update();
	}

	private void Build() {
		var track = Track;

		if (workDirection > 0) {
			if (track.NextTrack) Selection.activeGameObject = track.NextTrack.gameObject;
			else {
				var newTrack = TrackEditor.AddTrack(track, false);
				newTrack.TrackEnd = new SimpleTransform(new Vector3(0, 0, shim.z));
				newTrack.curveEndStrength = newTrack.curveStartStrength = Track.DefaultCurveStrength;
				Selection.activeGameObject = newTrack.gameObject;
				Undo.RegisterCreatedObjectUndo(newTrack.gameObject, "Build Track");
			}
		} else {
			if (track.PrevTrack) Selection.activeGameObject = track.PrevTrack.gameObject;
			else {
				var newTrack = TrackEditor.AddTrack(track, true);
				newTrack.curveEndStrength = newTrack.curveStartStrength = Track.DefaultCurveStrength;
				var absEnd = newTrack.TrackAbsoluteEnd;
				newTrack.TrackAbsoluteStart = new SimpleTransform(
					absEnd.position + absEnd.rotation * new Vector3(0, 0, -shim.z),
					absEnd.rotation
				);
				Selection.activeGameObject = newTrack.gameObject;
				Undo.RegisterCreatedObjectUndo(newTrack.gameObject, "Build Track");
			}
		}
	}

	private void Unbuild() {
		var track = Track;

		if (workDirection > 0) {
			if (track.PrevTrack) Selection.activeGameObject = track.PrevTrack.gameObject;
		} else {
			if (track.NextTrack) Selection.activeGameObject = track.NextTrack.gameObject;
		}

		Undo.RecordObjects(TrackEditor.GetObjectsInvolvedWithTrack(track), "Unbuild");

		if (track.PrevTrack) track.PrevTrack.NextTrack = null;
		if (track.NextTrack) track.NextTrack.PrevTrack = null;

		Undo.DestroyObjectImmediate(track.gameObject);

	}

	private void Clicked(ToggleElement el) {
		if (el.action != null) el.action();
		else HandleChangeSelection(el);
	}

	protected void HandleChangeSelection(ToggleElement el) {
		Func<float, float, float, float> incr = (current, add, snap) => {
			if (!float.IsNaN(add)) {
				current = Mathf.Round((current + add * snap) / snap) * snap;
			}
			return current;
		};

		bend.x = incr(bend.x, el.bend.x * workDirection, snapAngle);
		bend.y = incr(bend.y, el.bend.y, snapAngle);
		bend.z = incr(bend.z, el.bend.z * workDirection, snapAngle);

		shim.x = incr(shim.x, el.shim.x, snapLength);
		shim.y = incr(shim.y, el.shim.y, snapLength);
		shim.z = incr(shim.z, el.shim.z, snapLength);
		if (shim.z < .5f) shim.z = .5f;

		Bend();
	}

	void Nav(int direction, bool addToSelection = false) {
		var newObj = direction * workDirection < 0 ? Track.PrevTrack.gameObject : Track.NextTrack.gameObject;
		if (!addToSelection) Selection.activeGameObject = newObj;
		else TrackEditorUtil.AddToSelection(newObj);
	}

	void SetBuildDirection(int direction) {
		workDirection = direction;
		lengthButtons.selected = workDirection > 0 ? -1 : 3;
		Regen();
	}

	protected void CreateNewTrackUnderSelection() {
		var parent = Selection.activeGameObject ? Selection.activeGameObject.transform : null;

		var track = TrackEditor.CreateNewTrack();

		if (parent) {
			track.transform.parent = parent;
			track.transform.localRotation = Quaternion.identity;
			track.transform.localPosition = Vector3.zero;
		}

		Undo.RegisterCreatedObjectUndo(track.gameObject, "Create Track");
		Selection.activeGameObject = track.gameObject;
	}

	/** Bends the currently selected track pieces according to the current parameters. */
	protected void Bend() {
		ignoreNextChange = true;
		foreach (var obj in Selection.transforms) {
			var track = obj.GetComponent<Track>();
			if (!track) continue;

			Undo.RecordObjects(TrackEditor.GetObjectsInvolvedWithTrack(track), "Bend Track");

			if (workDirection > 0) {
				var rot = Quaternion.Euler(bend);
				var pos = Vector3.Lerp(track.TrackAbsoluteStart.rotation * shim, rot * shim, .5f) + track.TrackAbsoluteStart.position;

				track.TrackAbsoluteEnd = new SimpleTransform(pos, rot);
				track.Update();

				if (track.NextTrack) {
					track.NextTrack.TrackAbsoluteStart = track.TrackAbsoluteEnd;
					track.NextTrack.Update();
				}
			} else {
				var rot = Quaternion.Euler(bend) * Quaternion.AngleAxis(180, Vector3.up);
				var facedEnd = track.TrackAbsoluteEnd.AboutFaced();
				var pos = Vector3.Lerp(facedEnd.rotation * shim, rot * shim, .5f) + facedEnd.position;

				track.TrackAbsoluteStart = new SimpleTransform(pos, rot).AboutFace();
				track.Update();

				if (track.PrevTrack) {
					track.PrevTrack.TrackAbsoluteEnd = track.TrackAbsoluteStart;
					track.PrevTrack.Update();
				}
			}
		}

	}

}

}
