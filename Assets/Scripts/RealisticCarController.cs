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

    private Rigidbody _rb;
    private float _currentSteerAngle;
    private float _currentAcceleration;
    private float _currentBrakeForce;
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
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = carData.sidewaysFrictionStiffness;
        wheel.sidewaysFriction = sideways;
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        ApplyDownforce();
        // Anti-roll disabled - was causing car to fly
        // ApplyAntiRoll(frontLeftCollider, frontRightCollider);
        // ApplyAntiRoll(rearLeftCollider, rearRightCollider);
        UpdateWheels();
    }

    private void HandleMotor()
    {
        // ... (Same motor logic as previous script) ...
        float moveInput = Input.GetAxis("Vertical");
        float currentSpeed = _rb.linearVelocity.magnitude * 3.6f;

        if (currentSpeed < carData.maxSpeed)
        {
            _currentAcceleration = moveInput * carData.maxMotorTorque;
            if (currentSpeed < carData.lowSpeedTorqueKph)
            {
                _currentAcceleration *= carData.lowSpeedTorqueMultiplier;
            }
        }
        else
        {
            _currentAcceleration = 0;
        }

        _currentBrakeForce = Input.GetKey(KeyCode.Space) ? carData.brakePower : 0f;

        float rearTorque = _currentBrakeForce > 0f ? 0f : _currentAcceleration;
        rearTorque = ApplyTractionControl(rearLeftCollider, rearTorque, currentSpeed);
        rearTorque = ApplyTractionControl(rearRightCollider, rearTorque, currentSpeed);

        rearLeftCollider.motorTorque = rearTorque;
        rearRightCollider.motorTorque = rearTorque;
        
        frontLeftCollider.brakeTorque = _currentBrakeForce;
        frontRightCollider.brakeTorque = _currentBrakeForce;
        rearLeftCollider.brakeTorque = _currentBrakeForce;
        rearRightCollider.brakeTorque = _currentBrakeForce;
    }

    private void HandleSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        float steerScale = 1f;
        if (carData.useSpeedSensitiveSteering)
        {
            float speedKmh = _rb.linearVelocity.magnitude * 3.6f;
            float speedFactor = Mathf.InverseLerp(0f, carData.maxSpeed, speedKmh);
            steerScale = Mathf.Lerp(1f, Mathf.Clamp01(carData.steerAtMaxSpeed), speedFactor);
            steerScale = Mathf.Max(steerScale, Mathf.Clamp01(carData.minSteerScale));
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
            float slipFactor = Mathf.InverseLerp(0.2f, 0.8f, slip);
            float tc = Mathf.Clamp01(carData.tractionControl);
            torque *= Mathf.Lerp(1f, 1f - tc, slipFactor);
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
        float downforceAmount = carData.downforce * speed * speed * 0.01f;
        _rb.AddForce(-transform.up * downforceAmount, ForceMode.Force);
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
}