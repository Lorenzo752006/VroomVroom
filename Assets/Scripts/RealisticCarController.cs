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
    }

    private void UpdateWheelSuspension(WheelCollider wheel)
    {
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = carData.suspensionSpring;
        spring.damper = carData.suspensionDamper;
        wheel.suspensionSpring = spring;
        wheel.suspensionDistance = carData.suspensionDistance;
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
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
        }
        else
        {
            _currentAcceleration = 0;
        }

        rearLeftCollider.motorTorque = _currentAcceleration;
        rearRightCollider.motorTorque = _currentAcceleration;

        _currentBrakeForce = Input.GetKey(KeyCode.Space) ? carData.brakePower : 0f;
        
        frontLeftCollider.brakeTorque = _currentBrakeForce;
        frontRightCollider.brakeTorque = _currentBrakeForce;
        rearLeftCollider.brakeTorque = _currentBrakeForce;
        rearRightCollider.brakeTorque = _currentBrakeForce;
    }

    private void HandleSteering()
    {
        float steerInput = Input.GetAxis("Horizontal");
        _currentSteerAngle = carData.maxSteeringAngle * steerInput;

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