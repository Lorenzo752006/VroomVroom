using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeMovement : MonoBehaviour
{
    [Header("Car Configuration")]
    public CarData carData;

    [Header("Wheel Colliders (Physics)")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheel Bones (Visuals)")]
    public Transform frontLeftBone;
    public Transform frontRightBone;
    public Transform rearLeftBone;
    public Transform rearRightBone;

    [Header("Bone Fixes")]
    public Vector3 wheelRotationOffset;

    [Header("Arcade Settings")]
    [Tooltip("Multiplier for acceleration (higher = faster)")]
    public float accelerationMultiplier = 2.5f;
    [Tooltip("Multiplier for steering response (higher = sharper turns)")]
    public float steeringMultiplier = 1.5f;
    [Tooltip("How much to boost grip (higher = less sliding)")]
    public float gripBoost = 1.8f;
    [Tooltip("Additional downforce for stability")]
    public float arcadeDownforce = 15f;
    [Tooltip("Auto-correct rotation to keep car upright")]
    public float uprightForce = 5f;
    [Tooltip("Simplified braking power")]
    public float arcadeBrakeMultiplier = 1.5f;
    [Tooltip("Engine brake multiplier when coasting (0 = none, 5 = very strong)")]
    [Range(0f, 5f)]
    public float coastingBrakeMultiplier = 2.5f;
    [Tooltip("Air resistance coefficient when coasting (0 = none, 3 = very strong)")]
    [Range(0f, 3f)]
    public float airResistanceCoefficient = 1.2f;
    [Tooltip("Minimum speed (m/s) before stopping completely")]
    [Range(0f, 2f)]
    public float minCoastSpeed = 0.5f;

    [Header("Dynamic Steering")]
    [Tooltip("Steering at low speeds (0-30 km/h)")]
    public float lowSpeedSteerMultiplier = 1.2f;
    [Tooltip("Steering at medium speeds (30-100 km/h)")]
    public float mediumSpeedSteerMultiplier = 1.0f;
    [Tooltip("Steering at high speeds (100-200 km/h)")]
    public float highSpeedSteerMultiplier = 0.5f;
    [Tooltip("Steering at max speeds (200+ km/h)")]
    public float maxSpeedSteerMultiplier = 0.3f;
    [Tooltip("How smoothly steering transitions between speeds")]
    public float steeringTransitionSpeed = 5f;

    private Rigidbody _rb;
    private float _currentSteerAngle;
    private float _targetSteerMultiplier;
    private float _smoothedSteerMultiplier;
    private Quaternion _frontLeftRotOffset;
    private Quaternion _frontRightRotOffset;
    private Quaternion _rearLeftRotOffset;
    private Quaternion _rearRightRotOffset;
    private Vector3 _frontLeftPosOffset;
    private Vector3 _frontRightPosOffset;
    private Vector3 _rearLeftPosOffset;
    private Vector3 _rearRightPosOffset;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        ApplyCarData();
        CacheWheelOffsets();

        // Configure rigidbody for more responsive arcade physics
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Initialize steering multiplier
        _smoothedSteerMultiplier = lowSpeedSteerMultiplier;
    }

    private void CacheWheelOffsets()
    {
        CacheWheelOffset(frontLeftCollider, frontLeftBone, out _frontLeftRotOffset, out _frontLeftPosOffset);
        CacheWheelOffset(frontRightCollider, frontRightBone, out _frontRightRotOffset, out _frontRightPosOffset);
        CacheWheelOffset(rearLeftCollider, rearLeftBone, out _rearLeftRotOffset, out _rearLeftPosOffset);
        CacheWheelOffset(rearRightCollider, rearRightBone, out _rearRightRotOffset, out _rearRightPosOffset);
    }

    private void CacheWheelOffset(WheelCollider collider, Transform bone, out Quaternion rotOffset, out Vector3 posOffset)
    {
        rotOffset = Quaternion.identity;
        posOffset = Vector3.zero;

        if (collider == null || bone == null)
        {
            return;
        }

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        rotOffset = bone.rotation * Quaternion.Inverse(rot);
        posOffset = bone.position - pos;
    }

    public void ApplyCarData()
    {
        if (carData == null) return;

        _rb.mass = carData.mass;
        _rb.centerOfMass = carData.centerOfMassOffset;

        // Make suspension stiffer for arcade feel
        UpdateWheelSuspension(frontLeftCollider);
        UpdateWheelSuspension(frontRightCollider);
        UpdateWheelSuspension(rearLeftCollider);
        UpdateWheelSuspension(rearRightCollider);

        // Boost friction for better grip
        ApplyWheelFriction(frontLeftCollider);
        ApplyWheelFriction(frontRightCollider);
        ApplyWheelFriction(rearLeftCollider);
        ApplyWheelFriction(rearRightCollider);
    }

    private void UpdateWheelSuspension(WheelCollider wheel)
    {
        JointSpring spring = wheel.suspensionSpring;
        // Stiffer suspension for arcade feel
        spring.spring = carData.suspensionSpring * 1.3f;
        spring.damper = carData.suspensionDamper * 1.2f;
        wheel.suspensionSpring = spring;
        wheel.suspensionDistance = carData.suspensionDistance * 0.8f; // Shorter travel
    }

    private void ApplyWheelFriction(WheelCollider wheel)
    {
        if (wheel == null) return;

        WheelFrictionCurve forward = wheel.forwardFriction;
        // Boost forward grip significantly
        forward.stiffness = carData.forwardFrictionStiffness * gripBoost;
        forward.extremumSlip = 0.3f; // Tighter slip range
        forward.extremumValue = 1.2f; // More grip
        forward.asymptoteSlip = 0.6f;
        forward.asymptoteValue = 0.8f;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        // Boost sideways grip for less sliding
        sideways.stiffness = carData.sidewaysFrictionStiffness * gripBoost;
        sideways.extremumSlip = 0.25f;
        sideways.extremumValue = 1.2f;
        sideways.asymptoteSlip = 0.5f;
        sideways.asymptoteValue = 0.85f;
        wheel.sidewaysFriction = sideways;
    }

    private void FixedUpdate()
    {
        HandleArcadeMotor();
        HandleArcadeSteering();
        ApplyArcadeDownforce();
        ApplyUprightForce();
        ApplyCoastingDrag();
        UpdateWheels();
    }

    private void HandleArcadeMotor()
    {
        float moveInput = Input.GetAxis("Vertical");
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;

        // Simplified arcade torque - no RPM/gear simulation
        float baseTorque = carData.maxMotorTorque * accelerationMultiplier;

        // Speed-based torque reduction (keeps max speed realistic)
        float speedFactor = 1f - Mathf.Clamp01(currentSpeed / carData.maxSpeed);
        float torque = baseTorque * speedFactor;

        bool isHandbrake = Input.GetKey(KeyCode.LeftShift);

        if (isHandbrake)
        {
            // Handbrake - lock rear wheels and reduce friction for drifting
            rearLeftCollider.motorTorque = 0;
            rearRightCollider.motorTorque = 0;

            float handbrakeTorque = carData.handBrakePower * 1.2f;
            rearLeftCollider.brakeTorque = handbrakeTorque;
            rearRightCollider.brakeTorque = handbrakeTorque;

            // Front wheels get no brake during handbrake
            frontLeftCollider.brakeTorque = 0;
            frontRightCollider.brakeTorque = 0;

            // Reduce rear sideways friction for drift
            WheelFrictionCurve rearLeftSideways = rearLeftCollider.sidewaysFriction;
            WheelFrictionCurve rearRightSideways = rearRightCollider.sidewaysFriction;
            rearLeftSideways.stiffness = carData.sidewaysFrictionStiffness * 0.4f;
            rearRightSideways.stiffness = carData.sidewaysFrictionStiffness * 0.4f;
            rearLeftCollider.sidewaysFriction = rearLeftSideways;
            rearRightCollider.sidewaysFriction = rearRightSideways;
        }
        else if (moveInput > 0.01f && currentSpeed < carData.maxSpeed)
        {
            // Forward acceleration - apply to rear wheels for RWD arcade feel
            float appliedTorque = moveInput * torque;
            rearLeftCollider.motorTorque = appliedTorque;
            rearRightCollider.motorTorque = appliedTorque;

            // Release brakes
            ReleaseBrakes();

            // Restore friction
            ApplyWheelFriction(rearLeftCollider);
            ApplyWheelFriction(rearRightCollider);
        }
        else if (moveInput < -0.01f)
        {
            // S key pressed - brake or reverse depending on speed
            if (currentSpeed > 5f)
            {
                // Moving forward - apply brakes
                float brakeTorque = carData.brakePower * arcadeBrakeMultiplier;
                ApplyBrakes(brakeTorque);

                // Cut motor torque
                rearLeftCollider.motorTorque = 0;
                rearRightCollider.motorTorque = 0;
            }
            else
            {
                // Slow enough to reverse
                ReleaseBrakes();
                float reverseTorque = Mathf.Abs(moveInput) * baseTorque * 0.6f;
                rearLeftCollider.motorTorque = -reverseTorque;
                rearRightCollider.motorTorque = -reverseTorque;
            }

            // Restore friction
            ApplyWheelFriction(rearLeftCollider);
            ApplyWheelFriction(rearRightCollider);
        }
        else
        {
            // No input - coast (drag is applied in ApplyCoastingDrag)
            rearLeftCollider.motorTorque = 0;
            rearRightCollider.motorTorque = 0;
            ReleaseBrakes();

            // Restore friction
            ApplyWheelFriction(rearLeftCollider);
            ApplyWheelFriction(rearRightCollider);
        }
    }

    private void ApplyBrakes(float brakeTorque)
    {
        // Apply brakes with bias toward front (uses carData brake bias)
        frontLeftCollider.brakeTorque = brakeTorque * carData.brakeFrontBias;
        frontRightCollider.brakeTorque = brakeTorque * carData.brakeFrontBias;
        rearLeftCollider.brakeTorque = brakeTorque * (1f - carData.brakeFrontBias);
        rearRightCollider.brakeTorque = brakeTorque * (1f - carData.brakeFrontBias);
    }

    private void ReleaseBrakes()
    {
        frontLeftCollider.brakeTorque = 0;
        frontRightCollider.brakeTorque = 0;
        rearLeftCollider.brakeTorque = 0;
        rearRightCollider.brakeTorque = 0;
    }

    private void ApplyCoastingDrag()
    {
        // Apply gradual slowdown when coasting (no input)
        float moveInput = Input.GetAxis("Vertical");
        bool isHandbrake = Input.GetKey(KeyCode.LeftShift);

        // Only apply coasting drag when there's no input and not using handbrake
        if (Mathf.Abs(moveInput) < 0.01f && !isHandbrake)
        {
            Vector3 velocity = _rb.linearVelocity;
            float currentSpeed = velocity.magnitude * 3.6f; // km/h
            float currentSpeedMps = velocity.magnitude;

            if (currentSpeedMps > minCoastSpeed)
            {
                // Apply engine braking through brake torque
                float engineBrake = carData.engineBrakePower * coastingBrakeMultiplier;
                float frontBrake = engineBrake * carData.brakeFrontBias;
                float rearBrake = engineBrake * (1f - carData.brakeFrontBias);
                
                frontLeftCollider.brakeTorque = frontBrake;
                frontRightCollider.brakeTorque = frontBrake;
                rearLeftCollider.brakeTorque = rearBrake;
                rearRightCollider.brakeTorque = rearBrake;
                
                // Apply air resistance proportional to speed squared
                float airResistance = currentSpeedMps * currentSpeedMps * airResistanceCoefficient;
                _rb.AddForce(-velocity.normalized * airResistance, ForceMode.Force);
            }
            else if (currentSpeedMps > 0.1f && currentSpeedMps <= minCoastSpeed)
            {
                // Almost stopped - bring to complete stop
                _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
                ReleaseBrakes();
            }
            else
            {
                // Completely stopped
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                ReleaseBrakes();
            }
        }
    }

    private void HandleArcadeSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        float speedKmh = _rb.linearVelocity.magnitude * 3.6f;

        // Calculate dynamic steering multiplier based on speed ranges
        if (speedKmh < 30f)
        {
            // Low speed: tight turning for parking/maneuvering
            _targetSteerMultiplier = lowSpeedSteerMultiplier;
        }
        else if (speedKmh < 100f)
        {
            // Medium speed: blend between low and medium
            float t = Mathf.InverseLerp(30f, 100f, speedKmh);
            _targetSteerMultiplier = Mathf.Lerp(lowSpeedSteerMultiplier, mediumSpeedSteerMultiplier, t);
        }
        else if (speedKmh < 200f)
        {
            // High speed: blend between medium and high
            float t = Mathf.InverseLerp(100f, 200f, speedKmh);
            _targetSteerMultiplier = Mathf.Lerp(mediumSpeedSteerMultiplier, highSpeedSteerMultiplier, t);
        }
        else
        {
            // Max speed: blend between high and max
            float t = Mathf.InverseLerp(200f, carData.maxSpeed, speedKmh);
            _targetSteerMultiplier = Mathf.Lerp(highSpeedSteerMultiplier, maxSpeedSteerMultiplier, t);
        }

        // Smoothly transition to target multiplier for natural feel
        _smoothedSteerMultiplier = Mathf.Lerp(
            _smoothedSteerMultiplier,
            _targetSteerMultiplier,
            Time.fixedDeltaTime * steeringTransitionSpeed
        );

        // Calculate final steering angle
        float baseSteer = carData.maxSteeringAngle * steeringMultiplier;
        _currentSteerAngle = baseSteer * steerInput * _smoothedSteerMultiplier;

        // Apply steering
        frontLeftCollider.steerAngle = _currentSteerAngle;
        frontRightCollider.steerAngle = _currentSteerAngle;
    }

    private void ApplyArcadeDownforce()
    {
        // Strong downforce for stability at all speeds
        float speed = _rb.linearVelocity.magnitude;

        // Combine CarData downforce with arcade boost
        float totalDownforce = (carData.downforce + arcadeDownforce) * speed * speed * 0.01f;

        // Add constant downforce for ground contact
        float constantDownforce = 5000f;

        _rb.AddForce(-transform.up * (totalDownforce + constantDownforce), ForceMode.Force);
    }

    private void ApplyUprightForce()
    {
        // Keep car upright for arcade stability
        Quaternion currentRotation = transform.rotation;
        Quaternion uprightRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * currentRotation;

        Quaternion deltaRotation = uprightRotation * Quaternion.Inverse(currentRotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        // Normalize angle
        if (angle > 180f) angle -= 360f;

        // Apply corrective torque
        _rb.AddTorque(axis.normalized * angle * uprightForce, ForceMode.Acceleration);
    }

    private void UpdateWheels()
    {
        UpdateBoneTransform(frontLeftCollider, frontLeftBone);
        UpdateBoneTransform(frontRightCollider, frontRightBone);
        UpdateBoneTransform(rearLeftCollider, rearLeftBone);
        UpdateBoneTransform(rearRightCollider, rearRightBone);
    }

    private void UpdateBoneTransform(WheelCollider collider, Transform bone)
    {
        Vector3 pos;
        Quaternion rot;

        collider.GetWorldPose(out pos, out rot);
        bone.position = pos + GetPositionOffset(bone);
        bone.rotation = rot * GetRotationOffset(bone) * Quaternion.Euler(wheelRotationOffset);
    }

    private Quaternion GetRotationOffset(Transform bone)
    {
        if (bone == frontLeftBone) return _frontLeftRotOffset;
        if (bone == frontRightBone) return _frontRightRotOffset;
        if (bone == rearLeftBone) return _rearLeftRotOffset;
        if (bone == rearRightBone) return _rearRightRotOffset;
        return Quaternion.identity;
    }

    private Vector3 GetPositionOffset(Transform bone)
    {
        if (bone == frontLeftBone) return _frontLeftPosOffset;
        if (bone == frontRightBone) return _frontRightPosOffset;
        if (bone == rearLeftBone) return _rearLeftPosOffset;
        if (bone == rearRightBone) return _rearRightPosOffset;
        return Vector3.zero;
    }
}