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
using System.Collections.Generic;
using System.Linq;

/**
 * This class generates the meshes for tracks.
 */
public class TrackMeshGenerator {

	/** 
	 * Maximum number of ties that we will generate a track with.
	 * This protects us from OOM issues if you put something like tieInterval = 0.00000001 in the editor.
	 */
	const int MaxTiesCount = 800;

	protected struct Vertex {
		public Vector3 pos, normal;
		public Vector2 uv;
	}

	protected class MiniMesh {
		public List<Vertex> verts = new List<Vertex>();
		public List<int> tris = new List<int>();
	}

	protected Mesh sourceRailMesh, tieMesh;
	protected MiniMesh railMesh, capMesh;

	/**
	 * Returns an array of steps in [0, 1] that represent the points in the track we should sample.
	 */
	protected float[] GetSteps(Track track) {
		var steps = (int)Mathf.Clamp(
			Mathf.Round(track.resolution * track.Length), 
			1, 300
		);

		//todo later: we can look at the curve and make intelligent decisions (for example, return just two values for straight lines, 
		//or cluster the division points at sharp parts of the curve.
		//Related: http://www.antigrain.com/research/adaptive_bezier/index.html

		var ret = new float[steps + 1];
		for (var i = 0; i <= steps; ++i) ret[i] = i / (float)steps;
		return ret;
	}

	private delegate void _addFaceSig(int v1, int v2, int v3, Dictionary<int, int> mapping, MiniMesh dest);

	public void SetMeshes(Mesh sourceRailMesh, Mesh tieMesh) {
		this.tieMesh = tieMesh;

		if (this.sourceRailMesh == sourceRailMesh) return;

		this.sourceRailMesh = sourceRailMesh;

		if (!sourceRailMesh) return;

		//Figure out what faces are rail vs. cap
		//(Originally we made the modeler do this, but modeling stuff is so much easier if you can keep the caps attached,
		//so we'll make the modeler's life simpler at the cost of some code.)

		railMesh = new MiniMesh();
		capMesh = new MiniMesh();

		var verts = sourceRailMesh.vertices;
		var normals = sourceRailMesh.normals;
		var uvs = sourceRailMesh.uv;
		var tris = sourceRailMesh.triangles;
		var railTris = new Dictionary<int, int>();//maps source vertex index => dest vertex index
		var capTris = new Dictionary<int, int>();

		System.Func<int, Dictionary<int, int>, MiniMesh, int> mapVert = (v, mapping, dest) => {
			int pos;
			if (mapping.TryGetValue(v, out pos)) return pos;
			else {
				dest.verts.Add(new Vertex{
					pos = verts[v], normal = normals[v], uv = uvs[v],
				});
				var newV = dest.verts.Count - 1;
				mapping[v] = newV;
				return newV;
			}
		};

		_addFaceSig addFace = (v1, v2, v3, mapping, dest) => {
			v1 = mapVert(v1, mapping, dest);
			v2 = mapVert(v2, mapping, dest);
			v3 = mapVert(v3, mapping, dest);

			dest.tris.Add(v1);
			dest.tris.Add(v2);
			dest.tris.Add(v3);
		};

		//for each face
		for (int i = 0; i < tris.Length; i += 3) {
			int v1 = tris[i], v2 = tris[i + 1], v3 = tris[i + 2];
			//Does this face cross zero?
			var sideCount = (verts[v1].z > 0 ? 0 : 1) + (verts[v2].z > 0 ? 0 : 1) + (verts[v3].z > 0 ? 0 : 1);

			//Add it to the right set
			if (sideCount == 0 || sideCount == 3) addFace(v1, v2, v3, capTris, capMesh);
			else addFace(v1, v2, v3, railTris, railMesh);
		}

		//nix empty stuff
		if (railMesh.tris.Count == 0) railMesh = null;
		if (capMesh.tris.Count == 0) capMesh = null;
	}

