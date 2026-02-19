using UnityEngine;

public class CarCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRigidbody;

    [Header("Follow Settings - Forza Horizon Style")]
    [Tooltip("Base camera offset (local to car)")]
    public Vector3 baseOffset = new Vector3(0f, 2.5f, -7f);
    
    [Tooltip("How quickly camera follows position")]
    [Range(0.05f, 0.5f)]
    public float positionSmoothTime = 0.12f;
    
    [Tooltip("How quickly camera follows rotation")]
    [Range(0.05f, 0.5f)]
    public float rotationSmoothTime = 0.15f;

    [Header("Speed-Based Camera")]
    [Tooltip("Use FOV zoom (true) or distance zoom (false)")]
    public bool useFovZoom = true;
    
    [Tooltip("Field of view at low speeds")]
    [Range(50f, 70f)]
    public float minFov = 60f;
    
    [Tooltip("Field of view at high speeds")]
    [Range(70f, 90f)]
    public float maxFov = 75f;
    
    [Tooltip("Camera distance at low speeds")]
    public float minDistance = 6.5f;
    
    [Tooltip("Camera distance at high speeds")]
    public float maxDistance = 9f;
    
    [Tooltip("Speed (m/s) for maximum zoom/distance")]
    public float maxSpeedForZoom = 50f; // ~180 km/h
    
    [Tooltip("FOV/distance transition smoothness")]
    public float zoomSmoothTime = 0.3f;

    [Header("Dynamic Height (Forza-style)")]
    [Tooltip("Enable dynamic height based on speed")]
    public bool useDynamicHeight = true;
    
    [Tooltip("Extra height at high speeds")]
    public float speedHeightBonus = 1.2f;
    
    [Tooltip("Height adjustment smoothness")]
    public float heightSmoothTime = 0.4f;

    [Header("Look-Ahead (Forza-style)")]
    [Tooltip("Camera looks ahead based on velocity")]
    public bool useLookAhead = true;
    
    [Tooltip("How far ahead to look (0-1)")]
    [Range(0f, 1f)]
    public float lookAheadAmount = 0.3f;
    
    [Tooltip("Look-ahead smoothness")]
    public float lookAheadSmoothTime = 0.25f;

    [Header("Corner Tilt (Forza-style)")]
    [Tooltip("Enable camera tilt during turns")]
    public bool useCornerTilt = true;
    
    [Tooltip("Maximum tilt angle in degrees")]
    [Range(0f, 15f)]
    public float maxTiltAngle = 8f;
    
    [Tooltip("Tilt transition smoothness")]
    public float tiltSmoothTime = 0.2f;

    [Header("Drift Camera Effects")]
    [Tooltip("Enable special camera behavior during drifts")]
    public bool useDriftEffects = true;
    
    [Tooltip("Extra FOV during drifts")]
    [Range(0f, 10f)]
    public float driftFovBonus = 5f;
    
    [Tooltip("Camera pulls wider during drift")]
    public float driftDistanceBonus = 1.5f;
    
    [Tooltip("Minimum sideways speed to detect drift (m/s)")]
    public float driftThreshold = 3f;

    [Header("Collision Avoidance")]
    [Tooltip("Enable camera collision detection")]
    public bool useCollisionAvoidance = true;
    
    [Tooltip("Layers to check for collisions")]
    public LayerMask collisionLayers = -1;
    
    [Tooltip("Camera collision padding")]
    public float collisionPadding = 0.3f;

    [Header("Manual Look (Right Mouse)")]
    public bool allowManualLook = true;
    public string horizontalAxis = "Mouse X";
    public string verticalAxis = "Mouse Y";
    public bool requireMouseButton = true;
    public int mouseButton = 1;
    
    [Range(1f, 5f)]
    public float lookSensitivity = 2.5f;
    
    public float pitchMin = -25f;
    public float pitchMax = 40f;
    public float recenterDelay = 1.0f;
    public float recenterSmoothTime = 0.3f;

    // Private variables
    private Camera _cam;
    private Vector3 _positionVelocity;
    private float _fovVelocity;
    private float _distanceVelocity;
    private float _heightVelocity;
    private Vector3 _lookAheadVelocity;
    private float _tiltVelocity;
    private float _currentTilt;
    private float _currentHeight;
    private Vector3 _currentLookAhead;
    private float _yaw;
    private float _pitch;
    private float _yawVelocity;
    private float _pitchVelocity;
    private float _lastInputTime;
    private float _currentDriftFactor;
    private float _driftFactorVelocity;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (targetRigidbody == null && target != null)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
        }
        
        // Initialize values
        _currentHeight = baseOffset.y;
        _currentLookAhead = Vector3.zero;
        _currentTilt = 0f;
        _currentDriftFactor = 0f;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Get speed and velocity info
        float speed = targetRigidbody != null ? targetRigidbody.linearVelocity.magnitude : 0f;
        Vector3 localVelocity = target.InverseTransformDirection(targetRigidbody != null ? targetRigidbody.linearVelocity : Vector3.zero);
        
        // Calculate normalized speed (0-1)
        float speedNormalized = Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeedForZoom));
        
        // Detect drifting (Forza-style)
        float driftFactor = CalculateDriftFactor(localVelocity, speed);
        
        // Calculate dynamic offset
        Vector3 dynamicOffset = CalculateDynamicOffset(speedNormalized, driftFactor);
        
        // Calculate look-ahead
        Vector3 lookAhead = CalculateLookAhead(speedNormalized);
        
        // Calculate corner tilt
        float tilt = CalculateCornerTilt(localVelocity, speed);
        
        // Apply FOV or distance zoom
        ApplySpeedZoom(speedNormalized, driftFactor);
        
        // Handle manual look
        HandleManualLook();
        
        // Calculate final camera position and rotation
        PositionAndRotateCamera(dynamicOffset, lookAhead, tilt);
    }

    private float CalculateDriftFactor(Vector3 localVelocity, float speed)
    {
        if (!useDriftEffects || speed < 5f) 
        {
            _currentDriftFactor = Mathf.SmoothDamp(_currentDriftFactor, 0f, ref _driftFactorVelocity, 0.3f);
            return _currentDriftFactor;
        }

        // Detect sideways velocity (drift)
        float sidewaysSpeed = Mathf.Abs(localVelocity.x);
        float forwardSpeed = Mathf.Abs(localVelocity.z);
        
        float targetDriftFactor = 0f;
        if (forwardSpeed > 1f && sidewaysSpeed > driftThreshold)
        {
            // Calculate drift intensity
            float slideRatio = sidewaysSpeed / forwardSpeed;
            targetDriftFactor = Mathf.Clamp01(slideRatio * 2f);
        }
        
        // Smooth drift factor
        _currentDriftFactor = Mathf.SmoothDamp(_currentDriftFactor, targetDriftFactor, ref _driftFactorVelocity, 0.3f);
        return _currentDriftFactor;
    }

    private Vector3 CalculateDynamicOffset(float speedNormalized, float driftFactor)
    {
        Vector3 offset = baseOffset;
        
        // Dynamic height (Forza-style - higher at speed)
        if (useDynamicHeight)
        {
            float targetHeight = baseOffset.y + (speedHeightBonus * speedNormalized);
            _currentHeight = Mathf.SmoothDamp(_currentHeight, targetHeight, ref _heightVelocity, heightSmoothTime);
            offset.y = _currentHeight;
        }
        
        // Distance adjustment (zoom out at speed)
        if (!useFovZoom)
        {
            float targetDistance = Mathf.Lerp(minDistance, maxDistance, speedNormalized);
            
            // Extra distance during drift
            if (useDriftEffects)
            {
                targetDistance += driftFactor * driftDistanceBonus;
            }
            
            float currentDistance = Mathf.SmoothDamp(
                offset.magnitude,
                targetDistance,
                ref _distanceVelocity,
                zoomSmoothTime
            );
            
            offset = offset.normalized * currentDistance;
        }
        
        // Pull back slightly during drift for better view
        if (useDriftEffects && driftFactor > 0.1f)
        {
            offset.z -= driftFactor * 1.5f;
        }
        
        return offset;
    }

    private Vector3 CalculateLookAhead(float speedNormalized)
    {
        if (!useLookAhead || targetRigidbody == null)
        {
            _currentLookAhead = Vector3.SmoothDamp(_currentLookAhead, Vector3.zero, ref _lookAheadVelocity, lookAheadSmoothTime);
            return _currentLookAhead;
        }
        
        // Look ahead in the direction of velocity (Forza-style)
        Vector3 velocity = targetRigidbody.linearVelocity;
        Vector3 targetLookAhead = velocity.normalized * lookAheadAmount * speedNormalized * 3f;
        
        _currentLookAhead = Vector3.SmoothDamp(_currentLookAhead, targetLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);
        return _currentLookAhead;
    }

    private float CalculateCornerTilt(Vector3 localVelocity, float speed)
    {
        if (!useCornerTilt || speed < 10f)
        {
            _currentTilt = Mathf.SmoothDampAngle(_currentTilt, 0f, ref _tiltVelocity, tiltSmoothTime);
            return _currentTilt;
        }
        
        // Calculate angular velocity for tilt (Forza-style lean into turns)
        float angularVelocity = targetRigidbody != null ? targetRigidbody.angularVelocity.y : 0f;
        
        // Also use steering input for more responsive tilt
        float steerInput = Input.GetAxis("Horizontal");
        
        // Combine angular velocity and steering
        float targetTilt = -angularVelocity * maxTiltAngle * 0.5f; // Angular velocity component
        targetTilt += -steerInput * maxTiltAngle * 0.5f; // Steering input component
        
        // Clamp to max tilt
        targetTilt = Mathf.Clamp(targetTilt, -maxTiltAngle, maxTiltAngle);
        
        // Smooth tilt transition
        _currentTilt = Mathf.SmoothDampAngle(_currentTilt, targetTilt, ref _tiltVelocity, tiltSmoothTime);
        return _currentTilt;
    }

    private void ApplySpeedZoom(float speedNormalized, float driftFactor)
    {
        if (_cam == null) return;
        
        if (useFovZoom)
        {
            // FOV zoom (Forza-style)
            float targetFov = Mathf.Lerp(minFov, maxFov, speedNormalized);
            
            // Extra FOV during drift for dramatic effect
            if (useDriftEffects)
            {
                targetFov += driftFactor * driftFovBonus;
            }
            
            _cam.fieldOfView = Mathf.SmoothDamp(
                _cam.fieldOfView,
                targetFov,
                ref _fovVelocity,
                zoomSmoothTime
            );
        }
    }

    private void PositionAndRotateCamera(Vector3 dynamicOffset, Vector3 lookAhead, float tilt)
    {
        // Apply manual look rotation
        Quaternion manualRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 rotatedOffset = manualRotation * dynamicOffset;
        
        // Calculate desired position
        Vector3 desiredPosition = target.TransformPoint(rotatedOffset);
        
        // Apply collision avoidance
        if (useCollisionAvoidance)
        {
            desiredPosition = ApplyCollisionAvoidance(target.position, desiredPosition);
        }
        
        // Smooth position
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _positionVelocity,
            positionSmoothTime
        );
        
        // Calculate look target with look-ahead
        Vector3 lookTarget = target.position + lookAhead;
        
        // Calculate desired rotation
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        
        // Apply corner tilt (roll)
        desiredRotation *= Quaternion.Euler(0f, 0f, tilt);
        
        // Smooth rotation
        transform.rotation = SmoothDampRotation(transform.rotation, desiredRotation, rotationSmoothTime);
    }

    private Vector3 ApplyCollisionAvoidance(Vector3 targetPos, Vector3 desiredPos)
    {
        Vector3 direction = desiredPos - targetPos;
        float distance = direction.magnitude;
        
        if (distance < 0.1f) return desiredPos;
        
        RaycastHit hit;
        if (Physics.Raycast(targetPos, direction.normalized, out hit, distance, collisionLayers))
        {
            // Pull camera forward to avoid collision
            return hit.point - direction.normalized * collisionPadding;
        }
        
        return desiredPos;
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

        // Auto-recenter after delay (Forza-style)
        if (!hasInput && recenterDelay >= 0f && (Time.time - _lastInputTime) >= recenterDelay)
        {
            _yaw = Mathf.SmoothDampAngle(_yaw, 0f, ref _yawVelocity, recenterSmoothTime);
            _pitch = Mathf.SmoothDampAngle(_pitch, 0f, ref _pitchVelocity, recenterSmoothTime);
        }
    }

    private Quaternion SmoothDampRotation(Quaternion current, Quaternion target, float smoothTime)
    {
        if (smoothTime <= 0f) return target;
        
        // Smooth quaternion interpolation (Forza-style)
        float t = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
        return Quaternion.Slerp(current, target, t);
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        
        // Draw base offset
        Gizmos.color = Color.yellow;
        Vector3 offsetPos = target.TransformPoint(baseOffset);
        Gizmos.DrawWireSphere(offsetPos, 0.3f);
        Gizmos.DrawLine(target.position, offsetPos);
        
        // Draw look-ahead
        if (useLookAhead && Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(target.position + _currentLookAhead, 0.2f);
            Gizmos.DrawLine(target.position, target.position + _currentLookAhead);
        }
    }
}
