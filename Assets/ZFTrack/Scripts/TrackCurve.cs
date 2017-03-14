/**
 * <copyright>
 * Tracks and Rails Asset Package by Zen Fulcrum
 * Copyright © 2015 Zen Fulcrum LLC
 * Usage is subject to Unity's Asset Store EULA (https://unity3d.com/legal/as_terms)
 * </copyright>
 */

using System.Collections.Generic;

namespace ZenFulcrum.Track {

using UnityEngine;
using System.Collections;

/**
 * A 3D curve with rotation along its length.
 *
 * All curves start at (0, 0, 0) heading toward +z. (That is, all the positions you can get from this class
 * are in local coordinates.)
 */
public class TrackCurve {
	/**
	 * How many steps we break a curve into when calculating distances.
	 * Higher numbers are more accurate and use more CPU/mem.
	 */
	public const int DistanceQuality = 10;

	const float TooSmall = 0.000001f;

	public enum CurveType {
		/*
		 * These types have been removed because they do not guarantee that the tangent of the curve matches the
		 * direction of the curve at the start and end points (CatmullRom) or anywhere at all (Linear):
		 * Linear = 0,
		 * CatmullRom = 1,
		 */

		/** Old behavior, included for compatibility, does a poor job when tracks curve up or down more than 80-90° */
		Hermite = 2,
		/**
		 * Uses a Hermite spline to generate curves.
		 * Roll is linearly applied from the start to the end.
		 */
		HermiteV2 = 3,
	}


	protected CurveType curveType;
	protected SimpleTransform end;
	protected float startStrength, endStrength;
	protected PathPoint[] pathData;
	protected float _length;

	public TrackCurve(CurveType curveType, SimpleTransform end, float startStrength, float endStrength) {
		this.curveType = curveType;
		this.end = end;
		this.startStrength = startStrength;
		this.endStrength = endStrength;

		if (Vector3.Distance(Vector3.zero, end.position) < 0.000001f) {
			throw new System.ArgumentOutOfRangeException("end", "This curve is a point. Move the start and end apart.");
		}

		CalculateData();
	}

	protected struct PathPoint {
		/** Mathematical fraction along the curve this point is at. */
		public float fraction;
		/**
		 * Physical distance along the path this point is.
		 */
		public float distance;
		/** Our actual point/position. */
		public SimpleTransform pos;
	}

	/** Tracks a position+tangent, used when calculating the final direction (position+tangent+normal). */
	protected struct Moment {
		/** Position at our point along the curve. */
		public Vector3 pos;
		/** Vector for our direction at this point on the curve. (not normalized) */
		public Vector3 direction;
	}

	/**
	 * Calculates some information about the path and caches it for later.
	 */
	protected void CalculateData(int steps = -1) {
		//Ideally, we should compute a set of "key" points (more points on curves) to use, but for now we just approximate.
		_length = 0;

		if (steps <= 0) steps = DistanceQuality;
		pathData = new PathPoint[steps + 1];

		pathData[0].pos = new SimpleTransform();
		pathData[0].fraction = 0;
		pathData[0].distance = 0;

		/*
		 * To track the upward direction along the curve, we repeatedly project the previous up vector along a number
		 * of points along the spline, then determine the resulting "up direction" error and apply linearly apply a
		 * correction rotation to each intermediate node.
		 */

		//For each sample point, record information.
		var pos = pathData[0].pos;
		var lastUp = Vector3.up;
		_length = 0;
		for (int i = 1; i <= steps; i++) {
			pathData[i].fraction = i / (float)steps;

			var moment = GetMoment(pathData[i].fraction);

			_length += (pos.position - moment.pos).magnitude;
			pathData[i].distance = _length;

			//Determine (approx.) which way is up by updating the last up vector against the current forward vector
			var rotation = Quaternion.LookRotation(moment.direction, lastUp);

			pathData[i].pos = pos = new SimpleTransform(moment.pos, rotation);

			lastUp = rotation * Vector3.up;
		}

		//Now that we have a rough idea of which way is up at each point, refine it so we end with the correct curve.
		var lastRotation = pathData[steps].pos.rotation;
		var expectedRotation = end.rotation;
		//lastRotation and expectedRotation should both rotate a Vector.forward to the same value, but
		//most likely, they will point a Vector3.up different directions.
		//Let's find out how far off they are.
		//Rotate last into expected's frame, then decompose to Euler angles.
		var errorAngles = (Quaternion.Inverse(expectedRotation) * lastRotation).eulerAngles;

		//errorAngles.x and y should be about 0 or 360. Our error is in errorAngles.z
		var error = errorAngles.z > 180 ? errorAngles.z - 360 : errorAngles.z;

		//adjust each step to remove the error
		for (int i = 1; i < steps; i++) {
			var counterRotate = -error * pathData[i].distance / _length;

			pathData[i].pos.rotation = Quaternion.AngleAxis(counterRotate, pathData[i].pos.rotation * Vector3.forward) * pathData[i].pos.rotation;
		}

		//The last item should always fit perfectly
		pathData[steps].pos = end;
	}

