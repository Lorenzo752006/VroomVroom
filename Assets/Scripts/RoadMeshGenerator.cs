using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[ExecuteAlways]
public class RoadMeshGenerator : MonoBehaviour
{
    [Header("Spline")]
    public RoadSpline spline;

    [Header("Mesh")]
    [Min(0.5f)]
    public float roadWidth = 6f;
    [Range(2, 200)]
    public int samplesPerSegment = 20;
    [Min(0.01f)]
    public float uvScale = 1f;
    public bool generateCollider = true;
    [Min(0.001f)]
    public float minSampleSpacing = 0.05f;
    public bool flipWinding = true;

    [Header("Rebuild")]
    public bool autoUpdate = true;

    private MeshFilter _meshFilter;
    private MeshCollider _meshCollider;
    private Vector3[] _lastPositions;
    private int _lastSampleSetting;
    private float _lastWidth;

    private void Awake()
    {
        EnsureReferences();
        Rebuild();
    }

    private void OnEnable()
    {
        EnsureReferences();
        Rebuild();
    }

    private void Start()
    {
        EnsureReferences();
        Rebuild();
    }

    private void OnValidate()
    {
        if (samplesPerSegment < 2) samplesPerSegment = 2;
        EnsureReferences();
        if (autoUpdate)
        {
            Rebuild();
        }
    }

    private void Update()
    {
        if (!autoUpdate || spline == null)
        {
            return;
        }

        if (HasSplineChanged())
        {
            Rebuild();
        }
    }

    private void EnsureReferences()
    {
        if (_meshFilter == null)
        {
            _meshFilter = GetComponent<MeshFilter>();
        }

        if (generateCollider && _meshCollider == null)
        {
            _meshCollider = GetComponent<MeshCollider>();
            if (_meshCollider == null)
            {
                _meshCollider = gameObject.AddComponent<MeshCollider>();
            }
        }

        if (spline == null)
        {
            spline = GetComponent<RoadSpline>();
        }
    }

    private bool HasSplineChanged()
    {
        if (spline == null || spline.controlPoints == null)
        {
            return false;
        }

        if (_lastPositions == null || _lastPositions.Length != spline.controlPoints.Count)
        {
            return true;
        }

        if (_lastSampleSetting != samplesPerSegment || Mathf.Abs(_lastWidth - roadWidth) > 0.0001f)
        {
            return true;
        }

        for (int i = 0; i < spline.controlPoints.Count; i++)
        {
            Transform cp = spline.controlPoints[i];
            if (cp == null) return true;
            if (_lastPositions[i] != cp.position) return true;
        }

        return false;
    }

    public void Rebuild()
    {
        EnsureReferences();
        if (spline == null || spline.controlPoints == null || spline.controlPoints.Count < 2)
        {
            return;
        }

        int segmentCount = spline.SegmentCount;
        if (segmentCount <= 0)
        {
            return;
        }

        int stepsPerSegment = Mathf.Max(2, samplesPerSegment);
        int totalSteps = (segmentCount * stepsPerSegment) + 1;

        List<Vector3> vertices = new List<Vector3>(totalSteps * 2);
        List<Vector2> uvs = new List<Vector2>(totalSteps * 2);
        List<int> triangles = new List<int>((totalSteps - 1) * 6);

        float halfWidth = roadWidth * 0.5f;
        float distance = 0f;

        Vector3 prevPoint = spline.GetPointOnSegment(0, 0f);
        bool hasPrev = false;
        bool isClosed = spline.closed;
        for (int s = 0; s < segmentCount; s++)
        {
            for (int i = 0; i <= stepsPerSegment; i++)
            {
                if (s > 0 && i == 0)
                {
                    continue;
                }
                float t = i / (float)stepsPerSegment;
                Vector3 point = spline.GetPointOnSegment(s, t);
                Vector3 tangent = spline.GetTangentOnSegment(s, t);
                if (tangent.sqrMagnitude < 0.000001f)
                {
                    continue;
                }
                tangent.Normalize();
                Vector3 left = Vector3.Cross(tangent, Vector3.up);
                if (left.sqrMagnitude < 0.000001f)
                {
                    left = Vector3.Cross(tangent, Vector3.forward);
                }
                if (left.sqrMagnitude < 0.000001f)
                {
                    left = Vector3.Cross(tangent, Vector3.right);
                }
                left.Normalize();

                bool isLastEndpoint = isClosed && s == segmentCount - 1 && i == stepsPerSegment;
                if (hasPrev && !isLastEndpoint && Vector3.Distance(prevPoint, point) < minSampleSpacing)
                {
                    continue;
                }

                Vector3 vLeft = point + left * halfWidth;
                Vector3 vRight = point - left * halfWidth;

                if (hasPrev)
                {
                    distance += Vector3.Distance(prevPoint, point);
                }

                float v = distance * uvScale;
                vertices.Add(transform.InverseTransformPoint(vLeft));
                vertices.Add(transform.InverseTransformPoint(vRight));
                uvs.Add(new Vector2(0f, v));
                uvs.Add(new Vector2(1f, v));

                prevPoint = point;
                hasPrev = true;
            }
        }

        int vertCount = vertices.Count;
        if (vertCount < 4)
        {
            if (generateCollider && _meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
            }
            return;
        }
        for (int i = 0; i < vertCount - 2; i += 2)
        {
            int i0 = i;
            int i1 = i + 1;
            int i2 = i + 2;
            int i3 = i + 3;

            if (flipWinding)
            {
                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);

                triangles.Add(i2);
                triangles.Add(i3);
                triangles.Add(i1);
            }
            else
            {
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);

                triangles.Add(i2);
                triangles.Add(i1);
                triangles.Add(i3);
            }
        }

        if (triangles.Count < 3)
        {
            if (generateCollider && _meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
            }
            return;
        }

        Mesh mesh = _meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Road Mesh";
        }
        else
        {
            mesh.Clear();
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _meshFilter.sharedMesh = mesh;
        if (generateCollider && _meshCollider != null)
        {
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = mesh;
        }

        CacheLastState();
    }

    private void CacheLastState()
    {
        if (spline == null || spline.controlPoints == null)
        {
            return;
        }

        _lastPositions = new Vector3[spline.controlPoints.Count];
        for (int i = 0; i < spline.controlPoints.Count; i++)
        {
            Transform cp = spline.controlPoints[i];
            _lastPositions[i] = cp != null ? cp.position : Vector3.zero;
        }

        _lastSampleSetting = samplesPerSegment;
        _lastWidth = roadWidth;
    }
}
