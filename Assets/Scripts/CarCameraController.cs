using UnityEngine;

public class CarCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRigidbody;

    [Header("Follow")]
    public Vector3 offset = new Vector3(0f, 3.5f, -7f);
    public float positionSmoothTime = 0.15f;
    public float rotationSmoothTime = 0.1f;

    [Header("Speed Zoom")]
    public bool useFovZoom = true;
    public float minFov = 60f;
    public float maxFov = 80f;
    public float minDistance = 6.5f;
    public float maxDistance = 10f;
    public float maxSpeedForZoom = 40f; // m/s
    public float zoomSmoothTime = 0.2f;

    [Header("Speed Offset")]
    public Vector3 speedOffsetAtMax = new Vector3(0f, 0.5f, -3f);
    public float speedOffsetSmoothTime = 0.25f;

    [Header("Manual Look")]
    public bool allowManualLook = true;
    public string horizontalAxis = "Mouse X";
    public string verticalAxis = "Mouse Y";
    public bool requireMouseButton = true;
    public int mouseButton = 1;
    public float lookSensitivity = 2f;
    public float pitchMin = -20f;
    public float pitchMax = 35f;
    public float recenterDelay = 1.25f;
    public float recenterSmoothTime = 0.25f;

    private Camera _cam;
    private Vector3 _positionVelocity;
    private float _fovVelocity;
    private float _distanceVelocity;
    private Quaternion _rotationVelocity;
    private Vector3 _speedOffsetVelocity;
    private Vector3 _currentSpeedOffset;
    private float _yaw;
    private float _pitch;
    private float _yawVelocity;
    private float _pitchVelocity;
    private float _lastInputTime;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (targetRigidbody == null && target != null)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float speed = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude : 0f;
        float t = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeedForZoom));

        Vector3 baseOffset = offset;

        if (!useFovZoom)
        {
            float distance = Mathf.SmoothDamp(
                baseOffset.magnitude,
                Mathf.Lerp(minDistance, maxDistance, t),
                ref _distanceVelocity,
                zoomSmoothTime
            );

            baseOffset = baseOffset.normalized * distance;
        }
        else if (_cam != null)
        {
            _cam.fieldOfView = Mathf.SmoothDamp(
                _cam.fieldOfView,
                Mathf.Lerp(minFov, maxFov, t),
                ref _fovVelocity,
                zoomSmoothTime
            );
        }

        Vector3 targetSpeedOffset = Vector3.Lerp(Vector3.zero, speedOffsetAtMax, t);
        _currentSpeedOffset = Vector3.SmoothDamp(
            _currentSpeedOffset,
            targetSpeedOffset,
            ref _speedOffsetVelocity,
            speedOffsetSmoothTime
        );

        baseOffset += _currentSpeedOffset;

        HandleManualLook();
        Quaternion manualRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 desiredOffset = manualRotation * baseOffset;

        Vector3 desiredPosition = target.TransformPoint(desiredOffset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        transform.rotation = SmoothDampRotation(transform.rotation, desiredRotation, rotationSmoothTime);
    }

    private void HandleManualLook()
    {
        bool hasInput = false;
        if (allowManualLook)
        {
            if (!requireMouseButton || Input.GetMouseButton(mouseButton))
            {
                float inputX = Input.GetAxis(horizontalAxis);
                float inputY = Input.GetAxis(verticalAxis);
                if (Mathf.Abs(inputX) > 0.0001f || Mathf.Abs(inputY) > 0.0001f)
                {
                    _yaw += inputX * lookSensitivity;
                    _pitch -= inputY * lookSensitivity;
                    _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
                    _lastInputTime = Time.time;
                    hasInput = true;
                }
            }
        }

        if (!hasInput && recenterDelay >= 0f && (Time.time - _lastInputTime) >= recenterDelay)
        {
            _yaw = Mathf.SmoothDamp(_yaw, 0f, ref _yawVelocity, recenterSmoothTime);
            _pitch = Mathf.SmoothDamp(_pitch, 0f, ref _pitchVelocity, recenterSmoothTime);
        }
    }

    private Quaternion SmoothDampRotation(Quaternion current, Quaternion targetRotation, float smoothTime)
    {
        if (smoothTime <= 0f) return targetRotation;
        return Quaternion.Slerp(current, targetRotation, 1f - Mathf.Exp(-Time.deltaTime / smoothTime));
    }
}
