using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class RoadSpline : MonoBehaviour
{
	[Header("Control Points")]
	public List<Transform> controlPoints = new List<Transform>();
	public bool closed;
	public bool autoCollectChildren = true;

	[Header("Sampling")]
	[Range(2, 200)]
	public int samplesPerSegment = 20;

	private void OnEnable()
	{
		AutoCollect();
	}

	private void OnValidate()
	{
		if (samplesPerSegment < 2) samplesPerSegment = 2;
		AutoCollect();
	}

	private void AutoCollect()
	{
		if (!autoCollectChildren)
		{
			return;
		}

		controlPoints.Clear();
		for (int i = 0; i < transform.childCount; i++)
		{
			controlPoints.Add(transform.GetChild(i));
		}
	}

	public int SegmentCount
	{
		get
		{
			int count = controlPoints != null ? controlPoints.Count : 0;
			if (closed)
			{
				return Mathf.Max(0, count);
			}
			return Mathf.Max(0, count - 1);
		}
	}

	public Vector3 GetPoint(float t)
	{
		int count = controlPoints != null ? controlPoints.Count : 0;
		if (count == 0)
		{
			return transform.position;
		}
		if (count == 1)
		{
			return controlPoints[0] ? controlPoints[0].position : transform.position;
		}

		t = Mathf.Clamp01(t);
		float totalSegments = SegmentCount;
		if (totalSegments <= 0f)
		{
			return controlPoints[0] ? controlPoints[0].position : transform.position;
		}

		float segmentT = t * totalSegments;
		int segIndex = Mathf.FloorToInt(segmentT);
		float localT = segmentT - segIndex;

		if (segIndex >= totalSegments)
		{
			segIndex = Mathf.Max(0, SegmentCount - 1);
			localT = 1f;
		}

		return GetPointOnSegment(segIndex, localT);
	}

	public Vector3 GetTangent(float t)
	{
		int count = controlPoints != null ? controlPoints.Count : 0;
		if (count < 2)
		{
			return transform.forward;
		}

		t = Mathf.Clamp01(t);
		float totalSegments = SegmentCount;
		if (totalSegments <= 0f)
		{
			return transform.forward;
		}

		float segmentT = t * totalSegments;
		int segIndex = Mathf.FloorToInt(segmentT);
		float localT = segmentT - segIndex;

		if (segIndex >= totalSegments)
		{
			segIndex = Mathf.Max(0, SegmentCount - 1);
			localT = 1f;
		}

		return GetTangentOnSegment(segIndex, localT).normalized;
	}

	public Vector3 GetPointOnSegment(int segIndex, float t)
	{
		int count = controlPoints != null ? controlPoints.Count : 0;
		if (count < 2)
		{
			return transform.position;
		}

		GetSegmentIndices(segIndex, out int i0, out int i1, out int i2, out int i3);

		Vector3 p0 = GetControlPoint(i0);
		Vector3 p1 = GetControlPoint(i1);
		Vector3 p2 = GetControlPoint(i2);
		Vector3 p3 = GetControlPoint(i3);

		return CatmullRom(p0, p1, p2, p3, t);
	}

	public Vector3 GetTangentOnSegment(int segIndex, float t)
	{
		int count = controlPoints != null ? controlPoints.Count : 0;
		if (count < 2)
		{
			return transform.forward;
		}

		GetSegmentIndices(segIndex, out int i0, out int i1, out int i2, out int i3);

		Vector3 p0 = GetControlPoint(i0);
		Vector3 p1 = GetControlPoint(i1);
		Vector3 p2 = GetControlPoint(i2);
		Vector3 p3 = GetControlPoint(i3);

		return CatmullRomTangent(p0, p1, p2, p3, t);
	}

	private Vector3 GetControlPoint(int index)
	{
		if (controlPoints == null || controlPoints.Count == 0)
		{
			return transform.position;
		}

		index = Mathf.Clamp(index, 0, controlPoints.Count - 1);
		Transform cp = controlPoints[index];
		return cp ? cp.position : transform.position;
	}

	private void GetSegmentIndices(int segIndex, out int i0, out int i1, out int i2, out int i3)
	{
		int count = controlPoints != null ? controlPoints.Count : 0;

		if (closed)
		{
			i1 = Mod(segIndex, count);
			i2 = Mod(segIndex + 1, count);
			i0 = Mod(segIndex - 1, count);
			i3 = Mod(segIndex + 2, count);
		}
		else
		{
			i1 = Mathf.Clamp(segIndex, 0, count - 1);
			i2 = Mathf.Clamp(segIndex + 1, 0, count - 1);
			i0 = Mathf.Clamp(segIndex - 1, 0, count - 1);
			i3 = Mathf.Clamp(segIndex + 2, 0, count - 1);
		}
	}

	private static int Mod(int x, int m)
	{
		if (m == 0) return 0;
		int r = x % m;
		return r < 0 ? r + m : r;
	}

	private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;
		return 0.5f * (
			(2f * p1) +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3
		);
	}

	private static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t;
		return 0.5f * (
			(-p0 + p2) +
			2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
			3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
		);
	}

	public void SetControlPoints(IList<Vector3> points, bool worldSpace = true, bool clearExisting = true)
	{
		if (clearExisting)
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				Transform child = transform.GetChild(i);
				if (Application.isPlaying)
				{
					Destroy(child.gameObject);
				}
				else
				{
					DestroyImmediate(child.gameObject);
				}
			}
			controlPoints.Clear();
		}

		if (points == null)
		{
			return;
		}

		for (int i = 0; i < points.Count; i++)
		{
			GameObject go = new GameObject($"Point {i}");
			go.transform.SetParent(transform, false);
			if (worldSpace)
			{
				go.transform.position = points[i];
			}
			else
			{
				go.transform.localPosition = points[i];
			}
			controlPoints.Add(go.transform);
		}
	}

	private void OnDrawGizmos()
	{
		if (controlPoints == null || controlPoints.Count < 2)
		{
			return;
		}

		Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
		int totalSegments = SegmentCount;
		if (totalSegments <= 0)
		{
			return;
		}

		int steps = Mathf.Max(2, samplesPerSegment);
		Vector3 prev = GetPointOnSegment(0, 0f);
		for (int s = 0; s < totalSegments; s++)
		{
			for (int i = 1; i <= steps; i++)
			{
				float t = i / (float)steps;
				Vector3 p = GetPointOnSegment(s, t);
				Gizmos.DrawLine(prev, p);
				prev = p;
			}
		}
	}
}
