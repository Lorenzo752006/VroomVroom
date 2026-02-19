using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RealisticCarController : MonoBehaviour
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
    // If your wheels are rotated wrong, try (0, 90, 0) or (0, 0, 90)
    public Vector3 wheelRotationOffset;

    [Header("Deceleration Settings")]
    [Tooltip("Engine braking multiplier when coasting (0 = none, 5 = very strong)")]
    [Range(0f, 5f)]
    public float engineBrakeMultiplier = 4.0f;

    [Tooltip("Air resistance coefficient when coasting (0 = none, 2 = very strong)")]
    [Range(0f, 3f)]
    public float airResistanceCoefficient = 0.8f;

    [Tooltip("Minimum speed (km/h) before air resistance applies")]
    [Range(0f, 20f)]
    public float airResistanceMinSpeed = 5f;

    private Rigidbody _rb;
    private float _currentSteerAngle;
    private float _currentAcceleration;
    private float _currentBrakeForce;
    private int _currentGear = 1;
    private float _currentRPM = 1000f;
    private float _lastShiftTime = 0f;
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
        _lastShiftTime = Time.time;
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

        UpdateWheelSuspension(frontLeftCollider);
        UpdateWheelSuspension(frontRightCollider);
        UpdateWheelSuspension(rearLeftCollider);
        UpdateWheelSuspension(rearRightCollider);

        ApplyWheelFriction(frontLeftCollider);
        ApplyWheelFriction(frontRightCollider);
        ApplyWheelFriction(rearLeftCollider);
        ApplyWheelFriction(rearRightCollider);
    }

    private void UpdateWheelSuspension(WheelCollider wheel)
    {
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = carData.suspensionSpring;
        spring.damper = carData.suspensionDamper;
        wheel.suspensionSpring = spring;
        wheel.suspensionDistance = carData.suspensionDistance;
    }

    private void ApplyWheelFriction(WheelCollider wheel)
    {
        if (wheel == null) return;

        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = carData.forwardFrictionStiffness;
        forward.extremumSlip = 0.4f;
        forward.extremumValue = 1.0f;
        forward.asymptoteSlip = 0.8f;
        forward.asymptoteValue = 0.5f;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = carData.sidewaysFrictionStiffness;
        sideways.extremumSlip = 0.3f;
        sideways.extremumValue = 1.0f;
        sideways.asymptoteSlip = 0.6f;
        sideways.asymptoteValue = 0.75f;
        wheel.sidewaysFriction = sideways;
    }

    private void FixedUpdate()
    {
        ApplyTireLoadSensitivity(); // Apply base friction FIRST
        HandleMotor();              // Then apply motor/brake/handbrake (can override friction)
        HandleSteering();
        ApplyDownforce();
        // Anti-roll disabled - was causing car to fly
        // ApplyAntiRoll(frontLeftCollider, frontRightCollider);
        // ApplyAntiRoll(rearLeftCollider, rearRightCollider);
        UpdateWheels();
    }

    private void HandleMotor()
    {
        float moveInput = Input.GetAxis("Vertical");
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;
        
        // Check if moving backwards
        bool isMovingBackward = Vector3.Dot(_rb.linearVelocity, transform.forward) < 0;
        float actualSpeed = isMovingBackward ? -currentSpeed : currentSpeed;

        // Calculate RPM based on vehicle speed (more stable than wheel RPM)
        float speedMps = _rb.linearVelocity.magnitude;
        float wheelRPM = (speedMps / (2f * Mathf.PI * carData.wheelRadius)) * 60f;
        float gearRatio = GetCurrentGearRatio();
        _currentRPM = Mathf.Abs(wheelRPM * gearRatio * carData.finalDriveRatio);
        _currentRPM = Mathf.Clamp(_currentRPM, carData.minRPM, carData.maxRPM);

        // Auto shift
        HandleAutoShift();

        // Calculate torque with curve
        float torqueMultiplier = GetTorqueMultiplier(_currentRPM);
        
        // Handle forward and reverse
        if (moveInput > 0.01f)
        {
            // Forward acceleration
            if (currentSpeed < carData.maxSpeed)
            {
                _currentAcceleration = moveInput * carData.maxMotorTorque * torqueMultiplier;
                
                // Extra boost for low gears (arcade feel)
                if (_currentGear <= 2)
                {
                    _currentAcceleration *= 1.3f;
                }
            }
            else
            {
                _currentAcceleration = 0;
            }
        }
        else if (moveInput < -0.01f)
        {
            // Reverse
            if (currentSpeed < 5f || isMovingBackward)
            {
                // Allow reverse if slow or already reversing
                _currentAcceleration = moveInput * carData.maxMotorTorque * 0.5f;
            }
            else
            {
                // Apply brakes if moving forward and trying to reverse
                _currentAcceleration = 0;
            }
        }
        else
        {
            _currentAcceleration = 0;
        }

        // Brake input (space) and handbrake (left shift)
        bool isBraking = Input.GetKey(KeyCode.Space);
        bool isHandbrake = Input.GetKey(KeyCode.LeftShift);
        
        // Auto-brake when pressing opposite direction
        if (moveInput < -0.01f && !isMovingBackward && currentSpeed > 5f)
        {
            _currentBrakeForce = carData.brakePower * 1.5f;
        }
        else
        {
            _currentBrakeForce = isBraking ? carData.brakePower : 0f;
        }
        
        // Engine braking when off throttle
        float engineBrake = 0f;
        if (Mathf.Abs(moveInput) < 0.01f && currentSpeed > 1f && !isBraking)
        {
            engineBrake = carData.engineBrakePower * engineBrakeMultiplier;
            
            // Add speed-dependent air resistance when coasting
            if (currentSpeed > airResistanceMinSpeed)
            {
                float airResistance = currentSpeed * currentSpeed * airResistanceCoefficient;
                _rb.AddForce(-_rb.linearVelocity.normalized * airResistance, ForceMode.Force);
            }
        }

        // Cut motor torque when handbrake is active
        float rearTorque = (_currentBrakeForce > 0f || isHandbrake) ? 0f : _currentAcceleration;
        rearTorque = ApplyTractionControl(rearLeftCollider, rearTorque, currentSpeed);
        rearTorque = ApplyTractionControl(rearRightCollider, rearTorque, currentSpeed);

        // Apply differential to distribute torque between rear wheels
        float leftTorque = rearTorque;
        float rightTorque = rearTorque;
        
        if (carData.differentialLockRatio < 1f && Mathf.Abs(rearTorque) > 0.1f)
        {
            float leftRPM = rearLeftCollider.rpm;
            float rightRPM = rearRightCollider.rpm;
            float rpmDiff = Mathf.Abs(leftRPM - rightRPM);
            
            if (rpmDiff > carData.differentialSlipThreshold)
            {
                // Limited slip: slower wheel gets more torque
                float slipFactor = Mathf.Clamp01(rpmDiff / 200f);
                float torqueShift = rearTorque * (1f - carData.differentialLockRatio) * slipFactor * 0.3f;
                
                if (leftRPM > rightRPM)
                {
                    // Left spinning faster, give more to right
                    leftTorque -= torqueShift;
                    rightTorque += torqueShift;
                }
                else
                {
                    // Right spinning faster, give more to left
                    leftTorque += torqueShift;
                    rightTorque -= torqueShift;
                }
            }
        }

        rearLeftCollider.motorTorque = leftTorque;
        rearRightCollider.motorTorque = rightTorque;
        
        float frontBrake = (_currentBrakeForce * carData.brakeFrontBias) + engineBrake;
        float rearBrake = (_currentBrakeForce * (1f - carData.brakeFrontBias)) + engineBrake;
        
        // Handbrake only on rear wheels - reduce sideways friction for drift
        // IMPORTANT: This must happen AFTER ApplyTireLoadSensitivity to not be overwritten
        if (isHandbrake)
        {
            rearBrake += carData.handBrakePower;
            
            // Reduce rear sideways friction during handbrake for easier sliding
            WheelFrictionCurve rearLeftSideways = rearLeftCollider.sidewaysFriction;
            WheelFrictionCurve rearRightSideways = rearRightCollider.sidewaysFriction;
            rearLeftSideways.stiffness = carData.sidewaysFrictionStiffness * 0.3f;
            rearRightSideways.stiffness = carData.sidewaysFrictionStiffness * 0.3f;
            rearLeftCollider.sidewaysFriction = rearLeftSideways;
            rearRightCollider.sidewaysFriction = rearRightSideways;
        }
        
        frontLeftCollider.brakeTorque = frontBrake;
        frontRightCollider.brakeTorque = frontBrake;
        rearLeftCollider.brakeTorque = rearBrake;
        rearRightCollider.brakeTorque = rearBrake;
    }

    private void HandleSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        
        float steerScale = 1f;
        if (carData.useSpeedSensitiveSteering)
        {
            float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
            
            // More aggressive speed-sensitive steering curve with better high-speed control
            if (speedKmh < 30f)
            {
                // Full steering at low speeds
                steerScale = 1f;
            }
            else if (speedKmh < 100f)
            {
                // Gradual reduction in mid speeds
                float midSpeedFactor = Mathf.InverseLerp(30f, 100f, speedKmh);
                steerScale = Mathf.Lerp(1f, 0.6f, midSpeedFactor);
            }
            else if (speedKmh < 200f)
            {
                // Further reduction at high speeds
                float highSpeedFactor = Mathf.InverseLerp(100f, 200f, speedKmh);
                steerScale = Mathf.Lerp(0.6f, 0.4f, highSpeedFactor);
            }
            else
            {
                // Very gentle steering at extreme speeds
                float extremeSpeedFactor = Mathf.InverseLerp(200f, carData.maxSpeed, speedKmh);
                steerScale = Mathf.Lerp(0.4f, Mathf.Clamp01(carData.steerAtMaxSpeed), extremeSpeedFactor);
                steerScale = Mathf.Max(steerScale, Mathf.Clamp01(carData.minSteerScale));
            }
        }

        _currentSteerAngle = carData.maxSteeringAngle * steerInput * steerScale;

        frontLeftCollider.steerAngle = _currentSteerAngle;
        frontRightCollider.steerAngle = _currentSteerAngle;
    }

    private void UpdateWheels()
    {
        // We pass the wheel collider and the corresponding BONE
        UpdateBoneTransform(frontLeftCollider, frontLeftBone);
        UpdateBoneTransform(frontRightCollider, frontRightBone);
        UpdateBoneTransform(rearLeftCollider, rearLeftBone);
        UpdateBoneTransform(rearRightCollider, rearRightBone);
    }

    private void UpdateBoneTransform(WheelCollider collider, Transform bone)
    {
        Vector3 pos;
        Quaternion rot;
        
        // 1. Get the physics position/rotation
        collider.GetWorldPose(out pos, out rot);

        // 2. Apply position with original offset
        bone.position = pos + GetPositionOffset(bone);

        // 3. Apply rotation with original offset and optional manual tweak
        bone.rotation = rot * GetRotationOffset(bone) * Quaternion.Euler(wheelRotationOffset);
    }

    private float ApplyTractionControl(WheelCollider wheel, float torque, float speedKmh)
    {
        if (wheel == null || carData == null || carData.tractionControl <= 0f)
        {
            return torque;
        }

        if (speedKmh < carData.tractionControlMinKph)
        {
            return torque;
        }

        WheelHit hit;
        if (wheel.GetGroundHit(out hit))
        {
            float slip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
            // More aggressive traction control with tighter slip range
            float slipFactor = Mathf.InverseLerp(0.15f, 0.6f, slip);
            float tc = Mathf.Clamp01(carData.tractionControl);
            torque *= Mathf.Lerp(1f, 1f - tc * 0.8f, slipFactor);
        }

        return torque;
    }

    private void ApplyDownforce()
    {
        if (carData == null || carData.downforce <= 0f)
        {
            return;
        }

        float speed = _rb.linearVelocity.magnitude;
        
        // Balanced downforce - less aggressive base, more gradual scaling
        float baseDownforce = carData.downforce * 0.5f;
        float speedDownforce = carData.downforce * speed * speed * 0.015f;
        float totalDownforce = baseDownforce + speedDownforce;
        
        _rb.AddForce(-transform.up * totalDownforce, ForceMode.Force);
    }

    private void ApplyAntiRoll(WheelCollider leftWheel, WheelCollider rightWheel)
    {
        if (carData == null || carData.antiRollStiffness <= 0f || leftWheel == null || rightWheel == null)
        {
            return;
        }

        float leftTravel = 1.0f;
        float rightTravel = 1.0f;

        WheelHit hit;
        bool leftGrounded = leftWheel.GetGroundHit(out hit);
        if (leftGrounded)
        {
            leftTravel = (-leftWheel.transform.InverseTransformPoint(hit.point).y - leftWheel.radius) /
                         leftWheel.suspensionDistance;
            leftTravel = Mathf.Clamp01(leftTravel);
        }

        bool rightGrounded = rightWheel.GetGroundHit(out hit);
        if (rightGrounded)
        {
            rightTravel = (-rightWheel.transform.InverseTransformPoint(hit.point).y - rightWheel.radius) /
                          rightWheel.suspensionDistance;
            rightTravel = Mathf.Clamp01(rightTravel);
        }

        if (!leftGrounded || !rightGrounded)
        {
            return;
        }

        float antiRollForce = (leftTravel - rightTravel) * carData.antiRollStiffness;

        _rb.AddForceAtPosition(leftWheel.transform.up * -antiRollForce, leftWheel.transform.position);
        _rb.AddForceAtPosition(rightWheel.transform.up * antiRollForce, rightWheel.transform.position);
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

    private float GetCurrentGearRatio()
    {
        if (carData == null || carData.gearRatios == null || carData.gearRatios.Length == 0)
        {
            return 1f;
        }
        
        int gearIndex = Mathf.Clamp(_currentGear - 1, 0, carData.gearRatios.Length - 1);
        return carData.gearRatios[gearIndex];
    }

    private void HandleAutoShift()
    {
        if (carData == null) return;

        float timeSinceLastShift = Time.time - _lastShiftTime;
        
        // Require 0.7 second delay between shifts to prevent rapid cycling
        if (timeSinceLastShift < 0.7f) return;

        // Shift up with hysteresis
        if (_currentRPM >= carData.shiftUpRPM && _currentGear < carData.gearCount)
        {
            _currentGear++;
            _lastShiftTime = Time.time;
        }
        // Shift down - only if RPM is very low to prevent cycling
        else if (_currentRPM <= carData.shiftDownRPM && _currentGear > 1)
        {
            _currentGear--;
            _lastShiftTime = Time.time;
        }
    }

    private float GetTorqueMultiplier(float rpm)
    {
        if (carData == null) return 1f;
        
        // Normalize RPM to 0-1 range
        float normalizedRPM = Mathf.InverseLerp(carData.minRPM, carData.maxRPM, rpm);
        
        // Evaluate torque curve
        return carData.torqueCurve.Evaluate(normalizedRPM);
    }

    public int GetCurrentGear()
    {
        return _currentGear;
    }

    public float GetCurrentRPM()
    {
        return _currentRPM;
    }

    private void ApplyTireLoadSensitivity()
    {
        if (carData == null || !carData.enableLoadSensitivity) return;

        ApplyLoadToWheel(frontLeftCollider);
        ApplyLoadToWheel(frontRightCollider);
        ApplyLoadToWheel(rearLeftCollider);
        ApplyLoadToWheel(rearRightCollider);
    }

    private void ApplyLoadToWheel(WheelCollider wheel)
    {
        if (wheel == null) return;

        WheelHit hit;
        if (wheel.GetGroundHit(out hit))
        {
            // Calculate load on this wheel from suspension force
            float suspensionForce = hit.force;
            
            // Calculate grip multiplier based on load vs optimal load
            float loadRatio = suspensionForce / carData.optimalLoad;
            float gripMultiplier = 1f;
            
            if (loadRatio < 1f)
            {
                // Under optimal load: gentler reduction to prevent low-speed instability
                gripMultiplier = Mathf.Lerp(0.85f, 1f, loadRatio);
            }
            else
            {
                // Over optimal load: slight decrease (tire saturation)
                gripMultiplier = Mathf.Lerp(1f, 0.9f, Mathf.Clamp01((loadRatio - 1f) * 0.5f));
            }
            
            // Apply sensitivity factor
            gripMultiplier = Mathf.Lerp(1f, gripMultiplier, carData.loadSensitivityFactor);
            
            // Modify friction curves
            WheelFrictionCurve forward = wheel.forwardFriction;
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            
            forward.stiffness = carData.forwardFrictionStiffness * gripMultiplier;
            sideways.stiffness = carData.sidewaysFrictionStiffness * gripMultiplier;
            
            wheel.forwardFriction = forward;
            wheel.sidewaysFriction = sideways;
        }
    }
}