	public void GenerateMesh(Mesh mesh, Track track) {
		//This function is optimized.
		//There are some places where simpler, more fluent language constructs are avoided in the interest of performance.

		if (railMesh == null && capMesh == null && !tieMesh) {
			//no results case
			mesh.Clear();
			return;
		}


		//build the rails (rails are built by bridging each set of points we render)
		List<Vector3> allVerts = new List<Vector3>();
		List<Vector3> allNormals = new List<Vector3>();
		List<Vector2> allUVs = new List<Vector2>();
		List<int> allTris = new List<int>();

		if (railMesh != null) {
			//Rail mesh should consist of a straight piece of track along z from -.5 to .5.
			int vertOffset = allVerts.Count;

			SimpleTransform lastStep = null;
			var steps = GetSteps(track);

			var vertCount = (steps.Length - 1) * railMesh.verts.Count;
			var triCount = (steps.Length - 1) * railMesh.tris.Count;
			allVerts.EnsureSpace(vertCount);
			allNormals.EnsureSpace(vertCount);
			allUVs.EnsureSpace(vertCount);
			allTris.EnsureSpace(triCount);

			foreach (float step in steps) {
				//Note: we could use Curve.GetIntervals, but it's better to have more steps along curves
				//which tends to happen (a little bit) automatically if we use the mathematical fraction.
				SimpleTransform curStep = track.Curve.GetPointAt(step);

				if (lastStep != null) {
					//bridge between two steps
					int pInitial = allVerts.Count;
					int j = 0;
					foreach (Vertex vert in railMesh.verts) {
						Vector3 pos = vert.pos;
						if (pos.z < 0) {
							//this vert falls on the prev side
							pos.z += .5f;//the model has data one unit wide, offset that
							Vector3 p = lastStep * pos;
							allVerts.Add(p);
							allNormals.Add(lastStep.rotation * vert.normal);
							allUVs.Add(vert.uv);
						} else {
							//this vert falls on the next side
							pos.z -= .5f;//the model has data one unit wide, offset that
							Vector3 p = curStep * pos;
							allVerts.Add(p);
							allNormals.Add(curStep.rotation * vert.normal);
							allUVs.Add(vert.uv);
						}

						j++;
					}

					foreach (int idx in railMesh.tris) {
						allTris.Add(idx + pInitial + vertOffset);
					}
				}

				lastStep = curStep;
			}
		}

		if (capMesh != null && (!track.NextTrack || !track.PrevTrack || track.forceEndCaps)) {
			//Cap mesh should consist of faces in +z or -z
			//No faces may cross 0

			List<Vector3> verts = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();
			List<Vector2> uvs = new List<Vector2>();
			List<int> tris = new List<int>();

			SimpleTransform firstStep = new SimpleTransform(), lastStep = track.Curve.GetPointAt(1);

			var caps = (track.PrevTrack ? 0 : 1) + (track.NextTrack ? 0 : 1);
			//We assume that the start and end caps have the same number of verts/tris, if not this will run a little slower or use more memory.
			var vertCount = caps * capMesh.verts.Count / 2;
			var triCount = caps * capMesh.tris.Count / 2;
			allVerts.EnsureSpace(vertCount);
			allNormals.EnsureSpace(vertCount);
			allUVs.EnsureSpace(vertCount);
			allTris.EnsureSpace(triCount);

			for (int triIdx = 0; triIdx < capMesh.tris.Count; triIdx += 3) {
				//grab the first vert in the face to see what side the face is on
				Vector3 pos0 = capMesh.verts[capMesh.tris[triIdx + 0]].pos;

				float offset;//how much to offset Z to zero out the tri
				SimpleTransform trans;

				if (pos0.z < 0) {
					//this tri falls on the start side
					if (track.PrevTrack && ! track.forceEndCaps) continue;//can't see this cap
					offset = .5f;
					trans = firstStep;
				} else {
					//this tri falls on the end side
					if (track.NextTrack && ! track.forceEndCaps) continue;//can't see this cap
					offset = -.5f;
					trans = lastStep;
				}

				//Copy each vert from this face
				for (int i = 0; i < 3; ++i) {
					int idx = capMesh.tris[triIdx + i];

					Vertex v = capMesh.verts[idx];
					Vector3 vPos = v.pos;
					vPos.z += offset;

					tris.Add(verts.Count);
					verts.Add(trans * vPos);
					normals.Add(trans.rotation * v.normal);
					uvs.Add(v.uv);
				}

			}

			//Add intermediate results to the final mesh
			int vertOffset = allVerts.Count;
			allVerts.AddRange(verts);
			allNormals.AddRange(normals);
			allUVs.AddRange(uvs);
			foreach (var vertIdx in tris) {
				allTris.Add(vertIdx + vertOffset);
			}
		}

		if (tieMesh) {
			//Tie mesh should consist of some polygons we can throw in at intervals to tie the track together.

			var trackLength = track.Length;

			var numTies = Mathf.Floor(trackLength / track.tieInterval);
			// Give the number of ties a reasonable limit.
			if (track.tieInterval <= 0) numTies = 0;
			if (numTies > MaxTiesCount) numTies = MaxTiesCount;

			// Space ties evenly along the length (results in a slightly higher tie interval than requested)
			var effectiveInterval = trackLength / numTies;

			var offset = .5f * effectiveInterval;

			var tieVerts = tieMesh.vertices;
			var tieNormals = tieMesh.normals;
			var uvTris = tieMesh.uv;
			var tieTris = tieMesh.triangles;

			var vertCount = (int)numTies * tieVerts.Length;
			var triCount = (int)numTies * tieTris.Length;
			allVerts.EnsureSpace(vertCount);
			allNormals.EnsureSpace(vertCount);
			allUVs.EnsureSpace(vertCount);
			allTris.EnsureSpace(triCount);

			var intervalIter = track.Curve.GetIntervals(offset, effectiveInterval).GetEnumerator();
			for (int i = 0; i < numTies; i++) {
				if (!intervalIter.MoveNext()) {
					Debug.LogWarning("Tie count didn't match up", track);
					break;
				}

				var tiePos = intervalIter.Current;
				int pInitial = allVerts.Count;

				//allVerts.AddRange(from vPos in tieMesh.vertices select tiePos * vPos);
				for (int j = 0; j < tieVerts.Length; ++j) {
					allVerts.Add(tiePos * tieVerts[j]);
				}

				//allNormals.AddRange(from normal in tieMesh.normals select tiePos.rotation * normal);
				for (int j = 0; j < tieNormals.Length; ++j) {
					allNormals.Add(tiePos.rotation * tieNormals[j]);
				}

				//allUVs.AddRange(tieMesh.uv);
				for (int j = 0; j < uvTris.Length; ++j) {
					allUVs.Add(uvTris[j]);
				}

				//allTris.AddRange(from idx in tieMesh.triangles select idx + pInitial);
				for (int j = 0; j < tieTris.Length; ++j) {
					allTris.Add(pInitial + tieTris[j]);
				}

			}
		}

		#if UNITY_EDITOR
		if (!Application.isPlaying) {
			//If the mesh hasn't changed, don't modify it while editing. This will avoid marking the scene as changed when it hasn't.
			//During play mode, don't worry about it. It's stupid to burn CPU to avoid a no-op.

			if (
				allVerts.SequenceEqual(mesh.vertices) &&
				allNormals.SequenceEqual(mesh.normals) &&
				allUVs.SequenceEqual(mesh.uv) &&
				allTris.SequenceEqual(mesh.triangles)
			) {
				//turns out, we didn't need to do anything after all
				return;
			}
		}
		#endif

		mesh.Clear();
		mesh.vertices = allVerts.ToArray();
		mesh.normals = allNormals.ToArray();
		mesh.uv = allUVs.ToArray();
		mesh.triangles = allTris.ToArray();
	}
}

}