	/**
	 * Returns the length of this section of track.
	 * (This is the track length, not the distance between start and end.)
	 */
	public float Length { get { return _length; } }


	/**
	 * Give us a position in local space and we will return:
	 *   If in [0, 1]: if the position can be mapped onto the curve. The returned value is a curve fraction for
	 *     where on the track is nearest.
	 *   If < 0: The given position appears to be before the start of the curve.
	 *   If > 1: The given position appears to be after the start of the curve.
	 *
	 * Except when mapped onto the curve, the exact magnitude of the returned value is undefined and should not be used
	 * except to determine which side is nearer.
	 */
	public float GetFraction(Vector3 position) {
		//todo: use cached pathData for better performance

		//Put together a series of lines (for now, just a half dozen segments from the curve)
		//Also include rays cast from the start and end to infinity.

		//Determine which line segment position is closest to.
		//Use Vector3.Project and throw out distances that are not in the current segment
		//Return lerp start and end point of a segment percent along the track
		const int numPieces = DistanceQuality;

		var closestFraction = 0f;
		var closestDistance = float.PositiveInfinity;

		var lastPos = GetPointAt(0);
		var lastPosFraction = 0f;

		//Debug.Log("Position is " + position);
		for (int i = 1; i <= numPieces; ++i) {
			var fraction = i / (float)numPieces;
			var pos = GetPointAt(fraction);

			var lineClosestFraction = NearestPartOfSegment(position, lastPos.position, pos.position);
			var lineDistance = DistanceFromSegment(position, lastPos.position, pos.position);

			//Debug.Log("Step " + i + " is nearest " + lineClosestFraction +
			//	"(" + (lastPosFraction + lineClosestFraction / (float)numPieces) +
			//	") along the line at " + lineDistance);

			if (lineDistance < closestDistance) {
				//closer than what we have so far
				closestDistance = lineDistance;
				closestFraction = lastPosFraction + lineClosestFraction / (float)numPieces;
			}

			lastPosFraction = fraction;
			lastPos = pos;
		}

		//Debug.Log("Picked " + closestFraction + " at " + closestDistance + " away");


		//Count items exactly on the edge as over the edge.
		if (closestFraction == 0) closestFraction = -1;
		else if (closestFraction == 1) closestFraction = 2;


		return closestFraction;
	}

	/**
	 * Returns a series of transforms {interval} distance apart along the path, starting {offset} units into the curve.
	 *
	 * Note that this returns (approximately) evenly spaced points, which is typically more useful
	 * than calling GetPointAt with a series of values.
	 *
	 */
	public IEnumerable<SimpleTransform> GetIntervals(float offset, float interval) {
		if (interval <= .001 || float.IsNaN(interval)) {
			Debug.LogWarning("Unreasonable distanceInterval " + interval);
			yield break;
		}

		//var numIntervals = Mathf.Floor(_length / distanceInterval);

		//how far along the curve we've traveled
		float position = offset;

		var idx = 1;
		var lastPointFraction = 0f;
		var lastPointDistance = 0f;
		while (idx < pathData.Length) {
			while (position < pathData[idx].distance) {
				//Find the (approx.) math fraction that should represent our distance
				var fraction = Mathf.Lerp(
					lastPointFraction,
					pathData[idx].fraction,
					(position - lastPointDistance) / (pathData[idx].distance - lastPointDistance)
				);

				yield return GetPointAt(fraction);
				position += interval;
			}

			lastPointFraction = pathData[idx].fraction;
			lastPointDistance = pathData[idx].distance;

			++idx;
		}

	}


	#region Spline Math

	const float sqrt2 = 1.4142135623730950488016887242097f;

	public static Vector3 CalcHermite(Vector3 p1, Vector3 t1, Vector3 p2, Vector3 t2, float percent) {
		//http://cubic.org/docs/hermite.htm
		float s = percent, s2 = s * s, s3 = s2 * s;
		float h1 = 2 * s3 - 3 * s2 + 1;
		float h2 = -2 * s3 + 3 * s2;
		float h3 = s3 - 2 * s2 + s;
		float h4 = s3 - s2;

		return new Vector3(
			h1 * p1.x + h2 * p2.x + h3 * t1.x + h4 * t2.x,
			h1 * p1.y + h2 * p2.y + h3 * t1.y + h4 * t2.y,
			h1 * p1.z + h2 * p2.z + h3 * t1.z + h4 * t2.z
		);
	}

