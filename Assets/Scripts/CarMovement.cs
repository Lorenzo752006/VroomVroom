using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarMovement : MonoBehaviour
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

    [Header("Visual Settings")]
    public Vector3 wheelRotationOffset;

    [Header("Forza Horizon Style Settings")]
    [Tooltip("Makes acceleration more responsive (1.0 = realistic, 2.0 = arcadey)")]
    [Range(1f, 3f)]
    public float accelerationBoost = 1.6f;

    [Tooltip("Automatic stability assist to prevent spinning out")]
    [Range(0f, 1f)]
    public float stabilityAssist = 0.5f;

    [Tooltip("Counter-steering assistance (helps during drifts)")]
    [Range(0f, 1f)]
    public float counterSteerAssist = 0.35f;

    [Tooltip("How aggressively the car tries to stay flat")]
    [Range(0f, 10f)]
    public float antiRollForce = 5f;

    [Tooltip("Additional grip at speed for stability")]
    [Range(0f, 500f)]
    public float speedDownforce = 200f;

    [Tooltip("Enable assisted braking when reversing direction")]
    public bool assistedBraking = true;

    [Tooltip("Drift mode friction reduction (lower = easier drifts)")]
    [Range(0.2f, 0.8f)]
    public float driftFriction = 0.35f;

    [Header("Deceleration Settings")]
    [Tooltip("Engine braking multiplier when coasting (0 = none, 3 = strong)")]
    [Range(0f, 5f)]
    public float engineBrakeMultiplier = 3.0f;

    [Tooltip("Air resistance coefficient when coasting (0 = none, 2 = very strong)")]
    [Range(0f, 3f)]
    public float airResistanceCoefficient = 0.8f;

    [Tooltip("Minimum speed (km/h) before air resistance applies")]
    [Range(0f, 20f)]
    public float airResistanceMinSpeed = 5f;

    private Rigidbody _rb;
    private float _currentSteerAngle;
    private int _currentGear = 1;
    private float _currentRPM;
    private float _lastShiftTime;
    private Vector3 _localVelocity;
    private bool _isHandbraking;

    // Cached wheel offsets
    private Quaternion _frontLeftRotOffset;
    private Quaternion _frontRightRotOffset;
    private Quaternion _rearLeftRotOffset;
    private Quaternion _rearRightRotOffset;
    private Vector3 _frontLeftPosOffset;
    private Vector3 _frontRightPosOffset;
    private Vector3 _rearLeftPosOffset;
    private Vector3 _rearRightPosOffset;

    // Base friction values (cached at start)
    private float _baseFrictionStiffness;
    private float _baseSidewaysStiffness;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();

        if (carData == null)
        {
            Debug.LogError("CarMovement: CarData not assigned!");
            return;
        }

        ApplyCarConfiguration();
        CacheWheelOffsets();

        // Cache base friction values
        _baseFrictionStiffness = carData.forwardFrictionStiffness;
        _baseSidewaysStiffness = carData.sidewaysFrictionStiffness;

        // Optimize rigidbody settings for smoother physics
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        _lastShiftTime = Time.time;
    }

    private void ApplyCarConfiguration()
    {
        _rb.mass = carData.mass;
        _rb.centerOfMass = carData.centerOfMassOffset;

        // Configure all wheels
        ConfigureWheel(frontLeftCollider);
        ConfigureWheel(frontRightCollider);
        ConfigureWheel(rearLeftCollider);
        ConfigureWheel(rearRightCollider);
    }

    private void ConfigureWheel(WheelCollider wheel)
    {
        if (wheel == null) return;

        // Suspension setup
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = carData.suspensionSpring;
        spring.damper = carData.suspensionDamper;
        wheel.suspensionSpring = spring;
        wheel.suspensionDistance = carData.suspensionDistance;

        // Friction curves - Forza-style balanced grip
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = carData.forwardFrictionStiffness * 1.1f; // Slight boost
        forward.extremumSlip = 0.35f;
        forward.extremumValue = 1.0f;
        forward.asymptoteSlip = 0.75f;
        forward.asymptoteValue = 0.6f;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = carData.sidewaysFrictionStiffness * 1.15f; // Better cornering
        sideways.extremumSlip = 0.3f;
        sideways.extremumValue = 1.0f;
        sideways.asymptoteSlip = 0.65f;
        sideways.asymptoteValue = 0.7f;
        wheel.sidewaysFriction = sideways;
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

        if (collider == null || bone == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        rotOffset = bone.rotation * Quaternion.Inverse(rot);
        posOffset = bone.position - pos;
    }

    private void FixedUpdate()
    {
        if (carData == null) return;

        // Cache local velocity for calculations
        _localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);

        // Core systems
        UpdateDynamicFriction(); // Apply FIRST to set base grip
        HandlePowerAndBraking();
        HandleSteering();
        HandleGearShifting();

        // Forza-style assists
        ApplyStabilityControl();
        ApplyCounterSteerAssist();
        ApplyAntiRoll();
        ApplyDownforce();

        // Update visual wheels
        UpdateWheelVisuals();
    }

    private void HandlePowerAndBraking()
    {
        float throttleInput = Input.GetAxis("Vertical");
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f; // km/h

        // Determine if moving forward or backward
        bool movingForward = _localVelocity.z > 0.5f;
        bool movingBackward = _localVelocity.z < -0.5f;

        _isHandbraking = Input.GetKey(KeyCode.LeftShift);

        // Calculate engine RPM
        CalculateRPM();

        // Get torque from RPM curve
        float torqueMultiplier = GetTorqueMultiplier(_currentRPM);
        float maxTorque = carData.maxMotorTorque * torqueMultiplier * accelerationBoost;

        float motorTorque = 0f;
        float brakeTorque = 0f;

        if (_isHandbraking)
        {
            // Handbrake - drift mode
            HandleHandbrake();
            return;
        }

        if (throttleInput > 0.01f)
        {
            // Accelerating forward
            if (currentSpeed < carData.maxSpeed)
            {
                motorTorque = throttleInput * maxTorque;

                // Extra low-speed boost (Forza feel)
                if (currentSpeed < 30f)
                {
                    motorTorque *= 1.4f;
                }
            }

            // If trying to accelerate while reversing, apply brakes first
            if (movingBackward && assistedBraking)
            {
                brakeTorque = carData.brakePower;
                motorTorque = 0f;
            }
        }
        else if (throttleInput < -0.01f)
        {
            // Braking or reversing
            if (movingForward && currentSpeed > 3f)
            {
                // Apply brakes when moving forward
                brakeTorque = carData.brakePower * Mathf.Abs(throttleInput);
                if (assistedBraking) brakeTorque *= 1.2f; // Assisted braking
            }
            else
            {
                // Reverse gear
                motorTorque = throttleInput * maxTorque * 0.5f; // Half torque in reverse
            }
        }
        else
        {
            // No input - apply engine braking and air resistance
            brakeTorque = carData.engineBrakePower * engineBrakeMultiplier;

            // Add speed-dependent air resistance when coasting
            if (currentSpeed > airResistanceMinSpeed)
            {
                float airResistance = currentSpeed * currentSpeed * airResistanceCoefficient;
                _rb.AddForce(-_rb.linearVelocity.normalized * airResistance, ForceMode.Force);
            }
        }

        // Apply traction control (Forza-style - subtle but effective)
        motorTorque = ApplyTractionControl(motorTorque, currentSpeed);

        // Apply torque to rear wheels (RWD for sporty feel)
        rearLeftCollider.motorTorque = motorTorque;
        rearRightCollider.motorTorque = motorTorque;
        frontLeftCollider.motorTorque = 0f;
        frontRightCollider.motorTorque = 0f;

        // Apply brakes with bias - ENSURE PERFECT SYMMETRY
        float frontBrake = brakeTorque * carData.brakeFrontBias;
        float rearBrake = brakeTorque * (1f - carData.brakeFrontBias);

        // Force identical brake values to prevent pulling during braking
        frontLeftCollider.brakeTorque = frontBrake;
        frontRightCollider.brakeTorque = frontBrake;
        rearLeftCollider.brakeTorque = rearBrake;
        rearRightCollider.brakeTorque = rearBrake;
        
        // Verify friction symmetry when braking to prevent pull
        if (brakeTorque > 0.1f)
        {
            EnsureSymmetricFriction();
        }
    }

    private void HandleHandbrake()
    {
        // Cut motor power
        rearLeftCollider.motorTorque = 0f;
        rearRightCollider.motorTorque = 0f;
        frontLeftCollider.motorTorque = 0f;
        frontRightCollider.motorTorque = 0f;

        // Apply handbrake to rear wheels only
        rearLeftCollider.brakeTorque = carData.handBrakePower;
        rearRightCollider.brakeTorque = carData.handBrakePower;
        frontLeftCollider.brakeTorque = 0f;
        frontRightCollider.brakeTorque = 0f;

        // Reduce rear friction for drift (Forza-style)
        // Note: Only modify sideways friction, keep forward friction normal
        WheelFrictionCurve rearLeftSideways = rearLeftCollider.sidewaysFriction;
        WheelFrictionCurve rearRightSideways = rearRightCollider.sidewaysFriction;
        rearLeftSideways.stiffness = _baseSidewaysStiffness * driftFriction;
        rearRightSideways.stiffness = _baseSidewaysStiffness * driftFriction;
        rearLeftCollider.sidewaysFriction = rearLeftSideways;
        rearRightCollider.sidewaysFriction = rearRightSideways;
    }

    private void HandleSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f; // km/h

        // Forza Horizon-style speed-sensitive steering
        float steerMultiplier = CalculateSteeringMultiplier(currentSpeed);

        _currentSteerAngle = carData.maxSteeringAngle * steerInput * steerMultiplier;

        frontLeftCollider.steerAngle = _currentSteerAngle;
        frontRightCollider.steerAngle = _currentSteerAngle;
    }

    private float CalculateSteeringMultiplier(float speedKmh)
    {
        // Forza-style progressive steering reduction
        if (speedKmh < 20f)
        {
            return 1.0f; // Full steering at low speeds
        }
        else if (speedKmh < 80f)
        {
            // Gentle reduction up to highway speeds
            return Mathf.Lerp(1.0f, 0.75f, (speedKmh - 20f) / 60f);
        }
        else if (speedKmh < 150f)
        {
            // More reduction at high speeds
            return Mathf.Lerp(0.75f, 0.5f, (speedKmh - 80f) / 70f);
        }
        else
        {
            // Max speed - minimal but responsive steering
            return Mathf.Lerp(0.5f, 0.35f, (speedKmh - 150f) / (carData.maxSpeed - 150f));
        }
    }

    private void ApplyStabilityControl()
    {
        if (stabilityAssist <= 0f) return;

        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;

        // Only apply at medium-high speeds
        if (currentSpeed < 30f) return;

        // Detect sliding by checking sideways velocity
        float sidewaysSpeed = Mathf.Abs(_localVelocity.x);
        float forwardSpeed = Mathf.Abs(_localVelocity.z);

        if (forwardSpeed < 1f) return; // Don't apply when stationary

        float slideRatio = sidewaysSpeed / forwardSpeed;

        // If sliding too much, reduce it (Forza-style gentle correction)
        if (slideRatio > 0.15f && !_isHandbraking)
        {
            float correction = Mathf.Clamp01((slideRatio - 0.15f) * 2f) * stabilityAssist;

            // Scale force with speed - MORE AGGRESSIVE reduction at extreme speeds
            float speedFactor;
            if (currentSpeed < 100f)
            {
                // Ramps up to 100 km/h
                speedFactor = Mathf.Clamp01(currentSpeed / 100f);
            }
            else if (currentSpeed < 180f)
            {
                // Full strength 100-180 km/h (reduced from 200)
                speedFactor = 1f;
            }
            else
            {
                // More aggressive reduction at extreme speeds (180+ km/h)
                float excessSpeed = currentSpeed - 180f;
                speedFactor = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(excessSpeed / 150f));
            }

            float forceMultiplier = Mathf.Lerp(5000f, 12000f, speedFactor);

            Vector3 correctionForce = -transform.right * _localVelocity.x * correction * forceMultiplier;
            _rb.AddForce(correctionForce, ForceMode.Force);
        }
    }

    private void ApplyCounterSteerAssist()
    {
        if (counterSteerAssist <= 0f || _isHandbraking) return;

        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;
        if (currentSpeed < 20f) return;

        // Reduce counter-steer at extreme speeds to prevent fighting with stability control
        float speedReduction = 1f;
        if (currentSpeed > 200f)
        {
            speedReduction = Mathf.Lerp(1f, 0.5f, (currentSpeed - 200f) / 200f);
        }

        // Detect if car is rotating too much
        float angularVel = _rb.angularVelocity.y;
        float steerInput = Input.GetAxis("Horizontal");

        // If rotating opposite to input (oversteering), apply counter-torque
        if (Mathf.Sign(angularVel) != Mathf.Sign(steerInput) && Mathf.Abs(angularVel) > 0.5f)
        {
            float counterTorque = -angularVel * counterSteerAssist * 1000f * speedReduction;
            _rb.AddTorque(transform.up * counterTorque, ForceMode.Force);
        }
    }

    private void ApplyAntiRoll()
    {
        if (antiRollForce <= 0f) return;

        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;

        // More aggressive reduction at high speeds to prevent lateral oscillation
        float speedFactor = 1f;
        if (currentSpeed > 120f)
        {
            // Start reducing earlier (120 km/h instead of 150)
            // Reduce more aggressively to prevent oscillation
            float excessSpeed = currentSpeed - 120f;
            speedFactor = Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(excessSpeed / 150f));
        }

        // Front axle
        ApplyAntiRollToAxle(frontLeftCollider, frontRightCollider, speedFactor);

        // Rear axle
        ApplyAntiRollToAxle(rearLeftCollider, rearRightCollider, speedFactor);
    }

    private void ApplyAntiRollToAxle(WheelCollider leftWheel, WheelCollider rightWheel, float speedFactor = 1f)
    {
        if (leftWheel == null || rightWheel == null) return;

        WheelHit leftHit;
        WheelHit rightHit;
        bool leftGrounded = leftWheel.GetGroundHit(out leftHit);
        bool rightGrounded = rightWheel.GetGroundHit(out rightHit);

        if (!leftGrounded || !rightGrounded) return;

        float leftTravel = (-leftWheel.transform.InverseTransformPoint(leftHit.point).y - leftWheel.radius) / leftWheel.suspensionDistance;
        float rightTravel = (-rightWheel.transform.InverseTransformPoint(rightHit.point).y - rightWheel.radius) / rightWheel.suspensionDistance;

        float antiRoll = (leftTravel - rightTravel) * antiRollForce * 1000f * speedFactor;

        if (leftGrounded)
            _rb.AddForceAtPosition(leftWheel.transform.up * -antiRoll, leftWheel.transform.position);
        if (rightGrounded)
            _rb.AddForceAtPosition(rightWheel.transform.up * antiRoll, rightWheel.transform.position);
    }

    private void ApplyDownforce()
    {
        float speed = _rb.linearVelocity.magnitude;
        float speedKmh = speed * 3.6f;

        // Progressive downforce (Forza-style) - more aggressive at medium-high speeds
        // Below 50 km/h: minimal
        // 50-150 km/h: progressive increase (where the issue occurs)
        // 150+ km/h: maximum downforce
        float speedBasedDownforce = 0f;

        if (speedKmh > 50f)
        {
            // More aggressive multiplier for better grip at 100+ km/h
            float normalizedSpeed = (speedKmh - 50f) / (carData.maxSpeed - 50f);
            speedBasedDownforce = (carData.downforce + speedDownforce) * normalizedSpeed * normalizedSpeed * speed * 0.25f;
        }

        // Base downforce for general stability
        float baseDownforce = 3500f;

        // Total downforce
        float totalDownforce = baseDownforce + speedBasedDownforce;

        _rb.AddForce(-transform.up * totalDownforce, ForceMode.Force);
    }

    private float ApplyTractionControl(float torque, float speedKmh)
    {
        if (speedKmh < carData.tractionControlMinKph) return torque;

        // Check rear wheel slip
        float totalSlip = 0f;
        int slippingWheels = 0;

        WheelHit hit;
        if (rearLeftCollider.GetGroundHit(out hit))
        {
            totalSlip += Mathf.Abs(hit.forwardSlip);
            slippingWheels++;
        }
        if (rearRightCollider.GetGroundHit(out hit))
        {
            totalSlip += Mathf.Abs(hit.forwardSlip);
            slippingWheels++;
        }

        if (slippingWheels == 0) return torque;

        float avgSlip = totalSlip / slippingWheels;

        // Forza-style TCS - gentle and progressive
        if (avgSlip > 0.3f)
        {
            float reduction = Mathf.Clamp01((avgSlip - 0.3f) * carData.tractionControl);
            torque *= (1f - reduction * 0.6f); // Max 60% reduction
        }

        return torque;
    }

    private void CalculateRPM()
    {
        // Calculate RPM based on wheel speed and current gear
        float wheelRPM = Mathf.Abs((rearLeftCollider.rpm + rearRightCollider.rpm) / 2f);
        float gearRatio = GetCurrentGearRatio();
        _currentRPM = wheelRPM * gearRatio * carData.finalDriveRatio;
        _currentRPM = Mathf.Clamp(_currentRPM, carData.minRPM, carData.maxRPM);
    }

    private void HandleGearShifting()
    {
        float timeSinceShift = Time.time - _lastShiftTime;
        if (timeSinceShift < 0.5f) return; // Prevent rapid shifting

        // Shift up
        if (_currentRPM >= carData.shiftUpRPM && _currentGear < carData.gearCount)
        {
            _currentGear++;
            _lastShiftTime = Time.time;
        }
        // Shift down
        else if (_currentRPM <= carData.shiftDownRPM && _currentGear > 1)
        {
            _currentGear--;
            _lastShiftTime = Time.time;
        }
    }

    private float GetCurrentGearRatio()
    {
        if (carData.gearRatios == null || carData.gearRatios.Length == 0) return 1f;
        int index = Mathf.Clamp(_currentGear - 1, 0, carData.gearRatios.Length - 1);
        return carData.gearRatios[index];
    }

    private float GetTorqueMultiplier(float rpm)
    {
        float normalizedRPM = Mathf.Lerp(carData.minRPM, carData.maxRPM, rpm);
        return carData.torqueCurve.Evaluate(normalizedRPM);
    }

    private void UpdateWheelVisuals()
    {
        UpdateWheelBone(frontLeftCollider, frontLeftBone, _frontLeftRotOffset, _frontLeftPosOffset);
        UpdateWheelBone(frontRightCollider, frontRightBone, _frontRightRotOffset, _frontRightPosOffset);
        UpdateWheelBone(rearLeftCollider, rearLeftBone, _rearLeftRotOffset, _rearLeftPosOffset);
        UpdateWheelBone(rearRightCollider, rearRightBone, _rearRightRotOffset, _rearRightPosOffset);
    }

    private void UpdateWheelBone(WheelCollider collider, Transform bone, Quaternion rotOffset, Vector3 posOffset)
    {
        if (collider == null || bone == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        bone.position = pos + posOffset;
        bone.rotation = rot * rotOffset * Quaternion.Euler(wheelRotationOffset);
    }

    private void UpdateDynamicFriction()
    {
        // Skip if handbraking (friction is modified intentionally)
        if (_isHandbraking) return;

        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;

        // Calculate speed-based grip multiplier
        float speedGripBonus = 1f;
        if (currentSpeed > 60f)
        {
            // Increase grip progressively from 60-150 km/h
            float speedFactor = Mathf.Clamp01((currentSpeed - 60f) / 90f);
            speedGripBonus = Mathf.Lerp(1f, 1.25f, speedFactor);
        }

        // Apply load-sensitive grip if enabled in CarData
        float loadMultiplier = 1f;
        if (carData.enableLoadSensitivity)
        {
            loadMultiplier = CalculateLoadSensitiveGrip();
        }

        // Calculate final friction values
        float finalForwardStiffness = _baseFrictionStiffness * 1.1f * speedGripBonus * loadMultiplier;
        float finalSidewaysStiffness = _baseSidewaysStiffness * 1.15f * speedGripBonus * loadMultiplier;

        // Apply to all wheels
        ApplyFrictionToWheel(frontLeftCollider, finalForwardStiffness, finalSidewaysStiffness);
        ApplyFrictionToWheel(frontRightCollider, finalForwardStiffness, finalSidewaysStiffness);
        ApplyFrictionToWheel(rearLeftCollider, finalForwardStiffness, finalSidewaysStiffness);
        ApplyFrictionToWheel(rearRightCollider, finalForwardStiffness, finalSidewaysStiffness);
    }

    private float CalculateLoadSensitiveGrip()
    {
        // Calculate average suspension force across all wheels
        float totalLoad = 0f;
        int groundedWheels = 0;

        WheelHit hit;
        if (frontLeftCollider.GetGroundHit(out hit)) { totalLoad += hit.force; groundedWheels++; }
        if (frontRightCollider.GetGroundHit(out hit)) { totalLoad += hit.force; groundedWheels++; }
        if (rearLeftCollider.GetGroundHit(out hit)) { totalLoad += hit.force; groundedWheels++; }
        if (rearRightCollider.GetGroundHit(out hit)) { totalLoad += hit.force; groundedWheels++; }

        if (groundedWheels == 0) return 1f;

        float avgLoad = totalLoad / groundedWheels;
        float loadRatio = avgLoad / carData.optimalLoad;

        // Optimal load = 1.0x grip
        // Under-loaded = reduced grip (car is too light/airborne)
        // Over-loaded = slightly reduced (tire saturation)
        float gripMultiplier = 1f;

        if (loadRatio < 1f)
        {
            // Under optimal load - gentle reduction
            gripMultiplier = Mathf.Lerp(0.9f, 1f, loadRatio);
        }
        else
        {
            // Over optimal load - very slight reduction
            gripMultiplier = Mathf.Lerp(1f, 0.95f, Mathf.Clamp01((loadRatio - 1f) * 0.3f));
        }

        // Apply sensitivity factor from CarData
        return Mathf.Lerp(1f, gripMultiplier, carData.loadSensitivityFactor);
    }

    private void ApplyFrictionToWheel(WheelCollider wheel, float forwardStiffness, float sidewaysStiffness)
    {
        if (wheel == null) return;

        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.stiffness = forwardStiffness;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = sidewaysStiffness;
        wheel.sidewaysFriction = sideways;
    }

    private void EnsureSymmetricFriction()
    {
        // Force all wheels to have identical friction to prevent braking pull
        // This prevents asymmetric grip causing the car to veer during braking
        
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;
        
        // Use the base friction values for perfect symmetry
        float speedGripBonus = 1f;
        if (currentSpeed > 60f)
        {
            float speedFactor = Mathf.Clamp01((currentSpeed - 60f) / 90f);
            speedGripBonus = Mathf.Lerp(1f, 1.25f, speedFactor);
        }
        
        float symmetricForward = _baseFrictionStiffness * 1.1f * speedGripBonus;
        float symmetricSideways = _baseSidewaysStiffness * 1.15f * speedGripBonus;
        
        // Apply identical friction to all wheels
        ApplyFrictionToWheel(frontLeftCollider, symmetricForward, symmetricSideways);
        ApplyFrictionToWheel(frontRightCollider, symmetricForward, symmetricSideways);
        ApplyFrictionToWheel(rearLeftCollider, symmetricForward, symmetricSideways);
        ApplyFrictionToWheel(rearRightCollider, symmetricForward, symmetricSideways);
    }

    // Public getters for UI/debugging
    public int GetCurrentGear() => _currentGear;
    public float GetCurrentRPM() => _currentRPM;
    public float GetCurrentSpeed() => _rb.linearVelocity.magnitude * 3.6f;
}