	/**
	 * Given two position+tangent of the track {percent} along the curve,
	 * returns a Quaternion of our rotation at that point.
	 */
	protected Quaternion GetRotation(Moment moment, float percent) {
		switch (curveType) {
			case CurveType.HermiteV2: {
				//find the pre-calculated rotations before and after this point.
				var prev = pathData[0];
				var idx = 1;
				while (idx < pathData.Length && pathData[idx].fraction < percent) {
					prev = pathData[idx];
					++idx;
				}
				var next = pathData[idx % pathData.Length];

				//our up is somewhere between the two values
				var approxRot = Quaternion.Slerp(
					prev.pos.rotation, next.pos.rotation, 
					(percent - prev.fraction) / (next.fraction - prev.fraction)
				);

				var up = approxRot * Vector3.up;
				if (moment.direction.sqrMagnitude < TooSmall || up.sqrMagnitude < TooSmall) {
					return Quaternion.identity;
				}

				return Quaternion.LookRotation(moment.direction, up);
			}

			case CurveType.Hermite:
			default: {
				//Prefer using HermiteV2 instead oft his, this doesn't deal well with cases where we pitch up or down
				//more than 80 or 90 degrees

				//first, get a rotation that turns toward our main direction
				var ret = Quaternion.LookRotation(moment.direction, Vector3.up);

				//then lerp in the roll
				float targetRoll = end.rotation.eulerAngles.z;
				if (targetRoll > 180) {
					targetRoll -= 360;
				}

				ret = ret * Quaternion.AngleAxis(targetRoll * percent, Vector3.forward);

				return ret;
			}
		}
	}

	protected Moment GetMoment(float percent) {
		const float delta = .001f;
		switch (curveType) {
			case CurveType.Hermite:
			case CurveType.HermiteV2:
			default: {
				//Hermite spline:
				Quaternion endRotation = end.rotation;

				float power = end.position.magnitude;//tangent strength

				Vector3 p1 = new Vector3(0, 0, 0);
				Vector3 t1 = new Vector3(0, 0, power * startStrength);
				Vector3 p2 = end.position;
				Vector3 t2 = endRotation * new Vector3(0, 0, power * endStrength);

				var pos = CalcHermite(p1, t1, p2, t2, percent);
				var dPos = CalcHermite(p1, t1, p2, t2, percent + delta);

				var direction = dPos - pos;

				if (direction.sqrMagnitude < TooSmall) direction = Vector3.forward;//something is wrong

				return new Moment() {
					pos = pos,
					direction = direction
				};
			}
		}
	}


	/**
	 * Returns the position and location of the track "percent" percent of the distance into the
	 * track (relative to the track itself).
	 */
	public SimpleTransform GetPointAt(float percent) {
		var moment = GetMoment(percent);

		return new SimpleTransform(
			moment.pos,
			GetRotation(moment, percent)
		);
	}

	/**
	 * Returns where on the line segment from {a}->{b} {point} is nearest to.
	 * 0 indicates the {point} is nearest {a}, 1 indicates it is nearest {b}, a value between indicates
	 * what percentage from {a} to {b} is nearest.
	 */
	public static float NearestPartOfSegment(Vector3 point, Vector3 a, Vector3 b) {
		var lineDirection = b - a;
		var relPoint = point - a;

		var projected = Vector3.Project(relPoint, lineDirection);

		if (Vector3.Dot(projected, lineDirection) <= 0) {
			//projected vector in the opposite direction of a->b, nearest is a
			return 0;
		} else if (projected.sqrMagnitude > lineDirection.sqrMagnitude) {
			//projected vector is beyond point b, b is nearest point
			return 1;
		} else {
			//nearest point is along the line, calculate distance
			return projected.magnitude / lineDirection.magnitude;
		}
	}

	/**
	 * Returns the distance from the given point to the nearest point on the line segment defined as a->b.
	 */
	public static float DistanceFromSegment(Vector3 point, Vector3 a, Vector3 b) {
		return Vector3.Distance(
			Vector3.Lerp(a, b, NearestPartOfSegment(point, a, b)),
			point
		);
		//var lineDirection = b - a;
		//var relPoint = point - a;

		//var projected = Vector3.Project(relPoint, lineDirection);

		//if (Vector3.Dot(projected, lineDirection) <= 0) {
		//	//projected vector in the opposite direction of a->b, distance is distance to a
		//	return Vector3.Distance(a, point);
		//} else if (projected.sqrMagnitude > lineDirection.sqrMagnitude) {
		//	//projected vector is beyond point b, b is nearest point
		//	return Vector3.Distance(b, point);
		//} else {
		//	//nearest point is along the line, calculate distance from projected
		//	return Vector3.Distance(projected, relPoint);
		//}

	}

	#endregion

}

}
